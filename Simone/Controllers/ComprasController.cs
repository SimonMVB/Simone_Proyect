using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Simone.Data;
using Simone.Extensions;
using Simone.Models;
using Simone.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ResolverPagos = Simone.Services.PagosResolver;

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
        private readonly IWebHostEnvironment _env;

        private readonly ResolverPagos _pagosResolver;     // mono/multi
        private readonly IBancosConfigService _bancosSvc;   // admin + tiendas

        // Cálculo de envíos
        private readonly EnviosCarritoService _enviosCarrito;

        private const int MAX_FILE_MB = 5;
        private static readonly HashSet<string> _extPermitidas =
            new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp", ".pdf" };

        public ComprasController(
            UserManager<Usuario> user,
            TiendaDbContext context,
            ProductosService productos,
            CategoriasService categorias,
            SubcategoriasService subcategorias,
            CarritoService carrito,
            ILogger<ComprasController> logger,
            IWebHostEnvironment env,
            ResolverPagos pagosResolver,
            IBancosConfigService bancosSvc,
            EnviosCarritoService enviosCarrito
        )
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _productos = productos ?? throw new ArgumentNullException(nameof(productos));
            _categorias = categorias ?? throw new ArgumentNullException(nameof(categorias));
            _subcategorias = subcategorias ?? throw new ArgumentNullException(nameof(subcategorias));
            _carrito = carrito ?? throw new ArgumentNullException(nameof(carrito));
            _userManager = user ?? throw new ArgumentNullException(nameof(user));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _env = env ?? throw new ArgumentNullException(nameof(env));
            _pagosResolver = pagosResolver ?? throw new ArgumentNullException(nameof(pagosResolver));
            _bancosSvc = bancosSvc ?? throw new ArgumentNullException(nameof(bancosSvc));
            _enviosCarrito = enviosCarrito ?? throw new ArgumentNullException(nameof(enviosCarrito));
        }

        // =========================
        // CATÁLOGO / PRODUCTO
        // =========================
        [HttpGet]
        public async Task<IActionResult> Catalogo(int? categoriaID, int[]? subcategoriaIDs, int pageNumber = 1, int pageSize = 20, CancellationToken ct = default)
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

            var totalProducts = await query.CountAsync(ct);

            var productos = await query
                .OrderBy(p => p.Nombre)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

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

            if (User.Identity?.IsAuthenticated == true)
            {
                var userId = _userManager.GetUserId(User);
                model.ProductoIDsFavoritos = await _context.Favoritos
                    .Where(f => f.UsuarioId == userId)
                    .Select(f => f.ProductoId)
                    .ToListAsync(ct);
            }

            return View(model);
        }

        [HttpGet]
        [Route("/p/{productoID:int}/{slug?}")]
        public async Task<IActionResult> VerProducto(int productoID, CancellationToken ct = default)
        {
            if (productoID <= 0) return RedirectToAction(nameof(Catalogo));

            var producto = await _context.Productos
                .AsNoTracking()
                .Include(p => p.Categoria)
                .Include(p => p.Subcategoria)
                .FirstOrDefaultAsync(p => p.ProductoID == productoID, ct);

            if (producto == null)
            {
                TempData["MensajeError"] = "Producto no encontrado.";
                return RedirectToAction(nameof(Catalogo));
            }

            var relacionados = _context.Productos.AsNoTracking()
                .Where(p => p.ProductoID != producto.ProductoID);

            if (producto.SubcategoriaID != 0)
                relacionados = relacionados.Where(p => p.SubcategoriaID == producto.SubcategoriaID);
            else if (producto.CategoriaID != 0)
                relacionados = relacionados.Where(p => p.CategoriaID == producto.CategoriaID);

            ViewBag.Relacionados = await relacionados
                .OrderByDescending(p => p.FechaAgregado)
                .ThenBy(p => p.Nombre)
                .Take(8)
                .ToListAsync(ct);

            return View(producto);
        }

        // =========================
        // CARRITO
        // =========================
        [HttpPost, ValidateAntiForgeryToken]
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

            var carrito = await _carrito.GetByClienteIdAsync(user.Id)
                        ?? (await _carrito.AddAsync(user) ? await _carrito.GetByClienteIdAsync(user.Id) : null);

            if (carrito == null)
            {
                TempData["MensajeError"] = "No fue posible obtener tu carrito.";
                return RedirectToReferrerOr("Catalogo");
            }

            var detalles = await _carrito.LoadCartDetails(carrito.CarritoID);
            var enCarrito = detalles.Where(c => c.ProductoID == model.ProductoID).Sum(c => c.Cantidad);

            if (enCarrito + model.Cantidad > producto.Stock)
            {
                var msg = "La cantidad solicitada supera el stock disponible.";
                if (EsAjax()) return Json(new { ok = false, error = msg, stock = producto.Stock, enCarrito });
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

            var count = await _context.CarritoDetalle
                .Where(cd => cd.CarritoID == carrito.CarritoID)
                .SumAsync(cd => cd.Cantidad, ct);

            if (EsAjax()) return Json(new { ok = true, count });

            TempData["MensajeExito"] = "Producto añadido al carrito con éxito.";
            return RedirectToReferrerOr("Catalogo");
        }

        // Mini resumen del carrito (HTML) para refrescar el panel por AJAX
        [HttpGet]
        [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> Mini(CancellationToken ct = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return PartialView("_CartPartial", Enumerable.Empty<CarritoDetalle>());

            var carrito = await _carrito.GetByClienteIdAsync(user.Id);
            var detalles = carrito == null
                ? Enumerable.Empty<CarritoDetalle>()
                : await _carrito.LoadCartDetails(carrito.CarritoID);

            return PartialView("_CartPartial", detalles);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarCantidad(int carritoDetalleID, int cantidad, CancellationToken ct = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();
            if (cantidad <= 0) cantidad = 1;

            var detalle = await _context.CarritoDetalle
                .Include(cd => cd.Carrito)
                .Include(cd => cd.Producto)
                .FirstOrDefaultAsync(cd => cd.CarritoDetalleID == carritoDetalleID, ct);

            if (detalle == null || detalle.Carrito?.UsuarioId != user.Id) return NotFound();
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

            if (EsAjax()) return Json(new { ok = true, subtotal = detalle.Cantidad * detalle.Precio });

            TempData["MensajeExito"] = "Cantidad actualizada.";
            return RedirectToReferrerOr("Resumen");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarArticulo(int carritoDetalleID, CancellationToken ct = default)
        {
            var ok = await _carrito.BorrarProductoCarrito(carritoDetalleID);

            if (EsAjax())
                return Json(new { ok });

            TempData["MensajeExito"] = ok
                ? "Producto eliminado del carrito con éxito."
                : "No se pudo eliminar el producto del carrito.";
            return RedirectToAction("Catalogo");
        }

        [HttpGet]
        public async Task<IActionResult> CartInfo(CancellationToken ct)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(new { count = 0, subtotal = 0m });

            var carrito = await _carrito.GetByClienteIdAsync(user.Id);
            if (carrito == null) return Json(new { count = 0, subtotal = 0m });

            var detalles = await _carrito.LoadCartDetails(carrito.CarritoID);
            return Json(new { count = detalles.Sum(d => d.Cantidad), subtotal = detalles.Sum(d => d.Cantidad * d.Precio) });
        }

        // =========================
        // RESUMEN / CHECKOUT
        // =========================
        [HttpGet]
        public async Task<IActionResult> Resumen(CancellationToken ct = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                TempData["MensajeError"] = "Debes iniciar sesión para ver el resumen de tu carrito.";
                return RedirectToAction("Login", "Cuenta");
            }

            var carrito = await _carrito.GetByClienteIdAsync(user.Id)
                         ?? (await _carrito.AddAsync(user) ? await _carrito.GetByClienteIdAsync(user.Id) : null);

            if (carrito == null)
            {
                TempData["MensajeError"] = "No fue posible obtener tu carrito.";
                return RedirectToAction("Catalogo");
            }

            var detalles = await _carrito.LoadCartDetails(carrito.CarritoID);
            var subtotal = detalles.Sum(cd => cd.Precio * cd.Cantidad);

            // ---------- Envío por vendedor (vendedor = AspNetUsers.Id del que subió el producto) ----------
            var vendedorUserIds = detalles
                .Where(d => d.Producto != null && !string.IsNullOrWhiteSpace(d.Producto!.VendedorID))
                .Select(d => d.Producto!.VendedorID!)
                .Distinct()
                .ToList();

            decimal envioTotal = 0m;
            Dictionary<string, decimal> envioPorVendedor = new();
            List<string> envioMensajes = new();

            if (!string.IsNullOrWhiteSpace(user.Provincia) && vendedorUserIds.Count > 0)
            {
                try
                {
                    var envRes = await _enviosCarrito
                        .CalcularAsync(vendedorUserIds, user.Provincia, user.Ciudad, ct)
                        .ConfigureAwait(false);

                    envioTotal = envRes?.TotalEnvio ?? 0m;
                    envioPorVendedor = envRes?.PorVendedor ?? new Dictionary<string, decimal>();
                    envioMensajes = envRes?.Mensajes ?? new List<string>();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudo calcular el envío para el resumen.");
                }
            }

            // Resolver mono/multi
            var decision = await _pagosResolver.ResolverAsync(user.Id, carrito.CarritoID, ct);
            ViewBag.EsMultiVendedor = decision.EsMultiVendedor;
            ViewBag.VendedorIdUnico = decision.VendedorIdUnico;
            ViewBag.VendedoresIds = decision.VendedoresIds;

            // Cuentas
            if (!decision.EsMultiVendedor && !string.IsNullOrWhiteSpace(decision.VendedorIdUnico))
            {
                var cuentasProv = new List<Simone.Configuration.CuentaBancaria>();
                try
                {
                    cuentasProv = (await _bancosSvc.GetByProveedorAsync(decision.VendedorIdUnico!, ct))
                                  .Where(c => c?.Activo == true).ToList();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudieron cargar cuentas del proveedor {vid}", decision.VendedorIdUnico);
                }

                ViewBag.CuentasProveedor = cuentasProv;
                if (cuentasProv.Count == 0)
                {
                    var admin = (await _bancosSvc.GetAdminAsync(ct)).Where(c => c?.Activo == true).ToList();
                    ViewBag.CuentasAdmin = admin;
                    ViewBag.FallbackAdmin = true;
                }
                else
                {
                    ViewBag.FallbackAdmin = false;
                }
            }
            else
            {
                var admin = (await _bancosSvc.GetAdminAsync(ct)).Where(c => c?.Activo == true).ToList();
                ViewBag.CuentasAdmin = admin;
                ViewBag.FallbackAdmin = true;
            }

            // ---------- NUEVO: convertir {userId -> $} a {NombreTienda -> $} ----------
            // 1) Traer (userId, VendedorId)
            var usuarios = await _context.Users.AsNoTracking()
                .Where(u => vendedorUserIds.Contains(u.Id))
                .Select(u => new { u.Id, u.VendedorId, u.NombreCompleto, u.Email })
                .ToListAsync(ct);

            // 2) Traer nombres de tienda por VendedorId
            var tiendaIds = usuarios.Where(x => x.VendedorId.HasValue)
                                    .Select(x => x.VendedorId!.Value)
                                    .Distinct()
                                    .ToList();

            var mapTienda = tiendaIds.Count == 0
                ? new Dictionary<int, string>()
                : await _context.Vendedores.AsNoTracking()
                    .Where(t => tiendaIds.Contains(t.VendedorId))
                    .ToDictionaryAsync(t => t.VendedorId, t => t.Nombre, ct);

            // 3) Construir {NombreTienda legible -> $}
            var envioPorTienda = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in envioPorVendedor)
            {
                var u = usuarios.FirstOrDefault(x => x.Id == kv.Key);
                string etiqueta;

                if (u?.VendedorId != null && mapTienda.TryGetValue(u.VendedorId.Value, out var tiendaNombre) && !string.IsNullOrWhiteSpace(tiendaNombre))
                    etiqueta = tiendaNombre;                                 // ✅ Nombre de la tienda
                else
                    etiqueta = u?.NombreCompleto ?? u?.Email ?? kv.Key;      // fallback

                if (!envioPorTienda.ContainsKey(etiqueta))
                    envioPorTienda[etiqueta] = 0m;
                envioPorTienda[etiqueta] += kv.Value;
            }

            // Datos para la vista
            ViewBag.CarritoDetalles = detalles;
            ViewBag.Subtotal = subtotal;
            ViewBag.TotalCompra = subtotal;
            ViewBag.EnvioTotal = envioTotal;
            ViewBag.EnvioPorVendedor = envioPorVendedor; // por compatibilidad
            ViewBag.EnvioPorTienda = envioPorTienda;     // ← usar este en la vista
            ViewBag.EnvioMensajes = envioMensajes;
            ViewBag.CanComputeShipping = !string.IsNullOrWhiteSpace(user.Provincia);
            ViewBag.HasAddress = !string.IsNullOrWhiteSpace(user.Direccion);

            return View(user);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmarCompra(
            string? MetodoPago,
            string? BancoSeleccionado,
            string? Depositante,
            IFormFile? Comprobante,
            CancellationToken ct = default)
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

            var detalles = await _carrito.LoadCartDetails(carrito.CarritoID);
            if (detalles == null || !detalles.Any())
            {
                TempData["MensajeError"] = "Tu carrito está vacío.";
                return RedirectToAction(nameof(Resumen));
            }

            if (string.IsNullOrWhiteSpace(user.Direccion))
            {
                ViewBag.HasAddress = false;
                ViewBag.CarritoDetalles = detalles;
                ViewBag.TotalCompra = detalles.Sum(cd => cd.Precio * cd.Cantidad);
                TempData["MensajeError"] = "Agrega una dirección para continuar con la compra.";
                return View("Resumen", user);
            }

            // Stock
            var faltantes = detalles.Where(d => d.Producto != null && d.Producto.Stock < d.Cantidad).ToList();
            if (faltantes.Any())
            {
                ViewBag.HasAddress = true;
                ViewBag.CarritoDetalles = detalles;
                ViewBag.TotalCompra = detalles.Sum(cd => cd.Precio * cd.Cantidad);
                TempData["MensajeError"] = "Stock insuficiente para: " +
                    string.Join(", ", faltantes.Select(f => $"{f.Producto!.Nombre} (disp: {f.Producto.Stock}, pediste: {f.Cantidad})"));
                return View("Resumen", user);
            }

            // Resolver pagos (mono/multi)
            var decision = await _pagosResolver.ResolverAsync(user.Id, carrito.CarritoID, ct);
            var esDeposito = (MetodoPago ?? "").StartsWith("dep", StringComparison.OrdinalIgnoreCase);

            if (esDeposito)
            {
                if (string.IsNullOrWhiteSpace(BancoSeleccionado) ||
                    string.IsNullOrWhiteSpace(Depositante) ||
                    Comprobante == null || Comprobante.Length == 0)
                {
                    TempData["MensajeError"] = "Para depósito, selecciona banco, indica el nombre del depositante y adjunta el comprobante.";
                    return RedirectToAction(nameof(Resumen));
                }

                if (!EsMimePermitido(Comprobante) || Comprobante.Length > MAX_FILE_MB * 1024 * 1024)
                {
                    TempData["MensajeError"] = $"Comprobante inválido. Formatos: JPG, PNG, WEBP o PDF. Máximo {MAX_FILE_MB}MB.";
                    return RedirectToAction(nameof(Resumen));
                }
            }

            try
            {
                // 1) Registrar venta desde carrito
                var ok = await _carrito.ProcessCartDetails(carrito.CarritoID, user);
                if (!ok)
                {
                    TempData["MensajeError"] = "No se pudo completar la compra. Revisa tu carrito e intenta nuevamente.";
                    return RedirectToAction(nameof(Resumen));
                }

                // 2) Obtener venta creada
                var ventaId = await _context.Ventas
                    .Where(v => v.UsuarioId == user.Id)
                    .OrderByDescending(v => v.FechaVenta)
                    .Select(v => v.VentaID)
                    .FirstOrDefaultAsync(ct);

                // 2.b — cálculo de envío definitivo (opcional)
                decimal envioTotal = 0m;
                if (ventaId > 0)
                {
                    var vendedorIds = detalles
                        .Where(d => d.Producto != null && !string.IsNullOrWhiteSpace(d.Producto.VendedorID))
                        .Select(d => d.Producto!.VendedorID!)
                        .Distinct()
                        .ToList();

                    if (vendedorIds.Count > 0 && !string.IsNullOrWhiteSpace(user.Provincia))
                    {
                        try
                        {
                            var envRes = await _enviosCarrito.CalcularAsync(vendedorIds, user.Provincia, user.Ciudad, ct);
                            envioTotal = envRes?.TotalEnvio ?? 0m;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "No se pudo calcular el envío al confirmar compra.");
                        }
                    }
                }

                if (ventaId > 0)
                {
                    Directory.CreateDirectory(UploadsFolderAbs());

                    // Resolver banco elegido (valida admin/tienda y Activo)
                    var (okBanco, metaBancoObj, destinoPago, errBanco) =
                        await ResolverBancoSeleccionadoAsync(BancoSeleccionado ?? "", ct);

                    // En multi-tienda solo admin
                    if (decision.EsMultiVendedor && okBanco &&
                        !string.Equals(destinoPago, "admin", StringComparison.OrdinalIgnoreCase))
                    {
                        TempData["MensajeError"] = "En pedidos multi-tienda, el pago se realiza únicamente a cuentas del administrador.";
                        return RedirectToAction(nameof(Resumen));
                    }

                    if (esDeposito && !okBanco)
                    {
                        TempData["MensajeError"] = errBanco ?? "Banco seleccionado inválido.";
                        return RedirectToAction(nameof(Resumen));
                    }

                    // Metadatos del pago + envío
                    var metaObj = new
                    {
                        metodo = MetodoPago,
                        destino = okBanco ? destinoPago : null,
                        bancoSeleccion = okBanco ? metaBancoObj : new { valor = BancoSeleccionado },
                        depositante = string.IsNullOrWhiteSpace(Depositante) ? null : Depositante.Trim(),
                        envio = new { total = envioTotal },
                        ts = DateTime.UtcNow
                    };
                    var metaPath = Path.Combine(UploadsFolderAbs(), $"venta-{ventaId}.meta.json");
                    await System.IO.File.WriteAllTextAsync(metaPath, JsonSerializer.Serialize(metaObj), ct);

                    // Comprobante
                    if (esDeposito && Comprobante != null && Comprobante.Length > 0)
                    {
                        var save = await GuardarComprobanteAsync(ventaId, Comprobante, ct);
                        if (!save.ok)
                        {
                            TempData["MensajeError"] = save.error ?? "No se pudo guardar el comprobante.";
                            return RedirectToAction(nameof(Resumen));
                        }
                    }

                    // 3) Actualizar total de la venta con envío (y cupón si corresponde)
                    var venta = await _context.Ventas.FirstOrDefaultAsync(v => v.VentaID == ventaId, ct);
                    if (venta != null)
                    {
                        var subtotal = detalles.Sum(c => c.Precio * c.Cantidad);
                        var cupon = HttpContext.Session.GetObjectFromJson<Promocion>("Cupon");
                        decimal descuento = cupon?.Descuento ?? 0m;
                        decimal baseTotal = Math.Max(0m, subtotal - descuento);

                        venta.Total = baseTotal + envioTotal;
                        await _context.SaveChangesAsync(ct);
                    }
                }

                // 4) Reiniciar carrito
                await _carrito.AddAsync(user);

                TempData["MensajeExito"] = "¡Gracias por tu compra!";
                if (ventaId > 0) return RedirectToAction(nameof(CompraExito), new { id = ventaId });
                return RedirectToAction("Index", "MisCompras");
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Validación al confirmar compra");
                var det = await _carrito.LoadCartDetails(carrito.CarritoID);
                ViewBag.HasAddress = true;
                ViewBag.CarritoDetalles = det;
                ViewBag.TotalCompra = det.Sum(cd => cd.Precio * cd.Cantidad);
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

        // =========================
        // SUBIR COMPROBANTE / PANTALLAS
        // =========================
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SubirComprobante(int id, IFormFile? archivo, string? depositante, string? banco, CancellationToken ct = default)
        {
            var uid = _userManager.GetUserId(User);
            if (uid == null) return Unauthorized();

            var venta = await _context.Ventas
                .Include(v => v.Usuario)
                .FirstOrDefaultAsync(v => v.VentaID == id && v.UsuarioId == uid, ct);

            if (venta == null)
                return EsAjax() ? Json(new { ok = false, error = "Venta no encontrada." }) : NotFound();

            string? relUrl = null;

            if (archivo != null)
            {
                var res = await GuardarComprobanteAsync(id, archivo, ct);
                if (!res.ok)
                {
                    if (EsAjax()) return Json(new { ok = false, error = res.error });
                    TempData["MensajeError"] = res.error;
                    return RedirectToAction(nameof(CompraExito), new { id });
                }
                relUrl = res.relUrl;
            }

            if (!string.IsNullOrWhiteSpace(depositante) || !string.IsNullOrWhiteSpace(banco))
                GuardarMetaDeposito(id, depositante, banco);

            if (EsAjax()) return Json(new { ok = true, url = relUrl });

            TempData["MensajeExito"] = "Información de depósito registrada.";
            return RedirectToAction(nameof(CompraExito), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> CompraExito(int id, CancellationToken ct = default)
        {
            var uid = _userManager.GetUserId(User);
            var venta = await _context.Ventas
                .AsNoTracking()
                .Include(v => v.Usuario)
                .FirstOrDefaultAsync(v => v.VentaID == id && v.UsuarioId == uid, ct);
            if (venta == null) return RedirectToAction("Index", "Home");
            return View(venta);
        }

        [HttpGet] public IActionResult CompraError() => View();

        [HttpGet]
        public async Task<IActionResult> Comprobante(int id, CancellationToken ct = default)
        {
            var uid = _userManager.GetUserId(User);
            var venta = await _context.Ventas
                .Include(v => v.Usuario)
                .Include(v => v.DetalleVentas).ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(v => v.VentaID == id && v.UsuarioId == uid, ct);
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
{string.Join("", venta.DetalleVentas.Select(d => $"<tr><td>{d.Producto?.Nombre}</td><td class='r'>{d.Cantidad}</td><td class='r'>{d.PrecioUnitario:C2}</td><td class='r'>{d.Subtotal:C2}</td></tr>"))}
</tbody>
<tfoot><tr><th colspan='3' class='r'>Total</th><th class='r'>{venta.Total:C2}</th></tr></tfoot>
</table>
<p class='muted'>Método de pago: {venta.MetodoPago ?? "N/D"} · Estado: {venta.Estado ?? "N/D"}</p>
</body></html>";
            var bytes = System.Text.Encoding.UTF8.GetBytes(html);
            return File(bytes, "text/html", $"comprobante-{venta.VentaID}.html");
        }

        // =========================
        // HELPERS
        // =========================
        private bool EsAjax() =>
            string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

        private IActionResult RedirectToReferrerOr(string action, string controller = "Compras")
        {
            var referer = Request.Headers["Referer"].ToString();
            return !string.IsNullOrWhiteSpace(referer) ? Redirect(referer) : RedirectToAction(action, controller);
        }

        private string UploadsFolderAbs() =>
            Path.Combine(_env.WebRootPath, "uploads", "comprobantes");

        private bool EsMimePermitido(IFormFile f)
        {
            if (f == null) return false;
            var okMime = (f.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ?? false)
                         || string.Equals(f.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase);
            var ext = Path.GetExtension(f.FileName ?? string.Empty);
            return okMime && _extPermitidas.Contains(ext);
        }

        private async Task<(bool ok, string? relUrl, string? error)> GuardarComprobanteAsync(int ventaId, IFormFile archivo, CancellationToken ct)
        {
            if (archivo == null || archivo.Length == 0)
                return (false, null, "No se recibió archivo.");
            if (!EsMimePermitido(archivo))
                return (false, null, "Formato no permitido. Usa JPG, PNG, WEBP o PDF.");
            if (archivo.Length > MAX_FILE_MB * 1024 * 1024)
                return (false, null, $"El archivo supera {MAX_FILE_MB}MB.");

            var folder = UploadsFolderAbs();
            Directory.CreateDirectory(folder);

            foreach (var old in Directory.EnumerateFiles(folder, $"venta-{ventaId}.*", SearchOption.TopDirectoryOnly))
            {
                try { System.IO.File.Delete(old); } catch { /* ignore */ }
            }

            var ext = Path.GetExtension(archivo.FileName);
            var fileAbs = Path.Combine(folder, $"venta-{ventaId}{ext}");
            await using var fs = new FileStream(fileAbs, FileMode.Create, FileAccess.Write, FileShare.None);
            await archivo.CopyToAsync(fs, ct);

            var rel = Path.GetRelativePath(_env.WebRootPath, fileAbs).Replace("\\", "/");
            return (true, "/" + rel.TrimStart('/'), null);
        }

        private void GuardarMetaDeposito(int ventaId, string? depositante, string? banco)
        {
            var folder = UploadsFolderAbs();
            Directory.CreateDirectory(folder);

            var metaObj = new
            {
                depositante = string.IsNullOrWhiteSpace(depositante) ? null : depositante.Trim(),
                banco = string.IsNullOrWhiteSpace(banco) ? null : banco.Trim(),
                ts = DateTime.UtcNow
            };
            var metaPath = Path.Combine(folder, $"venta-{ventaId}.meta.json");
            System.IO.File.WriteAllText(metaPath, JsonSerializer.Serialize(metaObj));
        }

        // Resuelve admin/tienda con validación de Activo.
        private async Task<(bool ok, object metaBanco, string destino, string? error)>
            ResolverBancoSeleccionadoAsync(string bancoSeleccionado, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(bancoSeleccionado))
                return (false, new { }, "", "No se especificó banco.");

            var raw = bancoSeleccionado.Trim();
            if (!raw.Contains(':', StringComparison.Ordinal))
                raw = $"admin:{raw}";

            var parts = raw.Split(':', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) return (false, new { }, "", "Formato de banco inválido.");

            var scope = parts[0].ToLowerInvariant();

            // ADMIN
            if (scope == "admin")
            {
                var codigo = parts[1].Trim().ToLowerInvariant();
                var admin = (await _bancosSvc.GetAdminAsync(ct)).Where(c => c.Activo).ToList();
                var sel = admin.FirstOrDefault(c => string.Equals(c.Codigo?.Trim(), codigo, StringComparison.OrdinalIgnoreCase));
                if (sel == null) return (false, new { }, "admin", "Banco del administrador inválido o inactivo.");

                var meta = new
                {
                    destino = "admin",
                    banco = new { codigo = sel.Codigo, nombre = sel.Nombre, numero = sel.Numero, tipo = sel.Tipo, titular = sel.Titular, ruc = sel.Ruc }
                };
                return (true, meta, "admin", null);
            }

            // TIENDA
            if (scope == "tienda")
            {
                var vendedorId = parts[1].Trim();
                if (string.IsNullOrWhiteSpace(vendedorId))
                    return (false, new { }, "tienda", "Vendedor no especificado.");

                var cuentas = (await _bancosSvc.GetByProveedorAsync(vendedorId, ct)).Where(c => c.Activo).ToList();
                if (cuentas.Count == 0)
                    return (false, new { }, "tienda", "El vendedor no tiene cuentas activas.");

                Simone.Configuration.CuentaBancaria? sel = null;

                if (parts.Length >= 3)
                {
                    var codigo = parts[2].Trim();
                    sel = cuentas.FirstOrDefault(c => string.Equals(c.Codigo?.Trim(), codigo, StringComparison.OrdinalIgnoreCase));
                    if (sel == null)
                        return (false, new { }, "tienda", "La cuenta seleccionada del vendedor no existe o no está activa.");
                }
                else
                {
                    if (cuentas.Count == 1) sel = cuentas[0];
                    else return (false, new { }, "tienda", "Selecciona una cuenta específica del vendedor.");
                }

                var meta = new
                {
                    destino = "tienda",
                    vendedorId,
                    banco = new { codigo = sel!.Codigo, nombre = sel.Nombre, numero = sel.Numero, tipo = sel.Tipo, titular = sel.Titular, ruc = sel.Ruc }
                };
                return (true, meta, "tienda", null);
            }

            return (false, new { }, "", "Ámbito de banco inválido.");
        }
    }
}
