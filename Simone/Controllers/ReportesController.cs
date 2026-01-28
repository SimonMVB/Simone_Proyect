using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Simone.Data;
using Simone.Models;
using Simone.ViewModels.Reportes;

namespace Simone.Controllers
{
    /// <summary>
    /// Controlador de reportes administrativos
    /// Gestiona visualización de ventas, detalles y reversiones
    /// Versión optimizada con mejores prácticas empresariales
    /// </summary>
    [Authorize(Roles = "Administrador")]
    [Route("Panel")]
    [AutoValidateAntiforgeryToken]
    public class ReportesController : Controller
    {
        #region Dependencias

        private readonly TiendaDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<ReportesController> _logger;

        #endregion

        #region Constantes

        // Roles
        private const string ROL_ADMINISTRADOR = "Administrador";

        // Rutas
        private const string FOLDER_WWWROOT = "wwwroot";
        private const string FOLDER_UPLOADS = "uploads";
        private const string FOLDER_COMPROBANTES = "comprobantes";
        private const string PATTERN_VENTA_FILE = "venta-{0}.*";
        private const string PATTERN_VENTA_META_JSON = "venta-{0}.meta.json";
        private const string PATTERN_VENTA_META_TXT = "venta-{0}.txt";

        // Extensiones permitidas
        private static readonly HashSet<string> EXTENSIONES_PERMITIDAS_COMPROBANTE = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".pdf"
        };

        // JSON Properties
        private const string JSON_PROP_DEPOSITANTE = "depositante";
        private const string JSON_PROP_BANCO = "banco";
        private const string JSON_PROP_BANCO_SELECCION = "bancoSeleccion";
        private const string JSON_PROP_CODIGO = "codigo";
        private const string JSON_PROP_NOMBRE = "nombre";
        private const string JSON_PROP_VALOR = "valor";
        private const string JSON_PROP_TIMESTAMP = "ts";

        // Estados de venta
        private const string ESTADO_ENVIADO = "Enviado";
        private const string ESTADO_CANCELADA = "Cancelada (revertida)";
        private const string ESTADO_CANCELADA_LOWERCASE = "cancel";

        // Motivos de reversión
        private const string MOTIVO_DEVOLUCION = "devolucion";
        private const string MOTIVO_DEPOSITO_FALSO = "deposito_falso";
        private const string MOTIVO_OTRO = "otro";

        // Mensajes
        private const string MSG_ERROR_DATOS_INVALIDOS = "Datos inválidos.";
        private const string MSG_ERROR_VENTA_NO_ENCONTRADA = "Venta no encontrada.";
        private const string MSG_ERROR_VENTA_YA_CANCELADA = "La venta ya se encuentra cancelada.";
        private const string MSG_ERROR_NO_REVERTIR_VENTA = "No se pudo revertir la venta.";
        private const string MSG_EXITO_VENTA_ENVIADA = "Venta marcada como enviada.";
        private const string MSG_EXITO_VENTA_REVERTIDA = "La venta fue revertida y el stock repuesto.";
        private const string MSG_ERROR_CARGAR_REPORTES = "No se pudieron cargar los reportes.";
        private const string MSG_ERROR_CARGAR_DETALLE = "No se pudo cargar el detalle de la venta.";

        // View names
        private const string VIEW_REPORTES = "~/Views/Reportes/Reportes.cshtml";
        private const string VIEW_VENTA_DETALLE = "~/Views/Reportes/VentaDetalle.cshtml";

        // Otros
        private const string TEXTO_SIN_USUARIO = "(sin usuario)";
        private const string TEXTO_PRODUCTO_GENERICO = "(producto)";
        private const string TEXTO_PRODUCTO_ID = "#{0}";
        private const string HEADER_AJAX_REQUEST = "X-Requested-With";
        private const string HEADER_AJAX_VALUE = "XMLHttpRequest";
        private const int TOP_VENTAS_RECIENTES = 50;
        private const int MESES_CLIENTES_NUEVOS = -1;

