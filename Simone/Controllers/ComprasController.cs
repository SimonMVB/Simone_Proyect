using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
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
    /// <summary>
    /// Controlador de compras - Catálogo, Productos, Carrito y Checkout
    /// Versión CORREGIDA: Guarda Depositante, Banco y ComprobanteUrl en la BD
    /// </summary>
    public class ComprasController : Controller
    {
        #region Dependencias

        private readonly TiendaDbContext _context;
        private readonly ProductosService _productos;
        private readonly CategoriasService _categorias;
        private readonly SubcategoriasService _subcategorias;
        private readonly CarritoService _carrito;
        private readonly UserManager<Usuario> _userManager;
        private readonly ILogger<ComprasController> _logger;
        private readonly IWebHostEnvironment _env;
        private readonly ResolverPagos _pagosResolver;
        private readonly IBancosConfigService _bancosSvc;
        private readonly EnviosCarritoService _enviosCarrito;
        private readonly IMemoryCache _cache;

        #endregion

        #region Constantes

        // Paginación
        private const int PAGE_SIZE_MIN = 4;
        private const int PAGE_SIZE_MAX = 60;
        private const int PAGE_SIZE_DEFAULT = 20;

        // Archivos
        private const int MAX_FILE_SIZE_MB = 5;
        private const int MAX_FILE_SIZE_BYTES = MAX_FILE_SIZE_MB * 1024 * 1024;

        // Productos relacionados
        private const int MAX_RELATED_PRODUCTS = 8;

        // Cache
        private const string CACHE_KEY_CATEGORIAS = "Categorias_All";
        private const string CACHE_KEY_SUBCATEGORIAS_PREFIX = "Subcategorias_Cat_";
        private const string CACHE_KEY_PRODUCTOS_POPULARES = "Productos_Populares";

        private static readonly TimeSpan CACHE_DURATION_CATEGORIAS = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan CACHE_DURATION_SUBCATEGORIAS = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan CACHE_DURATION_PRODUCTOS = TimeSpan.FromMinutes(15);

        // Session & Headers
        private const string SESSION_KEY_CUPON = "Cupon";
        private const string HEADER_AJAX = "X-Requested-With";
        private const string HEADER_AJAX_VALUE = "XMLHttpRequest";
        private const string HEADER_REFERER = "Referer";

        // Folders
        private const string FOLDER_UPLOADS = "uploads";
        private const string FOLDER_COMPROBANTES = "comprobantes";
        private const string FOLDER_IMAGES = "images";
        private const string FOLDER_PRODUCTOS = "Productos";

        // Extensiones permitidas
        private static readonly HashSet<string> _extPermitidas = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".pdf"
        };

        #endregion

        #region Constructor

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
            EnviosCarritoService enviosCarrito,
            IMemoryCache cache)
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
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        #endregion

        #region Helpers - General

        /// <summary>
        /// Determina si la solicitud es AJAX de forma segura
        /// </summary>
        private bool EsAjax()
        {
            return Request.Headers.TryGetValue(HEADER_AJAX, out var value) &&
                   string.Equals(value, HEADER_AJAX_VALUE, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Redirige al referer o a una acción por defecto
        /// </summary>
        private IActionResult RedirectToReferrerOr(string action, string controller = "Compras")
        {
            if (Request.Headers.TryGetValue(HEADER_REFERER, out var referer) &&
                !string.IsNullOrWhiteSpace(referer))
            {
                return Redirect(referer.ToString());
            }

            return RedirectToAction(action, controller);
        }

        #endregion

        #region Helpers - Cache

        /// <summary>
        /// Obtiene categorías con cache
        /// </summary>
        private async Task<List<Categorias>> GetCategoriasConCacheAsync()
        {
            return await _cache.GetOrCreateAsync(
                CACHE_KEY_CATEGORIAS,
                async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = CACHE_DURATION_CATEGORIAS;
                    _logger.LogDebug("Cargando categorías desde BD (cache miss)");

                    var categorias = await _categorias.GetAllAsync();
                    return categorias?.ToList() ?? new List<Categorias>();
                }) ?? new List<Categorias>();
        }

        /// <summary>
        /// Obtiene subcategorías con cache
        /// </summary>
        private async Task<List<Subcategorias>> GetSubcategoriasConCacheAsync(int categoriaId)
        {
            var cacheKey = $"{CACHE_KEY_SUBCATEGORIAS_PREFIX}{categoriaId}";

            return await _cache.GetOrCreateAsync(
                cacheKey,
                async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = CACHE_DURATION_SUBCATEGORIAS;
                    _logger.LogDebug("Cargando subcategorías desde BD (cache miss) - CategoriaId: {CatId}", categoriaId);

                    var subcategorias = await _subcategorias.GetByCategoriaIdAsync(categoriaId);
                    return subcategorias?.ToList() ?? new List<Subcategorias>();
                }) ?? new List<Subcategorias>();
        }

        #endregion

        #region Helpers - Archivos

        /// <summary>
        /// Obtiene ruta absoluta de la carpeta de uploads
        /// </summary>
        private string UploadsFolderAbs() =>
            Path.Combine(_env.WebRootPath, FOLDER_UPLOADS, FOLDER_COMPROBANTES);

        /// <summary>
        /// Obtiene ruta absoluta de la carpeta de un producto
        /// </summary>
        private string ProductFolderAbs(int productId) =>
            Path.Combine(_env.WebRootPath, FOLDER_IMAGES, FOLDER_PRODUCTOS, productId.ToString());

        /// <summary>
        /// Valida si el archivo tiene un MIME type permitido
        /// </summary>
        private bool EsMimePermitido(IFormFile? archivo)
        {
            if (archivo == null) return false;

            var okMime = (archivo.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ?? false)
                         || string.Equals(archivo.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase);

            var ext = Path.GetExtension(archivo.FileName ?? string.Empty);
            return okMime && _extPermitidas.Contains(ext);
        }

        /// <summary>
        /// Normaliza una URL relativa
        /// </summary>
        private static string? NormalizeRel(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            var normalized = url.Replace("\\", "/");
            return normalized.StartsWith("/") ? normalized : "/" + normalized;
        }

        #endregion

        #region CATÁLOGO

        /// <summary>
        /// GET: /Compras/Catalogo
        /// Muestra el catálogo de productos con filtros, paginación y ordenamiento
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Catalogo(
            string? searchTerm,
            decimal? precioMin,
            decimal? precioMax,
            int? categoriaID,
            int[]? subcategoriaIDs,
            [FromQuery] string[]? ColoresSeleccionados,
            [FromQuery] string[]? TallasSeleccionadas,
            [FromQuery] bool SoloDisponibles = false,
            [FromQuery] string? sort = null,
            int pageNumber = 1,
            int pageSize = PAGE_SIZE_DEFAULT,
            CancellationToken ct = default)
        {
            try
            {
                // Validar paginación
                pageNumber = Math.Max(1, pageNumber);
                pageSize = Math.Clamp(pageSize, PAGE_SIZE_MIN, PAGE_SIZE_MAX);

                _logger.LogInformation(
                    "Catálogo solicitado. CategoriaId: {CatId}, Búsqueda: {Search}, " +
                    "PrecioMin: {PMin}, PrecioMax: {PMax}, Página: {Page}, " +
                    "PageSize: {PageSize}, Sort: {Sort}, SoloDisponibles: {Disponibles}",
                    categoriaID, searchTerm, precioMin, precioMax, pageNumber, pageSize, sort, SoloDisponibles);

                // Cargar sidebar con cache
                var categorias = await GetCategoriasConCacheAsync();
                var subcategorias = categoriaID.HasValue
                    ? await GetSubcategoriasConCacheAsync(categoriaID.Value)
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

                // Base query con Include para evitar N+1
                IQueryable<Producto> query = _context.Productos
                    .Include(p => p.Variantes)
                    .AsNoTracking();

                if (categoriaID.HasValue)
                    query = query.Where(p => p.CategoriaID == categoriaID.Value);

                if (subcategoriaIDs is { Length: > 0 })
                    query = query.Where(p => p.SubcategoriaID.HasValue && subcategoriaIDs!.Contains(p.SubcategoriaID.Value));

                // Búsqueda por texto
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    var search = searchTerm.Trim().ToLower();
                    query = query.Where(p =>
                        (p.Nombre != null && p.Nombre.ToLower().Contains(search)) ||
                        (p.Descripcion != null && p.Descripcion.ToLower().Contains(search)) ||
                        (p.Marca != null && p.Marca.ToLower().Contains(search)));
                }

                // Filtro por precio mínimo
                if (precioMin.HasValue && precioMin.Value > 0)
                {
                    query = query.Where(p =>
                        (p.Variantes.Any() && p.Variantes.Any(v => v.PrecioVenta >= precioMin.Value)) ||
                        (!p.Variantes.Any() && p.PrecioVenta >= precioMin.Value));
                }

                // Filtro por precio máximo
                if (precioMax.HasValue && precioMax.Value > 0)
                {
                    query = query.Where(p =>
                        (p.Variantes.Any() && p.Variantes.Any(v => v.PrecioVenta <= precioMax.Value)) ||
                        (!p.Variantes.Any() && p.PrecioVenta <= precioMax.Value));
                }

                // Solo disponibles
                if (SoloDisponibles)
                {
                    query = query.Where(p =>
                        p.Variantes.Any(v => v.Stock > 0) ||
                        (!p.Variantes.Any() && p.Stock > 0));
                }

                // Filtro por Color
                if (coloresSel.Any())
                {
                    query = query.Where(p =>
                        p.Variantes.Any(v => v.Color != null && coloresSel.Contains(v.Color)) ||
                        (p.Color != null && coloresSel.Contains(p.Color)));
                }

                // Filtro por Talla
                if (tallasSel.Any())
                {
                    query = query.Where(p =>
                        p.Variantes.Any(v => v.Talla != null && tallasSel.Contains(v.Talla)) ||
                        (p.Talla != null && tallasSel.Contains(p.Talla)));
                }

                // Ordenamiento
                query = (sort ?? "").Trim().ToLowerInvariant() switch
                {
                    "precio_asc" => query
                        .Select(p => new
                        {
                            P = p,
                            MinPrecio = p.Variantes.Any()
                                ? p.Variantes.Min(v => v.PrecioVenta)
                                : p.PrecioVenta
                        })
                        .OrderBy(x => x.MinPrecio)
                        .ThenBy(x => x.P.Nombre)
                        .Select(x => x.P),

                    "precio_desc" => query
                        .Select(p => new
                        {
                            P = p,
                            MaxPrecio = p.Variantes.Any()
                                ? p.Variantes.Max(v => v.PrecioVenta)
                                : p.PrecioVenta
                        })
                        .OrderByDescending(x => x.MaxPrecio)
                        .ThenBy(x => x.P.Nombre)
                        .Select(x => x.P),

                    "nuevos" => query
                        .OrderByDescending(p => p.FechaAgregado)
                        .ThenBy(p => p.Nombre),

                    "mas_vendidos" => query
                        .GroupJoin(
                            _context.DetalleVentas
                                .AsNoTracking()
                                .GroupBy(d => d.ProductoID)
                                .Select(g => new { ProductoID = g.Key, Cant = g.Sum(x => x.Cantidad) }),
                            p => p.ProductoID,
                            s => s.ProductoID,
                            (p, s) => new { P = p, Cant = s.Select(x => (int?)x.Cant).FirstOrDefault() ?? 0 })
                        .OrderByDescending(x => x.Cant)
                        .ThenBy(x => x.P.Nombre)
                        .Select(x => x.P),

                    _ => query.OrderBy(p => p.Nombre)
                };

                // Conteo total
                var totalProducts = await query.CountAsync(ct);

                _logger.LogDebug(
                    "Productos encontrados: {Total} (antes de paginación)",
                    totalProducts);

                // Paginación
                var productos = await query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(ct);

                // Facets
                var filteredProductIds = await query
                    .Select(p => p.ProductoID)
                    .ToListAsync(ct);

                var variantsFilteredQ = _context.ProductoVariantes
                    .AsNoTracking()
                    .Where(v => filteredProductIds.Contains(v.ProductoID));

                if (SoloDisponibles)
                    variantsFilteredQ = variantsFilteredQ.Where(v => v.Stock > 0);

                var coloresVar = await variantsFilteredQ
                    .Where(v => !string.IsNullOrEmpty(v.Color))
                    .Select(v => v.Color!)
                    .Distinct()
                    .ToListAsync(ct);

                var tallasVar = await variantsFilteredQ
                    .Where(v => !string.IsNullOrEmpty(v.Talla))
                    .Select(v => v.Talla!)
                    .Distinct()
                    .ToListAsync(ct);

                // Productos sin variantes
                var sinVarQ = _context.Productos
                    .AsNoTracking()
                    .Where(p => filteredProductIds.Contains(p.ProductoID) &&
                                !_context.ProductoVariantes.Any(v => v.ProductoID == p.ProductoID));

                if (SoloDisponibles)
                    sinVarQ = sinVarQ.Where(p => p.Stock > 0);

                var coloresProd = await sinVarQ
                    .Where(p => !string.IsNullOrEmpty(p.Color))
                    .Select(p => p.Color!)
                    .Distinct()
                    .ToListAsync(ct);

                var tallasProd = await sinVarQ
                    .Where(p => !string.IsNullOrEmpty(p.Talla))
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

                // Diccionario de variantes
                var variantesPorProducto = productos
                    .Where(p => p.Variantes != null)
                    .ToDictionary(
                        p => p.ProductoID,
                        p => p.Variantes!.ToList());

                // Modelo
                var model = new CatalogoViewModel
                {
                    Categorias = categorias,
                    SelectedCategoriaID = categoriaID,
                    Subcategorias = subcategorias,
                    SelectedSubcategoriaIDs = subcategoriaIDs?.ToList() ?? new List<int>(),
                    Productos = productos,
                    ColoresSeleccionados = coloresSel,
                    TallasSeleccionadas = tallasSel,
                    SoloDisponibles = SoloDisponibles,
                    ColoresDisponibles = coloresDisponibles,
                    TallasDisponibles = tallasDisponibles,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalProducts = totalProducts,
                    VariantesPorProducto = variantesPorProducto,
                    Sort = sort,
                    SearchTerm = searchTerm,
                    PrecioMin = precioMin,
                    PrecioMax = precioMax
                };

                // Favoritos
                if (User.Identity?.IsAuthenticated == true)
                {
                    var userId = _userManager.GetUserId(User);
                    model.ProductoIDsFavoritos = await _context.Favoritos
                        .AsNoTracking()
                        .Where(f => f.UsuarioId == userId)
                        .Select(f => f.ProductoId)
                        .ToListAsync(ct);
                }
                else
                {
                    model.ProductoIDsFavoritos = new List<int>();
                }

                _logger.LogInformation(
                    "Catálogo cargado exitosamente. Productos: {Count}, Página: {Page}/{TotalPages}",
                    productos.Count,
                    pageNumber,
                    (int)Math.Ceiling(totalProducts / (double)pageSize));

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error al cargar catálogo. CategoriaId: {CatId}, Página: {Page}",
                    categoriaID, pageNumber);

                TempData["MensajeError"] = "Error al cargar el catálogo. Por favor, intenta nuevamente.";
                return View(new CatalogoViewModel
                {
                    Categorias = new List<Categorias>(),
                    Subcategorias = new List<Subcategorias>(),
                    Productos = new List<Producto>(),
                    ColoresDisponibles = new List<string>(),
                    TallasDisponibles = new List<string>(),
                    ProductoIDsFavoritos = new List<int>()
                });
            }
        }

        /// <summary>
        /// GET: /Compras/SearchProducts
        /// Búsqueda AJAX de productos para el buscador inteligente del navbar
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> SearchProducts(string q, int limit = 8, CancellationToken ct = default)
        {
            try
            {
                // Validar query
                if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
                {
                    return Json(new List<object>());
                }

                // Normalizar búsqueda
                var searchTerm = q.Trim().ToLower();
                limit = Math.Clamp(limit, 1, 20);

                _logger.LogInformation(
                    "Búsqueda AJAX. Query: {Query}, Limit: {Limit}, Usuario: {User}",
                    searchTerm, limit, User.Identity?.Name ?? "Anónimo");

                // Buscar productos
                var productos = await _context.Productos
                    .AsNoTracking()
                    .Where(p =>
                        (p.Nombre != null && p.Nombre.ToLower().Contains(searchTerm)) ||
                        (p.Descripcion != null && p.Descripcion.ToLower().Contains(searchTerm)) ||
                        (p.Marca != null && p.Marca.ToLower().Contains(searchTerm)))
                    .OrderBy(p => p.Nombre)
                    .Take(limit)
                    .Select(p => new
                    {
                        productoID = p.ProductoID,
                        nombre = p.Nombre,
                        marca = p.Marca,
                        precio = p.PrecioVenta,
                        stock = p.Stock,
                        imagenUrl = !string.IsNullOrEmpty(p.ImagenPath)
                            ? p.ImagenPath
                            : "/images/placeholder-product.png"
                    })
                    .ToListAsync(ct);

                _logger.LogDebug("Búsqueda completada. Resultados: {Count}", productos.Count);

                return Json(productos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en búsqueda AJAX. Query: {Query}", q);
                return Json(new List<object>());
            }
        }

        #endregion

        #region PRODUCTO INDIVIDUAL

        /// <summary>
        /// GET: /p/{productoID}/{slug?}
        /// Muestra los detalles de un producto específico
        /// </summary>
        [HttpGet]
        [Route("/p/{productoID:int}/{slug?}")]
        public async Task<IActionResult> VerProducto(int productoID, CancellationToken ct = default)
        {
            if (productoID <= 0)
            {
                _logger.LogWarning("Intento de ver producto con ID inválido: {ProductoId}", productoID);
                return RedirectToAction(nameof(Catalogo));
            }

            try
            {
                var producto = await _context.Productos
                    .AsNoTracking()
                    .Include(p => p.Categoria)
                    .Include(p => p.Subcategoria)
                    .Include(p => p.Variantes)
                    .FirstOrDefaultAsync(p => p.ProductoID == productoID, ct);

                if (producto == null)
                {
                    _logger.LogWarning("Producto no encontrado: {ProductoId}", productoID);
                    TempData["MensajeError"] = "Producto no encontrado.";
                    return RedirectToAction(nameof(Catalogo));
                }

                _logger.LogInformation(
                    "Producto visualizado. ProductoId: {ProductoId}, Nombre: {Nombre}, " +
                    "Usuario: {UserId}",
                    productoID,
                    producto.Nombre,
                    User.Identity?.Name);

                // Galería de imágenes
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

                // Productos relacionados
                var relacionados = _context.Productos
                    .AsNoTracking()
                    .Where(p => p.ProductoID != producto.ProductoID);

                if (producto.SubcategoriaID != 0)
                    relacionados = relacionados.Where(p => p.SubcategoriaID == producto.SubcategoriaID);
                else if (producto.CategoriaID != 0)
                    relacionados = relacionados.Where(p => p.CategoriaID == producto.CategoriaID);

                ViewBag.Relacionados = await relacionados
                    .OrderByDescending(p => p.FechaAgregado)
                    .ThenBy(p => p.Nombre)
                    .Take(MAX_RELATED_PRODUCTS)
                    .ToListAsync(ct);

                ViewBag.Variantes = producto.Variantes?
                    .OrderBy(v => v.Color)
                    .ThenBy(v => v.Talla)
                    .ToList() ?? new List<ProductoVariante>();

                return View(producto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar producto: {ProductoId}", productoID);
                TempData["MensajeError"] = "Error al cargar el producto.";
                return RedirectToAction(nameof(Catalogo));
            }
        }

        /// <summary>
        /// Carga la galería de imágenes de un producto
        /// </summary>
        private async Task<(string? portada, List<string> imagenes)> CargarGaleriaAsync(
            int productoId,
            CancellationToken ct)
        {
            // 1) Intentar JSON de galería
            try
            {
                var folder = ProductFolderAbs(productoId);
                var metaPath = Path.Combine(folder, $"product-{productoId}.gallery.json");

                if (System.IO.File.Exists(metaPath))
                {
                    using var stream = System.IO.File.OpenRead(metaPath);
                    using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

                    string? portada = null;
                    var imgs = new List<string>();

                    if (doc.RootElement.TryGetProperty("portada", out var p) &&
                        p.ValueKind == JsonValueKind.String)
                    {
                        portada = p.GetString();
                    }

                    if (doc.RootElement.TryGetProperty("images", out var arr) &&
                        arr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var element in arr.EnumerateArray())
                        {
                            if (element.ValueKind == JsonValueKind.String)
                            {
                                var rel = element.GetString();
                                if (!string.IsNullOrWhiteSpace(rel))
                                    imgs.Add(NormalizeRel(rel!)!);
                            }
                        }
                    }

                    return (NormalizeRel(portada), imgs.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo leer JSON de galería para producto {ProductoId}", productoId);
            }

            // 2) Fallback a DB
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
                    var imgs = filas
                        .Select(x => NormalizeRel(x.Path)!)
                        .Where(x => x != null)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    return (NormalizeRel(portada), imgs);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo leer ProductoImagenes para producto {ProductoId}", productoId);
            }

            return (null, new List<string>());
        }

        #endregion

        #region CARRITO

        /// <summary>
        /// POST: /Compras/AnadirAlCarrito
        /// Agrega un producto al carrito
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AnadirAlCarrito(
            [Bind("ProductoID,Cantidad")] CatalogoViewModel model,
            [FromForm(Name = "ProductoVarianteID")] int? productoVarianteId,
            CancellationToken ct = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                _logger.LogWarning(
                    "Intento de agregar al carrito sin autenticación. ProductoId: {ProductoId}",
                    model.ProductoID);

                if (EsAjax())
                    return Json(new { ok = false, needLogin = true, error = "Debes iniciar sesión." });

                TempData["MensajeError"] = "Debes iniciar sesión para añadir productos al carrito.";
                return RedirectToAction("Login", "Cuenta");
            }

            if (model.Cantidad <= 0) model.Cantidad = 1;

            try
            {
                var producto = await _productos.GetByIdAsync(model.ProductoID);
                if (producto == null)
                {
                    _logger.LogWarning(
                        "Intento de agregar producto inexistente. ProductoId: {ProductoId}",
                        model.ProductoID);

                    if (EsAjax())
                        return Json(new { ok = false, error = "Producto no encontrado." });

                    TempData["MensajeError"] = "Producto no encontrado.";
                    return RedirectToReferrerOr(nameof(Catalogo));
                }

                // Verificar si tiene variantes
                var tieneVariantes = await _context.ProductoVariantes
                    .AsNoTracking()
                    .AnyAsync(v => v.ProductoID == model.ProductoID, ct);

                ProductoVariante? variante = null;
                if (tieneVariantes)
                {
                    if (!productoVarianteId.HasValue)
                    {
                        var msg = "Selecciona Color y Talla antes de añadir al carrito.";
                        _logger.LogWarning(
                            "Intento de agregar producto con variantes sin seleccionar. ProductoId: {ProductoId}",
                            model.ProductoID);

                        if (EsAjax())
                            return Json(new { ok = false, needVariant = true, error = msg });

                        TempData["MensajeError"] = msg;
                        return RedirectToReferrerOr("VerProducto");
                    }

                    variante = await _context.ProductoVariantes
                        .AsNoTracking()
                        .FirstOrDefaultAsync(v => v.ProductoVarianteID == productoVarianteId.Value, ct);

                    if (variante == null || variante.ProductoID != model.ProductoID)
                    {
                        var msg = "La combinación seleccionada no es válida para este producto.";
                        _logger.LogWarning(
                            "Variante inválida. ProductoId: {ProductoId}, VarianteId: {VarianteId}",
                            model.ProductoID,
                            productoVarianteId);

                        if (EsAjax())
                            return Json(new { ok = false, error = msg });

                        TempData["MensajeError"] = msg;
                        return RedirectToReferrerOr("VerProducto");
                    }
                }

                // Obtener o crear carrito
                var carrito = await _carrito.GetByClienteIdAsync(user.Id)
                            ?? (await _carrito.AddAsync(user)
                                ? await _carrito.GetByClienteIdAsync(user.Id)
                                : null);

                if (carrito == null)
                {
                    _logger.LogError("No se pudo obtener/crear carrito para usuario: {UserId}", user.Id);
                    TempData["MensajeError"] = "No fue posible obtener tu carrito.";
                    return RedirectToReferrerOr(nameof(Catalogo));
                }

                // Validar stock
                var detalles = await _carrito.LoadCartDetails(carrito.CarritoID);
                var enCarrito = detalles
                    .Where(c => c.ProductoID == model.ProductoID &&
                                c.ProductoVarianteID == productoVarianteId)
                    .Sum(c => c.Cantidad);

                if (tieneVariantes)
                {
                    var disponible = variante!.Stock;
                    if (enCarrito + model.Cantidad > disponible)
                    {
                        var msg = "La cantidad solicitada supera el stock disponible para la combinación seleccionada.";
                        _logger.LogWarning(
                            "Stock insuficiente. ProductoId: {ProductoId}, VarianteId: {VarianteId}, " +
                            "Disponible: {Stock}, Solicitado: {Cantidad}, EnCarrito: {EnCarrito}",
                            model.ProductoID,
                            productoVarianteId,
                            disponible,
                            model.Cantidad,
                            enCarrito);

                        if (EsAjax())
                            return Json(new { ok = false, error = msg, stock = disponible, enCarrito });

                        TempData["MensajeError"] = msg;
                        return RedirectToReferrerOr("VerProducto");
                    }
                }
                else
                {
                    if (enCarrito + model.Cantidad > producto.Stock)
                    {
                        var msg = "La cantidad solicitada supera el stock disponible.";
                        _logger.LogWarning(
                            "Stock insuficiente. ProductoId: {ProductoId}, " +
                            "Disponible: {Stock}, Solicitado: {Cantidad}, EnCarrito: {EnCarrito}",
                            model.ProductoID,
                            producto.Stock,
                            model.Cantidad,
                            enCarrito);

                        if (EsAjax())
                            return Json(new { ok = false, error = msg, stock = producto.Stock, enCarrito });

                        TempData["MensajeError"] = msg;
                        return RedirectToReferrerOr(nameof(Catalogo));
                    }
                }

                // Añadir al carrito
                var ok = await _carrito.AnadirProducto(producto, user, model.Cantidad, productoVarianteId);
                if (!ok)
                {
                    _logger.LogError(
                        "Falló añadir producto al carrito. ProductoId: {ProductoId}, UserId: {UserId}",
                        model.ProductoID,
                        user.Id);

                    if (EsAjax())
                        return Json(new { ok = false, error = "No se pudo añadir al carrito." });

                    TempData["MensajeError"] = "No se pudo añadir el producto al carrito.";
                    return RedirectToReferrerOr(nameof(Catalogo));
                }

                var count = await _context.CarritoDetalle
                    .Where(cd => cd.CarritoID == carrito.CarritoID)
                    .SumAsync(cd => cd.Cantidad, ct);

                _logger.LogInformation(
                    "Producto agregado al carrito. ProductoId: {ProductoId}, " +
                    "Cantidad: {Cantidad}, UserId: {UserId}, Total items: {Count}",
                    model.ProductoID,
                    model.Cantidad,
                    user.Id,
                    count);

                if (EsAjax())
                    return Json(new { ok = true, count });

                TempData["MensajeExito"] = "Producto añadido al carrito con éxito.";
                return RedirectToReferrerOr(nameof(Catalogo));
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error al añadir producto al carrito. ProductoId: {ProductoId}, UserId: {UserId}",
                    model.ProductoID,
                    user.Id);

                if (EsAjax())
                    return Json(new { ok = false, error = "Error inesperado" });

                TempData["MensajeError"] = "Error al añadir el producto.";
                return RedirectToReferrerOr(nameof(Catalogo));
            }
        }

        /// <summary>
        /// GET: /Compras/Mini
        /// Resumen del carrito para AJAX
        /// </summary>
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

        /// <summary>
        /// POST: /Compras/ActualizarCantidad
        /// Actualiza la cantidad de un producto en el carrito
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarCantidad(
            int carritoDetalleID,
            int cantidad,
            CancellationToken ct = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();
            if (cantidad <= 0) cantidad = 1;

            try
            {
                var detalle = await _context.CarritoDetalle
                    .Include(cd => cd.Carrito)
                    .Include(cd => cd.Producto)
                    .Include(cd => cd.Variante)
                    .FirstOrDefaultAsync(cd => cd.CarritoDetalleID == carritoDetalleID, ct);

                if (detalle == null || detalle.Carrito?.UsuarioId != user.Id)
                {
                    _logger.LogWarning(
                        "Intento de actualizar detalle no autorizado. DetalleId: {DetalleId}, UserId: {UserId}",
                        carritoDetalleID,
                        user.Id);
                    return NotFound();
                }

                if (detalle.Producto == null) return NotFound();

                // Validar stock
                if (detalle.Variante != null)
                {
                    if (cantidad > detalle.Variante.Stock)
                    {
                        var msg = "La cantidad supera el stock disponible para la combinación seleccionada.";
                        _logger.LogWarning(
                            "Stock insuficiente al actualizar. DetalleId: {DetalleId}, " +
                            "Stock: {Stock}, Solicitado: {Cantidad}",
                            carritoDetalleID,
                            detalle.Variante.Stock,
                            cantidad);

                        if (EsAjax())
                            return Json(new { ok = false, error = msg, stock = detalle.Variante.Stock });

                        TempData["MensajeError"] = msg;
                        return RedirectToReferrerOr(nameof(Resumen));
                    }
                }
                else
                {
                    if (cantidad > detalle.Producto.Stock)
                    {
                        var msg = "La cantidad supera el stock disponible.";
                        _logger.LogWarning(
                            "Stock insuficiente al actualizar. DetalleId: {DetalleId}, " +
                            "Stock: {Stock}, Solicitado: {Cantidad}",
                            carritoDetalleID,
                            detalle.Producto.Stock,
                            cantidad);

                        if (EsAjax())
                            return Json(new { ok = false, error = msg, stock = detalle.Producto.Stock });

                        TempData["MensajeError"] = msg;
                        return RedirectToReferrerOr(nameof(Resumen));
                    }
                }

                detalle.Cantidad = cantidad;
                await _context.SaveChangesAsync(ct);

                _logger.LogInformation(
                    "Cantidad actualizada. DetalleId: {DetalleId}, Nueva cantidad: {Cantidad}",
                    carritoDetalleID,
                    cantidad);

                if (EsAjax())
                    return Json(new { ok = true, subtotal = detalle.Cantidad * detalle.Precio });

                TempData["MensajeExito"] = "Cantidad actualizada.";
                return RedirectToReferrerOr(nameof(Resumen));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar cantidad. DetalleId: {DetalleId}", carritoDetalleID);

                if (EsAjax())
                    return Json(new { ok = false, error = "Error al actualizar cantidad" });

                TempData["MensajeError"] = "Error al actualizar la cantidad.";
                return RedirectToReferrerOr(nameof(Resumen));
            }
        }

        /// <summary>
        /// POST: /Compras/EliminarArticulo
        /// Elimina un producto del carrito
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarArticulo(int carritoDetalleID, CancellationToken ct = default)
        {
            try
            {
                var ok = await _carrito.BorrarProductoCarrito(carritoDetalleID);

                _logger.LogInformation(
                    "Producto eliminado del carrito. DetalleId: {DetalleId}, Exitoso: {Ok}",
                    carritoDetalleID,
                    ok);

                if (EsAjax())
                    return Json(new { ok });

                TempData["MensajeExito"] = ok
                    ? "Producto eliminado del carrito con éxito."
                    : "No se pudo eliminar el producto del carrito.";

                return RedirectToAction(nameof(Catalogo));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar artículo. DetalleId: {DetalleId}", carritoDetalleID);

                if (EsAjax())
                    return Json(new { ok = false });

                TempData["MensajeError"] = "Error al eliminar el producto.";
                return RedirectToAction(nameof(Catalogo));
            }
        }

        /// <summary>
        /// GET: /Compras/CartInfo
        /// Info rápida del carrito (cantidad y subtotal)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> CartInfo(CancellationToken ct)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                    return Json(new { count = 0, subtotal = 0m });

                var carrito = await _carrito.GetByClienteIdAsync(user.Id);
                if (carrito == null)
                    return Json(new { count = 0, subtotal = 0m });

                var detalles = await _carrito.LoadCartDetails(carrito.CarritoID);
                return Json(new
                {
                    count = detalles.Sum(d => d.Cantidad),
                    subtotal = detalles.Sum(d => d.Cantidad * d.Precio)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener CartInfo");
                return Json(new { count = 0, subtotal = 0m });
            }
        }

        #endregion

        #region RESUMEN / CHECKOUT

        /// <summary>
        /// GET: /Compras/Resumen
        /// Muestra el resumen del carrito antes de confirmar la compra
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Resumen(CancellationToken ct = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                _logger.LogWarning("Intento de acceder a Resumen sin autenticación");
                TempData["MensajeError"] = "Debes iniciar sesión para ver el resumen de tu carrito.";
                return RedirectToAction("Login", "Cuenta");
            }

            try
            {
                var carrito = await _carrito.GetByClienteIdAsync(user.Id)
                             ?? (await _carrito.AddAsync(user)
                                 ? await _carrito.GetByClienteIdAsync(user.Id)
                                 : null);

                if (carrito == null)
                {
                    _logger.LogError("No se pudo obtener/crear carrito para resumen. UserId: {UserId}", user.Id);
                    TempData["MensajeError"] = "No fue posible obtener tu carrito.";
                    return RedirectToAction(nameof(Catalogo));
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
                            .CalcularAsync(vendedorUserIds, user.Provincia, user.Ciudad, ct);

                        envioTotal = envRes?.TotalEnvio ?? 0m;
                        envioPorVendedor = envRes?.PorVendedor ?? new Dictionary<string, decimal>();
                        envioMensajes = envRes?.Mensajes ?? new List<string>();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "No se pudo calcular el envío para el resumen. UserId: {UserId}", user.Id);
                    }
                }

                // Resolver mono/multi
                var decision = await _pagosResolver.ResolverAsync(user.Id, carrito.CarritoID, ct);
                ViewBag.EsMultiVendedor = decision.EsMultiVendedor;
                ViewBag.VendedorIdUnico = decision.VendedorIdUnico;
                ViewBag.VendedoresIds = decision.VendedoresIds;

                // Cuentas bancarias
                if (!decision.EsMultiVendedor && !string.IsNullOrWhiteSpace(decision.VendedorIdUnico))
                {
                    var cuentasProv = new List<Simone.Configuration.CuentaBancaria>();
                    try
                    {
                        cuentasProv = (await _bancosSvc.GetByProveedorAsync(decision.VendedorIdUnico!, ct))
                                      .Where(c => c?.Activo == true)
                                      .ToList();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "No se pudieron cargar cuentas del proveedor {VendedorId}", decision.VendedorIdUnico);
                    }

                    ViewBag.CuentasProveedor = cuentasProv;
                    if (cuentasProv.Count == 0)
                    {
                        var admin = (await _bancosSvc.GetAdminAsync(ct))
                            .Where(c => c?.Activo == true)
                            .ToList();
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
                    var admin = (await _bancosSvc.GetAdminAsync(ct))
                        .Where(c => c?.Activo == true)
                        .ToList();
                    ViewBag.CuentasAdmin = admin;
                    ViewBag.FallbackAdmin = true;
                }

                // Mapa legible {NombreTienda -> $}
                var usuarios = await _context.Users
                    .AsNoTracking()
                    .Where(u => vendedorUserIds.Contains(u.Id))
                    .Select(u => new { u.Id, u.VendedorId, u.NombreCompleto, u.Email })
                    .ToListAsync(ct);

                var tiendaIds = usuarios
                    .Where(x => x.VendedorId.HasValue)
                    .Select(x => x.VendedorId!.Value)
                    .Distinct()
                    .ToList();

                var mapTienda = tiendaIds.Count == 0
                    ? new Dictionary<int, string>()
                    : await _context.Vendedores
                        .AsNoTracking()
                        .Where(t => tiendaIds.Contains(t.VendedorId))
                        .ToDictionaryAsync(t => t.VendedorId, t => t.Nombre, ct);

                var envioPorTienda = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
                foreach (var kv in envioPorVendedor)
                {
                    var u = usuarios.FirstOrDefault(x => x.Id == kv.Key);
                    string etiqueta;

                    if (u?.VendedorId != null &&
                        mapTienda.TryGetValue(u.VendedorId.Value, out var tiendaNombre) &&
                        !string.IsNullOrWhiteSpace(tiendaNombre))
                    {
                        etiqueta = tiendaNombre;
                    }
                    else
                    {
                        etiqueta = u?.NombreCompleto ?? u?.Email ?? kv.Key;
                    }

                    if (!envioPorTienda.ContainsKey(etiqueta))
                        envioPorTienda[etiqueta] = 0m;

                    envioPorTienda[etiqueta] += kv.Value;
                }

                // ViewBag para la vista
                ViewBag.CarritoDetalles = detalles;
                ViewBag.Subtotal = subtotal;
                ViewBag.TotalCompra = subtotal;
                ViewBag.EnvioTotal = envioTotal;
                ViewBag.EnvioPorVendedor = envioPorVendedor;
                ViewBag.EnvioPorTienda = envioPorTienda;
                ViewBag.EnvioMensajes = envioMensajes;
                ViewBag.CanComputeShipping = !string.IsNullOrWhiteSpace(user.Provincia);
                ViewBag.HasAddress = !string.IsNullOrWhiteSpace(user.Direccion);

                _logger.LogInformation(
                    "Resumen de carrito cargado. UserId: {UserId}, Items: {Count}, Subtotal: {Subtotal:C}",
                    user.Id,
                    detalles.Count,
                    subtotal);

                return View(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar resumen. UserId: {UserId}", user.Id);
                TempData["MensajeError"] = "Error al cargar el resumen.";
                return RedirectToAction(nameof(Catalogo));
            }
        }

        /// <summary>
        /// POST: /Compras/ConfirmarCompra
        /// Procesa y confirma la compra
        /// CORREGIDO: Guarda Depositante, Banco y ComprobanteUrl en la BD
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
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
                _logger.LogWarning("Intento de confirmar compra sin autenticación");
                TempData["MensajeError"] = "Debes iniciar sesión para confirmar la compra.";
                return RedirectToAction("Login", "Cuenta");
            }

            try
            {
                var carrito = await _carrito.GetByClienteIdAsync(user.Id);
                if (carrito == null)
                {
                    _logger.LogWarning("Intento de confirmar compra sin carrito. UserId: {UserId}", user.Id);
                    TempData["MensajeError"] = "Tu carrito está vacío.";
                    return RedirectToAction(nameof(Resumen));
                }

                var detalles = await _carrito.LoadCartDetails(carrito.CarritoID);
                if (detalles == null || !detalles.Any())
                {
                    _logger.LogWarning("Intento de confirmar compra con carrito vacío. UserId: {UserId}", user.Id);
                    TempData["MensajeError"] = "Tu carrito está vacío.";
                    return RedirectToAction(nameof(Resumen));
                }

                if (string.IsNullOrWhiteSpace(user.Direccion))
                {
                    _logger.LogWarning("Intento de confirmar compra sin dirección. UserId: {UserId}", user.Id);
                    ViewBag.HasAddress = false;
                    ViewBag.CarritoDetalles = detalles;
                    ViewBag.TotalCompra = detalles.Sum(cd => cd.Precio * cd.Cantidad);
                    TempData["MensajeError"] = "Agrega una dirección para continuar con la compra.";
                    return View("Resumen", user);
                }

                // Verificación de stock
                var faltantes = detalles.Where(d =>
                    (d.Variante != null && d.Variante.Stock < d.Cantidad) ||
                    (d.Variante == null && d.Producto != null && d.Producto.Stock < d.Cantidad))
                    .ToList();

                if (faltantes.Any())
                {
                    var productos = string.Join(", ", faltantes.Select(f =>
                        $"{(f.Producto?.Nombre ?? "Producto")}" +
                        $"{(f.Variante != null ? $" [{f.Variante.Color}/{f.Variante.Talla}]" : "")}"));

                    _logger.LogWarning(
                        "Stock insuficiente al confirmar compra. UserId: {UserId}, Productos: {Productos}",
                        user.Id,
                        productos);

                    ViewBag.HasAddress = true;
                    ViewBag.CarritoDetalles = detalles;
                    ViewBag.TotalCompra = detalles.Sum(cd => cd.Precio * cd.Cantidad);
                    TempData["MensajeError"] = $"Stock insuficiente para: {productos}";
                    return View("Resumen", user);
                }

                // Validar método de pago
                var decision = await _pagosResolver.ResolverAsync(user.Id, carrito.CarritoID, ct);
                var esDeposito = (MetodoPago ?? "").StartsWith("dep", StringComparison.OrdinalIgnoreCase);

                if (esDeposito)
                {
                    if (string.IsNullOrWhiteSpace(BancoSeleccionado) ||
                        string.IsNullOrWhiteSpace(Depositante) ||
                        Comprobante == null || Comprobante.Length == 0)
                    {
                        _logger.LogWarning(
                            "Intento de confirmar depósito sin datos completos. UserId: {UserId}",
                            user.Id);

                        TempData["MensajeError"] = "Para depósito, selecciona banco, indica el nombre del depositante y adjunta el comprobante.";
                        return RedirectToAction(nameof(Resumen));
                    }

                    if (!EsMimePermitido(Comprobante) || Comprobante.Length > MAX_FILE_SIZE_BYTES)
                    {
                        _logger.LogWarning(
                            "Comprobante inválido. UserId: {UserId}, Size: {Size}, ContentType: {ContentType}",
                            user.Id,
                            Comprobante.Length,
                            Comprobante.ContentType);

                        TempData["MensajeError"] = $"Comprobante inválido. Formatos: JPG, PNG, WEBP o PDF. Máximo {MAX_FILE_SIZE_MB}MB.";
                        return RedirectToAction(nameof(Resumen));
                    }
                }

                _logger.LogInformation(
                    "Iniciando confirmación de compra. UserId: {UserId}, Items: {Items}, " +
                    "MetodoPago: {MetodoPago}, Banco: {Banco}",
                    user.Id,
                    detalles.Count,
                    MetodoPago,
                    BancoSeleccionado);

                // Procesar carrito
                var ok = await _carrito.ProcessCartDetails(carrito.CarritoID, user);
                if (!ok)
                {
                    _logger.LogError(
                        "Falló procesar carrito. UserId: {UserId}, CarritoId: {CarritoId}",
                        user.Id,
                        carrito.CarritoID);

                    TempData["MensajeError"] = "No se pudo completar la compra. Revisa tu carrito e intenta nuevamente.";
                    return RedirectToAction(nameof(Resumen));
                }

                // Obtener venta creada
                var ventaId = await _context.Ventas
                    .Where(v => v.UsuarioId == user.Id)
                    .OrderByDescending(v => v.FechaVenta)
                    .Select(v => v.VentaID)
                    .FirstOrDefaultAsync(ct);

                // Calcular envío definitivo
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
                            _logger.LogWarning(ex, "No se pudo calcular el envío al confirmar compra. VentaId: {VentaId}", ventaId);
                        }
                    }
                }

                if (ventaId > 0)
                {
                    Directory.CreateDirectory(UploadsFolderAbs());

                    // Resolver banco
                    var (okBanco, metaBancoObj, destinoPago, errBanco) =
                        await ResolverBancoSeleccionadoAsync(BancoSeleccionado ?? "", ct);

                    if (decision.EsMultiVendedor && okBanco &&
                        !string.Equals(destinoPago, "admin", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning(
                            "Intento de pago a proveedor en multi-tienda. VentaId: {VentaId}",
                            ventaId);

                        TempData["MensajeError"] = "En pedidos multi-tienda, el pago se realiza únicamente a cuentas del administrador.";
                        return RedirectToAction(nameof(Resumen));
                    }

                    if (esDeposito && !okBanco)
                    {
                        _logger.LogWarning(
                            "Banco seleccionado inválido. VentaId: {VentaId}, Error: {Error}",
                            ventaId,
                            errBanco);

                        TempData["MensajeError"] = errBanco ?? "Banco seleccionado inválido.";
                        return RedirectToAction(nameof(Resumen));
                    }

                    // Guardar metadatos en archivo JSON (para compatibilidad)
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

                    // =====================================================================
                    // NUEVO: Obtener la venta y guardar datos de pago en la BD
                    // =====================================================================
                    var venta = await _context.Ventas.FirstOrDefaultAsync(v => v.VentaID == ventaId, ct);

                    string? comprobanteUrl = null;

                    // Guardar comprobante
                    if (esDeposito && Comprobante != null && Comprobante.Length > 0)
                    {
                        var save = await GuardarComprobanteAsync(ventaId, Comprobante, ct);
                        if (!save.ok)
                        {
                            _logger.LogError(
                                "Error al guardar comprobante. VentaId: {VentaId}, Error: {Error}",
                                ventaId,
                                save.error);

                            TempData["MensajeError"] = save.error ?? "No se pudo guardar el comprobante.";
                            return RedirectToAction(nameof(Resumen));
                        }

                        comprobanteUrl = save.relUrl;

                        // NUEVO: Guardar URL en la BD
                        if (venta != null)
                        {
                            venta.ComprobanteUrl = comprobanteUrl;
                        }
                    }

                    // NUEVO: Guardar Depositante y Banco en la BD
                    if (venta != null)
                    {
                        if (!string.IsNullOrWhiteSpace(Depositante))
                        {
                            venta.Depositante = Depositante.Trim();
                        }

                        // Extraer nombre del banco
                        if (okBanco && metaBancoObj != null)
                        {
                            try
                            {
                                var jsonMeta = JsonSerializer.Serialize(metaBancoObj);
                                using var doc = JsonDocument.Parse(jsonMeta);
                                if (doc.RootElement.TryGetProperty("banco", out var bancoEl) &&
                                    bancoEl.TryGetProperty("nombre", out var nombreEl))
                                {
                                    venta.Banco = nombreEl.GetString();
                                }
                            }
                            catch
                            {
                                // Si falla parsear, usar el valor directo
                                if (!string.IsNullOrWhiteSpace(BancoSeleccionado))
                                {
                                    var parts = BancoSeleccionado.Split(':');
                                    venta.Banco = parts.Length > 1 ? parts.Last().Trim() : BancoSeleccionado.Trim();
                                }
                            }
                        }
                        else if (!string.IsNullOrWhiteSpace(BancoSeleccionado))
                        {
                            var parts = BancoSeleccionado.Split(':');
                            venta.Banco = parts.Length > 1 ? parts.Last().Trim() : BancoSeleccionado.Trim();
                        }
                    }
                    // =====================================================================

                    // Actualizar total de la venta con envío
                    if (venta != null)
                    {
                        var subtotal = detalles.Sum(c => c.Precio * c.Cantidad);
                        var cupon = HttpContext.Session.GetObjectFromJson<Promocion>(SESSION_KEY_CUPON);
                        decimal descuento = cupon?.Descuento ?? 0m;
                        decimal baseTotal = Math.Max(0m, subtotal - descuento);

                        venta.Total = baseTotal + envioTotal;
                        await _context.SaveChangesAsync(ct);

                        _logger.LogInformation(
                            "Datos de pago guardados en BD. VentaId: {VentaId}, Depositante: {Depositante}, " +
                            "Banco: {Banco}, ComprobanteUrl: {Url}",
                            ventaId,
                            venta.Depositante ?? "(vacío)",
                            venta.Banco ?? "(vacío)",
                            venta.ComprobanteUrl ?? "(sin archivo)");
                    }
                }

                // Reiniciar carrito
                await _carrito.AddAsync(user);

                _logger.LogInformation(
                    "Compra confirmada exitosamente. VentaId: {VentaId}, UserId: {UserId}, " +
                    "Total: {Total:C}, EnvioTotal: {Envio:C}",
                    ventaId,
                    user.Id,
                    detalles.Sum(d => d.Precio * d.Cantidad) + envioTotal,
                    envioTotal);

                TempData["MensajeExito"] = "¡Gracias por tu compra!";

                if (ventaId > 0)
                    return RedirectToAction(nameof(CompraExito), new { id = ventaId });

                return RedirectToAction("Index", "MisCompras");
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Validación al confirmar compra. UserId: {UserId}", user.Id);

                var det = await _carrito.LoadCartDetails((await _carrito.GetByClienteIdAsync(user.Id))!.CarritoID);
                ViewBag.HasAddress = true;
                ViewBag.CarritoDetalles = det;
                ViewBag.TotalCompra = det.Sum(cd => cd.Precio * cd.Cantidad);
                TempData["MensajeError"] = ex.Message;
                return View("Resumen", user);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error de BD al confirmar compra. UserId: {UserId}", user.Id);
                TempData["MensajeError"] = "Error al guardar en base de datos. Por favor, intenta nuevamente.";
                return RedirectToAction(nameof(CompraError));
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "Error de I/O al confirmar compra. UserId: {UserId}", user.Id);
                TempData["MensajeError"] = "Error al guardar archivos. Por favor, intenta nuevamente.";
                return RedirectToAction(nameof(CompraError));
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Error crítico al confirmar compra. UserId: {UserId}", user.Id);
                TempData["MensajeError"] = "Error inesperado al procesar tu pedido. Por favor, contacta a soporte.";
                return RedirectToAction(nameof(CompraError));
            }
        }

        #endregion

        #region PANTALLAS DE COMPROBANTE

        /// <summary>
        /// POST: /Compras/SubirComprobante
        /// Sube un comprobante de pago y guarda los datos en la BD
        /// CORREGIDO: Ahora persiste Depositante, Banco y ComprobanteUrl en la tabla Ventas
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubirComprobante(
            int id,
            IFormFile? archivo,
            string? depositante,
            string? banco,
            CancellationToken ct = default)
        {
            var uid = _userManager.GetUserId(User);
            if (uid == null) return Unauthorized();

            try
            {
                // CAMBIO: Usar tracking para poder modificar la entidad
                var venta = await _context.Ventas
                    .Include(v => v.Usuario)
                    .FirstOrDefaultAsync(v => v.VentaID == id && v.UsuarioId == uid, ct);

                if (venta == null)
                {
                    _logger.LogWarning(
                        "Intento de subir comprobante a venta no autorizada. VentaId: {VentaId}, UserId: {UserId}",
                        id,
                        uid);

                    return EsAjax()
                        ? Json(new { ok = false, error = "Venta no encontrada." })
                        : NotFound();
                }

                string? relUrl = null;

                // Guardar archivo de comprobante
                if (archivo != null && archivo.Length > 0)
                {
                    var res = await GuardarComprobanteAsync(id, archivo, ct);
                    if (!res.ok)
                    {
                        _logger.LogError(
                            "Error al guardar comprobante. VentaId: {VentaId}, Error: {Error}",
                            id,
                            res.error);

                        if (EsAjax())
                            return Json(new { ok = false, error = res.error });

                        TempData["MensajeError"] = res.error;
                        return RedirectToAction(nameof(CompraExito), new { id });
                    }

                    relUrl = res.relUrl;

                    // NUEVO: Guardar URL en la venta
                    venta.ComprobanteUrl = relUrl;
                }

                // NUEVO: Guardar depositante y banco en la BD
                if (!string.IsNullOrWhiteSpace(depositante))
                {
                    venta.Depositante = depositante.Trim();
                }

                if (!string.IsNullOrWhiteSpace(banco))
                {
                    venta.Banco = banco.Trim();
                }

                // Guardar cambios en BD
                await _context.SaveChangesAsync(ct);

                // OPCIONAL: Mantener compatibilidad con archivos JSON
                if (!string.IsNullOrWhiteSpace(depositante) || !string.IsNullOrWhiteSpace(banco))
                {
                    GuardarMetaDeposito(id, depositante, banco);
                }

                _logger.LogInformation(
                    "Comprobante subido y datos guardados en BD. VentaId: {VentaId}, UserId: {UserId}, " +
                    "Depositante: {Depositante}, Banco: {Banco}, ComprobanteUrl: {Url}",
                    id,
                    uid,
                    venta.Depositante ?? "(vacío)",
                    venta.Banco ?? "(vacío)",
                    venta.ComprobanteUrl ?? "(sin archivo)");

                if (EsAjax())
                    return Json(new { ok = true, url = relUrl });

                TempData["MensajeExito"] = "Información de depósito registrada correctamente.";
                return RedirectToAction(nameof(CompraExito), new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al subir comprobante. VentaId: {VentaId}", id);

                if (EsAjax())
                    return Json(new { ok = false, error = "Error al subir comprobante" });

                TempData["MensajeError"] = "Error al subir el comprobante.";
                return RedirectToAction(nameof(CompraExito), new { id });
            }
        }

        /// <summary>
        /// GET: /Compras/CompraExito/{id}
        /// Pantalla de confirmación de compra exitosa
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> CompraExito(int id, CancellationToken ct = default)
        {
            var uid = _userManager.GetUserId(User);

            try
            {
                var venta = await _context.Ventas
                    .AsNoTracking()
                    .Include(v => v.Usuario)
                    .FirstOrDefaultAsync(v => v.VentaID == id && v.UsuarioId == uid, ct);

                if (venta == null)
                {
                    _logger.LogWarning(
                        "Intento de ver compra exitosa no autorizada. VentaId: {VentaId}, UserId: {UserId}",
                        id,
                        uid);
                    return RedirectToAction("Index", "Home");
                }

                return View(venta);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar CompraExito. VentaId: {VentaId}", id);
                return RedirectToAction("Index", "Home");
            }
        }

        /// <summary>
        /// GET: /Compras/CompraError
        /// Pantalla de error en compra
        /// </summary>
        [HttpGet]
        public IActionResult CompraError() => View();

        /// <summary>
        /// GET: /Compras/Comprobante/{id}
        /// Genera comprobante HTML de la venta
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Comprobante(int id, CancellationToken ct = default)
        {
            var uid = _userManager.GetUserId(User);

            try
            {
                var venta = await _context.Ventas
                    .AsNoTracking()
                    .Include(v => v.Usuario)
                    .Include(v => v.DetalleVentas)
                        .ThenInclude(d => d.Producto)
                    .FirstOrDefaultAsync(v => v.VentaID == id && v.UsuarioId == uid, ct);

                if (venta == null)
                {
                    _logger.LogWarning(
                        "Intento de ver comprobante no autorizado. VentaId: {VentaId}, UserId: {UserId}",
                        id,
                        uid);
                    return NotFound();
                }

                var html = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, Helvetica, sans-serif; }}
        h1 {{ font-size:20px;margin:0 0 8px }}
        table {{ width:100%;border-collapse:collapse;margin-top:12px }}
        th,td {{ border:1px solid #ddd;padding:8px;font-size:12px }}
        th {{ background:#f3f4f6;text-align:left }}
        .r {{ text-align:right }}
        .muted {{ color:#6b7280;font-size:12px }}
    </style>
</head>
<body>
    <h1>Comprobante de compra</h1>
    <div class='muted'>Referencia: #{venta.VentaID} · Fecha: {venta.FechaVenta:dd/MM/yyyy HH:mm}</div>
    <div class='muted'>Cliente: {venta.Usuario?.NombreCompleto} · Email: {venta.Usuario?.Email}</div>
    <table>
        <thead>
            <tr>
                <th>Producto</th>
                <th class='r'>Cant.</th>
                <th class='r'>Precio</th>
                <th class='r'>Subtotal</th>
            </tr>
        </thead>
        <tbody>
            {string.Join("", venta.DetalleVentas.Select(d =>
                $"<tr><td>{d.Producto?.Nombre}</td>" +
                $"<td class='r'>{d.Cantidad}</td>" +
                $"<td class='r'>{d.PrecioUnitario:C2}</td>" +
                $"<td class='r'>{d.Subtotal:C2}</td></tr>"))}
        </tbody>
        <tfoot>
            <tr>
                <th colspan='3' class='r'>Total</th>
                <th class='r'>{venta.Total:C2}</th>
            </tr>
        </tfoot>
    </table>
    <p class='muted'>Método de pago: {venta.MetodoPago ?? "N/D"} · Estado: {venta.Estado ?? "N/D"}</p>
</body>
</html>";

                var bytes = System.Text.Encoding.UTF8.GetBytes(html);
                return File(bytes, "text/html", $"comprobante-{venta.VentaID}.html");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar comprobante. VentaId: {VentaId}", id);
                return NotFound();
            }
        }

        #endregion

        #region HELPERS - ARCHIVOS Y METADATOS

        /// <summary>
        /// Guarda un comprobante de pago
        /// </summary>
        private async Task<(bool ok, string? relUrl, string? error)> GuardarComprobanteAsync(
            int ventaId,
            IFormFile archivo,
            CancellationToken ct)
        {
            if (archivo == null || archivo.Length == 0)
                return (false, null, "No se recibió archivo.");

            if (!EsMimePermitido(archivo))
                return (false, null, "Formato no permitido. Usa JPG, PNG, WEBP o PDF.");

            if (archivo.Length > MAX_FILE_SIZE_BYTES)
                return (false, null, $"El archivo supera {MAX_FILE_SIZE_MB}MB.");

            try
            {
                var folder = UploadsFolderAbs();
                Directory.CreateDirectory(folder);

                // Eliminar comprobantes anteriores
                foreach (var old in Directory.EnumerateFiles(folder, $"venta-{ventaId}.*", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        System.IO.File.Delete(old);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "No se pudo eliminar archivo antiguo: {Path}", old);
                    }
                }

                var ext = Path.GetExtension(archivo.FileName);
                var fileAbs = Path.Combine(folder, $"venta-{ventaId}{ext}");

                await using var fs = new FileStream(fileAbs, FileMode.Create, FileAccess.Write, FileShare.None);
                await archivo.CopyToAsync(fs, ct);

                var rel = Path.GetRelativePath(_env.WebRootPath, fileAbs).Replace("\\", "/");
                return (true, "/" + rel.TrimStart('/'), null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar comprobante. VentaId: {VentaId}", ventaId);
                return (false, null, "Error al guardar el archivo");
            }
        }

        /// <summary>
        /// Guarda metadatos del depósito en archivo JSON (para compatibilidad)
        /// </summary>
        private void GuardarMetaDeposito(int ventaId, string? depositante, string? banco)
        {
            try
            {
                var folder = UploadsFolderAbs();
                Directory.CreateDirectory(folder);

                var metaPath = Path.Combine(folder, $"venta-{ventaId}.meta.json");

                // Si el archivo ya existe, intentar actualizarlo sin perder el formato
                if (System.IO.File.Exists(metaPath))
                {
                    try
                    {
                        var existingJson = System.IO.File.ReadAllText(metaPath);
                        using var doc = JsonDocument.Parse(existingJson);
                        var root = doc.RootElement;

                        // Si tiene formato completo (con bancoSeleccion), actualizar solo los campos necesarios
                        if (root.TryGetProperty("bancoSeleccion", out _))
                        {
                            // Leer el JSON existente como Dictionary para poder modificarlo
                            var existingObj = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(existingJson);
                            if (existingObj != null)
                            {
                                // Actualizar solo depositante si se proporciona
                                if (!string.IsNullOrWhiteSpace(depositante))
                                {
                                    existingObj["depositante"] = JsonSerializer.SerializeToElement(depositante.Trim());
                                }

                                // Actualizar timestamp
                                existingObj["ts"] = JsonSerializer.SerializeToElement(DateTime.UtcNow);

                                // Guardar de vuelta
                                System.IO.File.WriteAllText(metaPath, JsonSerializer.Serialize(existingObj));

                                _logger.LogInformation(
                                    "Metadata actualizado (formato preservado). VentaId: {VentaId}",
                                    ventaId);

                                return;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "No se pudo actualizar metadata existente, se creará uno nuevo. VentaId: {VentaId}",
                            ventaId);
                    }
                }

                // Si no existe o no se pudo actualizar, crear uno nuevo con formato compatible
                object metaObj;

                if (!string.IsNullOrWhiteSpace(banco))
                {
                    // Formato compatible con MisComprasController
                    metaObj = new
                    {
                        depositante = string.IsNullOrWhiteSpace(depositante) ? null : depositante.Trim(),
                        bancoSeleccion = new
                        {
                            banco = new
                            {
                                nombre = banco.Trim(),
                                codigo = banco.Trim().ToLowerInvariant()
                            }
                        },
                        ts = DateTime.UtcNow
                    };
                }
                else
                {
                    // Si no hay banco, formato simple
                    metaObj = new
                    {
                        depositante = string.IsNullOrWhiteSpace(depositante) ? null : depositante.Trim(),
                        ts = DateTime.UtcNow
                    };
                }

                System.IO.File.WriteAllText(metaPath, JsonSerializer.Serialize(metaObj));

                _logger.LogInformation(
                    "Metadata creado (formato compatible). VentaId: {VentaId}, Depositante: {Dep}, Banco: {Banco}",
                    ventaId,
                    depositante ?? "(vacío)",
                    banco ?? "(vacío)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar meta depósito. VentaId: {VentaId}", ventaId);
            }
        }

        /// <summary>
        /// Resuelve el banco seleccionado validando admin/tienda
        /// </summary>
        private async Task<(bool ok, object metaBanco, string destino, string? error)>
            ResolverBancoSeleccionadoAsync(string bancoSeleccionado, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(bancoSeleccionado))
                return (false, new { }, "", "No se especificó banco.");

            var raw = bancoSeleccionado.Trim();
            if (!raw.Contains(':', StringComparison.Ordinal))
                raw = $"admin:{raw}";

            var parts = raw.Split(':', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                return (false, new { }, "", "Formato de banco inválido.");

            var scope = parts[0].ToLowerInvariant();

            // ADMIN
            if (scope == "admin")
            {
                var codigo = parts[1].Trim().ToLowerInvariant();
                var admin = (await _bancosSvc.GetAdminAsync(ct))
                    .Where(c => c.Activo)
                    .ToList();

                var sel = admin.FirstOrDefault(c =>
                    string.Equals(c.Codigo?.Trim(), codigo, StringComparison.OrdinalIgnoreCase));

                if (sel == null)
                    return (false, new { }, "admin", "Banco del administrador inválido o inactivo.");

                var meta = new
                {
                    destino = "admin",
                    banco = new
                    {
                        codigo = sel.Codigo,
                        nombre = sel.Nombre,
                        numero = sel.Numero,
                        tipo = sel.Tipo,
                        titular = sel.Titular,
                        ruc = sel.Ruc
                    }
                };

                return (true, meta, "admin", null);
            }

            // TIENDA
            if (scope == "tienda")
            {
                var vendedorId = parts[1].Trim();
                if (string.IsNullOrWhiteSpace(vendedorId))
                    return (false, new { }, "tienda", "Vendedor no especificado.");

                var cuentas = (await _bancosSvc.GetByProveedorAsync(vendedorId, ct))
                    .Where(c => c.Activo)
                    .ToList();

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
                    if (cuentas.Count == 1)
                        sel = cuentas[0];
                    else
                        return (false, new { }, "tienda", "Selecciona una cuenta específica del vendedor.");
                }

                var meta = new
                {
                    destino = "tienda",
                    vendedorId,
                    banco = new
                    {
                        codigo = sel!.Codigo,
                        nombre = sel.Nombre,
                        numero = sel.Numero,
                        tipo = sel.Tipo,
                        titular = sel.Titular,
                        ruc = sel.Ruc
                    }
                };

                return (true, meta, "tienda", null);
            }

            return (false, new { }, "", "Ámbito de banco inválido.");
        }

        #endregion
    }
}
