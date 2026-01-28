using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Simone.Data;
using Simone.Models;

namespace Simone.Controllers
{
    /// <summary>
    /// Controlador de historial de compras del usuario
    /// Permite ver el listado y detalle de compras realizadas
    /// Versión optimizada con mejores prácticas empresariales
    /// </summary>
    [Authorize]
    [AutoValidateAntiforgeryToken]
    public class MisComprasController : Controller
    {
        #region Dependencias

        private readonly TiendaDbContext _context;
        private readonly UserManager<Usuario> _userManager;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<MisComprasController> _logger;
        private readonly IMemoryCache _cache;

        #endregion

        #region Constantes

        // Paginación
        private const int PAGE_SIZE_MIN = 5;
        private const int PAGE_SIZE_MAX = 50;
        private const int PAGE_SIZE_DEFAULT = 15;
        private const int PAGE_NUMBER_MIN = 1;

        // Archivos
        private const long MAX_COMPROBANTE_SIZE = 5 * 1024 * 1024; // 5MB

        private static readonly HashSet<string> EXTENSIONES_PERMITIDAS = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".pdf"
        };

        // Rutas
        private const string FOLDER_WWWROOT = "wwwroot";
        private const string FOLDER_UPLOADS = "uploads";
        private const string FOLDER_COMPROBANTES = "comprobantes";
        private const string PATTERN_VENTA_FILE = "venta-{0}.*";
        private const string PATTERN_VENTA_META_JSON = "venta-{0}.meta.json";
        private const string PATTERN_VENTA_META_TXT = "venta-{0}.txt";

        // Prefijos URL
        private const string URL_PREFIX_HTTP = "http://";
        private const string URL_PREFIX_HTTPS = "https://";
        private const string URL_PREFIX_TILDE = "~/";
        private const string URL_PREFIX_SLASH = "/";

        // JSON Properties
        private const string JSON_PROP_DEPOSITANTE = "depositante";
        private const string JSON_PROP_BANCO = "banco";
        private const string JSON_PROP_BANCO_SELECCION = "bancoSeleccion";
        private const string JSON_PROP_NOMBRE = "nombre";
        private const string JSON_PROP_CODIGO = "codigo";
        private const string JSON_PROP_VALOR = "valor";

        // Mensajes
        private const string MSG_ERROR_NO_AUTENTICADO = "Debes iniciar sesión para ver tus compras.";
        private const string MSG_ERROR_COMPRA_NO_ENCONTRADA = "No se encontró la compra solicitada.";

        // Cache
        private const string CACHE_KEY_COMPRAS_USUARIO_PREFIX = "Compras_Usuario_";
        private const string CACHE_KEY_DETALLE_VENTA_PREFIX = "Detalle_Venta_";

        private static readonly TimeSpan CACHE_DURATION_COMPRAS = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan CACHE_DURATION_DETALLE = TimeSpan.FromMinutes(10);

        #endregion

        #region Constructor

        /// <summary>
        /// Constructor con inyección de dependencias
        /// </summary>
        public MisComprasController(
            TiendaDbContext context,
            UserManager<Usuario> userManager,
            IWebHostEnvironment env,
            ILogger<MisComprasController> logger,
            IMemoryCache cache)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _env = env ?? throw new ArgumentNullException(nameof(env));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        #endregion

        #region Listado de Compras

