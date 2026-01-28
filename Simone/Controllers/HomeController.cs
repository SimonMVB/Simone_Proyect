using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Simone.Data;
using Simone.Models;

namespace Simone.Controllers
{
    /// <summary>
    /// Controlador principal de la aplicación
    /// Maneja las páginas de inicio, privacidad, ofertas, nosotros y el manejo de errores
    /// Versión optimizada con mejores prácticas empresariales
    /// </summary>
    public class HomeController : Controller
    {
        #region Dependencias

        private readonly ILogger<HomeController> _logger;
        private readonly TiendaDbContext _context;
        private readonly IMemoryCache _cache;

        #endregion

        #region Constantes

        // Productos destacados
        private const int PRODUCTOS_DESTACADOS_CANTIDAD = 4;
        private const int PRODUCTOS_DESTACADOS_MINIMO = 1;

        // Cache
        private const string CACHE_KEY_PRODUCTOS_DESTACADOS = "ProductosDestacados";
        private const string CACHE_KEY_PRODUCTOS_OFERTAS = "ProductosOfertas";
        private const string CACHE_KEY_ESTADISTICAS_HOME = "EstadisticasHome";

        private static readonly TimeSpan CACHE_DURATION_DESTACADOS = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan CACHE_DURATION_OFERTAS = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan CACHE_DURATION_ESTADISTICAS = TimeSpan.FromMinutes(30);

        // Vistas
        private const string VISTA_OFERTAS = "~/Views/Panel/Ofertas.cshtml";

        #endregion

        #region Constructor

        /// <summary>
        /// Constructor que recibe las dependencias necesarias
        /// </summary>
        /// <param name="logger">Instancia de ILogger para registro de eventos</param>
        /// <param name="context">Contexto de la base de datos para consultas</param>
        /// <param name="cache">Cache en memoria para optimización de consultas</param>
        public HomeController(
            ILogger<HomeController> logger,
            TiendaDbContext context,
            IMemoryCache cache)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        #endregion

        #region Index

        /// <summary>
        /// GET: /Home/Index o /
        /// Página de inicio con productos destacados
        /// </summary>
        /// <param name="ct">Token de cancelación</param>
        /// <returns>Vista principal con productos destacados</returns>
        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken ct = default)
        {
            try
            {
                var destacados = await ObtenerProductosDestacadosAsync(ct);

                _logger.LogInformation(
                    "Página de inicio cargada. Productos destacados: {Count}",
                    destacados.Count);

                return View(destacados);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar página de inicio");

                // Retornar vista vacía en caso de error
                return View(new List<Producto>());
            }
        }

