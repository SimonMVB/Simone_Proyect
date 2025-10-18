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
        public async Task<IActionResult> Catalogo(
            int? categoriaID,
            int[]? subcategoriaIDs,
            [FromQuery] string[]? ColoresSeleccionados,
            [FromQuery] string[]? TallasSeleccionadas,
            [FromQuery] bool SoloDisponibles = false,
            [FromQuery] string? sort = null,
            int pageNumber = 1,
            int pageSize = 20,
            CancellationToken ct = default)
        {
            // Sanidad de paginación
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Clamp(pageSize, 4, 60);

            // Sidebar: categorías y subcategorías
            var categorias = await _categorias.GetAllAsync();
            var subcategorias = categoriaID.HasValue
                ? await _subcategorias.GetByCategoriaIdAsync(categoriaID.Value)
                : new List<Subcategorias>();

            // Normalizar filtros
            var coloresSel = (ColoresSeleccionados ?? Array.Empty<string>())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var tallasSel = (TallasSeleccionadas ?? Array.Empty<string>())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Base query
            IQueryable<Producto> query = _context.Productos.AsNoTracking();

            if (categoriaID.HasValue)
                query = query.Where(p => p.CategoriaID == categoriaID.Value);

            if (subcategoriaIDs is { Length: > 0 })
                query = query.Where(p => subcategoriaIDs!.Contains(p.SubcategoriaID));

            // Solo disponibles (variante o producto simple)
            if (SoloDisponibles)
            {
                query = query.Where(p =>
                    _context.ProductoVariantes.Any(v => v.ProductoID == p.ProductoID && v.Stock > 0)
                    || (!_context.ProductoVariantes.Any(v => v.ProductoID == p.ProductoID) && p.Stock > 0)
                );
            }

            // Filtro por Color
            if (coloresSel.Any())
            {
                query = query.Where(p =>
                    _context.ProductoVariantes.Any(v => v.ProductoID == p.ProductoID && v.Color != null && coloresSel.Contains(v.Color))
                    || (p.Color != null && coloresSel.Contains(p.Color))
                );
            }

            // Filtro por Talla
            if (tallasSel.Any())
            {
                query = query.Where(p =>
                    _context.ProductoVariantes.Any(v => v.ProductoID == p.ProductoID && v.Talla != null && tallasSel.Contains(v.Talla))
                    || (p.Talla != null && tallasSel.Contains(p.Talla))
                );
            }

            // Ordenamiento
            switch ((sort ?? "").Trim().ToLowerInvariant())
            {
                case "precio_asc":
                    query = query
                        .Select(p => new
                        {
                            P = p,
                            MinPrecio = _context.ProductoVariantes
                                .Where(v => v.ProductoID == p.ProductoID)
                                .Select(v => (decimal?)v.PrecioVenta)
                                .DefaultIfEmpty(p.PrecioVenta)
                                .Min()
                        })
                        .OrderBy(x => x.MinPrecio)
                        .ThenBy(x => x.P.Nombre)
                        .Select(x => x.P);
                    break;

                case "precio_desc":
                    query = query
                        .Select(p => new
                        {
                            P = p,
                            MaxPrecio = _context.ProductoVariantes
                                .Where(v => v.ProductoID == p.ProductoID)
                                .Select(v => (decimal?)v.PrecioVenta)
                                .DefaultIfEmpty(p.PrecioVenta)
                                .Max()
                        })
                        .OrderByDescending(x => x.MaxPrecio)
                        .ThenBy(x => x.P.Nombre)
                        .Select(x => x.P);
                    break;

                case "nuevos":
                    query = query.OrderByDescending(p => p.FechaAgregado).ThenBy(p => p.Nombre);
                    break;

                case "mas_vendidos":
                    {
                        var ventasQ = _context.DetalleVentas.AsNoTracking()
                            .GroupBy(d => d.ProductoID)
                            .Select(g => new { ProductoID = g.Key, Cant = g.Sum(x => x.Cantidad) });

                        query = query
                            .GroupJoin(ventasQ, p => p.ProductoID, s => s.ProductoID,
                                (p, s) => new { P = p, Cant = s.Select(x => (int?)x.Cant).FirstOrDefault() ?? 0 })
                            .OrderByDescending(x => x.Cant)
                            .ThenBy(x => x.P.Nombre)
                            .Select(x => x.P);
                    }
                    break;

                default:
                    query = query.OrderBy(p => p.Nombre);
                    break;
            }

            // Conteo total tras filtros
            var totalProducts = await query.CountAsync(ct);

            // Paginación
            var productos = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            // Variantes para productos de la página
            var pageIds = productos.Select(p => p.ProductoID).ToList();

            var variantesPageQ = _context.ProductoVariantes
                .AsNoTracking()
                .Where(v => pageIds.Contains(v.ProductoID));

            if (SoloDisponibles)
                variantesPageQ = variantesPageQ.Where(v => v.Stock > 0);

            var variantesPage = await variantesPageQ.ToListAsync(ct);

            var variantesPorProducto = variantesPage
                .GroupBy(v => v.ProductoID)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Facets (sobre todo el set filtrado, no solo la página)
            var filteredProductIds = await query.Select(p => p.ProductoID).ToListAsync(ct);

            var variantsFilteredQ = _context.ProductoVariantes.AsNoTracking()
                .Where(v => filteredProductIds.Contains(v.ProductoID));

            if (SoloDisponibles)
                variantsFilteredQ = variantsFilteredQ.Where(v => v.Stock > 0);

            var coloresVar = await variantsFilteredQ
                .Where(v => v.Color != null && v.Color != "")
                .Select(v => v.Color!)
                .Distinct()
                .ToListAsync(ct);

            var tallasVar = await variantsFilteredQ
                .Where(v => v.Talla != null && v.Talla != "")
                .Select(v => v.Talla!)
                .Distinct()
                .ToListAsync(ct);

            // Incluir color/talla de productos sin variantes dentro del set filtrado
            var sinVarQ = _context.Productos.AsNoTracking().Where(p => filteredProductIds.Contains(p.ProductoID));

            if (SoloDisponibles)
                sinVarQ = sinVarQ.Where(p =>
                    !_context.ProductoVariantes.Any(v => v.ProductoID == p.ProductoID) && p.Stock > 0);

            var coloresProd = await sinVarQ
                .Where(p => p.Color != null && p.Color != "")
                .Select(p => p.Color!)
                .Distinct()
                .ToListAsync(ct);

            var tallasProd = await sinVarQ
                .Where(p => p.Talla != null && p.Talla != "")
                .Select(p => p.Talla!)
                .Distinct()
                .ToListAsync(ct);

            var coloresDisponibles = coloresVar
                .Concat(coloresProd)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var tallasDisponibles = tallasVar
                .Concat(tallasProd)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Modelo
            var model = new CatalogoViewModel
            {
                Categorias = categorias,
                SelectedCategoriaID = categoriaID,
                Subcategorias = subcategorias,
                SelectedSubcategoriaIDs = subcategoriaIDs?.ToList() ?? new List<int>(),
                Productos = productos,

                // filtros & facets
                ColoresSeleccionados = coloresSel,
                TallasSeleccionadas = tallasSel,
                SoloDisponibles = SoloDisponibles,
                ColoresDisponibles = coloresDisponibles,
                TallasDisponibles = tallasDisponibles,

                // paginación
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalProducts = totalProducts,

                // variantes diccionario (para la vista)
                VariantesPorProducto = variantesPorProducto,

                // sort
                Sort = sort
            };

            // Favoritos
            if (User.Identity?.IsAuthenticated == true)
            {
                var userId = _userManager.GetUserId(User);
                model.ProductoIDsFavoritos = await _context.Favoritos
                    .Where(f => f.UsuarioId == userId)
                    .Select(f => f.ProductoId)
                    .ToListAsync(ct);
            }
            else
            {
                model.ProductoIDsFavoritos = new List<int>();
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
                .Include(p => p.Variantes)
                .FirstOrDefaultAsync(p => p.ProductoID == productoID, ct);

            if (producto == null)
            {
                TempData["MensajeError"] = "Producto no encontrado.";
                return RedirectToAction(nameof(Catalogo));
            }

            // >>> GALERÍA: cargar lista de imágenes (JSON de galería o DB) <<<
            var (portada, imagenes) = await CargarGaleriaAsync(producto.ProductoID, ct);
            if (string.IsNullOrWhiteSpace(portada))
                portada = producto.ImagenPath;

            if (imagenes == null || imagenes.Count == 0)
            {
                imagenes = string.IsNullOrWhiteSpace(portada)
                    ? new List<string>()
                    : new List<string> { portada };
            }

            ViewBag.Portada = portada;
            ViewBag.Galeria = imagenes;

            // Relacionados
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

            ViewBag.Variantes = producto.Variantes?.OrderBy(v => v.Color).ThenBy(v => v.Talla).ToList()
                                ?? new List<ProductoVariante>();

            return View(producto);
        }

        // =========================
        // CARRITO
        // =========================
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AnadirAlCarrito(
            [Bind("ProductoID,Cantidad")] CatalogoViewModel model,
            [FromForm(Name = "ProductoVarianteID")] int? productoVarianteId,  // 👈 bind seguro desde la vista
            CancellationToken ct = default)
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

            // ¿Tiene variantes?
            var tieneVariantes = await _context.ProductoVariantes
                .AsNoTracking()
                .AnyAsync(v => v.ProductoID == model.ProductoID, ct);

            ProductoVariante? variante = null;
            if (tieneVariantes)
            {
                if (!productoVarianteId.HasValue)
                {
                    var msg = "Selecciona Color y Talla antes de añadir al carrito.";
                    if (EsAjax()) return Json(new { ok = false, needVariant = true, error = msg });
                    TempData["MensajeError"] = msg;
                    return RedirectToReferrerOr("VerProducto");
                }

                variante = await _context.ProductoVariantes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(v => v.ProductoVarianteID == productoVarianteId.Value, ct);

                if (variante == null || variante.ProductoID != model.ProductoID)
                {
                    var msg = "La combinación seleccionada no es válida para este producto.";
                    if (EsAjax()) return Json(new { ok = false, error = msg });
                    TempData["MensajeError"] = msg;
                    return RedirectToReferrerOr("VerProducto");
                }
            }

            var carrito = await _carrito.GetByClienteIdAsync(user.Id)
                        ?? (await _carrito.AddAsync(user) ? await _carrito.GetByClienteIdAsync(user.Id) : null);

            if (carrito == null)
            {
                TempData["MensajeError"] = "No fue posible obtener tu carrito.";
                return RedirectToReferrerOr("Catalogo");
            }

            // Cargar detalles para validar stock respecto a lo ya añadido
            var detalles = await _carrito.LoadCartDetails(carrito.CarritoID);
            var enCarrito = detalles
                .Where(c => c.ProductoID == model.ProductoID &&
                            c.ProductoVarianteID == (productoVarianteId.HasValue ? productoVarianteId.Value : (int?)null))
                .Sum(c => c.Cantidad);

            // Validación de stock (variante o producto)
            if (tieneVariantes)
            {
                var disponible = variante!.Stock;
                if (enCarrito + model.Cantidad > disponible)
                {
                    var msg = "La cantidad solicitada supera el stock disponible para la combinación seleccionada.";
                    if (EsAjax()) return Json(new { ok = false, error = msg, stock = disponible, enCarrito });
                    TempData["MensajeError"] = msg;
                    return RedirectToReferrerOr("VerProducto");
                }
            }
            else
            {
                if (enCarrito + model.Cantidad > producto.Stock)
                {
                    var msg = "La cantidad solicitada supera el stock disponible.";
                    if (EsAjax()) return Json(new { ok = false, error = msg, stock = producto.Stock, enCarrito });
                    TempData["MensajeError"] = msg;
                    return RedirectToReferrerOr("Catalogo");
                }
            }

            // Añadir (con variante si aplica)
            var ok = await _carrito.AnadirProducto(producto, user, model.Cantidad, productoVarianteId);
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
                .Include(cd => cd.Variante)
                .FirstOrDefaultAsync(cd => cd.CarritoDetalleID == carritoDetalleID, ct);

            if (detalle == null || detalle.Carrito?.UsuarioId != user.Id) return NotFound();
            if (detalle.Producto == null) return NotFound();

            // Validación de stock
            if (detalle.Variante != null)
            {
                if (cantidad > detalle.Variante.Stock)
                {
                    var msg = "La cantidad supera el stock disponible para la combinación seleccionada.";
                    if (EsAjax()) return Json(new { ok = false, error = msg, stock = detalle.Variante.Stock });
                    TempData["MensajeError"] = msg;
                    return RedirectToReferrerOr("Resumen");
                }
            }
            else
            {
                if (cantidad > detalle.Producto.Stock)
                {
                    var msg = "La cantidad supera el stock disponible.";
                    if (EsAjax()) return Json(new { ok = false, error = msg, stock = detalle.Producto.Stock });
                    TempData["MensajeError"] = msg;
                    return RedirectToReferrerOr("Resumen");
                }
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

            // Envío por vendedor
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

            // Mapa legible {NombreTienda -> $}
            var usuarios = await _context.Users.AsNoTracking()
                .Where(u => vendedorUserIds.Contains(u.Id))
                .Select(u => new { u.Id, u.VendedorId, u.NombreCompleto, u.Email })
                .ToListAsync(ct);

            var tiendaIds = usuarios.Where(x => x.VendedorId.HasValue)
                                    .Select(x => x.VendedorId!.Value)
                                    .Distinct()
                                    .ToList();

            var mapTienda = tiendaIds.Count == 0
                ? new Dictionary<int, string>()
                : await _context.Vendedores.AsNoTracking()
                    .Where(t => tiendaIds.Contains(t.VendedorId))
                    .ToDictionaryAsync(t => t.VendedorId, t => t.Nombre, ct);

            var envioPorTienda = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in envioPorVendedor)
            {
                var u = usuarios.FirstOrDefault(x => x.Id == kv.Key);
                string etiqueta;

                if (u?.VendedorId != null && mapTienda.TryGetValue(u.VendedorId.Value, out var tiendaNombre) && !string.IsNullOrWhiteSpace(tiendaNombre))
                    etiqueta = tiendaNombre;
                else
                    etiqueta = u?.NombreCompleto ?? u?.Email ?? kv.Key;

                if (!envioPorTienda.ContainsKey(etiqueta))
                    envioPorTienda[etiqueta] = 0m;
                envioPorTienda[etiqueta] += kv.Value;
            }

            // Datos para la vista
            ViewBag.CarritoDetalles = detalles;
            ViewBag.Subtotal = subtotal;
            ViewBag.TotalCompra = subtotal;
            ViewBag.EnvioTotal = envioTotal;
            ViewBag.EnvioPorVendedor = envioPorVendedor; // compat
            ViewBag.EnvioPorTienda = envioPorTienda;
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

            // Verificación previa con variantes
            var faltantes = detalles.Where(d =>
                (d.Variante != null && d.Variante.Stock < d.Cantidad) ||
                (d.Variante == null && d.Producto != null && d.Producto.Stock < d.Cantidad))
                .ToList();

            if (faltantes.Any())
            {
                ViewBag.HasAddress = true;
                ViewBag.CarritoDetalles = detalles;
                ViewBag.TotalCompra = detalles.Sum(cd => cd.Precio * cd.Cantidad);
                TempData["MensajeError"] = "Stock insuficiente para: " +
                    string.Join(", ", faltantes.Select(f => $"{(f.Producto?.Nombre ?? "Producto")}{(f.Variante != null ? $" [{f.Variante.Color}/{f.Variante.Talla}]" : "")}"));
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

                    // Resolver banco elegido
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
        public async Task<IActionResult> SubirComprobante(
            int id,
            IFormFile? archivo,
            string? depositante,
            string? banco,
            CancellationToken ct = default)
        {
            var uid = _userManager.GetUserId(User);
            if (uid == null) return Unauthorized();

            var venta = await _context.Ventas
                .Include(v => v.Usuario)
                .FirstOrDefaultAsync(v => v.VentaID == id && v.UsuarioId == uid, ct);

            if (venta == null)
                return EsAjax() ? Json(new { ok = false, error = "Venta no encontrada." }) : NotFound();

            // Fallback: algunos formularios envían "BancoSeleccionado" en vez de "banco"
            if (string.IsNullOrWhiteSpace(banco))
            {
                var alt = Request?.Form?["BancoSeleccionado"].ToString();
                if (!string.IsNullOrWhiteSpace(alt)) banco = alt;
            }

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
                relUrl = res.relUrl; // ya viene con ?v= (cache-buster)
            }

            // Persistir/merge de metadatos (no pisar si viene null)
            if (!string.IsNullOrWhiteSpace(depositante) || !string.IsNullOrWhiteSpace(banco))
                await GuardarMetaDepositoAsync(id, depositante, banco, ct);

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


        [HttpGet]
        [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult ComprobanteUrl(int id)
        {
            var folder = UploadsFolderAbs();
            var file = Directory.EnumerateFiles(folder, $"venta-{id}.*", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (string.IsNullOrEmpty(file)) return Json(new { ok = false });

            var rel = Path.GetRelativePath(_env.WebRootPath, file).Replace("\\", "/");
            var ticks = System.IO.File.GetLastWriteTimeUtc(file).Ticks;
            return Json(new { ok = true, url = "/" + rel.TrimStart('/') + "?v=" + ticks });
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

        private async Task<(bool ok, string? relUrl, string? error)> GuardarComprobanteAsync(
    int ventaId,
    IFormFile archivo,
    CancellationToken ct)
        {
            if (archivo == null || archivo.Length == 0)
                return (false, null, "No se recibió archivo.");
            if (!EsMimePermitido(archivo))
                return (false, null, "Formato no permitido. Usa JPG, PNG, WEBP o PDF.");
            if (archivo.Length > MAX_FILE_MB * 1024 * 1024)
                return (false, null, $"El archivo supera {MAX_FILE_MB}MB.");

            var folder = UploadsFolderAbs();
            Directory.CreateDirectory(folder);

            // Borrar anteriores (cualquier extensión)
            foreach (var old in Directory.EnumerateFiles(folder, $"venta-{ventaId}.*", SearchOption.TopDirectoryOnly))
            {
                try { System.IO.File.Delete(old); } catch { /* ignore */ }
            }

            var ext = Path.GetExtension(archivo.FileName);
            var fileAbs = Path.Combine(folder, $"venta-{ventaId}{ext}");

            await using (var fs = new FileStream(fileAbs, FileMode.Create, FileAccess.Write, FileShare.None))
                await archivo.CopyToAsync(fs, ct);

            // Cache-buster
            var ticks = System.IO.File.GetLastWriteTimeUtc(fileAbs).Ticks;
            var rel = Path.GetRelativePath(_env.WebRootPath, fileAbs).Replace("\\", "/");
            var url = "/" + rel.TrimStart('/') + "?v=" + ticks;

            return (true, url, null);
        }


        private async Task GuardarMetaDepositoAsync(int ventaId, string? depositante, string? bancoRaw, CancellationToken ct)
        {
            var folder = UploadsFolderAbs();
            Directory.CreateDirectory(folder);
            var metaPath = Path.Combine(folder, $"venta-{ventaId}.meta.json");

            // 1) Cargar meta existente (si lo hay)
            Dictionary<string, object?> meta;
            try
            {
                if (System.IO.File.Exists(metaPath))
                {
                    var json = await System.IO.File.ReadAllTextAsync(metaPath, ct);
                    meta = JsonSerializer.Deserialize<Dictionary<string, object?>>(json)
                           ?? new Dictionary<string, object?>();
                }
                else meta = new Dictionary<string, object?>();
            }
            catch
            {
                meta = new Dictionary<string, object?>();
            }

            // 2) Resolver banco si viene algo (admin:xxx | tienda:<uid>:xxx o nombre simple)
            string? bancoPretty = null;
            object? bancoSeleccionObj = null;

            if (!string.IsNullOrWhiteSpace(bancoRaw))
            {
                var (okBanco, metaBancoObj, destinoPago, _) = await ResolverBancoSeleccionadoAsync(bancoRaw!, ct);
                if (okBanco)
                {
                    // metaBancoObj = { destino, banco = { codigo, nombre, ... } }
                    try
                    {
                        var bancoJson = JsonSerializer.Serialize(metaBancoObj);
                        using var doc = JsonDocument.Parse(bancoJson);
                        if (doc.RootElement.TryGetProperty("banco", out var bank))
                        {
                            if (bank.TryGetProperty("nombre", out var nombre) && nombre.ValueKind == JsonValueKind.String)
                                bancoPretty = nombre.GetString();
                        }
                    }
                    catch { /* ignore parse */ }

                    bancoSeleccionObj = metaBancoObj; // full object (para vistas detalladas)
                }
                else
                {
                    // Guarda el raw si no se pudo resolver
                    bancoPretty = bancoRaw?.Trim();
                }
            }

            // 3) Merge no destructivo
            if (!string.IsNullOrWhiteSpace(depositante))
                meta["depositante"] = depositante.Trim();

            if (!string.IsNullOrWhiteSpace(bancoPretty))
                meta["banco"] = bancoPretty; // string “humano”

            if (bancoSeleccionObj != null)   // objeto rico si pudimos resolver
                meta["bancoSeleccion"] = bancoSeleccionObj;

            meta["ts"] = DateTime.UtcNow;

            // 4) Guardar
            await System.IO.File.WriteAllTextAsync(metaPath, JsonSerializer.Serialize(meta), ct);
        }


        // >>> GALERÍA — helpers para cargar imágenes del producto <<<
        private string ProductFolderAbs(int productId) =>
            Path.Combine(_env.WebRootPath, "images", "Productos", productId.ToString());

        private async Task<(string? portada, List<string> imagenes)> CargarGaleriaAsync(int productoId, CancellationToken ct)
        {
            // 1) Intentar JSON de galería
            try
            {
                var folder = ProductFolderAbs(productoId);
                var metaPath = Path.Combine(folder, $"product-{productoId}.gallery.json");
                if (System.IO.File.Exists(metaPath))
                {
                    using var s = System.IO.File.OpenRead(metaPath);
                    using var doc = await JsonDocument.ParseAsync(s, cancellationToken: ct);
                    string? portada = null;
                    var imgs = new List<string>();

                    if (doc.RootElement.TryGetProperty("portada", out var p) && p.ValueKind == JsonValueKind.String)
                        portada = p.GetString();

                    if (doc.RootElement.TryGetProperty("images", out var arr) && arr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var e in arr.EnumerateArray())
                        {
                            if (e.ValueKind == JsonValueKind.String)
                            {
                                var rel = e.GetString();
                                if (!string.IsNullOrWhiteSpace(rel))
                                    imgs.Add(NormalizeRel(rel!));
                            }
                        }
                    }

                    return (NormalizeRel(portada), imgs.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo leer el JSON de galería para producto {pid}", productoId);
            }

            // 2) Fallback a DB ProductoImagenes
            try
            {
                var filas = await _context.ProductoImagenes
                    .AsNoTracking()
                    .Where(pi => pi.ProductoID == productoId)
                    .OrderBy(pi => pi.Orden)
                    .ToListAsync(ct);

                if (filas.Count > 0)
                {
                    var portada = filas.FirstOrDefault(x => x.Principal)?.Path ?? filas.First().Path;
                    var imgs = filas.Select(x => NormalizeRel(x.Path)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    return (NormalizeRel(portada), imgs);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo leer ProductoImagenes para producto {pid}", productoId);
            }

            // 3) Sin datos
            return (null, new List<string>());
        }

        private static string? NormalizeRel(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            var u = url.Replace("\\", "/");
            return u.StartsWith("/") ? u : "/" + u;
        }
        // <<< GALERÍA

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
                var sel = admin.FirstOrDefault(c =>
    string.Equals(c.Codigo?.Trim(), codigo, StringComparison.OrdinalIgnoreCase));

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
                    sel = cuentas.FirstOrDefault(c =>
    string.Equals(c.Codigo?.Trim(), codigo, StringComparison.OrdinalIgnoreCase));
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