        /// <summary>
        /// GET: /MisCompras o /MisCompras/Index
        /// Historial de compras del usuario autenticado con paginación
        /// </summary>
        /// <param name="page">Número de página (default: 1)</param>
        /// <param name="pageSize">Tamaño de página (default: 15, min: 5, max: 50)</param>
        /// <param name="ct">Token de cancelación</param>
        /// <returns>Vista con listado de compras paginadas</returns>
        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Index(
            int page = PAGE_NUMBER_MIN,
            int pageSize = PAGE_SIZE_DEFAULT,
            CancellationToken ct = default)
        {
            try
            {
                var userId = _userManager.GetUserId(User);
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("Intento de acceder a historial de compras sin autenticación");
                    TempData["MensajeError"] = MSG_ERROR_NO_AUTENTICADO;
                    return RedirectToAction("Login", "Cuenta");
                }

                // Validar paginación
                page = Math.Max(PAGE_NUMBER_MIN, page);
                pageSize = Math.Clamp(pageSize, PAGE_SIZE_MIN, PAGE_SIZE_MAX);

                _logger.LogInformation(
                    "Cargando historial de compras. UserId: {UserId}, Página: {Page}, PageSize: {PageSize}",
                    userId,
                    page,
                    pageSize);

                // Obtener compras con cache
                var resultado = await ObtenerComprasUsuarioAsync(userId, page, pageSize, ct);

                _logger.LogDebug(
                    "Historial cargado. UserId: {UserId}, Total: {Total}, Página: {Page}/{TotalPages}",
                    userId,
                    resultado.Total,
                    page,
                    (int)Math.Ceiling(resultado.Total / (double)pageSize));

                // Pasar datos a la vista
                ViewBag.Page = page;
                ViewBag.PageSize = pageSize;
                ViewBag.Total = resultado.Total;
                ViewBag.TotalPages = (int)Math.Ceiling(resultado.Total / (double)pageSize);

                return View(resultado.Ventas);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar historial de compras");
                TempData["MensajeError"] = "Error al cargar tus compras. Por favor, intenta nuevamente.";
                return View(new List<Ventas>());
            }
        }

        /// <summary>
        /// Obtiene las compras del usuario con cache
        /// </summary>
        private async Task<ResultadoCompras> ObtenerComprasUsuarioAsync(
            string userId,
            int page,
            int pageSize,
            CancellationToken ct)
        {
            var cacheKey = $"{CACHE_KEY_COMPRAS_USUARIO_PREFIX}{userId}_P{page}_S{pageSize}";

            return await _cache.GetOrCreateAsync(
                cacheKey,
                async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = CACHE_DURATION_COMPRAS;

                    _logger.LogDebug(
                        "Cargando compras desde BD (cache miss). UserId: {UserId}",
                        userId);

                    var baseQuery = _context.Ventas
                        .AsNoTracking()
                        .Where(v => v.UsuarioId == userId)
                        .OrderByDescending(v => v.FechaVenta);

                    var total = await baseQuery.CountAsync(ct);

                    var ventas = await baseQuery
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .ToListAsync(ct);

                    return new ResultadoCompras(ventas, total);
                }) ?? new ResultadoCompras(new List<Ventas>(), 0);
        }

        #endregion

        #region Detalle de Compra

        /// <summary>
        /// GET: /MisCompras/Detalle/{id}
        /// Detalle completo de una compra específica del usuario
        /// </summary>
        /// <param name="id">ID de la venta</param>
        /// <param name="ct">Token de cancelación</param>
        /// <returns>Vista con detalle de la compra</returns>
        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Detalle(int id, CancellationToken ct = default)
        {
            try
            {
                var userId = _userManager.GetUserId(User);
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning(
                        "Intento de acceder a detalle de compra sin autenticación. VentaId: {VentaId}",
                        id);

                    TempData["MensajeError"] = MSG_ERROR_NO_AUTENTICADO;
                    return RedirectToAction("Login", "Cuenta");
                }

                _logger.LogInformation(
                    "Cargando detalle de compra. VentaId: {VentaId}, UserId: {UserId}",
                    id,
                    userId);

                var venta = await ObtenerDetalleVentaAsync(id, userId, ct);

                if (venta == null)
                {
                    _logger.LogWarning(
                        "Compra no encontrada o no autorizada. VentaId: {VentaId}, UserId: {UserId}",
                        id,
                        userId);

                    TempData["MensajeError"] = MSG_ERROR_COMPRA_NO_ENCONTRADA;
                    return RedirectToAction(nameof(Index));
                }

                // Enriquecer con información adicional
                var infoComprobante = ObtenerInformacionComprobante(venta);

                // Pasar datos a la vista
                ViewBag.ComprobanteUrl = infoComprobante.Url;
                ViewBag.Depositante = infoComprobante.Depositante;
                ViewBag.Banco = infoComprobante.Banco;
                ViewBag.TotalItems = venta.DetalleVentas?.Sum(d => d.Cantidad) ?? 0;

                _logger.LogDebug(
    "Detalle cargado. VentaId: {VentaId}, Items: {Items}, Total: {Total:C}",
    id,
    (int)ViewBag.TotalItems,  // ← Añadir (int)
    venta.Total);

                return View(venta);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error al cargar detalle de compra. VentaId: {VentaId}",
                    id);

                TempData["MensajeError"] = "Error al cargar el detalle de la compra.";
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// Obtiene el detalle de una venta con cache
        /// </summary>
        private async Task<Ventas?> ObtenerDetalleVentaAsync(
            int ventaId,
            string userId,
            CancellationToken ct)
        {
            var cacheKey = $"{CACHE_KEY_DETALLE_VENTA_PREFIX}{ventaId}_{userId}";

            return await _cache.GetOrCreateAsync(
                cacheKey,
                async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = CACHE_DURATION_DETALLE;

                    _logger.LogDebug(
                        "Cargando detalle desde BD (cache miss). VentaId: {VentaId}",
                        ventaId);

                    var venta = await _context.Ventas
                        .AsNoTracking()
                        .Where(v => v.VentaID == ventaId && v.UsuarioId == userId)
                        .Include(v => v.Usuario)
                        .Include(v => v.DetalleVentas)
                            .ThenInclude(dv => dv.Producto)
#if NET5_0_OR_GREATER
                        .AsSplitQuery()
#endif
                        .FirstOrDefaultAsync(ct);

                    return venta;
                });
        }

        #endregion

        #region Helpers - Rutas

        /// <summary>
        /// Obtiene la ruta absoluta de wwwroot
        /// </summary>
        private string WebRootAbs() =>
            _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), FOLDER_WWWROOT);

        /// <summary>
        /// Obtiene la ruta absoluta de la carpeta de comprobantes
        /// </summary>
        private string UploadsFolderAbs() =>
            Path.Combine(WebRootAbs(), FOLDER_UPLOADS, FOLDER_COMPROBANTES);

        /// <summary>
        /// Normaliza una URL para que sea servible en el navegador
        /// </summary>
        /// <param name="raw">URL cruda</param>
        /// <returns>URL normalizada o null</returns>
        private string? NormalizarCompUrl(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            var url = raw.Trim();

            // URLs absolutas (http/https)
            if (url.StartsWith(URL_PREFIX_HTTP, StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith(URL_PREFIX_HTTPS, StringComparison.OrdinalIgnoreCase))
            {
                return url;
            }

            // URLs con tilde (~/)
            if (url.StartsWith(URL_PREFIX_TILDE))
            {
                return Url.Content(url);
            }

            // URLs relativas con slash
            if (url.StartsWith(URL_PREFIX_SLASH))
            {
                return url;
            }

            // URLs relativas sin slash
            return URL_PREFIX_SLASH + url.TrimStart('/');
        }

        #endregion

        #region Helpers - Comprobantes

        /// <summary>
        /// Busca el comprobante de una venta en el sistema de archivos
        /// </summary>
        /// <param name="ventaId">ID de la venta</param>
        /// <returns>URL del comprobante o null si no existe</returns>
        private string? BuscarComprobanteUrl(int ventaId)
        {
            try
            {
                var folder = UploadsFolderAbs();
                if (!Directory.Exists(folder))
                {
                    _logger.LogDebug(
                        "Carpeta de comprobantes no existe. VentaId: {VentaId}",
                        ventaId);
                    return null;
                }

                var pattern = string.Format(PATTERN_VENTA_FILE, ventaId);
                var files = Directory.EnumerateFiles(folder, pattern, SearchOption.TopDirectoryOnly)
                    .Where(f => EXTENSIONES_PERMITIDAS.Contains(Path.GetExtension(f)))
                    .OrderByDescending(f => System.IO.File.GetLastWriteTimeUtc(f))
                    .ToList();

                if (files.Count == 0)
                {
                    _logger.LogDebug(
                        "No se encontró comprobante para venta. VentaId: {VentaId}",
                        ventaId);
                    return null;
                }

                var rel = Path.GetRelativePath(WebRootAbs(), files[0])
                    .Replace("\\", "/");

                var url = NormalizarCompUrl(URL_PREFIX_SLASH + rel.TrimStart('/'));

                _logger.LogDebug(
                    "Comprobante encontrado. VentaId: {VentaId}, Url: {Url}",
                    ventaId,
                    url);

                return url;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Error al buscar comprobante. VentaId: {VentaId}",
                    ventaId);
                return null;
            }
        }

        /// <summary>
        /// Busca los metadatos del depósito de una venta
        /// </summary>
        /// <param name="ventaId">ID de la venta</param>
        /// <returns>Tupla con depositante y banco</returns>
        private MetadatosDeposito BuscarMetaDeposito(int ventaId)
        {
            try
            {
                var folder = UploadsFolderAbs();
                if (!Directory.Exists(folder))
                {
                    return MetadatosDeposito.Empty;
                }

                // Intentar leer JSON primero
                var metaPath = Path.Combine(folder, string.Format(PATTERN_VENTA_META_JSON, ventaId));
                if (System.IO.File.Exists(metaPath))
                {
                    var resultado = LeerMetadatosJson(metaPath, ventaId);
                    if (resultado.TieneInformacion)
                    {
                        return resultado;
                    }
                }

                // Fallback a TXT legado
                var txtPath = Path.Combine(folder, string.Format(PATTERN_VENTA_META_TXT, ventaId));
                if (System.IO.File.Exists(txtPath))
                {
                    var depositante = System.IO.File.ReadAllText(txtPath).Trim();
                    if (!string.IsNullOrWhiteSpace(depositante))
                    {
                        _logger.LogDebug(
                            "Metadatos TXT encontrados. VentaId: {VentaId}",
                            ventaId);

                        return new MetadatosDeposito(depositante, null);
                    }
                }

                return MetadatosDeposito.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Error al buscar metadatos de depósito. VentaId: {VentaId}",
                    ventaId);
                return MetadatosDeposito.Empty;
            }
        }

        /// <summary>
        /// Lee metadatos desde archivo JSON
        /// </summary>
        private MetadatosDeposito LeerMetadatosJson(string metaPath, int ventaId)
        {
            try
            {
                var json = System.IO.File.ReadAllText(metaPath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string? depositante = null;
                string? banco = null;

                // Leer depositante
                if (root.TryGetProperty(JSON_PROP_DEPOSITANTE, out var pDep))
                {
                    depositante = pDep.ValueKind == JsonValueKind.String
                        ? pDep.GetString()
                        : pDep.ToString();
                }

                // Leer banco (puede ser string u objeto)
                if (root.TryGetProperty(JSON_PROP_BANCO_SELECCION, out var pSel))
                {
                    banco = ExtraerBancoDePropiedadJson(pSel);
                }
                else if (root.TryGetProperty(JSON_PROP_BANCO, out var pBanco))
                {
                    banco = pBanco.ValueKind == JsonValueKind.String
                        ? pBanco.GetString()
                        : pBanco.ToString();
                }

                depositante = NormalizeStringOrNull(depositante);
                banco = NormalizeStringOrNull(banco);

                _logger.LogDebug(
                    "Metadatos JSON leídos. VentaId: {VentaId}, Depositante: {Dep}, Banco: {Banco}",
                    ventaId,
                    depositante ?? "N/A",
                    banco ?? "N/A");

                return new MetadatosDeposito(depositante, banco);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "No se pudo leer meta JSON. VentaId: {VentaId}, Path: {Path}",
                    ventaId,
                    metaPath);
                return MetadatosDeposito.Empty;
            }
        }

        /// <summary>
        /// Extrae el nombre del banco de una propiedad JSON
        /// </summary>
        private static string? ExtraerBancoDePropiedadJson(JsonElement elemento)
        {
            if (elemento.ValueKind == JsonValueKind.String)
            {
                return elemento.GetString();
            }

            if (elemento.ValueKind == JsonValueKind.Object)
            {
                // Puede venir como { banco: {...} } o directamente {...}
                if (elemento.TryGetProperty(JSON_PROP_BANCO, out var pBancoObj) &&
                    pBancoObj.ValueKind == JsonValueKind.Object)
                {
                    return LeerNombreBancoDeObjeto(pBancoObj);
                }

                return LeerNombreBancoDeObjeto(elemento);
            }

            return null;
        }

        /// <summary>
        /// Lee el nombre del banco de un objeto JSON
        /// </summary>
        private static string? LeerNombreBancoDeObjeto(JsonElement obj)
        {
            if (obj.TryGetProperty(JSON_PROP_NOMBRE, out var pn) &&
                pn.ValueKind == JsonValueKind.String)
            {
                return pn.GetString();
            }

            if (obj.TryGetProperty(JSON_PROP_CODIGO, out var pc) &&
                pc.ValueKind == JsonValueKind.String)
            {
                return pc.GetString();
            }

            if (obj.TryGetProperty(JSON_PROP_VALOR, out var pv) &&
                pv.ValueKind == JsonValueKind.String)
            {
                return pv.GetString();
            }

            return null;
        }

        /// <summary>
        /// Obtiene la información completa del comprobante de una venta
        /// </summary>
        private InformacionComprobante ObtenerInformacionComprobante(Ventas venta)
        {
            try
            {
                // 1. Buscar URL del comprobante
                // Prioridad: ComprobanteUrl de BD > FotoComprobanteDeposito del usuario > Buscar en archivos
                var comprobanteUrl = NormalizarCompUrl(venta.ComprobanteUrl)
                                   ?? NormalizarCompUrl(venta.Usuario?.FotoComprobanteDeposito)
                                   ?? BuscarComprobanteUrl(venta.VentaID);

                // 2. Leer depositante y banco DIRECTAMENTE DE LA BASE DE DATOS (como hace Panel)
                var depositante = NormalizeStringOrNull(venta.Depositante);
                var banco = NormalizeStringOrNull(venta.Banco);

                // 3. Fallback: Si no están en BD, buscar en archivos metadata
                if (string.IsNullOrWhiteSpace(depositante) || string.IsNullOrWhiteSpace(banco))
                {
                    var metaDeposito = BuscarMetaDeposito(venta.VentaID);

                    if (string.IsNullOrWhiteSpace(depositante))
                    {
                        depositante = metaDeposito.Depositante;
                    }

                    if (string.IsNullOrWhiteSpace(banco))
                    {
                        banco = metaDeposito.Banco;
                    }
                }

                // 4. Fallback final: datos del perfil del usuario
                if (string.IsNullOrWhiteSpace(depositante))
                {
                    depositante = NormalizeStringOrNull(venta.Usuario?.NombreDepositante);
                }

                _logger.LogDebug(
                    "Info comprobante obtenida. VentaId: {VentaId}, Depositante: {Dep}, Banco: {Banco}, Url: {Url}",
                    venta.VentaID,
                    depositante ?? "NULL",
                    banco ?? "NULL",
                    comprobanteUrl ?? "NULL");

                return new InformacionComprobante(
                    comprobanteUrl,
                    depositante,
                    banco);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Error al obtener información de comprobante. VentaId: {VentaId}",
                    venta.VentaID);

                return InformacionComprobante.Empty;
            }
        }

        #endregion

        #region Helpers - Utilidades

        /// <summary>
        /// Normaliza un string: trim y null si está vacío
        /// </summary>
        private static string? NormalizeStringOrNull(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var trimmed = value.Trim();
            return string.IsNullOrEmpty(trimmed) ? null : trimmed;
        }

        #endregion

        #region Diagnóstico (TEMPORAL - Remover en producción)

        /// <summary>
        /// GET: /MisCompras/DiagnosticoMeta/{id}
        /// Endpoint de diagnóstico para verificar los archivos metadata
        /// TEMPORAL: Remover antes de producción
        /// </summary>
        [HttpGet]
        public IActionResult DiagnosticoMeta(int id)
        {
            try
            {
                var folder = UploadsFolderAbs();
                var jsonPath = Path.Combine(folder, string.Format(PATTERN_VENTA_META_JSON, id));
                var txtPath = Path.Combine(folder, string.Format(PATTERN_VENTA_META_TXT, id));

                var diagnostico = new
                {
                    VentaId = id,
                    FolderExists = Directory.Exists(folder),
                    FolderPath = folder,
                    JsonExists = System.IO.File.Exists(jsonPath),
                    JsonPath = jsonPath,
                    TxtExists = System.IO.File.Exists(txtPath),
                    TxtPath = txtPath,
                    AllFiles = Directory.Exists(folder)
                        ? Directory.GetFiles(folder, $"venta-{id}*").Select(Path.GetFileName).ToList()
                        : new List<string>()
                };

                // Si existe JSON, leerlo
                if (System.IO.File.Exists(jsonPath))
                {
                    var jsonContent = System.IO.File.ReadAllText(jsonPath);
                    var metadatos = BuscarMetaDeposito(id);

                    return Json(new
                    {
                        diagnostico.VentaId,
                        diagnostico.FolderExists,
                        diagnostico.FolderPath,
                        diagnostico.JsonExists,
                        diagnostico.JsonPath,
                        diagnostico.TxtExists,
                        diagnostico.TxtPath,
                        diagnostico.AllFiles,
                        JsonContent = jsonContent,
                        MetadatasParsed = new
                        {
                            metadatos.Depositante,
                            metadatos.Banco,
                            metadatos.TieneInformacion
                        }
                    });
                }

                return Json(diagnostico);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    Error = ex.Message,
                    StackTrace = ex.StackTrace
                });
            }
        }

        #endregion

        #region Records

        /// <summary>
        /// Resultado de consulta de compras
        /// </summary>
        private record ResultadoCompras(List<Ventas> Ventas, int Total);

        /// <summary>
        /// Metadatos del depósito
        /// </summary>
        private record MetadatosDeposito(string? Depositante, string? Banco)
        {
            public static readonly MetadatosDeposito Empty = new(null, null);
            public bool TieneInformacion => !string.IsNullOrWhiteSpace(Depositante) ||
                                           !string.IsNullOrWhiteSpace(Banco);
        }

        /// <summary>
        /// Información del comprobante de pago
        /// </summary>
        private record InformacionComprobante(string? Url, string? Depositante, string? Banco)
        {
            public static readonly InformacionComprobante Empty = new(null, null, null);
        }

        #endregion
    }
}