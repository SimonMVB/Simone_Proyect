// Simone/Models/AdminBancosController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Simone.Configuration;
using Simone.Services;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Simone.Controllers
{
    [Authorize(Roles = "Administrador")]
    [Route("Admin/Bancos")]
    public class AdminBancosController : Controller
    {
        private readonly IBancosConfigService _svc;
        private readonly ILogger<AdminBancosController> _logger;
        private readonly IWebHostEnvironment _env;

        public AdminBancosController(
            IBancosConfigService svc,
            ILogger<AdminBancosController> logger,
            IWebHostEnvironment env)
        {
            _svc = svc ?? throw new ArgumentNullException(nameof(svc));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _env = env ?? throw new ArgumentNullException(nameof(env));
        }

        // -----------------------------
        // Helpers
        // -----------------------------
        private static string NormalizeKey(string? s) => (s ?? string.Empty).Trim().ToLowerInvariant();
        private static string TrimOrEmpty(string? s) => (s ?? string.Empty).Trim();
        private static string? TrimOrNull(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        private string GetModelErrors()
        {
            var errors = ModelState
                .Where(kv => kv.Value?.Errors.Count > 0)
                .Select(kv => $"{kv.Key}: {string.Join(" | ", kv.Value!.Errors.Select(e => e.ErrorMessage))}");

            return string.Join("; ", errors);
        }

        // -----------------------------
        // Validation rules
        // -----------------------------
        private static readonly Regex CodigoRegex = new(@"^[a-z0-9_-]{2,50}$", RegexOptions.Compiled);
        private static readonly Regex NumeroCuentaRegex = new(@"^[0-9]{6,20}$", RegexOptions.Compiled);
        private static readonly Regex Texto40Regex = new(@"^.{1,40}$", RegexOptions.Compiled);
        private static readonly Regex Texto120Regex = new(@"^.{1,120}$", RegexOptions.Compiled);
        private static readonly Regex RucRegex = new(@"^\d{10}(\d{3})?$", RegexOptions.Compiled);

        private (bool IsValid, string? ErrorMessage) ValidateViewModel(AdminUpsertVm vm)
        {
            if (string.IsNullOrWhiteSpace(vm.Codigo) || string.IsNullOrWhiteSpace(vm.Nombre))
                return (false, "Selecciona un banco de la lista (código y nombre obligatorios).");

            if (!CodigoRegex.IsMatch(vm.Codigo))
                return (false, "Código inválido. Usa minúsculas, números, '-' o '_' (2-50 caracteres).");

            if (!Texto120Regex.IsMatch(vm.Nombre))
                return (false, "Nombre inválido (máximo 120 caracteres).");

            if (string.IsNullOrWhiteSpace(vm.Numero) || !NumeroCuentaRegex.IsMatch(vm.Numero))
                return (false, "Número de cuenta inválido (solo dígitos, 6-20 caracteres).");

            if (string.IsNullOrWhiteSpace(vm.Tipo) || !Texto40Regex.IsMatch(vm.Tipo))
                return (false, "Tipo de cuenta inválido.");

            if (!string.IsNullOrWhiteSpace(vm.Titular) && !Texto120Regex.IsMatch(vm.Titular))
                return (false, "Titular inválido (máximo 120 caracteres).");

            if (!string.IsNullOrWhiteSpace(vm.Ruc) && !RucRegex.IsMatch(vm.Ruc))
                return (false, "RUC/Cédula inválido (debe tener 10 o 13 dígitos).");

            // Validación mejorada para LogoPath
            if (!string.IsNullOrWhiteSpace(vm.LogoPath))
            {
                // Verificar que la ruta sea segura y válida
                if (vm.LogoPath.Contains("..") || vm.LogoPath.Contains("://") ||
                    vm.LogoPath.Contains("\\") || vm.LogoPath.Length > 200)
                    return (false, "Ruta del logo inválida.");

                // Verificar que el archivo exista físicamente
                var fullPath = Path.Combine(_env.WebRootPath, vm.LogoPath.TrimStart('/'));
                if (!System.IO.File.Exists(fullPath))
                {
                    _logger.LogWarning("Logo no encontrado en ruta: {LogoPath}", fullPath);
                    // No fallamos aquí, solo usamos placeholder
                    vm.LogoPath = "/images/Bancos/placeholder.png";
                }
            }

            return (true, null);
        }

        // -----------------------------
        // Views
        // -----------------------------
        [HttpGet("", Name = "AdminBancos_Index")]
        public async Task<IActionResult> Index()
        {
            if (User.IsInRole("Vendedor") && !User.IsInRole("Administrador"))
                return RedirectToAction("Bancos", "Vendedor");

            try
            {
                var cuentas = await _svc.GetAdminAsync();
                ViewBag.Cuentas = cuentas.OrderBy(x => x.Nombre).ThenBy(x => x.Codigo).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cargando cuentas bancarias del administrador");
                ViewBag.Cuentas = new List<CuentaBancaria>();
                TempData["MensajeError"] = "No se pudieron cargar las cuentas bancarias.";
            }

            // Preserve temp data for view
            ViewBag.MensajeExito = TempData["MensajeExito"];
            ViewBag.MensajeError = TempData["MensajeError"];
            ViewBag.ModelErrors = TempData["ModelErrors"];

            return View();
        }

        // -----------------------------
        // ViewModel
        // -----------------------------
        public sealed class AdminUpsertVm
        {
            public string? OriginalCodigo { get; set; }

            [Required(ErrorMessage = "El código es requerido")]
            [MaxLength(50)]
            public string Codigo { get; set; } = string.Empty;

            [Required(ErrorMessage = "El nombre es requerido")]
            [MaxLength(120)]
            public string Nombre { get; set; } = string.Empty;

            [Required(ErrorMessage = "El número de cuenta es requerido")]
            [MaxLength(20)]
            public string Numero { get; set; } = string.Empty;

            [Required(ErrorMessage = "El tipo de cuenta es requerido")]
            [MaxLength(40)]
            public string Tipo { get; set; } = "Cuenta de Ahorros";

            [MaxLength(120)]
            public string? Titular { get; set; }

            [MaxLength(20)]
            public string? Ruc { get; set; }

            [MaxLength(200)]
            public string? LogoPath { get; set; }

            public bool Activo { get; set; } = true;
        }

        // -----------------------------
        // Actions - CORREGIDO EL PROBLEMA DEL CHECKBOX
        // -----------------------------
        [HttpPost("SaveAdmin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveAdmin([FromForm] AdminUpsertVm vm)
        {
            if (vm == null)
            {
                TempData["MensajeError"] = "Datos del formulario inválidos.";
                return RedirectToAction(nameof(Index));
            }

            // **CORRECCIÓN CRÍTICA: Manejo correcto del checkbox Activo**
            // En ASP.NET Core, los checkboxes solo se envían cuando están marcados
            // Si no está marcado, la propiedad viene como false por defecto
            // No necesitamos manipular Request.Form manualmente

            // Normalize input
            vm.OriginalCodigo = TrimOrNull(vm.OriginalCodigo);
            vm.Codigo = TrimOrEmpty(vm.Codigo).ToLowerInvariant();
            vm.Nombre = TrimOrEmpty(vm.Nombre);
            vm.Numero = TrimOrEmpty(vm.Numero);
            vm.Tipo = TrimOrEmpty(vm.Tipo);
            vm.Titular = TrimOrNull(vm.Titular);
            vm.Ruc = TrimOrNull(vm.Ruc);
            vm.LogoPath = TrimOrNull(vm.LogoPath);

            // **ELIMINADO: Manipulación manual del checkbox que causaba el problema**
            // El model binding de ASP.NET Core ya maneja correctamente el valor del checkbox

            // Remove OriginalCodigo from ModelState to avoid validation issues
            ModelState.Remove(nameof(AdminUpsertVm.OriginalCodigo));
            ModelState.Remove(nameof(AdminUpsertVm.Activo)); // También removemos Activo del ModelState

            // Basic model validation
            if (!ModelState.IsValid)
            {
                var errors = GetModelErrors();
                _logger.LogWarning("ModelState inválido en SaveAdmin: {Errors}", errors);
                TempData["MensajeError"] = "Por favor, corrige los errores en el formulario.";
                TempData["ModelErrors"] = errors;
                return RedirectToAction(nameof(Index));
            }

            // Business logic validation
            var validationResult = ValidateViewModel(vm);
            if (!validationResult.IsValid)
            {
                TempData["MensajeError"] = validationResult.ErrorMessage;
                TempData["ModelErrors"] = validationResult.ErrorMessage;
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var cuentas = (await _svc.GetAdminAsync()).ToList();
                var normalizedCodigo = NormalizeKey(vm.Codigo);
                var normalizedOriginalCodigo = NormalizeKey(vm.OriginalCodigo);

                // Check for duplicates
                bool IsDuplicate(string codigo, CuentaBancaria? exclude = null)
                {
                    return cuentas.Any(c =>
                        !ReferenceEquals(c, exclude) &&
                        NormalizeKey(c.Codigo) == codigo);
                }

                if (string.IsNullOrEmpty(vm.OriginalCodigo))
                {
                    // CREATE - Check for existing code
                    if (IsDuplicate(normalizedCodigo))
                    {
                        TempData["MensajeError"] = "Ya existe una cuenta con ese código.";
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
                        LogoPath = vm.LogoPath,
                        Activo = vm.Activo // **AHORA SE PRESERVA CORRECTAMENTE**
                    };

                    cuentas.Add(nuevaCuenta);
                    _logger.LogInformation("Nueva cuenta bancaria creada: {Codigo}, Activo: {Activo}", vm.Codigo, vm.Activo);
                }
                else
                {
                    // UPDATE - Find existing account
                    var cuentaExistente = cuentas.FirstOrDefault(c =>
                        NormalizeKey(c.Codigo) == normalizedOriginalCodigo);

                    if (cuentaExistente == null)
                    {
                        TempData["MensajeError"] = "No se encontró la cuenta original para editar.";
                        return RedirectToAction(nameof(Index));
                    }

                    // Check if new code conflicts with another account
                    if (normalizedCodigo != normalizedOriginalCodigo && IsDuplicate(normalizedCodigo, cuentaExistente))
                    {
                        TempData["MensajeError"] = "El nuevo código ya pertenece a otra cuenta.";
                        return RedirectToAction(nameof(Index));
                    }

                    // Update properties
                    cuentaExistente.Codigo = vm.Codigo;
                    cuentaExistente.Nombre = vm.Nombre;
                    cuentaExistente.Numero = vm.Numero;
                    cuentaExistente.Tipo = vm.Tipo;
                    cuentaExistente.Titular = vm.Titular;
                    cuentaExistente.Ruc = vm.Ruc;
                    cuentaExistente.LogoPath = vm.LogoPath;
                    cuentaExistente.Activo = vm.Activo; // **AHORA SE ACTUALIZA CORRECTAMENTE**

                    _logger.LogInformation("Cuenta bancaria actualizada: {Codigo}, Activo: {Activo}", vm.Codigo, vm.Activo);
                }

                await _svc.SetAdminAsync(cuentas);
                TempData["MensajeExito"] = $"Cuenta {(string.IsNullOrEmpty(vm.OriginalCodigo) ? "creada" : "actualizada")} correctamente. Estado: {(vm.Activo ? "Activa" : "Inactiva")}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar cuenta bancaria (Código: {Codigo}, Activo: {Activo})", vm.Codigo, vm.Activo);
                TempData["MensajeError"] = "Ocurrió un error inesperado al guardar la cuenta bancaria.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost("DeleteAdmin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAdmin([FromForm] string codigo)
        {
            if (string.IsNullOrWhiteSpace(codigo))
            {
                TempData["MensajeError"] = "Código inválido.";
                return RedirectToAction(nameof(Index));
            }

            var normalizedCodigo = NormalizeKey(codigo);

            try
            {
                var cuentas = (await _svc.GetAdminAsync()).ToList();
                var cuentaAEliminar = cuentas.FirstOrDefault(c =>
                    NormalizeKey(c.Codigo) == normalizedCodigo);

                if (cuentaAEliminar != null)
                {
                    cuentas.Remove(cuentaAEliminar);
                    await _svc.SetAdminAsync(cuentas);
                    TempData["MensajeExito"] = "Cuenta eliminada correctamente.";
                }
                else
                {
                    TempData["MensajeError"] = "No se encontró la cuenta especificada.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar cuenta bancaria (Código: {Codigo})", codigo);
                TempData["MensajeError"] = "Ocurrió un error inesperado al eliminar la cuenta bancaria.";
            }

            return RedirectToAction(nameof(Index));
        }

        // Nuevo método para manejar imágenes faltantes
        private string GetSafeLogoPath(string? logoPath)
        {
            if (string.IsNullOrWhiteSpace(logoPath))
                return "/images/Bancos/placeholder.png";

            var fullPath = Path.Combine(_env.WebRootPath, logoPath.TrimStart('/'));
            return System.IO.File.Exists(fullPath) ? logoPath : "/images/Bancos/placeholder.png";
        }
    }
}