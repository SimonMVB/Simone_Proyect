using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Simone.Data;
using Simone.Models;
using Simone.Services;

namespace Simone.Controllers
{
    public class ComprasController : Controller
    {
        private readonly TiendaDbContext _context;
        private readonly ProductosService _productos;
        private readonly CategoriasService _categorias;
        private readonly SubcategoriasService _subcategorias;
        private readonly CarritoService _carrito;
        private readonly UserManager<Usuario> _userManager;
        private readonly ILogger<ComprasController> _logger;

        public ComprasController(
            UserManager<Usuario> user,
            TiendaDbContext context,
            ProductosService productos,
            CategoriasService categorias,
            SubcategoriasService subcategorias,
            CarritoService carrito,
            ILogger<ComprasController> logger)
        {
            _context = context;
            _productos = productos;
            _categorias = categorias;
            _subcategorias = subcategorias;
            _carrito = carrito;
            _userManager = user;
            _logger = logger;
        }

        // -----------------------------------------------------------
        // CATÁLOGO
        // -----------------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> Catalogo(int? categoriaID, int[]? subcategoriaIDs, int pageNumber = 1, int pageSize = 20)
        {
            var categorias = await _categorias.GetAllAsync();
            var subcategorias = categoriaID.HasValue
                ? await _subcategorias.GetByCategoriaIdAsync(categoriaID.Value)
                : new List<Subcategorias>();

            IQueryable<Producto> query = _context.Productos.AsNoTracking();

            if (categoriaID.HasValue)
                query = query.Where(p => p.CategoriaID == categoriaID.Value);

            if (subcategoriaIDs is { Length: > 0 })
                query = query.Where(p => subcategoriaIDs!.Contains(p.SubcategoriaID));

            var totalProducts = await query.CountAsync();

            var productos = await query
                .OrderBy(p => p.Nombre)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var model = new CatalogoViewModel
            {
                Categorias = categorias,
                SelectedCategoriaID = categoriaID,
                Subcategorias = subcategorias,
                SelectedSubcategoriaIDs = subcategoriaIDs?.ToList() ?? new List<int>(),
                Productos = productos,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalProducts = totalProducts,
                ProductoIDsFavoritos = new List<int>()
            };

            // Marcar favoritos si está autenticado
            if (User.Identity?.IsAuthenticated == true)
            {
                var userId = _userManager.GetUserId(User);
                model.ProductoIDsFavoritos = await _context.Favoritos
                    .Where(f => f.UsuarioId == userId)
                    .Select(f => f.ProductoId)
                    .ToListAsync();
            }

            return View(model);
        }

        // -----------------------------------------------------------
        // DETALLE DE PRODUCTO + RELACIONADOS
        // /Compras/VerProducto?productoID=1
        // (también acepta /p/{id}/{slug?})
        // -----------------------------------------------------------
        [HttpGet]
        [Route("/p/{productoID:int}/{slug?}")]
        public async Task<IActionResult> VerProducto(int productoID)
        {
            if (productoID <= 0)
                return RedirectToAction(nameof(Catalogo));

            var producto = await _context.Productos
                .AsNoTracking()
                .Include(p => p.Categoria)
                .Include(p => p.Subcategoria)
                .FirstOrDefaultAsync(p => p.ProductoID == productoID);

            if (producto == null)
            {
                TempData["MensajeError"] = "Producto no encontrado.";
                return RedirectToAction(nameof(Catalogo));
            }

            // Relacionados: misma subcategoría; si no hay, por categoría
            var relacionadosQuery = _context.Productos.AsNoTracking()
                .Where(p => p.ProductoID != producto.ProductoID);

            if (producto.SubcategoriaID != 0)
                relacionadosQuery = relacionadosQuery.Where(p => p.SubcategoriaID == producto.SubcategoriaID);
            else if (producto.CategoriaID != 0)
                relacionadosQuery = relacionadosQuery.Where(p => p.CategoriaID == producto.CategoriaID);

            ViewBag.Relacionados = await relacionadosQuery
                .OrderByDescending(p => p.FechaAgregado)
                .ThenBy(p => p.Nombre)
                .Take(8)
                .ToListAsync();

            return View(producto); // Views/Compras/VerProducto.cshtml
        }

