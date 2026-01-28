using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Simone.Configuration;
using Simone.Services;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Simone.Controllers
{
    /// <summary>
    /// Controlador administrativo para gestión de cuentas bancarias
    /// Versión optimizada con mejores prácticas empresariales
    /// </summary>
    [Authorize(Roles = "Administrador")]
    [Route("Admin/Bancos")]
    public class AdminBancosController : Controller
    {
        #region Dependencias e Inyección

        private readonly IBancosConfigService _bancosService;
        private readonly ILogger<AdminBancosController> _logger;
        private readonly IWebHostEnvironment _environment;
        private readonly IMemoryCache _cache;

        public AdminBancosController(
            IBancosConfigService bancosService,
            ILogger<AdminBancosController> logger,
            IWebHostEnvironment environment,
            IMemoryCache cache)
        {
            _bancosService = bancosService ?? throw new ArgumentNullException(nameof(bancosService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        #endregion

        #region Constantes

        private const string CACHE_KEY_CUENTAS_ADMIN = "CuentasBancariasAdmin";
        private static readonly TimeSpan CACHE_DURATION = TimeSpan.FromMinutes(15);

        private const int MAX_CODIGO_LENGTH = 50;
        private const int MIN_CODIGO_LENGTH = 2;
        private const int MAX_NOMBRE_LENGTH = 120;
        private const int MAX_NUMERO_CUENTA_LENGTH = 20;
        private const int MIN_NUMERO_CUENTA_LENGTH = 6;
        private const int MAX_TIPO_CUENTA_LENGTH = 40;
        private const int MAX_TITULAR_LENGTH = 120;
        private const int MAX_RUC_LENGTH = 20;
        private const int MAX_LOGO_PATH_LENGTH = 200;

        private const string DEFAULT_TIPO_CUENTA = "Cuenta de Ahorros";
        private const string PLACEHOLDER_LOGO = "/images/Bancos/placeholder.png";
        private const string BANCOS_IMAGES_PATH = "images/Bancos";

        #endregion

        #region Expresiones Regulares (Compiladas para Performance)

        private static readonly Regex CodigoRegex = new(
            @"^[a-z0-9_-]{2,50}$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));

        private static readonly Regex NumeroCuentaRegex = new(
            @"^[0-9]{6,20}$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));

        private static readonly Regex Texto40Regex = new(
            @"^.{1,40}$",
            RegexOptions.Compiled | RegexOptions.Singleline,
            TimeSpan.FromMilliseconds(100));

        private static readonly Regex Texto120Regex = new(
            @"^.{1,120}$",
            RegexOptions.Compiled | RegexOptions.Singleline,
            TimeSpan.FromMilliseconds(100));

        private static readonly Regex RucRegex = new(
            @"^\d{10}(\d{3})?$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));

        #endregion

        #region Métodos Helper

        /// <summary>
        /// Normaliza una clave para comparación case-insensitive
        /// </summary>
        private static string NormalizeKey(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return value.Trim().ToLowerInvariant();
        }

        /// <summary>
        /// Recorta espacios o retorna string vacío
        /// </summary>
        private static string TrimOrEmpty(string? value)
        {
            return value?.Trim() ?? string.Empty;
        }

        /// <summary>
        /// Recorta espacios o retorna null
        /// </summary>
        private static string? TrimOrNull(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        /// <summary>
        /// Obtiene todos los errores del ModelState como string
        /// </summary>
        private string GetModelErrors()
        {
            var errors = ModelState
                .Where(kv => kv.Value?.Errors.Count > 0)
                .Select(kv => $"{kv.Key}: {string.Join(" | ", kv.Value!.Errors.Select(e => e.ErrorMessage))}");

            return string.Join("; ", errors);
        }

        /// <summary>
        /// Obtiene ruta segura del logo, con fallback a placeholder
        /// </summary>
        private string GetSafeLogoPath(string? logoPath)
        {
            if (string.IsNullOrWhiteSpace(logoPath))
            {
                _logger.LogDebug("LogoPath vacío, usando placeholder");
                return PLACEHOLDER_LOGO;
            }

            // Validar que no contenga caracteres peligrosos
            if (logoPath.Contains("..", StringComparison.Ordinal) ||
                logoPath.Contains("://", StringComparison.Ordinal) ||
                logoPath.Contains('\\'))
            {
                _logger.LogWarning("LogoPath contiene caracteres peligrosos: {LogoPath}", logoPath);
                return PLACEHOLDER_LOGO;
            }

            try
            {
                var fullPath = Path.Combine(_environment.WebRootPath, logoPath.TrimStart('/'));

                if (!System.IO.File.Exists(fullPath))
                {
                    _logger.LogWarning("Logo no encontrado en ruta física: {FullPath}", fullPath);
                    return PLACEHOLDER_LOGO;
                }

                return logoPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al verificar ruta del logo: {LogoPath}", logoPath);
                return PLACEHOLDER_LOGO;
            }
        }

        /// <summary>
        /// Invalida el cache de cuentas bancarias
        /// </summary>
        private void InvalidateCache()
        {
            _cache.Remove(CACHE_KEY_CUENTAS_ADMIN);
            _logger.LogDebug("Cache de cuentas bancarias invalidado");
        }

        /// <summary>
        /// Obtiene las cuentas bancarias con cache
        /// </summary>
        private async Task<List<CuentaBancaria>> GetCuentasConCacheAsync()
        {
            return await _cache.GetOrCreateAsync(
                CACHE_KEY_CUENTAS_ADMIN,
                async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = CACHE_DURATION;
                    _logger.LogDebug("Cargando cuentas bancarias desde servicio (cache miss)");

                    var cuentas = await _bancosService.GetAdminAsync();
                    return cuentas.ToList();
                }) ?? new List<CuentaBancaria>();
        }

        #endregion

        #region Validación

        /// <summary>
        /// Resultado de validación
        /// </summary>
        private record ValidationResult(bool IsValid, string? ErrorMessage)
        {
            public static ValidationResult Success() => new(true, null);
            public static ValidationResult Failure(string message) => new(false, message);
        }

        /// <summary>
        /// Valida el ViewModel de forma exhaustiva
        /// </summary>
        private ValidationResult ValidateViewModel(AdminUpsertVm vm)
        {
            // Validación de código
            if (string.IsNullOrWhiteSpace(vm.Codigo))
                return ValidationResult.Failure("El código es obligatorio.");

            if (vm.Codigo.Length < MIN_CODIGO_LENGTH || vm.Codigo.Length > MAX_CODIGO_LENGTH)
                return ValidationResult.Failure($"El código debe tener entre {MIN_CODIGO_LENGTH} y {MAX_CODIGO_LENGTH} caracteres.");

            if (!CodigoRegex.IsMatch(vm.Codigo))
                return ValidationResult.Failure("Código inválido. Usa minúsculas, números, '-' o '_'.");

            // Validación de nombre
            if (string.IsNullOrWhiteSpace(vm.Nombre))
                return ValidationResult.Failure("El nombre es obligatorio.");

            if (vm.Nombre.Length > MAX_NOMBRE_LENGTH)
                return ValidationResult.Failure($"El nombre no puede exceder {MAX_NOMBRE_LENGTH} caracteres.");

            if (!Texto120Regex.IsMatch(vm.Nombre))
                return ValidationResult.Failure("El nombre contiene caracteres inválidos.");

            // Validación de número de cuenta
            if (string.IsNullOrWhiteSpace(vm.Numero))
                return ValidationResult.Failure("El número de cuenta es obligatorio.");

            if (vm.Numero.Length < MIN_NUMERO_CUENTA_LENGTH || vm.Numero.Length > MAX_NUMERO_CUENTA_LENGTH)
                return ValidationResult.Failure($"El número de cuenta debe tener entre {MIN_NUMERO_CUENTA_LENGTH} y {MAX_NUMERO_CUENTA_LENGTH} dígitos.");

            if (!NumeroCuentaRegex.IsMatch(vm.Numero))
                return ValidationResult.Failure("El número de cuenta solo debe contener dígitos.");

            // Validación de tipo de cuenta
            if (string.IsNullOrWhiteSpace(vm.Tipo))
                return ValidationResult.Failure("El tipo de cuenta es obligatorio.");

            if (vm.Tipo.Length > MAX_TIPO_CUENTA_LENGTH)
                return ValidationResult.Failure($"El tipo de cuenta no puede exceder {MAX_TIPO_CUENTA_LENGTH} caracteres.");

            if (!Texto40Regex.IsMatch(vm.Tipo))
                return ValidationResult.Failure("El tipo de cuenta contiene caracteres inválidos.");

            // Validación de titular (opcional)
            if (!string.IsNullOrWhiteSpace(vm.Titular))
            {
                if (vm.Titular.Length > MAX_TITULAR_LENGTH)
                    return ValidationResult.Failure($"El titular no puede exceder {MAX_TITULAR_LENGTH} caracteres.");

                if (!Texto120Regex.IsMatch(vm.Titular))
                    return ValidationResult.Failure("El titular contiene caracteres inválidos.");
            }

            // Validación de RUC (opcional)
            if (!string.IsNullOrWhiteSpace(vm.Ruc))
            {
                if (vm.Ruc.Length > MAX_RUC_LENGTH)
                    return ValidationResult.Failure($"El RUC/Cédula no puede exceder {MAX_RUC_LENGTH} caracteres.");

                if (!RucRegex.IsMatch(vm.Ruc))
                    return ValidationResult.Failure("RUC/Cédula inválido (debe tener 10 o 13 dígitos).");
            }

            // Validación de LogoPath (opcional)
            if (!string.IsNullOrWhiteSpace(vm.LogoPath))
            {
                if (vm.LogoPath.Length > MAX_LOGO_PATH_LENGTH)
                    return ValidationResult.Failure($"La ruta del logo no puede exceder {MAX_LOGO_PATH_LENGTH} caracteres.");

                // Validar seguridad de la ruta
                if (vm.LogoPath.Contains("..", StringComparison.Ordinal) ||
                    vm.LogoPath.Contains("://", StringComparison.Ordinal) ||
                    vm.LogoPath.Contains('\\'))
                {
                    return ValidationResult.Failure("La ruta del logo contiene caracteres no permitidos.");
                }

                // Usar ruta segura
                vm.LogoPath = GetSafeLogoPath(vm.LogoPath);
            }
            else
            {
                vm.LogoPath = PLACEHOLDER_LOGO;
            }

            return ValidationResult.Success();
        }

        #endregion

        #region Acciones - Vistas

        /// <summary>
        /// GET: /Admin/Bancos
        /// Vista principal de administración de cuentas bancarias
        /// </summary>
        [HttpGet("", Name = "AdminBancos_Index")]
        public async Task<IActionResult> Index(
            [FromQuery] string? filtro = null,
            [FromQuery] bool? soloActivas = null,
            [FromQuery] string? ordenar = null)
        {
            // Verificar autorización adicional
            if (User.IsInRole("Vendedor") && !User.IsInRole("Administrador"))
            {
                _logger.LogWarning(
                    "Usuario {UserName} intentó acceder a Admin/Bancos siendo solo Vendedor",
                    User.Identity?.Name);
                return RedirectToAction("Bancos", "Vendedor");
            }

            try
            {
                var cuentas = await GetCuentasConCacheAsync();

                // Aplicar filtros
                if (!string.IsNullOrWhiteSpace(filtro))
                {
                    var filtroLower = filtro.ToLowerInvariant();
                    cuentas = cuentas.Where(c =>
                        c.Codigo.ToLowerInvariant().Contains(filtroLower) ||
                        c.Nombre.ToLowerInvariant().Contains(filtroLower) ||
                        (c.Numero?.Contains(filtro, StringComparison.OrdinalIgnoreCase) ?? false)
                    ).ToList();
                }

                if (soloActivas.HasValue)
                {
                    cuentas = cuentas.Where(c => c.Activo == soloActivas.Value).ToList();
                }

                // Aplicar ordenamiento
                cuentas = ordenar?.ToLowerInvariant() switch
                {
                    "nombre" => cuentas.OrderBy(c => c.Nombre).ToList(),
                    "nombre_desc" => cuentas.OrderByDescending(c => c.Nombre).ToList(),
                    "codigo" => cuentas.OrderBy(c => c.Codigo).ToList(),
                    "codigo_desc" => cuentas.OrderByDescending(c => c.Codigo).ToList(),
                    "tipo" => cuentas.OrderBy(c => c.Tipo).ToList(),
                    _ => cuentas.OrderBy(c => c.Nombre).ThenBy(c => c.Codigo).ToList()
                };

                // Asegurar rutas seguras de logos
                foreach (var cuenta in cuentas)
                {
                    cuenta.LogoPath = GetSafeLogoPath(cuenta.LogoPath);
                }

                ViewBag.Cuentas = cuentas;
                ViewBag.Filtro = filtro;
                ViewBag.SoloActivas = soloActivas;
                ViewBag.Ordenar = ordenar;

                _logger.LogInformation(
                    "Cuentas bancarias cargadas. Total: {Total}, Filtradas: {Filtradas}",
                    cuentas.Count,
                    cuentas.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar cuentas bancarias del administrador");
                ViewBag.Cuentas = new List<CuentaBancaria>();
                TempData["MensajeError"] = "No se pudieron cargar las cuentas bancarias. Por favor, intenta nuevamente.";
            }

            // Preservar mensajes de TempData
            ViewBag.MensajeExito = TempData["MensajeExito"];
            ViewBag.MensajeError = TempData["MensajeError"];
            ViewBag.ModelErrors = TempData["ModelErrors"];

            return View();
        }

        #endregion

        #region Acciones - CRUD

        /// <summary>
        /// POST: /Admin/Bancos/SaveAdmin
        /// Crea o actualiza una cuenta bancaria
        /// </summary>
        [HttpPost("SaveAdmin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveAdmin([FromForm] AdminUpsertVm vm)
        {
            if (vm == null)
            {
                _logger.LogWarning("ViewModel nulo en SaveAdmin");
                TempData["MensajeError"] = "Datos del formulario inválidos.";
                return RedirectToAction(nameof(Index));
            }

            // Normalizar entrada
            vm.OriginalCodigo = TrimOrNull(vm.OriginalCodigo);
            vm.Codigo = TrimOrEmpty(vm.Codigo).ToLowerInvariant();
            vm.Nombre = TrimOrEmpty(vm.Nombre);
            vm.Numero = TrimOrEmpty(vm.Numero);
            vm.Tipo = TrimOrEmpty(vm.Tipo);
            vm.Titular = TrimOrNull(vm.Titular);
            vm.Ruc = TrimOrNull(vm.Ruc);
            vm.LogoPath = TrimOrNull(vm.LogoPath);

            // Remover del ModelState para evitar problemas de validación
            ModelState.Remove(nameof(AdminUpsertVm.OriginalCodigo));
            ModelState.Remove(nameof(AdminUpsertVm.Activo));

            // Validación básica del ModelState
            if (!ModelState.IsValid)
            {
                var errors = GetModelErrors();
                _logger.LogWarning(
                    "ModelState inválido en SaveAdmin. Usuario: {UserName}, Errores: {Errors}",
                    User.Identity?.Name,
                    errors);

                TempData["MensajeError"] = "Por favor, corrige los errores en el formulario.";
                TempData["ModelErrors"] = errors;
                return RedirectToAction(nameof(Index));
            }

            // Validación de negocio
            var validationResult = ValidateViewModel(vm);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning(
                    "Validación de negocio fallida. Código: {Codigo}, Error: {Error}",
                    vm.Codigo,
                    validationResult.ErrorMessage);

                TempData["MensajeError"] = validationResult.ErrorMessage;
                TempData["ModelErrors"] = validationResult.ErrorMessage;
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var cuentas = (await GetCuentasConCacheAsync()).ToList();
                var normalizedCodigo = NormalizeKey(vm.Codigo);
                var normalizedOriginalCodigo = NormalizeKey(vm.OriginalCodigo);

                var isCreate = string.IsNullOrEmpty(vm.OriginalCodigo);

                // Helper para verificar duplicados
                bool IsDuplicate(string codigo, CuentaBancaria? exclude = null)
                {
                    return cuentas.Any(c =>
                        !ReferenceEquals(c, exclude) &&
                        NormalizeKey(c.Codigo) == codigo);
                }

                if (isCreate)
                {
                    // CREATE - Verificar código duplicado
                    if (IsDuplicate(normalizedCodigo))
                    {
                        _logger.LogWarning(
                            "Intento de crear cuenta con código duplicado: {Codigo}",
                            vm.Codigo);

                        TempData["MensajeError"] = $"Ya existe una cuenta con el código '{vm.Codigo}'.";
                        return RedirectToAction(nameof(Index));
                    }

                    var nuevaCuenta = new CuentaBancaria
                    {
                        Codigo = vm.Codigo,
                        Nombre = vm.Nombre,
                        Numero = vm.Numero,
                        Tipo = vm.Tipo,
                        Titular = vm.Titular,
                        Ruc = vm.Ruc,
                        LogoPath = vm.LogoPath ?? PLACEHOLDER_LOGO,
                        Activo = vm.Activo
                    };

                    cuentas.Add(nuevaCuenta);

                    _logger.LogInformation(
                        "Nueva cuenta bancaria creada. Código: {Codigo}, Nombre: {Nombre}, " +
                        "Activo: {Activo}, Usuario: {UserName}",
                        vm.Codigo,
                        vm.Nombre,
                        vm.Activo,
                        User.Identity?.Name);

                    TempData["MensajeExito"] = $"Cuenta '{vm.Nombre}' creada correctamente. " +
                        $"Estado: {(vm.Activo ? "Activa" : "Inactiva")}.";
                }
                else
                {
                    // UPDATE - Buscar cuenta existente
                    var cuentaExistente = cuentas.FirstOrDefault(c =>
                        NormalizeKey(c.Codigo) == normalizedOriginalCodigo);

                    if (cuentaExistente == null)
                    {
                        _logger.LogWarning(
                            "Cuenta no encontrada para actualizar. Código original: {CodigoOriginal}",
                            vm.OriginalCodigo);

                        TempData["MensajeError"] = "No se encontró la cuenta original para editar.";
                        return RedirectToAction(nameof(Index));
                    }

                    // Verificar conflicto de código si cambió
                    if (normalizedCodigo != normalizedOriginalCodigo &&
                        IsDuplicate(normalizedCodigo, cuentaExistente))
                    {
                        _logger.LogWarning(
                            "Intento de actualizar a código duplicado. Original: {Original}, Nuevo: {Nuevo}",
                            vm.OriginalCodigo,
                            vm.Codigo);

                        TempData["MensajeError"] = $"El código '{vm.Codigo}' ya pertenece a otra cuenta.";
                        return RedirectToAction(nameof(Index));
                    }

                    // Actualizar propiedades
                    cuentaExistente.Codigo = vm.Codigo;
                    cuentaExistente.Nombre = vm.Nombre;
                    cuentaExistente.Numero = vm.Numero;
                    cuentaExistente.Tipo = vm.Tipo;
                    cuentaExistente.Titular = vm.Titular;
                    cuentaExistente.Ruc = vm.Ruc;
                    cuentaExistente.LogoPath = vm.LogoPath ?? PLACEHOLDER_LOGO;
                    cuentaExistente.Activo = vm.Activo;

                    _logger.LogInformation(
                        "Cuenta bancaria actualizada. Código anterior: {CodigoAnterior}, " +
                        "Código nuevo: {CodigoNuevo}, Nombre: {Nombre}, Activo: {Activo}, " +
                        "Usuario: {UserName}",
                        vm.OriginalCodigo,
                        vm.Codigo,
                        vm.Nombre,
                        vm.Activo,
                        User.Identity?.Name);

                    TempData["MensajeExito"] = $"Cuenta '{vm.Nombre}' actualizada correctamente. " +
                        $"Estado: {(vm.Activo ? "Activa" : "Inactiva")}.";
                }

                // Guardar cambios
                await _bancosService.SetAdminAsync(cuentas);

                // Invalidar cache
                InvalidateCache();

                _logger.LogInformation(
                    "Operación {Operacion} completada exitosamente para cuenta: {Codigo}",
                    isCreate ? "CREATE" : "UPDATE",
                    vm.Codigo);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error al guardar cuenta bancaria. Código: {Codigo}, Activo: {Activo}, " +
                    "Usuario: {UserName}",
                    vm.Codigo,
                    vm.Activo,
                    User.Identity?.Name);

                TempData["MensajeError"] = "Ocurrió un error inesperado al guardar la cuenta bancaria. " +
                    "Por favor, intenta nuevamente.";
            }

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// POST: /Admin/Bancos/DeleteAdmin
        /// Elimina una cuenta bancaria
        /// </summary>
        [HttpPost("DeleteAdmin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAdmin([FromForm] string codigo)
        {
            if (string.IsNullOrWhiteSpace(codigo))
            {
                _logger.LogWarning("Intento de eliminar cuenta con código vacío");
                TempData["MensajeError"] = "Código inválido.";
                return RedirectToAction(nameof(Index));
            }

            var normalizedCodigo = NormalizeKey(codigo);

            try
            {
                var cuentas = (await GetCuentasConCacheAsync()).ToList();
                var cuentaAEliminar = cuentas.FirstOrDefault(c =>
                    NormalizeKey(c.Codigo) == normalizedCodigo);

                if (cuentaAEliminar == null)
                {
                    _logger.LogWarning(
                        "Cuenta no encontrada para eliminar. Código: {Codigo}",
                        codigo);

                    TempData["MensajeError"] = $"No se encontró la cuenta con código '{codigo}'.";
                    return RedirectToAction(nameof(Index));
                }

                // Log antes de eliminar para auditoría
                _logger.LogWarning(
                    "Eliminando cuenta bancaria. Código: {Codigo}, Nombre: {Nombre}, " +
                    "Usuario: {UserName}",
                    cuentaAEliminar.Codigo,
                    cuentaAEliminar.Nombre,
                    User.Identity?.Name);

                cuentas.Remove(cuentaAEliminar);
                await _bancosService.SetAdminAsync(cuentas);

                // Invalidar cache
                InvalidateCache();

                TempData["MensajeExito"] = $"Cuenta '{cuentaAEliminar.Nombre}' eliminada correctamente.";

                _logger.LogInformation(
                    "Cuenta bancaria eliminada exitosamente. Código: {Codigo}",
                    codigo);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error al eliminar cuenta bancaria. Código: {Codigo}, Usuario: {UserName}",
                    codigo,
                    User.Identity?.Name);

                TempData["MensajeError"] = "Ocurrió un error inesperado al eliminar la cuenta bancaria. " +
                    "Por favor, intenta nuevamente.";
            }

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// POST: /Admin/Bancos/ToggleActivo
        /// Activa o desactiva una cuenta bancaria
        /// </summary>
        [HttpPost("ToggleActivo")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActivo([FromForm] string codigo)
        {
            if (string.IsNullOrWhiteSpace(codigo))
            {
                _logger.LogWarning("Intento de toggle activo con código vacío");
                return Json(new { success = false, message = "Código inválido" });
            }

            var normalizedCodigo = NormalizeKey(codigo);

            try
            {
                var cuentas = (await GetCuentasConCacheAsync()).ToList();
                var cuenta = cuentas.FirstOrDefault(c =>
                    NormalizeKey(c.Codigo) == normalizedCodigo);

                if (cuenta == null)
                {
                    _logger.LogWarning("Cuenta no encontrada para toggle. Código: {Codigo}", codigo);
                    return Json(new { success = false, message = "Cuenta no encontrada" });
                }

                cuenta.Activo = !cuenta.Activo;
                await _bancosService.SetAdminAsync(cuentas);

                // Invalidar cache
                InvalidateCache();

                _logger.LogInformation(
                    "Estado de cuenta cambiado. Código: {Codigo}, Nuevo estado: {Activo}, " +
                    "Usuario: {UserName}",
                    codigo,
                    cuenta.Activo,
                    User.Identity?.Name);

                return Json(new
                {
                    success = true,
                    activo = cuenta.Activo,
                    message = $"Cuenta {(cuenta.Activo ? "activada" : "desactivada")} correctamente"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cambiar estado de cuenta. Código: {Codigo}", codigo);
                return Json(new { success = false, message = "Error al cambiar estado" });
            }
        }

        #endregion

        #region ViewModel

        /// <summary>
        /// ViewModel para crear/actualizar cuenta bancaria
        /// </summary>
        public sealed class AdminUpsertVm
        {
            /// <summary>
            /// Código original de la cuenta (usado para UPDATE)
            /// </summary>
            public string? OriginalCodigo { get; set; }

            /// <summary>
            /// Código único de la cuenta bancaria
            /// </summary>
            [Required(ErrorMessage = "El código es requerido")]
            [StringLength(MAX_CODIGO_LENGTH, MinimumLength = MIN_CODIGO_LENGTH,
                ErrorMessage = "El código debe tener entre {2} y {1} caracteres")]
            [Display(Name = "Código")]
            public string Codigo { get; set; } = string.Empty;

            /// <summary>
            /// Nombre del banco
            /// </summary>
            [Required(ErrorMessage = "El nombre es requerido")]
            [StringLength(MAX_NOMBRE_LENGTH,
                ErrorMessage = "El nombre no puede exceder {1} caracteres")]
            [Display(Name = "Nombre del Banco")]
            public string Nombre { get; set; } = string.Empty;

            /// <summary>
            /// Número de cuenta bancaria
            /// </summary>
            [Required(ErrorMessage = "El número de cuenta es requerido")]
            [StringLength(MAX_NUMERO_CUENTA_LENGTH, MinimumLength = MIN_NUMERO_CUENTA_LENGTH,
                ErrorMessage = "El número debe tener entre {2} y {1} caracteres")]
            [Display(Name = "Número de Cuenta")]
            public string Numero { get; set; } = string.Empty;

            /// <summary>
            /// Tipo de cuenta (Ahorros, Corriente, etc.)
            /// </summary>
            [Required(ErrorMessage = "El tipo de cuenta es requerido")]
            [StringLength(MAX_TIPO_CUENTA_LENGTH,
                ErrorMessage = "El tipo no puede exceder {1} caracteres")]
            [Display(Name = "Tipo de Cuenta")]
            public string Tipo { get; set; } = DEFAULT_TIPO_CUENTA;

            /// <summary>
            /// Titular de la cuenta
            /// </summary>
            [StringLength(MAX_TITULAR_LENGTH,
                ErrorMessage = "El titular no puede exceder {1} caracteres")]
            [Display(Name = "Titular")]
            public string? Titular { get; set; }

            /// <summary>
            /// RUC o Cédula del titular
            /// </summary>
            [StringLength(MAX_RUC_LENGTH,
                ErrorMessage = "El RUC/Cédula no puede exceder {1} caracteres")]
            [Display(Name = "RUC/Cédula")]
            public string? Ruc { get; set; }

            /// <summary>
            /// Ruta del logo del banco
            /// </summary>
            [StringLength(MAX_LOGO_PATH_LENGTH,
                ErrorMessage = "La ruta del logo no puede exceder {1} caracteres")]
            [Display(Name = "Logo")]
            public string? LogoPath { get; set; }

            /// <summary>
            /// Indica si la cuenta está activa
            /// </summary>
            [Display(Name = "Activo")]
            public bool Activo { get; set; } = true;
        }

        #endregion
    }
}
