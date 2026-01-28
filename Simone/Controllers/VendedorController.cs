using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Simone.Data;
using Simone.Models;
using Simone.Services;
using Simone.ViewModels.Reportes;
using Cfg = Simone.Configuration;

namespace Simone.Controllers
{
    /// <summary>
    /// Controlador del panel de vendedor
    /// Gestiona bancos, envíos y reportes del vendedor
    /// Versión optimizada con mejores prácticas empresariales
    /// </summary>
    [Authorize(Roles = "Administrador,Vendedor")]
    [Route("Vendedor")]
    public class VendedorController : Controller
    {
        #region Dependencias

        private readonly UserManager<Usuario> _userManager;
        private readonly ILogger<VendedorController> _logger;
        private readonly IBancosConfigService _bancos;
        private readonly TiendaDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IEnviosConfigService _envios;

        #endregion

        #region Constantes

        // Roles
        private const string ROL_ADMINISTRADOR = "Administrador";
        private const string ROL_VENDEDOR = "Vendedor";

        // Rutas
        private const string FOLDER_WWWROOT = "wwwroot";
        private const string FOLDER_UPLOADS = "uploads";
        private const string FOLDER_COMPROBANTES = "comprobantes";
        private const string PATTERN_VENTA_FILE = "venta-{0}.*";
        private const string PATTERN_VENTA_META_JSON = "venta-{0}.meta.json";
        private const string PATTERN_VENTA_META_TXT = "venta-{0}.txt";

        // JSON Properties
        private const string JSON_PROP_DEPOSITANTE = "depositante";
        private const string JSON_PROP_BANCO = "banco";
        private const string JSON_PROP_BANCO_SELECCION = "bancoSeleccion";

        // Extensiones permitidas
        private static readonly HashSet<string> EXTENSIONES_PERMITIDAS_COMPROBANTE = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".pdf"
        };

        // Mensajes - General
        private const string MSG_ERROR_USUARIO_NO_DETERMINADO = "No se pudo determinar el usuario actual.";
        private const string MSG_ERROR_CARGAR_DATOS = "No se pudieron cargar los datos.";
        private const string MSG_ERROR_CODIGO_INVALIDO_PARAM = "Código inválido.";
        private const string MSG_ERROR_PROVINCIA_INVALIDA_PARAM = "Provincia inválida.";

        // Mensajes - Bancos
        private const string MSG_ERROR_CODIGO_INVALIDO = "Código inválido. Usa minúsculas, números, '-' o '_' (2-50).";
        private const string MSG_ERROR_NOMBRE_BANCO_INVALIDO = "Nombre de banco inválido (máximo 120).";
        private const string MSG_ERROR_NUMERO_CUENTA_INVALIDO = "Número de cuenta inválido (solo dígitos, 6-20).";
        private const string MSG_ERROR_TIPO_CUENTA_INVALIDO = "Tipo de cuenta inválido.";
        private const string MSG_ERROR_TITULAR_INVALIDO = "Titular inválido (máximo 120).";
        private const string MSG_ERROR_RUC_INVALIDO = "RUC/Cédula inválido (10 o 13 dígitos).";
        private const string MSG_ERROR_LOGO_INVALIDO = "Ruta de logo inválida (debe ser relativa).";
        private const string MSG_ERROR_CUENTA_DUPLICADA = "Ya existe una cuenta con ese código.";
        private const string MSG_ERROR_CUENTA_NO_ENCONTRADA = "No se encontró la cuenta especificada.";
        private const string MSG_ERROR_CUENTA_ORIGINAL_NO_ENCONTRADA = "No se encontró la cuenta original para editar.";
        private const string MSG_ERROR_CODIGO_DUPLICADO = "El nuevo código ya pertenece a otra cuenta.";
        private const string MSG_EXITO_CUENTA_GUARDADA = "Cuenta guardada correctamente.";
        private const string MSG_EXITO_CUENTA_ELIMINADA = "Cuenta eliminada correctamente.";
        private const string MSG_EXITO_ESTADO_ACTUALIZADO = "Estado actualizado.";
        private const string MSG_ERROR_GUARDAR_CUENTA = "Ocurrió un error al guardar la cuenta bancaria.";
        private const string MSG_ERROR_ELIMINAR_CUENTA = "Ocurrió un error al eliminar la cuenta bancaria.";
        private const string MSG_ERROR_TOGGLE_CUENTA = "No se pudo actualizar el estado.";

        // Mensajes - Envíos
        private const string MSG_ERROR_PROVINCIA_OBLIGATORIA = "La provincia es obligatoria.";
        private const string MSG_ERROR_PROVINCIA_INVALIDA = "Provincia inválida (máx. 120).";
        private const string MSG_ERROR_CIUDAD_INVALIDA = "Ciudad inválida (máx. 120).";
        private const string MSG_ERROR_PRECIO_RANGO = "El precio debe estar entre 0 y 9999.99.";
        private const string MSG_ERROR_PRECIO_INVALIDO = "Precio inválido. Usa formato 0.00 (ej.: 4,40 o 4.40).";
        private const string MSG_ERROR_PRECIO_FUERA_RANGO = "El precio debe estar entre 0 y 9.999,99.";
        private const string MSG_ERROR_NOTA_INVALIDA = "La nota supera el límite (máx. 120).";
        private const string MSG_ERROR_TARIFA_DUPLICADA = "Ya existe una regla para ese destino.";
        private const string MSG_ERROR_TARIFA_NO_ENCONTRADA = "No se encontró la tarifa especificada.";
        private const string MSG_ERROR_TARIFA_ORIGINAL_NO_ENCONTRADA = "No se encontró la regla original para editar.";
        private const string MSG_ERROR_DESTINO_DUPLICADO = "Ya existe otra regla con ese destino.";
        private const string MSG_EXITO_TARIFA_GUARDADA = "Tarifa guardada correctamente.";
        private const string MSG_EXITO_TARIFA_ELIMINADA = "Tarifa eliminada correctamente.";
        private const string MSG_ERROR_GUARDAR_TARIFA = "Ocurrió un error al guardar la tarifa.";
        private const string MSG_ERROR_ELIMINAR_TARIFA = "Ocurrió un error al eliminar la tarifa.";
        private const string MSG_ERROR_TOGGLE_TARIFA = "No se pudo actualizar el estado.";

        // Mensajes - Reportes
        private const string MSG_ERROR_VENTA_NO_ENCONTRADA = "Venta no encontrada.";
        private const string MSG_ERROR_SIN_PRODUCTOS_VENDEDOR = "La venta no tiene productos de este vendedor.";