        /// <summary>
        /// Obtiene productos destacados con cache
        /// </summary>
        private async Task<List<Producto>> ObtenerProductosDestacadosAsync(CancellationToken ct)
        {
            return await _cache.GetOrCreateAsync(
                CACHE_KEY_PRODUCTOS_DESTACADOS,
                async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = CACHE_DURATION_DESTACADOS;

                    _logger.LogDebug("Cargando productos destacados desde BD (cache miss)");

                    // Obtener productos activos/disponibles
                    var productos = await _context.Productos
                        .AsNoTracking()
                        .Where(p => p.Stock > 0) // Solo productos con stock
                        .ToListAsync(ct);

                    if (!productos.Any())
                    {
                        _logger.LogWarning("No hay productos disponibles para destacados");
                        return new List<Producto>();
                    }

                    // Selección aleatoria eficiente usando Random
                    var random = new Random();
                    var destacados = productos
                        .OrderBy(_ => random.Next())
                        .Take(PRODUCTOS_DESTACADOS_CANTIDAD)
                        .ToList();

                    _logger.LogDebug(
                        "Productos destacados cargados. Total disponibles: {Total}, Seleccionados: {Selected}",
                        productos.Count,
                        destacados.Count);

                    return destacados;
                }) ?? new List<Producto>();
        }

        #endregion

        #region Proximamente

        /// <summary>
        /// GET: /Home/Proximamente
        /// Página de "Próximamente" para funcionalidades en desarrollo
        /// </summary>
        /// <returns>Vista de Próximamente</returns>
        [HttpGet]
        public IActionResult Proximamente()
        {
            _logger.LogInformation("Página 'Próximamente' accedida");
            return View();
        }

        #endregion

        #region Privacy

        /// <summary>
        /// GET: /Home/Privacy
        /// Página de privacidad, términos y condiciones
        /// </summary>
        /// <returns>Vista de privacidad</returns>
        [HttpGet]
        public IActionResult Privacy()
        {
            try
            {
                _logger.LogInformation(
                    "Página de privacidad accedida. Usuario: {User}",
                    User?.Identity?.Name ?? "Anónimo");

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar página de privacidad");
                return View();
            }
        }

        #endregion

        #region Ofertas

        /// <summary>
        /// GET: /Home/Ofertas
        /// Página que muestra productos en oferta o promociones especiales
        /// </summary>
        /// <param name="ct">Token de cancelación</param>
        /// <returns>Vista de Ofertas con productos en promoción</returns>
        [HttpGet]
        public async Task<IActionResult> Ofertas(CancellationToken ct = default)
        {
            try
            {
                _logger.LogInformation(
                    "Página de Ofertas accedida. Usuario: {User}",
                    User?.Identity?.Name ?? "Anónimo");

                var productosEnOferta = await ObtenerProductosOfertaAsync(ct);

                _logger.LogDebug(
                    "Productos en oferta cargados: {Count}",
                    productosEnOferta.Count);

                // Usar ViewBag para pasar datos adicionales si es necesario
                ViewBag.TotalOfertas = productosEnOferta.Count;
                ViewBag.FechaActualizacion = DateTime.Now;

                return View(VISTA_OFERTAS);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar página de ofertas");
                return View(VISTA_OFERTAS);
            }
        }

        /// <summary>
        /// Obtiene productos en oferta con cache
        /// </summary>
        private async Task<List<Producto>> ObtenerProductosOfertaAsync(CancellationToken ct)
        {
            return await _cache.GetOrCreateAsync(
                CACHE_KEY_PRODUCTOS_OFERTAS,
                async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = CACHE_DURATION_OFERTAS;

                    _logger.LogDebug("Cargando productos en oferta desde BD (cache miss)");

                    // TODO: Implementar lógica de ofertas según tu modelo
                    // Ejemplo:
                    // var ofertas = await _context.Productos
                    //     .AsNoTracking()
                    //     .Where(p => p.EnOferta == true && p.Stock > 0)
                    //     .OrderByDescending(p => p.DescuentoPorcentaje)
                    //     .ToListAsync(ct);

                    // Por ahora retornamos lista vacía
                    var ofertas = new List<Producto>();

                    _logger.LogDebug("Productos en oferta cargados: {Count}", ofertas.Count);

                    return ofertas;
                }) ?? new List<Producto>();
        }

        #endregion

        #region Nosotros

        /// <summary>
        /// GET: /Home/Nosotros
        /// Página que muestra información sobre la empresa
        /// </summary>
        /// <returns>Vista de Nosotros</returns>
        [HttpGet]
        public IActionResult Nosotros()
        {
            try
            {
                _logger.LogInformation(
                    "Página 'Nosotros' accedida. Usuario: {User}",
                    User?.Identity?.Name ?? "Anónimo");

                // Aquí podrías cargar información de la empresa desde BD o config
                ViewBag.AnioFundacion = 2020;
                ViewBag.CantidadProductos = _context.Productos.Count();

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar página 'Nosotros'");
                return View();
            }
        }

        #endregion

        #region Estadísticas (API)

        /// <summary>
        /// GET: /Home/Estadisticas
        /// API para obtener estadísticas generales (AJAX)
        /// </summary>
        /// <param name="ct">Token de cancelación</param>
        /// <returns>JSON con estadísticas</returns>
        [HttpGet]
        [ResponseCache(Duration = 300)] // Cache HTTP de 5 minutos
        public async Task<IActionResult> Estadisticas(CancellationToken ct = default)
        {
            try
            {
                var stats = await ObtenerEstadisticasAsync(ct);

                _logger.LogDebug(
                    "Estadísticas solicitadas. Productos: {Productos}, Categorías: {Categorias}",
                    stats.TotalProductos,
                    stats.TotalCategorias);

                return Json(new
                {
                    ok = true,
                    data = stats
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener estadísticas");
                return Json(new
                {
                    ok = false,
                    error = "Error al obtener estadísticas"
                });
            }
        }

        /// <summary>
        /// Obtiene estadísticas generales con cache
        /// </summary>
        private async Task<EstadisticasHome> ObtenerEstadisticasAsync(CancellationToken ct)
        {
            return await _cache.GetOrCreateAsync(
                CACHE_KEY_ESTADISTICAS_HOME,
                async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = CACHE_DURATION_ESTADISTICAS;

                    _logger.LogDebug("Calculando estadísticas desde BD (cache miss)");

                    var totalProductos = await _context.Productos.CountAsync(ct);
                    var productosDisponibles = await _context.Productos
                        .CountAsync(p => p.Stock > 0, ct);
                    var totalCategorias = await _context.Categorias.CountAsync(ct);

                    return new EstadisticasHome
                    {
                        TotalProductos = totalProductos,
                        ProductosDisponibles = productosDisponibles,
                        TotalCategorias = totalCategorias,
                        FechaActualizacion = DateTime.UtcNow
                    };
                }) ?? new EstadisticasHome();
        }

        #endregion

        #region Error

        /// <summary>
        /// GET: /Home/Error
        /// Manejo avanzado de errores con logging detallado
        /// </summary>
        /// <returns>Vista de Error con información de diagnóstico</returns>
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            try
            {
                var errorId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;

                // Obtener información adicional del error si está disponible
                var statusCode = HttpContext.Response.StatusCode;
                var path = HttpContext.Request.Path;

                _logger.LogError(
                    "Error detectado. ID: {ErrorId}, StatusCode: {StatusCode}, Path: {Path}, " +
                    "Usuario: {User}",
                    errorId,
                    statusCode,
                    path,
                    User?.Identity?.Name ?? "Anónimo");

                var errorViewModel = new ErrorViewModel
                {
                    RequestId = errorId
                };

                return View(errorViewModel);
            }
            catch (Exception ex)
            {
                // Error al manejar error - log crítico
                _logger.LogCritical(ex, "Error crítico al procesar página de error");

                return View(new ErrorViewModel
                {
                    RequestId = "ERROR_HANDLER_FAILED"
                });
            }
        }

        #endregion

        #region Health Check

        /// <summary>
        /// GET: /Home/Health
        /// Health check endpoint para monitoreo
        /// </summary>
        /// <param name="ct">Token de cancelación</param>
        /// <returns>JSON con estado del sistema</returns>
        [HttpGet]
        [ResponseCache(Duration = 30)] // Cache de 30 segundos
        public async Task<IActionResult> Health(CancellationToken ct = default)
        {
            try
            {
                // Verificar conexión a BD
                var canConnect = await _context.Database.CanConnectAsync(ct);

                _logger.LogDebug("Health check ejecutado. DB Connected: {Connected}", canConnect);

                if (!canConnect)
                {
                    _logger.LogWarning("Health check: No se pudo conectar a la base de datos");
                }

                return Json(new
                {
                    status = canConnect ? "healthy" : "unhealthy",
                    timestamp = DateTime.UtcNow,
                    database = canConnect ? "connected" : "disconnected",
                    version = "1.0.0"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en health check");
                return Json(new
                {
                    status = "unhealthy",
                    timestamp = DateTime.UtcNow,
                    error = "Health check failed"
                });
            }
        }

        #endregion

        #region ViewModels y Records

        /// <summary>
        /// Estadísticas de la página de inicio
        /// </summary>
        private record EstadisticasHome
        {
            public int TotalProductos { get; init; }
            public int ProductosDisponibles { get; init; }
            public int TotalCategorias { get; init; }
            public DateTime FechaActualizacion { get; init; }
        }

        #endregion
    }
}