        // ViewBag
        private const string VIEWBAG_TOTAL_VENTAS = "TotalVentas";
        private const string VIEWBAG_TOTAL_INGRESOS = "TotalIngresos";
        private const string VIEWBAG_PRODUCTOS_VENDIDOS = "ProductosVendidos";
        private const string VIEWBAG_CLIENTES_NUEVOS = "ClientesNuevos";
        private const string TEMPDATA_MENSAJE_EXITO = "MensajeExito";
        private const string TEMPDATA_MENSAJE_ERROR = "MensajeError";

        #endregion

        #region Constructor

        /// <summary>
        /// Constructor con inyección de dependencias
        /// </summary>
        public ReportesController(
            TiendaDbContext context,
            IWebHostEnvironment env,
            ILogger<ReportesController> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _env = env ?? throw new ArgumentNullException(nameof(env));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

        #endregion

        #region Helpers - Normalización y Búsqueda

        /// <summary>
        /// Normaliza una ruta o URL del comprobante hacia algo servible por el navegador
        /// </summary>
        private string? NormalizarCompUrl(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            var v = raw.Trim();

            if (v.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                v.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return v;

            if (v.StartsWith("~/"))
                return Url.Content(v);

            if (v.StartsWith("/"))
                return v;

            return "/" + v.TrimStart('/');
        }

        /// <summary>
        /// Busca en el disco un archivo de comprobante y devuelve su URL relativa
        /// </summary>
        private string? BuscarComprobanteUrl(int ventaId)
        {
            try
            {
                var folder = UploadsFolderAbs();
                if (!Directory.Exists(folder))
                {
                    _logger.LogDebug("Carpeta de comprobantes no existe. VentaId: {VentaId}", ventaId);
                    return null;
                }

                var files = Directory.EnumerateFiles(folder, string.Format(PATTERN_VENTA_FILE, ventaId), SearchOption.TopDirectoryOnly)
                    .Where(f => EXTENSIONES_PERMITIDAS_COMPROBANTE.Contains(Path.GetExtension(f)))
                    .OrderByDescending(f => System.IO.File.GetLastWriteTimeUtc(f))
                    .ToList();

                if (files.Count == 0)
                {
                    _logger.LogDebug("No se encontró comprobante. VentaId: {VentaId}", ventaId);
                    return null;
                }

                var webroot = WebRootAbs();
                var rel = Path.GetRelativePath(webroot, files[0]).Replace("\\", "/");
                var url = NormalizarCompUrl("/" + rel.TrimStart('/'));

                _logger.LogInformation(
                    "Comprobante encontrado. VentaId: {VentaId}, Url: {Url}",
                    ventaId,
                    url ?? "(ninguno)");

                return url;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al buscar comprobante. VentaId: {VentaId}", ventaId);
                return null;
            }
        }

        #endregion

        #region Helpers - JSON Parsing

        /// <summary>
        /// Lee el primer string cuyo nombre coincida (case-insensitive)
        /// </summary>
        private static string? ReadStrCI(JsonElement el, params string[] names)
        {
            foreach (var p in el.EnumerateObject())
            {
                foreach (var n in names)
                {
                    if (string.Equals(p.Name, n, StringComparison.OrdinalIgnoreCase))
                    {
                        return p.Value.ValueKind == JsonValueKind.String
                            ? p.Value.GetString()
                            : p.Value.ToString();
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Intenta extraer el banco desde múltiples formas posibles
        /// </summary>
        private static string? TryExtractBanco(JsonElement root)
        {
            var bank = ReadStrCI(root, JSON_PROP_BANCO);
            if (!string.IsNullOrWhiteSpace(bank)) return bank;

            if (root.TryGetProperty(JSON_PROP_BANCO_SELECCION, out var sel))
            {
                if (sel.ValueKind == JsonValueKind.String)
                    return sel.GetString();

                if (sel.ValueKind == JsonValueKind.Object)
                {
                    bank = ReadStrCI(sel, JSON_PROP_CODIGO, JSON_PROP_NOMBRE, JSON_PROP_VALOR);
                    if (!string.IsNullOrWhiteSpace(bank)) return bank;

                    if (sel.TryGetProperty(JSON_PROP_BANCO, out var bancoObj) &&
                        bancoObj.ValueKind == JsonValueKind.Object)
                    {
                        bank = ReadStrCI(bancoObj, JSON_PROP_CODIGO, JSON_PROP_NOMBRE, JSON_PROP_VALOR);
                        if (!string.IsNullOrWhiteSpace(bank)) return bank;
                    }
                }
            }

            return null;
        }

        #endregion

        #region Helpers - Metadatos

        /// <summary>
        /// Lee metadatos de depósito (depositante y banco) desde archivos JSON o TXT
        /// </summary>
        private (string? depositante, string? banco) BuscarMetaDeposito(int ventaId)
        {
            try
            {
                var folder = UploadsFolderAbs();
                if (!Directory.Exists(folder))
                    return (null, null);

                var jsonMeta = Path.Combine(folder, string.Format(PATTERN_VENTA_META_JSON, ventaId));
                if (System.IO.File.Exists(jsonMeta))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(System.IO.File.ReadAllText(jsonMeta));
                        var root = doc.RootElement;

                        var dep = ReadStrCI(root, JSON_PROP_DEPOSITANTE);
                        var bank = TryExtractBanco(root);

                        dep = string.IsNullOrWhiteSpace(dep) ? null : dep.Trim();
                        bank = string.IsNullOrWhiteSpace(bank) ? null : bank.Trim();

                        if (dep == null && bank == null)
                        {
                            _logger.LogWarning(
                                "Meta JSON sin datos útiles. VentaId: {VentaId}, Path: {Path}",
                                ventaId,
                                jsonMeta);
                        }
                        else
                        {
                            _logger.LogDebug(
                                "Metadatos JSON cargados. VentaId: {VentaId}, Depositante: {Depositante}, Banco: {Banco}",
                                ventaId,
                                dep ?? "null",
                                bank ?? "null");
                        }

                        return (dep, bank);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Error al parsear meta JSON. VentaId: {VentaId}, Path: {Path}",
                            ventaId,
                            jsonMeta);
                    }
                }

                var txtMeta = Path.Combine(folder, string.Format(PATTERN_VENTA_META_TXT, ventaId));
                if (System.IO.File.Exists(txtMeta))
                {
                    var val = System.IO.File.ReadAllText(txtMeta).Trim();
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        _logger.LogDebug(
                            "Metadatos TXT cargados. VentaId: {VentaId}, Depositante: {Depositante}",
                            ventaId,
                            val);
                        return (val, null);
                    }
                }

                _logger.LogDebug(
                    "Sin metadatos de depósito. VentaId: {VentaId}, Folder: {Folder}",
                    ventaId,
                    folder);

                return (null, null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error general al buscar metadatos. VentaId: {VentaId}", ventaId);
                return (null, null);
            }
        }

        /// <summary>
        /// Persiste metadatos de depósito en archivo JSON
        /// </summary>
        private void GuardarMetaDeposito(int ventaId, string? depositante, string? banco)
        {
            try
            {
                var folder = UploadsFolderAbs();
                Directory.CreateDirectory(folder);

                var meta = new
                {
                    depositante = string.IsNullOrWhiteSpace(depositante) ? null : depositante.Trim(),
                    banco = string.IsNullOrWhiteSpace(banco) ? null : banco.Trim(),
                    ts = DateTime.UtcNow
                };

                var metaPath = Path.Combine(folder, string.Format(PATTERN_VENTA_META_JSON, ventaId));
                var json = JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = false });
                System.IO.File.WriteAllText(metaPath, json);

                _logger.LogInformation(
                    "Meta de depósito guardado. VentaId: {VentaId}, Path: {Path}",
                    ventaId,
                    metaPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar metadatos. VentaId: {VentaId}", ventaId);
            }
        }

        #endregion

        #region Reportes - Listado

        /// <summary>
        /// GET: /Panel/Reportes
        /// Listado de ventas recientes con métricas
        /// </summary>
        [HttpGet("Reportes")]
        public async Task<IActionResult> Reportes(CancellationToken ct = default)
        {
            try
            {
                _logger.LogInformation("Cargando reportes de ventas");

                var totalVentas = await _context.Ventas.AsNoTracking().CountAsync(ct);
                var totalIngresos = await _context.Ventas.AsNoTracking()
                    .SumAsync(v => (decimal?)v.Total, ct) ?? 0m;
                var productosVendidos = await _context.DetalleVentas.AsNoTracking()
                    .SumAsync(dv => (int?)dv.Cantidad, ct) ?? 0;

                var desde = DateTime.UtcNow.AddMonths(MESES_CLIENTES_NUEVOS);
                var clientesNuevos = await _context.Users
                    .OfType<Usuario>()
                    .AsNoTracking()
                    .CountAsync(u => u.FechaRegistro >= desde, ct);

                ViewBag.TotalVentas = totalVentas;
                ViewBag.TotalIngresos = totalIngresos;
                ViewBag.ProductosVendidos = productosVendidos;
                ViewBag.ClientesNuevos = clientesNuevos;

                _logger.LogDebug(
                    "Métricas cargadas. Ventas: {Ventas}, Ingresos: {Ingresos:C}, ProductosVendidos: {Productos}, ClientesNuevos: {Clientes}",
                    totalVentas,
                    totalIngresos,
                    productosVendidos,
                    clientesNuevos);

                var vm = await _context.Ventas
                    .AsNoTracking()
                    .OrderByDescending(v => v.FechaVenta)
                    .Take(TOP_VENTAS_RECIENTES)
                    .Select(v => new CompradorResumenVM
                    {
                        VentaID = v.VentaID,
                        UsuarioId = v.UsuarioId!,
                        Nombre = v.Usuario != null && !string.IsNullOrWhiteSpace(v.Usuario.NombreCompleto)
                            ? v.Usuario.NombreCompleto
                            : TEXTO_SIN_USUARIO,
                        Email = v.Usuario != null ? v.Usuario.Email : null,
                        Telefono = v.Usuario != null ? (v.Usuario.Telefono ?? v.Usuario.PhoneNumber) : null,
                        Fecha = v.FechaVenta,
                        Estado = v.Estado ?? string.Empty,
                        MetodoPago = v.MetodoPago ?? string.Empty,
                        Total = v.Total,
                        FotoPerfil = v.Usuario != null ? v.Usuario.FotoPerfil : null
                    })
                    .ToListAsync(ct);

                _logger.LogDebug("Ventas recientes cargadas. Count: {Count}", vm.Count);

                return View(VIEW_REPORTES, vm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar reportes");
                TempData[TEMPDATA_MENSAJE_ERROR] = MSG_ERROR_CARGAR_REPORTES;
                return View(VIEW_REPORTES, new List<CompradorResumenVM>());
            }
        }

        #endregion

        #region Reportes - Detalle

        /// <summary>
        /// GET: /Panel/VentaDetalle/{id}
        /// Detalle completo de una venta
        /// ACTUALIZADO: Lee Depositante, Banco, ComprobanteUrl desde BD primero
        /// </summary>
        [HttpGet("VentaDetalle/{id:int}", Name = "Panel_VentaDetalle")]
        public async Task<IActionResult> VentaDetalle(int id, CancellationToken ct = default)
        {
            try
            {
                _logger.LogInformation("Cargando detalle de venta. VentaId: {VentaId}", id);

                var v = await _context.Ventas
                    .AsNoTracking()
                    .Include(x => x.Usuario)
                    .Include(x => x.DetalleVentas).ThenInclude(d => d.Producto)
#if NET5_0_OR_GREATER
                    .AsSplitQuery()
#endif
                    .FirstOrDefaultAsync(x => x.VentaID == id, ct);

                if (v == null)
                {
                    _logger.LogWarning("Venta no encontrada. VentaId: {VentaId}", id);
                    return NotFound();
                }

                // =====================================================================
                // NUEVO: Leer datos de pago con prioridad BD > Archivos > Usuario
                // =====================================================================

                // 1. ComprobanteUrl: BD primero, luego buscar archivo, luego Usuario
                var compUrl = !string.IsNullOrWhiteSpace(v.ComprobanteUrl)
                    ? NormalizarCompUrl(v.ComprobanteUrl)
                    : (NormalizarCompUrl(v.Usuario?.FotoComprobanteDeposito) ?? BuscarComprobanteUrl(v.VentaID));

                // 2. Depositante y Banco: BD primero, luego archivos JSON, luego Usuario
                string? depositante = null;
                string? banco = null;

                // Intentar desde BD
                if (!string.IsNullOrWhiteSpace(v.Depositante))
                {
                    depositante = v.Depositante.Trim();
                }

                if (!string.IsNullOrWhiteSpace(v.Banco))
                {
                    banco = v.Banco.Trim();
                }

                // Si no hay datos en BD, buscar en archivos JSON (fallback para datos antiguos)
                if (string.IsNullOrWhiteSpace(depositante) || string.IsNullOrWhiteSpace(banco))
                {
                    var (depMeta, bancoMeta) = BuscarMetaDeposito(v.VentaID);

                    if (string.IsNullOrWhiteSpace(depositante) && !string.IsNullOrWhiteSpace(depMeta))
                    {
                        depositante = depMeta.Trim();

                        // OPCIONAL: Migrar a BD si encontramos datos en archivo
                        // Descomenta si quieres migrar automáticamente
                        /*
                        try
                        {
                            var ventaToUpdate = await _context.Ventas.FindAsync(new object[] { id }, ct);
                            if (ventaToUpdate != null && string.IsNullOrWhiteSpace(ventaToUpdate.Depositante))
                            {
                                ventaToUpdate.Depositante = depositante;
                                await _context.SaveChangesAsync(ct);
                                _logger.LogInformation("Depositante migrado a BD. VentaId: {VentaId}", id);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error al migrar depositante a BD. VentaId: {VentaId}", id);
                        }
                        */
                    }

                    if (string.IsNullOrWhiteSpace(banco) && !string.IsNullOrWhiteSpace(bancoMeta))
                    {
                        banco = bancoMeta.Trim();

                        // OPCIONAL: Migrar a BD
                        /*
                        try
                        {
                            var ventaToUpdate = await _context.Ventas.FindAsync(new object[] { id }, ct);
                            if (ventaToUpdate != null && string.IsNullOrWhiteSpace(ventaToUpdate.Banco))
                            {
                                ventaToUpdate.Banco = banco;
                                await _context.SaveChangesAsync(ct);
                                _logger.LogInformation("Banco migrado a BD. VentaId: {VentaId}", id);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error al migrar banco a BD. VentaId: {VentaId}", id);
                        }
                        */
                    }
                }

                // Último fallback: NombreDepositante del Usuario (solo para depositante)
                if (string.IsNullOrWhiteSpace(depositante) && !string.IsNullOrWhiteSpace(v.Usuario?.NombreDepositante))
                {
                    depositante = v.Usuario.NombreDepositante.Trim();
                }

                _logger.LogDebug(
                    "Datos de pago resueltos. VentaId: {VentaId}, Depositante: {Depositante}, " +
                    "Banco: {Banco}, ComprobanteUrl: {Url}",
                    id,
                    depositante ?? "(vacío)",
                    banco ?? "(vacío)",
                    compUrl ?? "(sin comprobante)");

                // =====================================================================

                var direcciones = new List<DireccionVM>();
                if (v.Usuario != null && !string.IsNullOrWhiteSpace(v.Usuario.Direccion))
                {
                    direcciones.Add(new DireccionVM
                    {
                        Calle = v.Usuario.Direccion,
                        Ciudad = v.Usuario.Ciudad,
                        EstadoProvincia = v.Usuario.Provincia,
                        CodigoPostal = v.Usuario.CodigoPostal,
                        TelefonoContacto = v.Usuario.Telefono ?? v.Usuario.PhoneNumber,
                        FechaRegistro = v.FechaVenta
                    });
                }

                var vm = new VentaDetalleVM
                {
                    // NUEVO: Usar datos resueltos con prioridad correcta
                    Banco = banco,
                    Depositante = depositante,
                    ComprobanteUrl = compUrl,

                    VentaID = v.VentaID,
                    Fecha = v.FechaVenta,
                    Estado = v.Estado ?? string.Empty,
                    MetodoPago = v.MetodoPago ?? string.Empty,
                    Total = v.Total,
                    UsuarioId = v.UsuarioId ?? v.Usuario?.Id ?? string.Empty,
                    Nombre = v.Usuario?.NombreCompleto ?? TEXTO_SIN_USUARIO,
                    Email = v.Usuario?.Email,
                    Telefono = v.Usuario?.Telefono ?? v.Usuario?.PhoneNumber,
                    Cedula = v.Usuario?.Cedula,
                    Direccion = v.Usuario?.Direccion,
                    FotoPerfil = v.Usuario?.FotoPerfil,
                    PerfilCiudad = v.Usuario?.Ciudad,
                    PerfilProvincia = v.Usuario?.Provincia,
                    PerfilReferencia = v.Usuario?.Referencia,
                    Direcciones = direcciones,
                    Detalles = (v.DetalleVentas ?? new List<DetalleVentas>())
                        .OrderBy(d => d.DetalleVentaID)
                        .Select(d => new DetalleVentaVM
                        {
                            Producto = d.Producto?.Nombre
                                ?? (d.ProductoID != 0
                                    ? string.Format(TEXTO_PRODUCTO_ID, d.ProductoID)
                                    : TEXTO_PRODUCTO_GENERICO),
                            Cantidad = d.Cantidad,
                            Subtotal = d.Subtotal ?? (d.PrecioUnitario * d.Cantidad)
                        })
                        .ToList()
                };

                _logger.LogDebug(
                    "Detalle venta cargado. VentaId: {VentaId}, Productos: {Count}, Total: {Total:C}",
                    id,
                    vm.Detalles.Count,
                    vm.Total);

                return View(VIEW_VENTA_DETALLE, vm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar detalle de venta. VentaId: {VentaId}", id);
                TempData[TEMPDATA_MENSAJE_ERROR] = MSG_ERROR_CARGAR_DETALLE;
                return RedirectToAction(nameof(Reportes));
            }
        }


        #endregion

        #region Acciones Rápidas

        /// <summary>
        /// POST: /Panel/MarcarEnviada
        /// Marca una venta como enviada
        /// </summary>
        [HttpPost("MarcarEnviada")]
        public async Task<IActionResult> MarcarEnviada(int id, CancellationToken ct = default)
        {
            try
            {
                _logger.LogInformation("Marcando venta como enviada. VentaId: {VentaId}", id);

                var venta = await _context.Ventas.FirstOrDefaultAsync(v => v.VentaID == id, ct);
                if (venta == null)
                {
                    _logger.LogWarning("Venta no encontrada al marcar enviada. VentaId: {VentaId}", id);
                    return AjaxOrRedirectError(MSG_ERROR_VENTA_NO_ENCONTRADA, nameof(Reportes));
                }

                venta.Estado = ESTADO_ENVIADO;
                await _context.SaveChangesAsync(ct);

                _logger.LogInformation(
                    "Venta marcada como enviada. VentaId: {VentaId}, Estado: {Estado}",
                    id,
                    venta.Estado);

                if (Request.Headers[HEADER_AJAX_REQUEST] == HEADER_AJAX_VALUE)
                    return Json(new { ok = true, estado = venta.Estado });

                TempData[TEMPDATA_MENSAJE_EXITO] = MSG_EXITO_VENTA_ENVIADA;
                return RedirectToAction(nameof(Reportes));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al marcar venta como enviada. VentaId: {VentaId}", id);
                return AjaxOrRedirectError(MSG_ERROR_CARGAR_REPORTES, nameof(Reportes));
            }
        }

        #endregion

        #region Reversión de Ventas

        /// <summary>
        /// POST: /Panel/ReportarVenta
        /// Reversa una venta: repone stocks, registra devoluciones y marca como cancelada
        /// </summary>
        /// <param name="ventaId">ID de la venta a revertir</param>
        /// <param name="motivo">Motivo: devolucion, deposito_falso, otro</param>
        /// <param name="nota">Nota opcional adicional</param>
        /// <param name="ct">Token de cancelación</param>
        [HttpPost("ReportarVenta")]
        public async Task<IActionResult> ReportarVenta(
            int ventaId,
            string motivo,
            string? nota,
            CancellationToken ct = default)
        {
            try
            {
                if (ventaId <= 0 || string.IsNullOrWhiteSpace(motivo))
                {
                    _logger.LogWarning(
                        "Intento de revertir venta con datos inválidos. VentaId: {VentaId}, Motivo: {Motivo}",
                        ventaId,
                        motivo ?? "null");
                    return AjaxOrRedirectError(MSG_ERROR_DATOS_INVALIDOS, nameof(Reportes));
                }

                _logger.LogInformation(
                    "Iniciando reversión de venta. VentaId: {VentaId}, Motivo: {Motivo}",
                    ventaId,
                    motivo);

                var venta = await _context.Ventas
                    .Include(v => v.DetalleVentas).ThenInclude(d => d.Producto)
                    .FirstOrDefaultAsync(v => v.VentaID == ventaId, ct);

                if (venta == null)
                {
                    _logger.LogWarning("Venta no encontrada al revertir. VentaId: {VentaId}", ventaId);
                    return AjaxOrRedirectError(MSG_ERROR_VENTA_NO_ENCONTRADA, nameof(Reportes));
                }

                var estado = (venta.Estado ?? string.Empty).ToLowerInvariant();
                if (estado.Contains(ESTADO_CANCELADA_LOWERCASE))
                {
                    _logger.LogWarning(
                        "Intento de revertir venta ya cancelada. VentaId: {VentaId}, Estado: {Estado}",
                        ventaId,
                        venta.Estado);
                    return AjaxOrRedirectError(MSG_ERROR_VENTA_YA_CANCELADA, nameof(Reportes));
                }

                await using var tx = await _context.Database.BeginTransactionAsync(ct);

                var detalles = venta.DetalleVentas ?? new List<DetalleVentas>();
                var productosActualizados = 0;

                foreach (var d in detalles)
                {
                    if (d.Producto != null)
                    {
                        d.Producto.Stock += d.Cantidad;
                        productosActualizados++;
                    }
                }

                _logger.LogDebug(
                    "Stock repuesto. VentaId: {VentaId}, ProductosActualizados: {Count}",
                    ventaId,
                    productosActualizados);

                var motivoCanon = motivo.Trim().ToLowerInvariant();
                var label = motivoCanon switch
                {
                    MOTIVO_DEVOLUCION => MOTIVO_DEVOLUCION,
                    MOTIVO_DEPOSITO_FALSO => MOTIVO_DEPOSITO_FALSO,
                    _ => MOTIVO_OTRO
                };

                var textoMotivo = string.IsNullOrWhiteSpace(nota)
                    ? label
                    : $"{label}: {nota.Trim()}";

                foreach (var d in detalles)
                {
                    _context.Devoluciones.Add(new Devoluciones
                    {
                        DetalleVentaID = d.DetalleVentaID,
                        FechaDevolucion = DateTime.UtcNow,
                        Motivo = textoMotivo,
                        CantidadDevuelta = d.Cantidad,
                        Aprobada = true
                    });
                }

                _logger.LogDebug(
                    "Devoluciones registradas. VentaId: {VentaId}, Count: {Count}",
                    ventaId,
                    detalles.Count);

                venta.Estado = ESTADO_CANCELADA;

                await _context.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                _logger.LogInformation(
                    "Venta revertida exitosamente. VentaId: {VentaId}, Motivo: {Motivo}, Productos: {Count}",
                    ventaId,
                    motivo,
                    detalles.Count);

                if (Request.Headers[HEADER_AJAX_REQUEST] == HEADER_AJAX_VALUE)
                    return Json(new { ok = true });

                TempData[TEMPDATA_MENSAJE_EXITO] = MSG_EXITO_VENTA_REVERTIDA;
                return RedirectToAction(nameof(Reportes));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al revertir venta. VentaId: {VentaId}", ventaId);

                if (Request.Headers[HEADER_AJAX_REQUEST] == HEADER_AJAX_VALUE)
                    return Json(new { ok = false, error = MSG_ERROR_NO_REVERTIR_VENTA });

                TempData[TEMPDATA_MENSAJE_ERROR] = MSG_ERROR_NO_REVERTIR_VENTA;
                return RedirectToAction(nameof(Reportes));
            }
        }

        #endregion

        #region Helpers - Respuestas

        /// <summary>
        /// Retorna JSON para AJAX o redirección para requests normales
        /// </summary>
        private IActionResult AjaxOrRedirectError(string message, string redirectAction)
        {
            if (Request.Headers[HEADER_AJAX_REQUEST] == HEADER_AJAX_VALUE)
                return Json(new { ok = false, error = message });

            TempData[TEMPDATA_MENSAJE_ERROR] = message;
            return RedirectToAction(redirectAction);
        }

        #endregion
    }
}