        // Límites de validación
        private const int MIN_CODIGO_LENGTH = 2;
        private const int MAX_CODIGO_LENGTH = 50;
        private const int MIN_CUENTA_LENGTH = 6;
        private const int MAX_CUENTA_LENGTH = 20;
        private const int MAX_TEXTO_40 = 40;
        private const int MAX_TEXTO_120 = 120;
        private const int MAX_TEXTO_200 = 200;
        private const int RUC_LENGTH_10 = 10;
        private const int RUC_LENGTH_13 = 13;
        private const decimal PRECIO_MIN = 0m;
        private const decimal PRECIO_MAX = 9999.99m;

        // Regex compilados
        private static readonly Regex CodigoRegex = new(@"^[a-z0-9_-]{2,50}$", RegexOptions.Compiled);
        private static readonly Regex NumeroCuentaRegex = new(@"^[0-9]{6,20}$", RegexOptions.Compiled);
        private static readonly Regex Texto40Regex = new(@"^.{1,40}$", RegexOptions.Compiled);
        private static readonly Regex Texto120Regex = new(@"^.{1,120}$", RegexOptions.Compiled);
        private static readonly Regex RucRegex = new(@"^\d{10}(\d{3})?$", RegexOptions.Compiled);
        private static readonly Regex LogoPathRegex = new(@"^[A-Za-z0-9_\-/\.]{1,200}$", RegexOptions.Compiled);

        // View names
        private const string VIEW_REPORTES = "~/Views/Reportes/Reportes.cshtml";
        private const string VIEW_VENTA_DETALLE = "~/Views/Reportes/VentaDetalle.cshtml";

        // Otros
        private const string TEXTO_SIN_USUARIO = "(sin usuario)";
        private const string TITLE_MIS_VENTAS = "Mis ventas";
        private const string VIEWBAG_MENSAJE_EXITO = "MensajeExito";
        private const string VIEWBAG_MENSAJE_ERROR = "MensajeError";
        private const string VIEWBAG_MODEL_ERRORS = "ModelErrors";

        #endregion

        #region Constructor

        /// <summary>
        /// Constructor con inyección de dependencias
        /// </summary>
        public VendedorController(
            UserManager<Usuario> userManager,
            ILogger<VendedorController> logger,
            IBancosConfigService bancos,
            TiendaDbContext context,
            IWebHostEnvironment env,
            IEnviosConfigService envios)
        {
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _bancos = bancos ?? throw new ArgumentNullException(nameof(bancos));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _env = env ?? throw new ArgumentNullException(nameof(env));
            _envios = envios ?? throw new ArgumentNullException(nameof(envios));
        }

        #endregion

        #region Helpers - General

        /// <summary>
        /// Obtiene el ID del vendedor actual (con soporte para Admin operando sobre otro vendedor)
        /// </summary>
        private string CurrentVendorId(string? vIdFromQuery = null)
        {
            var myId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(myId))
            {
                _logger.LogError("No se pudo determinar el usuario actual");
                throw new InvalidOperationException(MSG_ERROR_USUARIO_NO_DETERMINADO);
            }

            // Solo Admin puede operar sobre otro vendedor
            if (User.IsInRole(ROL_ADMINISTRADOR) && !string.IsNullOrWhiteSpace(vIdFromQuery))
            {
                _logger.LogDebug(
                    "Admin operando sobre vendedor. AdminId: {AdminId}, VendorId: {VendorId}",
                    myId,
                    vIdFromQuery);
                return vIdFromQuery;
            }