        // -----------------------------------------------------------
        // CARRITO: AÑADIR (POST normal o AJAX)
        // -----------------------------------------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AnadirAlCarrito([Bind("ProductoID,Cantidad")] CatalogoViewModel model, CancellationToken ct = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                if (EsAjax()) return Json(new { ok = false, needLogin = true, error = "Debes iniciar sesión." });
                TempData["MensajeError"] = "Debes iniciar sesión para añadir productos al carrito.";
                return RedirectToAction("Login", "Cuenta");
            }

            if (model.Cantidad <= 0) model.Cantidad = 1;

            var producto = await _productos.GetByIdAsync(model.ProductoID);
            if (producto == null)
            {
                if (EsAjax()) return Json(new { ok = false, error = "Producto no encontrado." });
                TempData["MensajeError"] = "Producto no encontrado.";
                return RedirectToReferrerOr("Catalogo");
            }

            // Asegurar carrito
            var carrito = await _carrito.GetByClienteIdAsync(user.Id);
            if (carrito == null)
            {
                await _carrito.AddAsync(user);
                carrito = await _carrito.GetByClienteIdAsync(user.Id);
            }

            // Stock: lo que hay ya en el carrito + lo solicitado ahora
            var detalles = await _carrito.LoadCartDetails(carrito.CarritoID);
            var cantidadEnCarrito = detalles.Where(c => c.ProductoID == model.ProductoID).Sum(c => c.Cantidad);

            if (cantidadEnCarrito + model.Cantidad > producto.Stock)
            {
                var msg = "La cantidad solicitada supera el stock disponible.";
                if (EsAjax()) return Json(new { ok = false, error = msg, stock = producto.Stock, enCarrito = cantidadEnCarrito });
                TempData["MensajeError"] = msg;
                return RedirectToReferrerOr("Catalogo");
            }

            var ok = await _carrito.AnadirProducto(producto, user, model.Cantidad);
            if (!ok)
            {
                if (EsAjax()) return Json(new { ok = false, error = "No se pudo añadir al carrito." });
                TempData["MensajeError"] = "No se pudo añadir el producto al carrito.";
                return RedirectToReferrerOr("Catalogo");
            }

            // Nuevo contador de ítems
            var count = await _context.CarritoDetalle
                .Where(cd => cd.CarritoID == carrito.CarritoID)
                .SumAsync(cd => cd.Cantidad, ct);

            if (EsAjax()) return Json(new { ok = true, count });