            return myId;
        }

        private static string K(string? s) => (s ?? string.Empty).Trim().ToLowerInvariant();
        private static string T(string? s) => (s ?? string.Empty).Trim();
        private static string? TN(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        #endregion

        #region Helpers - Archivos y Comprobantes

        /// <summary>
        /// Obtiene la ruta absoluta de la carpeta de comprobantes
        /// </summary>
        private string UploadsFolderAbs() =>
            Path.Combine(
                _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), FOLDER_WWWROOT),
                FOLDER_UPLOADS,
                FOLDER_COMPROBANTES);

        /// <summary>
        /// Normaliza URL de comprobante
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
        /// Busca el archivo de comprobante de una venta
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

                var webroot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), FOLDER_WWWROOT);
                var rel = Path.GetRelativePath(webroot, files[0]).Replace("\\", "/");
                var url = NormalizarCompUrl("/" + rel.TrimStart('/'));

                _logger.LogDebug(
                    "Comprobante encontrado. VentaId: {VentaId}, Url: {Url}",
                    ventaId,
                    url);

                return url;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al buscar comprobante. VentaId: {VentaId}", ventaId);
                return null;
            }
        }

        /// <summary>
        /// Busca metadatos de depósito (depositante y banco)
        /// </summary>
        private (string? depositante, string? banco) BuscarMetaDeposito(int ventaId)
        {
            try
            {
                var folder = UploadsFolderAbs();
                if (!Directory.Exists(folder))
                    return (null, null);

                // Primero intenta JSON
                var metaJson = Path.Combine(folder, string.Format(PATTERN_VENTA_META_JSON, ventaId));
                if (System.IO.File.Exists(metaJson))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(System.IO.File.ReadAllText(metaJson));
                        var root = doc.RootElement;

                        string? dep = null;
                        string? bank = null;

                        if (root.TryGetProperty(JSON_PROP_DEPOSITANTE, out var pDep))
                            dep = pDep.ValueKind == JsonValueKind.String ? pDep.GetString() : pDep.ToString();

                        if (root.TryGetProperty(JSON_PROP_BANCO, out var pBanco))
                            bank = pBanco.ValueKind == JsonValueKind.String ? pBanco.GetString() : pBanco.ToString();

                        _logger.LogDebug(
                            "Metadatos JSON encontrados. VentaId: {VentaId}, Depositante: {Depositante}, Banco: {Banco}",
                            ventaId,
                            dep ?? "null",
                            bank ?? "null");

                        return (dep, bank);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error al leer meta JSON. VentaId: {VentaId}", ventaId);
                    }
                }

                // Fallback: archivo .txt
                var metaTxt = Path.Combine(folder, string.Format(PATTERN_VENTA_META_TXT, ventaId));
                if (System.IO.File.Exists(metaTxt))
                {
                    try
                    {
                        var dep = System.IO.File.ReadAllText(metaTxt)?.Trim();

                        _logger.LogDebug(
                            "Metadatos TXT encontrados. VentaId: {VentaId}, Depositante: {Depositante}",
                            ventaId,
                            dep ?? "null");

                        return (string.IsNullOrWhiteSpace(dep) ? null : dep, null);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error al leer meta TXT. VentaId: {VentaId}", ventaId);
                    }
                }

                return (null, null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error general al buscar metadatos. VentaId: {VentaId}", ventaId);
                return (null, null);
            }
        }

        #endregion

        #region Index y Navegación

        /// <summary>
        /// GET: /Vendedor
        /// Redirige al panel de reportes
        /// </summary>
        [HttpGet("")]
        public IActionResult Index()
        {
            try
            {
                _logger.LogInformation(
                    "Acceso a panel vendedor. Usuario: {User}",
                    User?.Identity?.Name);

                return RedirectToAction(nameof(Reportes));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en Index vendedor");
                return RedirectToAction(nameof(Reportes));
            }
        }

        /// <summary>
        /// GET: /Vendedor/Productos
        /// Vista de productos del vendedor
        /// </summary>
        [HttpGet("Productos")]
        public IActionResult Productos() => View();

        /// <summary>
        /// GET: /Vendedor/AnadirProducto
        /// Formulario para añadir producto
        /// </summary>
        [HttpGet("AnadirProducto")]
        public IActionResult AnadirProducto() => View();

        #endregion

        #region Bancos - ViewModels

        /// <summary>
        /// ViewModel para crear/editar cuenta bancaria
        /// </summary>
        public sealed class UpsertVm
        {
            public string? OriginalCodigo { get; set; }
            public string Codigo { get; set; } = string.Empty;
            public string Nombre { get; set; } = string.Empty;
            public string Numero { get; set; } = string.Empty;
            public string Tipo { get; set; } = "Cuenta de Ahorros";
            public string? Titular { get; set; }
            public string? Ruc { get; set; }
            public string? LogoPath { get; set; }
            public bool Activo { get; set; } = true;
        }

        #endregion

        #region Bancos - Validación

        /// <summary>
        /// Valida los datos de una cuenta bancaria
        /// </summary>
        private static (bool ok, string? err) Validate(UpsertVm vm)
        {
            if (string.IsNullOrWhiteSpace(vm.Codigo) || !CodigoRegex.IsMatch(vm.Codigo))
                return (false, MSG_ERROR_CODIGO_INVALIDO);

            if (string.IsNullOrWhiteSpace(vm.Nombre) || !Texto120Regex.IsMatch(vm.Nombre))
                return (false, MSG_ERROR_NOMBRE_BANCO_INVALIDO);

            if (string.IsNullOrWhiteSpace(vm.Numero) || !NumeroCuentaRegex.IsMatch(vm.Numero))
                return (false, MSG_ERROR_NUMERO_CUENTA_INVALIDO);

            if (string.IsNullOrWhiteSpace(vm.Tipo) || !Texto40Regex.IsMatch(vm.Tipo))
                return (false, MSG_ERROR_TIPO_CUENTA_INVALIDO);

            if (!string.IsNullOrWhiteSpace(vm.Titular) && !Texto120Regex.IsMatch(vm.Titular))
                return (false, MSG_ERROR_TITULAR_INVALIDO);

            if (!string.IsNullOrWhiteSpace(vm.Ruc) && !RucRegex.IsMatch(vm.Ruc))
                return (false, MSG_ERROR_RUC_INVALIDO);

            if (!string.IsNullOrWhiteSpace(vm.LogoPath) &&
                (!LogoPathRegex.IsMatch(vm.LogoPath) || vm.LogoPath.Contains("..") || vm.LogoPath.Contains("://")))
                return (false, MSG_ERROR_LOGO_INVALIDO);

            return (true, null);
        }

        #endregion

        #region Bancos - Acciones

        /// <summary>
        /// GET: /Vendedor/Bancos?vId=&lt;opcional para Admin&gt;
        /// Listado de cuentas bancarias del vendedor
        /// </summary>
        [HttpGet("Bancos")]
        public async Task<IActionResult> Bancos(
            [FromQuery] string? vId = null,
            CancellationToken ct = default)
        {
            try
            {
                var vendorId = CurrentVendorId(vId);

                _logger.LogInformation(
                    "Cargando cuentas bancarias. VendorId: {VendorId}",
                    vendorId);

                var cuentas = (await _bancos.GetByProveedorAsync(vendorId))?.ToList()
                              ?? new List<Cfg.CuentaBancaria>();

                _logger.LogDebug(
                    "Cuentas bancarias cargadas. VendorId: {VendorId}, Count: {Count}",
                    vendorId,
                    cuentas.Count);

                ViewBag.Cuentas = cuentas.OrderBy(x => x.Nombre).ThenBy(x => x.Codigo).ToList();
                ViewBag.TargetVendorId = vendorId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cargando cuentas bancarias del vendedor");
                ViewBag.Cuentas = new List<Cfg.CuentaBancaria>();
                TempData[VIEWBAG_MENSAJE_ERROR] = MSG_ERROR_CARGAR_DATOS;
            }

            ViewBag.MensajeExito = TempData[VIEWBAG_MENSAJE_EXITO];
            ViewBag.MensajeError = TempData[VIEWBAG_MENSAJE_ERROR];
            ViewBag.ModelErrors = TempData[VIEWBAG_MODEL_ERRORS];

            return View();
        }

        /// <summary>
        /// POST: /Vendedor/Bancos/Save?vId=&lt;opcional para Admin&gt;
        /// Guarda o actualiza una cuenta bancaria
        /// </summary>
        [HttpPost("Bancos/Save")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(
            [FromForm] UpsertVm vm,
            [FromQuery] string? vId = null,
            CancellationToken ct = default)
        {
            var vendorId = CurrentVendorId(vId);

            _logger.LogInformation(
                "Guardando cuenta bancaria. VendorId: {VendorId}, Codigo: {Codigo}, EsNuevo: {EsNuevo}",
                vendorId,
                vm.Codigo,
                string.IsNullOrEmpty(vm.OriginalCodigo));

            vm.OriginalCodigo = TN(vm.OriginalCodigo);
            vm.Codigo = T(vm.Codigo).ToLowerInvariant();
            vm.Nombre = T(vm.Nombre);
            vm.Numero = T(vm.Numero);
            vm.Tipo = T(vm.Tipo);
            vm.Titular = TN(vm.Titular);
            vm.Ruc = TN(vm.Ruc);
            vm.LogoPath = TN(vm.LogoPath);

            var (ok, err) = Validate(vm);
            if (!ok)
            {
                _logger.LogWarning(
                    "Validación fallida. VendorId: {VendorId}, Error: {Error}",
                    vendorId,
                    err);

                TempData[VIEWBAG_MENSAJE_ERROR] = err;
                TempData[VIEWBAG_MODEL_ERRORS] = err;
                return RedirectToAction(nameof(Bancos), new { vId = (User.IsInRole(ROL_ADMINISTRADOR) ? vendorId : null) });
            }

            try
            {
                var cuentas = (await _bancos.GetByProveedorAsync(vendorId))?.ToList()
                              ?? new List<Cfg.CuentaBancaria>();

                bool IsDup(string code, Cfg.CuentaBancaria? exclude = null) =>
                    cuentas.Any(c => !ReferenceEquals(c, exclude) && K(c.Codigo) == K(code));

                if (string.IsNullOrEmpty(vm.OriginalCodigo))
                {
                    // CREAR
                    if (IsDup(vm.Codigo))
                    {
                        _logger.LogWarning(
                            "Intento de crear cuenta duplicada. VendorId: {VendorId}, Codigo: {Codigo}",
                            vendorId,
                            vm.Codigo);

                        TempData[VIEWBAG_MENSAJE_ERROR] = MSG_ERROR_CUENTA_DUPLICADA;
                        return RedirectToAction(nameof(Bancos), new { vId = (User.IsInRole(ROL_ADMINISTRADOR) ? vendorId : null) });
                    }

                    cuentas.Add(new Cfg.CuentaBancaria
                    {
                        Codigo = vm.Codigo,
                        Nombre = vm.Nombre,
                        Numero = vm.Numero,
                        Tipo = vm.Tipo,
                        Titular = vm.Titular,
                        Ruc = vm.Ruc,
                        LogoPath = vm.LogoPath,
                        Activo = vm.Activo
                    });

                    _logger.LogInformation(
                        "Cuenta creada. VendorId: {VendorId}, Codigo: {Codigo}, Nombre: {Nombre}",
                        vendorId,
                        vm.Codigo,
                        vm.Nombre);
                }
                else
                {
                    // EDITAR
                    var actual = cuentas.FirstOrDefault(c => K(c.Codigo) == K(vm.OriginalCodigo));
                    if (actual == null)
                    {
                        _logger.LogWarning(
                            "Cuenta original no encontrada para editar. VendorId: {VendorId}, Codigo: {Codigo}",
                            vendorId,
                            vm.OriginalCodigo);

                        TempData[VIEWBAG_MENSAJE_ERROR] = MSG_ERROR_CUENTA_ORIGINAL_NO_ENCONTRADA;
                        return RedirectToAction(nameof(Bancos), new { vId = (User.IsInRole(ROL_ADMINISTRADOR) ? vendorId : null) });
                    }

                    if (K(vm.Codigo) != K(vm.OriginalCodigo) && IsDup(vm.Codigo, actual))
                    {
                        _logger.LogWarning(
                            "Nuevo código duplicado. VendorId: {VendorId}, NuevoCodigo: {NuevoCodigo}",
                            vendorId,
                            vm.Codigo);

                        TempData[VIEWBAG_MENSAJE_ERROR] = MSG_ERROR_CODIGO_DUPLICADO;
                        return RedirectToAction(nameof(Bancos), new { vId = (User.IsInRole(ROL_ADMINISTRADOR) ? vendorId : null) });
                    }

                    actual.Codigo = vm.Codigo;
                    actual.Nombre = vm.Nombre;
                    actual.Numero = vm.Numero;
                    actual.Tipo = vm.Tipo;
                    actual.Titular = vm.Titular;
                    actual.Ruc = vm.Ruc;
                    actual.LogoPath = vm.LogoPath;
                    actual.Activo = vm.Activo;

                    _logger.LogInformation(
                        "Cuenta actualizada. VendorId: {VendorId}, Codigo: {Codigo}",
                        vendorId,
                        vm.Codigo);
                }

                await _bancos.SetByProveedorAsync(vendorId, cuentas);
                TempData[VIEWBAG_MENSAJE_EXITO] = MSG_EXITO_CUENTA_GUARDADA;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error al guardar cuenta bancaria. VendorId: {VendorId}, Codigo: {Codigo}",
                    vendorId,
                    vm.Codigo);

                TempData[VIEWBAG_MENSAJE_ERROR] = MSG_ERROR_GUARDAR_CUENTA;
            }

            return RedirectToAction(nameof(Bancos), new { vId = (User.IsInRole(ROL_ADMINISTRADOR) ? vendorId : null) });
        }

        /// <summary>
        /// POST: /Vendedor/Bancos/Delete?vId=&lt;opcional para Admin&gt;
        /// Elimina una cuenta bancaria
        /// </summary>
        [HttpPost("Bancos/Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(
            [FromForm] string codigo,
            [FromQuery] string? vId = null,
            CancellationToken ct = default)
        {
            var vendorId = CurrentVendorId(vId);

            if (string.IsNullOrWhiteSpace(codigo))
            {
                _logger.LogWarning(
                    "Intento de eliminar cuenta con código inválido. VendorId: {VendorId}",
                    vendorId);

                TempData[VIEWBAG_MENSAJE_ERROR] = MSG_ERROR_CODIGO_INVALIDO_PARAM;
                return RedirectToAction(nameof(Bancos), new { vId = (User.IsInRole(ROL_ADMINISTRADOR) ? vendorId : null) });
            }

            _logger.LogInformation(
                "Eliminando cuenta. VendorId: {VendorId}, Codigo: {Codigo}",
                vendorId,
                codigo);

            try
            {
                var cuentas = (await _bancos.GetByProveedorAsync(vendorId))?.ToList()
                              ?? new List<Cfg.CuentaBancaria>();

                var item = cuentas.FirstOrDefault(c => K(c.Codigo) == K(codigo));
                if (item != null)
                {
                    cuentas.Remove(item);
                    await _bancos.SetByProveedorAsync(vendorId, cuentas);

                    _logger.LogInformation(
                        "Cuenta eliminada. VendorId: {VendorId}, Codigo: {Codigo}",
                        vendorId,
                        codigo);

                    TempData[VIEWBAG_MENSAJE_EXITO] = MSG_EXITO_CUENTA_ELIMINADA;
                }
                else
                {
                    _logger.LogWarning(
                        "Intento de eliminar cuenta inexistente. VendorId: {VendorId}, Codigo: {Codigo}",
                        vendorId,
                        codigo);

                    TempData[VIEWBAG_MENSAJE_ERROR] = MSG_ERROR_CUENTA_NO_ENCONTRADA;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error al eliminar cuenta bancaria. VendorId: {VendorId}, Codigo: {Codigo}",
                    vendorId,
                    codigo);

                TempData[VIEWBAG_MENSAJE_ERROR] = MSG_ERROR_ELIMINAR_CUENTA;
            }

            return RedirectToAction(nameof(Bancos), new { vId = (User.IsInRole(ROL_ADMINISTRADOR) ? vendorId : null) });
        }

        /// <summary>
        /// POST: /Vendedor/Bancos/Toggle?vId=&lt;opcional para Admin&gt;
        /// Alterna el estado activo/inactivo de una cuenta
        /// </summary>
        [HttpPost("Bancos/Toggle")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Toggle(
            [FromForm] string codigo,
            [FromQuery] string? vId = null,
            CancellationToken ct = default)
        {
            var vendorId = CurrentVendorId(vId);

            _logger.LogInformation(
                "Alternando estado cuenta. VendorId: {VendorId}, Codigo: {Codigo}",
                vendorId,
                codigo);

            try
            {
                var cuentas = (await _bancos.GetByProveedorAsync(vendorId))?.ToList()
                              ?? new List<Cfg.CuentaBancaria>();

                var cta = cuentas.FirstOrDefault(c => K(c.Codigo) == K(codigo));
                if (cta != null)
                {
                    cta.Activo = !cta.Activo;
                    await _bancos.SetByProveedorAsync(vendorId, cuentas);

                    _logger.LogInformation(
                        "Estado cuenta actualizado. VendorId: {VendorId}, Codigo: {Codigo}, Activo: {Activo}",
                        vendorId,
                        codigo,
                        cta.Activo);

                    TempData[VIEWBAG_MENSAJE_EXITO] = MSG_EXITO_ESTADO_ACTUALIZADO;
                }
                else
                {
                    _logger.LogWarning(
                        "Intento de toggle cuenta inexistente. VendorId: {VendorId}, Codigo: {Codigo}",
                        vendorId,
                        codigo);

                    TempData[VIEWBAG_MENSAJE_ERROR] = MSG_ERROR_CUENTA_NO_ENCONTRADA;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error al toggle cuenta. VendorId: {VendorId}, Codigo: {Codigo}",
                    vendorId,
                    codigo);

                TempData[VIEWBAG_MENSAJE_ERROR] = MSG_ERROR_TOGGLE_CUENTA;
            }

            return RedirectToAction(nameof(Bancos), new { vId = (User.IsInRole(ROL_ADMINISTRADOR) ? vendorId : null) });
        }

        #endregion

        #region Envíos - ViewModels

        /// <summary>
        /// ViewModel para crear/editar tarifa de envío
        /// </summary>
        public sealed class EnvioUpsertVm
        {
            public string? OriginalProvincia { get; set; }
            public string? OriginalCiudad { get; set; }
            public string Provincia { get; set; } = string.Empty;
            public string? Ciudad { get; set; }
            public decimal Precio { get; set; }
            public bool Activo { get; set; } = true;
            public string? Nota { get; set; }
        }

        #endregion

        #region Envíos - Validación

        /// <summary>
        /// Valida los datos de una tarifa de envío
        /// </summary>
        private static (bool ok, string? err) ValidateEnvio(EnvioUpsertVm vm)
        {
            if (string.IsNullOrWhiteSpace(vm.Provincia))
                return (false, MSG_ERROR_PROVINCIA_OBLIGATORIA);

            if (!string.IsNullOrWhiteSpace(vm.Provincia) && !Texto120Regex.IsMatch(vm.Provincia))
                return (false, MSG_ERROR_PROVINCIA_INVALIDA);

            if (!string.IsNullOrWhiteSpace(vm.Ciudad) && !Texto120Regex.IsMatch(vm.Ciudad!))
                return (false, MSG_ERROR_CIUDAD_INVALIDA);

            if (vm.Precio < PRECIO_MIN || vm.Precio > PRECIO_MAX)
                return (false, MSG_ERROR_PRECIO_RANGO);

            if (!string.IsNullOrWhiteSpace(vm.Nota) && !Texto120Regex.IsMatch(vm.Nota!))
                return (false, MSG_ERROR_NOTA_INVALIDA);

            return (true, null);
        }

        /// <summary>
        /// Compara claves de reglas de envío
        /// </summary>
        private static bool SameKey(Cfg.TarifaEnvioRegla a, string prov, string? city) =>
            K(a.Provincia) == K(prov) && K(a.Ciudad ?? "") == K(city ?? "");

        /// <summary>
        /// Parsea precio con formato flexible (soporta coma y punto)
        /// </summary>
        private static bool TryParsePrecioFlexible(string raw, out decimal value)
        {
            value = 0m;
            if (string.IsNullOrWhiteSpace(raw)) return false;

            var s = raw.Trim().Replace(" ", "");

            var lastComma = s.LastIndexOf(',');
            var lastDot = s.LastIndexOf('.');

            if (lastComma >= 0 && lastDot >= 0)
            {
                if (lastComma > lastDot)
                {
                    s = s.Replace(".", "");
                    s = s.Replace(',', '.');
                }
                else
                {
                    s = s.Replace(",", "");
                }
            }
            else if (lastComma >= 0)
            {
                s = s.Replace(',', '.');
            }

            return decimal.TryParse(s,
                NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out value);
        }

        #endregion

        #region Envíos - Acciones

        /// <summary>
        /// GET: /Vendedor/Envios?vId=&lt;opcional para Admin&gt;
        /// Listado de tarifas de envío del vendedor
        /// </summary>
        [HttpGet("Envios")]
        public async Task<IActionResult> Envios(
            [FromQuery] string? vId = null,
            CancellationToken ct = default)
        {
            try
            {
                var vendorId = CurrentVendorId(vId);

                _logger.LogInformation(
                    "Cargando tarifas de envío. VendorId: {VendorId}",
                    vendorId);

                var reglas = (await _envios.GetByProveedorAsync(vendorId))?.ToList()
                             ?? new List<Cfg.TarifaEnvioRegla>();

                _logger.LogDebug(
                    "Tarifas de envío cargadas. VendorId: {VendorId}, Count: {Count}",
                    vendorId,
                    reglas.Count);

                ViewBag.Reglas = reglas
                    .OrderBy(r => r.Provincia)
                    .ThenBy(r => r.Ciudad ?? string.Empty)
                    .ThenBy(r => r.Precio)
                    .ToList();

                ViewBag.TargetVendorId = vendorId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cargando tarifas de envío del vendedor");
                ViewBag.Reglas = new List<Cfg.TarifaEnvioRegla>();
                TempData[VIEWBAG_MENSAJE_ERROR] = MSG_ERROR_CARGAR_DATOS;
            }

            ViewBag.MensajeExito = TempData[VIEWBAG_MENSAJE_EXITO];
            ViewBag.MensajeError = TempData[VIEWBAG_MENSAJE_ERROR];
            ViewBag.ModelErrors = TempData[VIEWBAG_MODEL_ERRORS];

            return View();
        }

        /// <summary>
        /// POST: /Vendedor/Envios/Save?vId=&lt;opcional para Admin&gt;
        /// Guarda o actualiza una tarifa de envío
        /// </summary>
        [HttpPost("Envios/Save")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnviosSave(
            [FromForm] EnvioUpsertVm vm,
            [FromQuery] string? vId = null,
            CancellationToken ct = default)
        {
            var vendorId = CurrentVendorId(vId);

            _logger.LogInformation(
                "Guardando tarifa de envío. VendorId: {VendorId}, Provincia: {Provincia}, Ciudad: {Ciudad}, EsNuevo: {EsNuevo}",
                vendorId,
                vm.Provincia,
                vm.Ciudad ?? "null",
                string.IsNullOrEmpty(vm.OriginalProvincia) && string.IsNullOrEmpty(vm.OriginalCiudad));

            // Parseo robusto de precio
            if (Request?.Form != null && Request.Form.ContainsKey("Precio"))
            {
                var raw = (Request.Form["Precio"].ToString() ?? "").Trim();

                if (!string.IsNullOrWhiteSpace(raw))
                {
                    if (!TryParsePrecioFlexible(raw, out var precio))
                    {
                        _logger.LogWarning(
                            "Precio inválido. VendorId: {VendorId}, Precio: {Precio}",
                            vendorId,
                            raw);

                        TempData[VIEWBAG_MENSAJE_ERROR] = MSG_ERROR_PRECIO_INVALIDO;
                        TempData[VIEWBAG_MODEL_ERRORS] = MSG_ERROR_PRECIO_INVALIDO;
                        return RedirectToAction(nameof(Envios), new { vId = (User.IsInRole(ROL_ADMINISTRADOR) ? vendorId : null) });
                    }

                    if (precio < PRECIO_MIN || precio > PRECIO_MAX)
                    {
                        _logger.LogWarning(
                            "Precio fuera de rango. VendorId: {VendorId}, Precio: {Precio}",
                            vendorId,
                            precio);

                        TempData[VIEWBAG_MENSAJE_ERROR] = MSG_ERROR_PRECIO_FUERA_RANGO;
                        TempData[VIEWBAG_MODEL_ERRORS] = MSG_ERROR_PRECIO_FUERA_RANGO;
                        return RedirectToAction(nameof(Envios), new { vId = (User.IsInRole(ROL_ADMINISTRADOR) ? vendorId : null) });
                    }

                    vm.Precio = precio;
                }
            }

            vm.OriginalProvincia = TN(vm.OriginalProvincia);
            vm.OriginalCiudad = TN(vm.OriginalCiudad);
            vm.Provincia = T(vm.Provincia);
            vm.Ciudad = TN(vm.Ciudad);
            vm.Nota = TN(vm.Nota);

            var (ok, err) = ValidateEnvio(vm);
            if (!ok)
            {
                _logger.LogWarning(
                    "Validación fallida. VendorId: {VendorId}, Error: {Error}",
                    vendorId,
                    err);

                TempData[VIEWBAG_MENSAJE_ERROR] = err;
                TempData[VIEWBAG_MODEL_ERRORS] = err;
                return RedirectToAction(nameof(Envios), new { vId = (User.IsInRole(ROL_ADMINISTRADOR) ? vendorId : null) });
            }

            try
            {
                var reglas = (await _envios.GetByProveedorAsync(vendorId))?.ToList()
                            ?? new List<Cfg.TarifaEnvioRegla>();

                bool IsDup(string prov, string? city, Cfg.TarifaEnvioRegla? exclude = null) =>
                    reglas.Any(r => !ReferenceEquals(r, exclude) && SameKey(r, prov, city));

                if (string.IsNullOrEmpty(vm.OriginalProvincia) && string.IsNullOrEmpty(vm.OriginalCiudad))
                {
                    // CREAR
                    if (IsDup(vm.Provincia, vm.Ciudad))
                    {
                        _logger.LogWarning(
                            "Intento de crear tarifa duplicada. VendorId: {VendorId}, Provincia: {Provincia}, Ciudad: {Ciudad}",
                            vendorId,
                            vm.Provincia,
                            vm.Ciudad ?? "null");

                        TempData[VIEWBAG_MENSAJE_ERROR] = MSG_ERROR_TARIFA_DUPLICADA;
                        return RedirectToAction(nameof(Envios), new { vId = (User.IsInRole(ROL_ADMINISTRADOR) ? vendorId : null) });
                    }

                    reglas.Add(new Cfg.TarifaEnvioRegla
                    {
                        Provincia = vm.Provincia,
                        Ciudad = vm.Ciudad,
                        Precio = vm.Precio,
                        Activo = vm.Activo,
                        Nota = vm.Nota
                    });

                    _logger.LogInformation(
                        "Tarifa creada. VendorId: {VendorId}, Provincia: {Provincia}, Ciudad: {Ciudad}, Precio: {Precio:C}",
                        vendorId,
                        vm.Provincia,
                        vm.Ciudad ?? "null",
                        vm.Precio);
                }
                else
                {
                    // EDITAR
                    var actual = reglas.FirstOrDefault(r => SameKey(r, vm.OriginalProvincia!, vm.OriginalCiudad));
                    if (actual == null)
                    {
                        _logger.LogWarning(
                            "Tarifa original no encontrada para editar. VendorId: {VendorId}, Provincia: {Provincia}, Ciudad: {Ciudad}",
                            vendorId,
                            vm.OriginalProvincia,
                            vm.OriginalCiudad ?? "null");

                        TempData[VIEWBAG_MENSAJE_ERROR] = MSG_ERROR_TARIFA_ORIGINAL_NO_ENCONTRADA;
                        return RedirectToAction(nameof(Envios), new { vId = (User.IsInRole(ROL_ADMINISTRADOR) ? vendorId : null) });
                    }

                    if (K(vm.Provincia) != K(vm.OriginalProvincia) || K(vm.Ciudad ?? "") != K(vm.OriginalCiudad ?? ""))
                    {
                        if (IsDup(vm.Provincia, vm.Ciudad, actual))
                        {
                            _logger.LogWarning(
                                "Nuevo destino duplicado. VendorId: {VendorId}, Provincia: {Provincia}, Ciudad: {Ciudad}",
                                vendorId,
                                vm.Provincia,
                                vm.Ciudad ?? "null");

                            TempData[VIEWBAG_MENSAJE_ERROR] = MSG_ERROR_DESTINO_DUPLICADO;
                            return RedirectToAction(nameof(Envios), new { vId = (User.IsInRole(ROL_ADMINISTRADOR) ? vendorId : null) });
                        }
                    }

                    actual.Provincia = vm.Provincia;
                    actual.Ciudad = vm.Ciudad;
                    actual.Precio = vm.Precio;
                    actual.Activo = vm.Activo;
                    actual.Nota = vm.Nota;

                    _logger.LogInformation(
                        "Tarifa actualizada. VendorId: {VendorId}, Provincia: {Provincia}, Ciudad: {Ciudad}, Precio: {Precio:C}",
                        vendorId,
                        vm.Provincia,
                        vm.Ciudad ?? "null",
                        vm.Precio);
                }

                await _envios.SetByProveedorAsync(vendorId, reglas);
                TempData[VIEWBAG_MENSAJE_EXITO] = MSG_EXITO_TARIFA_GUARDADA;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error al guardar tarifa de envío. VendorId: {VendorId}, Provincia: {Provincia}",
                    vendorId,
                    vm.Provincia);

                TempData[VIEWBAG_MENSAJE_ERROR] = MSG_ERROR_GUARDAR_TARIFA;
            }

            return RedirectToAction(nameof(Envios), new { vId = (User.IsInRole(ROL_ADMINISTRADOR) ? vendorId : null) });
        }

        /// <summary>
        /// POST: /Vendedor/Envios/Delete?vId=&lt;opcional para Admin&gt;
        /// Elimina una tarifa de envío
        /// </summary>
        [HttpPost("Envios/Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnviosDelete(
            [FromForm] string provincia,
            [FromForm] string? ciudad,
            [FromQuery] string? vId = null,
            CancellationToken ct = default)
        {
            var vendorId = CurrentVendorId(vId);

            if (string.IsNullOrWhiteSpace(provincia))
            {
                _logger.LogWarning(
                    "Intento de eliminar tarifa con provincia inválida. VendorId: {VendorId}",
                    vendorId);

                TempData[VIEWBAG_MENSAJE_ERROR] = MSG_ERROR_PROVINCIA_INVALIDA_PARAM;
                return RedirectToAction(nameof(Envios), new { vId = (User.IsInRole(ROL_ADMINISTRADOR) ? vendorId : null) });
            }

            _logger.LogInformation(
                "Eliminando tarifa. VendorId: {VendorId}, Provincia: {Provincia}, Ciudad: {Ciudad}",
                vendorId,
                provincia,
                ciudad ?? "null");

            try
            {
                var reglas = (await _envios.GetByProveedorAsync(vendorId))?.ToList()
                            ?? new List<Cfg.TarifaEnvioRegla>();

                var item = reglas.FirstOrDefault(r => SameKey(r, provincia, ciudad));
                if (item != null)
                {
                    reglas.Remove(item);
                    await _envios.SetByProveedorAsync(vendorId, reglas);

                    _logger.LogInformation(
                        "Tarifa eliminada. VendorId: {VendorId}, Provincia: {Provincia}, Ciudad: {Ciudad}",
                        vendorId,
                        provincia,
                        ciudad ?? "null");

                    TempData[VIEWBAG_MENSAJE_EXITO] = MSG_EXITO_TARIFA_ELIMINADA;
                }
                else
                {
                    _logger.LogWarning(
                        "Intento de eliminar tarifa inexistente. VendorId: {VendorId}, Provincia: {Provincia}, Ciudad: {Ciudad}",
                        vendorId,
                        provincia,
                        ciudad ?? "null");

                    TempData[VIEWBAG_MENSAJE_ERROR] = MSG_ERROR_TARIFA_NO_ENCONTRADA;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error al eliminar tarifa de envío. VendorId: {VendorId}, Prov: {Prov}, Ciudad: {Ciudad}",
                    vendorId,
                    provincia,
                    ciudad ?? "null");

                TempData[VIEWBAG_MENSAJE_ERROR] = MSG_ERROR_ELIMINAR_TARIFA;
            }

            return RedirectToAction(nameof(Envios), new { vId = (User.IsInRole(ROL_ADMINISTRADOR) ? vendorId : null) });
        }

        /// <summary>
        /// POST: /Vendedor/Envios/Toggle?vId=&lt;opcional para Admin&gt;
        /// Alterna el estado activo/inactivo de una tarifa
        /// </summary>
        [HttpPost("Envios/Toggle")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnviosToggle(
            [FromForm] string provincia,
            [FromForm] string? ciudad,
            [FromQuery] string? vId = null,
            CancellationToken ct = default)
        {
            var vendorId = CurrentVendorId(vId);

            _logger.LogInformation(
                "Alternando estado tarifa. VendorId: {VendorId}, Provincia: {Provincia}, Ciudad: {Ciudad}",
                vendorId,
                provincia,
                ciudad ?? "null");

            try
            {
                var reglas = (await _envios.GetByProveedorAsync(vendorId))?.ToList()
                            ?? new List<Cfg.TarifaEnvioRegla>();

                var r = reglas.FirstOrDefault(x => SameKey(x, provincia, ciudad));
                if (r != null)
                {
                    r.Activo = !r.Activo;
                    await _envios.SetByProveedorAsync(vendorId, reglas);

                    _logger.LogInformation(
                        "Estado tarifa actualizado. VendorId: {VendorId}, Provincia: {Provincia}, Ciudad: {Ciudad}, Activo: {Activo}",
                        vendorId,
                        provincia,
                        ciudad ?? "null",
                        r.Activo);

                    TempData[VIEWBAG_MENSAJE_EXITO] = MSG_EXITO_ESTADO_ACTUALIZADO;
                }
                else
                {
                    _logger.LogWarning(
                        "Intento de toggle tarifa inexistente. VendorId: {VendorId}, Provincia: {Provincia}, Ciudad: {Ciudad}",
                        vendorId,
                        provincia,
                        ciudad ?? "null");

                    TempData[VIEWBAG_MENSAJE_ERROR] = MSG_ERROR_TARIFA_NO_ENCONTRADA;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error al toggle tarifa. VendorId: {VendorId}, Prov: {Prov}, Ciudad: {Ciudad}",
                    vendorId,
                    provincia,
                    ciudad ?? "null");

                TempData[VIEWBAG_MENSAJE_ERROR] = MSG_ERROR_TOGGLE_TARIFA;
            }

            return RedirectToAction(nameof(Envios), new { vId = (User.IsInRole(ROL_ADMINISTRADOR) ? vendorId : null) });
        }

        #endregion

        #region Reportes

        /// <summary>
        /// GET: /Vendedor/Reportes
        /// Listado de ventas del vendedor
        /// </summary>
        [HttpGet("Reportes")]
        public async Task<IActionResult> Reportes(
            [FromQuery] string? vId,
            CancellationToken ct = default)
        {
            try
            {
                var vendorId = CurrentVendorId(vId);

                _logger.LogInformation(
                    "Cargando reportes. VendorId: {VendorId}",
                    vendorId);

                // Total por venta SOLO considerando ítems del vendedor
                var agregados = await _context.DetalleVentas
                    .AsNoTracking()
                    .Where(dv => dv.Producto != null && dv.Producto.VendedorID == vendorId)
                    .GroupBy(dv => dv.VentaID)
                    .Select(g => new
                    {
                        VentaID = g.Key,
                        TotalVendedor = g.Sum(dv => (dv.Subtotal ?? ((dv.PrecioUnitario * dv.Cantidad) - (dv.Descuento ?? 0m))))
                    })
                    .ToListAsync(ct);

                var ventaIds = agregados.Select(a => a.VentaID).ToList();
                if (ventaIds.Count == 0)
                {
                    _logger.LogDebug("No hay ventas para el vendedor. VendorId: {VendorId}", vendorId);
                    ViewData["Title"] = TITLE_MIS_VENTAS;
                    return View(VIEW_REPORTES, Enumerable.Empty<CompradorResumenVM>());
                }

                var vm = await _context.Ventas
                    .AsNoTracking()
                    .Where(v => ventaIds.Contains(v.VentaID))
                    .Include(v => v.Usuario)
                    .OrderByDescending(v => v.FechaVenta)
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
                        Total = 0m,
                        FotoPerfil = v.Usuario != null ? v.Usuario.FotoPerfil : null
                    })
                    .ToListAsync(ct);

                var lookup = agregados.ToDictionary(a => a.VentaID, a => a.TotalVendedor);
                foreach (var r in vm)
                    if (lookup.TryGetValue(r.VentaID, out var t)) r.Total = t;

                _logger.LogDebug(
                    "Reportes cargados. VendorId: {VendorId}, Ventas: {Count}",
                    vendorId,
                    vm.Count);

                ViewData["Title"] = TITLE_MIS_VENTAS;
                return View(VIEW_REPORTES, vm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar reportes");
                TempData[VIEWBAG_MENSAJE_ERROR] = MSG_ERROR_CARGAR_DATOS;
                ViewData["Title"] = TITLE_MIS_VENTAS;
                return View(VIEW_REPORTES, Enumerable.Empty<CompradorResumenVM>());
            }
        }

        /// <summary>
        /// GET: /Vendedor/Reportes/Detalle/{id}
        /// Detalle de una venta del vendedor
        /// </summary>
        [HttpGet("Reportes/Detalle/{id:int}", Name = "Vendedor_VentaDetalle")]
        public async Task<IActionResult> VentaDetalle(
            int id,
            [FromQuery] string? vId,
            CancellationToken ct = default)
        {
            try
            {
                var vendorId = CurrentVendorId(vId);

                _logger.LogInformation(
                    "Cargando detalle venta. VentaId: {VentaId}, VendorId: {VendorId}",
                    id,
                    vendorId);

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

                var misDetalles = (v.DetalleVentas ?? new List<DetalleVentas>())
                    .Where(d => d.Producto != null && d.Producto.VendedorID == vendorId)
                    .ToList();

                if (misDetalles.Count == 0)
                {
                    _logger.LogWarning(
                        "Venta sin productos del vendedor. VentaId: {VentaId}, VendorId: {VendorId}",
                        id,
                        vendorId);
                    return Forbid();
                }

                var totalVend = misDetalles.Sum(d => (d.Subtotal ?? ((d.PrecioUnitario * d.Cantidad) - (d.Descuento ?? 0m))));

                var compUrl =
                    NormalizarCompUrl(v.Usuario?.FotoComprobanteDeposito) ??
                    BuscarComprobanteUrl(v.VentaID);

                var (depositante, bancoMeta) = BuscarMetaDeposito(v.VentaID);

                var direcciones = new List<DireccionVM>();
                if (v.Usuario != null)
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
                    Banco = bancoMeta,
                    Depositante = depositante,
                    ComprobanteUrl = compUrl,
                    VentaID = v.VentaID,
                    Fecha = v.FechaVenta,
                    Estado = v.Estado ?? string.Empty,
                    MetodoPago = v.MetodoPago ?? string.Empty,
                    Total = totalVend,
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
                    Detalles = misDetalles.Select(d => new DetalleVentaVM
                    {
                        Producto = d.Producto?.Nombre ?? $"Producto #{d.ProductoID}",
                        Cantidad = d.Cantidad,
                        Subtotal = d.Subtotal ?? ((d.PrecioUnitario * d.Cantidad) - (d.Descuento ?? 0m))
                    }).ToList()
                };

                _logger.LogDebug(
                    "Detalle venta cargado. VentaId: {VentaId}, Productos: {Count}, Total: {Total:C}",
                    id,
                    misDetalles.Count,
                    totalVend);

                return View(VIEW_VENTA_DETALLE, vm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar detalle venta. VentaId: {VentaId}", id);
                TempData[VIEWBAG_MENSAJE_ERROR] = MSG_ERROR_CARGAR_DATOS;
                return RedirectToAction(nameof(Reportes), new { vId });
            }
        }

        #endregion
    }
}