            TempData["MensajeExito"] = "Producto añadido al carrito con éxito.";
            return RedirectToReferrerOr("Catalogo");
        }

        // Cambiar cantidad de un ítem del carrito (útil si lo usas en vistas futuras)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarCantidad(int carritoDetalleID, int cantidad, CancellationToken ct = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            if (cantidad <= 0) cantidad = 1;

            var detalle = await _context.CarritoDetalle
                .Include(cd => cd.Carrito)
                .Include(cd => cd.Producto)
                .FirstOrDefaultAsync(cd => cd.CarritoDetalleID == carritoDetalleID, ct);

            if (detalle == null || detalle.Carrito?.UsuarioId != user.Id)
                return NotFound();

            if (detalle.Producto == null) return NotFound();

            if (cantidad > detalle.Producto.Stock)
            {
                var msg = "La cantidad supera el stock disponible.";
                if (EsAjax()) return Json(new { ok = false, error = msg, stock = detalle.Producto.Stock });
                TempData["MensajeError"] = msg;
                return RedirectToReferrerOr("Resumen");
            }

            detalle.Cantidad = cantidad;
            await _context.SaveChangesAsync(ct);

            if (EsAjax())
            {
                var subtotal = detalle.Cantidad * detalle.Precio;
                return Json(new { ok = true, subtotal });
            }

            TempData["MensajeExito"] = "Cantidad actualizada.";
            return RedirectToReferrerOr("Resumen");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarArticulo(int carritoDetalleID)
        {
            await _carrito.BorrarProductoCarrito(carritoDetalleID);
            TempData["MensajeExito"] = "Producto eliminado del carrito con éxito.";
            return RedirectToAction("Catalogo");
        }

        // Info mínima del carrito para actualizar el badge del header
        [HttpGet]
        public async Task<IActionResult> CartInfo(CancellationToken ct)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(new { count = 0, subtotal = 0m });

            var carrito = await _carrito.GetByClienteIdAsync(user.Id);
            if (carrito == null) return Json(new { count = 0, subtotal = 0m });

            var detalles = await _carrito.LoadCartDetails(carrito.CarritoID);
            var count = detalles.Sum(d => d.Cantidad);
            var subtotal = detalles.Sum(d => d.Cantidad * d.Precio);

            return Json(new { count, subtotal });
        }

        // -----------------------------------------------------------
        // FAVORITOS (ruta usada en la vista: /Favoritos/Toggle/{id})
        // Si ya tienes un FavoritosController, puedes omitir esto.
        // -----------------------------------------------------------
        [HttpPost("Favoritos/Toggle/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleFavorito(int id, CancellationToken ct = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return EsAjax() ? Json(new { ok = false, needLogin = true }) : Unauthorized();

            var prodExists = await _context.Productos.AsNoTracking().AnyAsync(p => p.ProductoID == id, ct);
            if (!prodExists) return Json(new { ok = false, error = "Producto no encontrado" });

            var fav = await _context.Favoritos.FirstOrDefaultAsync(f => f.UsuarioId == user.Id && f.ProductoId == id, ct);
            bool esFavorito;

            if (fav == null)
            {
                _context.Favoritos.Add(new Favorito
                {
                    UsuarioId = user.Id,
                    ProductoId = id,
                    FechaGuardado = DateTime.UtcNow
                });
                esFavorito = true;
            }
            else
            {
                _context.Favoritos.Remove(fav);
                esFavorito = false;
            }

            await _context.SaveChangesAsync(ct);
            return Json(new { ok = true, esFavorito });
        }

        // -----------------------------------------------------------
        // RESUMEN / CHECKOUT
        // -----------------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> Resumen()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                TempData["MensajeError"] = "Debes iniciar sesión para ver el resumen de tu carrito.";
                return RedirectToAction("Login", "Cuenta");
            }

            // Asegurar carrito
            var carrito = await _carrito.GetByClienteIdAsync(user.Id)
                         ?? (await _carrito.AddAsync(user) ? await _carrito.GetByClienteIdAsync(user.Id) : null);

            if (carrito == null)
            {
                TempData["MensajeError"] = "No fue posible obtener tu carrito.";
                return RedirectToAction("Catalogo");
            }

            var carritoDetalles = await _carrito.LoadCartDetails(carrito.CarritoID);
            var totalCompra = carritoDetalles.Sum(cd => cd.Precio * cd.Cantidad);

            ViewBag.CarritoDetalles = carritoDetalles;
            ViewBag.TotalCompra = totalCompra;
            ViewBag.HasAddress = !string.IsNullOrWhiteSpace(user.Direccion);

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmarCompra()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                TempData["MensajeError"] = "Debes iniciar sesión para confirmar la compra.";
                return RedirectToAction("Login", "Cuenta");
            }

            var carrito = await _carrito.GetByClienteIdAsync(user.Id);
            if (carrito == null)
            {
                TempData["MensajeError"] = "Tu carrito está vacío.";
                return RedirectToAction(nameof(Resumen));
            }

            var carritoDetalles = await _carrito.LoadCartDetails(carrito.CarritoID);
            if (carritoDetalles == null || !carritoDetalles.Any())
            {
                TempData["MensajeError"] = "Tu carrito está vacío.";
                return RedirectToAction(nameof(Resumen));
            }

            // Requiere dirección
            if (string.IsNullOrWhiteSpace(user.Direccion))
            {
                ViewBag.HasAddress = false;
                ViewBag.CarritoDetalles = carritoDetalles;
                ViewBag.TotalCompra = carritoDetalles.Sum(cd => cd.Precio * cd.Cantidad);
                TempData["MensajeError"] = "Agrega una dirección para continuar con la compra.";
                return View("Resumen", user);
            }

            // Validación de stock (previa)
            var faltantes = carritoDetalles
                .Where(d => d.Producto != null && d.Producto.Stock < d.Cantidad)
                .ToList();

            if (faltantes.Any())
            {
                ViewBag.HasAddress = true;
                ViewBag.CarritoDetalles = carritoDetalles;
                ViewBag.TotalCompra = carritoDetalles.Sum(cd => cd.Precio * cd.Cantidad);
                TempData["MensajeError"] = "Stock insuficiente para: " +
                    string.Join(", ", faltantes.Select(f => $"{f.Producto!.Nombre} (disp: {f.Producto.Stock}, pediste: {f.Cantidad})"));
                return View("Resumen", user);
            }

            try
            {
                // Registra venta, descuenta stock y cierra carrito
                var ok = await _carrito.ProcessCartDetails(carrito.CarritoID, user);

                if (ok)
                {
                    // ID de la venta creada (la más reciente del usuario)
                    var ventaId = await _context.Ventas
                        .Where(v => v.UsuarioId == user.Id)
                        .OrderByDescending(v => v.FechaVenta)
                        .Select(v => v.VentaID)
                        .FirstOrDefaultAsync();

                    // Prepara un carrito nuevo y vacío
                    await _carrito.AddAsync(user);

                    TempData["MensajeExito"] = "¡Gracias por tu compra!";
                    if (ventaId > 0) return RedirectToAction(nameof(CompraExito), new { id = ventaId });
                    return RedirectToAction("Index", "MisCompras");
                }

                TempData["MensajeError"] = "No se pudo completar la compra. Revisa tu carrito e intenta nuevamente.";
                return RedirectToAction(nameof(Resumen));
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Validación al confirmar compra");

                var carritoDetallesView = await _carrito.LoadCartDetails(carrito.CarritoID);
                ViewBag.HasAddress = true;
                ViewBag.CarritoDetalles = carritoDetallesView;
                ViewBag.TotalCompra = carritoDetallesView.Sum(cd => cd.Precio * cd.Cantidad);

                TempData["MensajeError"] = ex.Message;
                return View("Resumen", user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al procesar el pedido");
                TempData["MensajeError"] = "Hubo un error al procesar tu pedido.";
                return RedirectToAction(nameof(CompraError));
            }
        }

        // -----------------------------------------------------------
        // PANTALLAS RESULTADO
        // -----------------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> CompraExito(int id)
        {
            var uid = _userManager.GetUserId(User);
            var venta = await _context.Ventas
                .AsNoTracking()
                .Include(v => v.Usuario)
                .FirstOrDefaultAsync(v => v.VentaID == id && v.UsuarioId == uid);

            if (venta == null)
                return RedirectToAction("Index", "Home");

            return View(venta);
        }

        [HttpGet]
        public IActionResult CompraError() => View();

        // -----------------------------------------------------------
        // DESCARGAR COMPROBANTE
        // -----------------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> Comprobante(int id)
        {
            var uid = _userManager.GetUserId(User);
            var venta = await _context.Ventas
                .Include(v => v.Usuario)
                .Include(v => v.DetalleVentas).ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(v => v.VentaID == id && v.UsuarioId == uid);

            if (venta == null) return NotFound();

            var html = $@"
<!DOCTYPE html><html><head><meta charset='utf-8'>
<style>
 body {{ font-family: Arial, Helvetica, sans-serif; }}
 h1 {{ font-size:20px;margin:0 0 8px }}
 table {{ width:100%;border-collapse:collapse;margin-top:12px }}
 th,td {{ border:1px solid #ddd;padding:8px;font-size:12px }}
 th {{ background:#f3f4f6;text-align:left }}
 .r {{ text-align:right }}
 .muted {{ color:#6b7280;font-size:12px }}
</style></head><body>
<h1>Comprobante de compra</h1>
<div class='muted'>Referencia: #{venta.VentaID} · Fecha: {venta.FechaVenta:dd/MM/yyyy HH:mm}</div>
<div class='muted'>Cliente: {venta.Usuario?.NombreCompleto} · Email: {venta.Usuario?.Email}</div>

<table>
<thead><tr><th>Producto</th><th class='r'>Cant.</th><th class='r'>Precio</th><th class='r'>Subtotal</th></tr></thead>
<tbody>
{string.Join("", venta.DetalleVentas.Select(d =>
            $"<tr><td>{d.Producto?.Nombre}</td><td class='r'>{d.Cantidad}</td><td class='r'>{d.PrecioUnitario:C2}</td><td class='r'>{d.Subtotal:C2}</td></tr>"
        ))}
</tbody>
<tfoot>
<tr><th colspan='3' class='r'>Total</th><th class='r'>{venta.Total:C2}</th></tr>
</tfoot>
</table>

<p class='muted'>Método de pago: {venta.MetodoPago ?? "N/D"} · Estado: {venta.Estado ?? "N/D"}</p>
</body></html>";

            var bytes = System.Text.Encoding.UTF8.GetBytes(html);
            var fileName = $"comprobante-{venta.VentaID}.html";
            return File(bytes, "text/html", fileName);
        }

        // -----------------------------------------------------------
        // HELPERS
        // -----------------------------------------------------------
        private bool EsAjax() =>
            string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

        private IActionResult RedirectToReferrerOr(string action, string controller = "Compras")
        {
            var referer = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrWhiteSpace(referer)) return Redirect(referer);
            return RedirectToAction(action, controller);
        }
    }
}
