using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Simone.Configuration;
using Simone.Services;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
// using Microsoft.AspNetCore.Authorization;

namespace Simone.Controllers
{
    // [Authorize(Roles = "Admin")]
    [Route("Admin/Bancos")]
    public class AdminBancosController : Controller
    {
        private readonly IBancosConfigService _svc;
        private readonly ILogger<AdminBancosController> _logger;

        public AdminBancosController(IBancosConfigService svc, ILogger<AdminBancosController> logger)
        {
            _svc = svc ?? throw new ArgumentNullException(nameof(svc));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // -----------------------------
        // Helpers
        // -----------------------------
        private static string NormKey(string? s) => (s ?? string.Empty).Trim().ToLowerInvariant();
        private static string TrimOrEmpty(string? s) => (s ?? string.Empty).Trim();
        private static string? TrimOrNull(string? s) => string.IsNullOrWhiteSpace(s) ? null : s!.Trim();

        private string ModelErrorsToString()
            => string.Join("; ",
               ModelState.Where(kv => kv.Value!.Errors.Count > 0)
                         .Select(kv => $"{kv.Key}: {string.Join(" | ", kv.Value!.Errors.Select(e => e.ErrorMessage))}"));

        // -----------------------------
        // Reglas de validación
        // -----------------------------
        private static readonly Regex CodigoRx = new(@"^[a-z0-9_-]{2,50}$", RegexOptions.Compiled);
        private static readonly Regex NumeroRx = new(@"^[0-9]{6,20}$", RegexOptions.Compiled); // solo dígitos
        private static readonly Regex Texto40Rx = new(@"^[^\r\n\t]{1,40}$", RegexOptions.Compiled);
        private static readonly Regex Texto120Rx = new(@"^[^\r\n\t]{1,120}$", RegexOptions.Compiled);
        private static readonly Regex RucRx = new(@"^\d{10}(\d{3})?$", RegexOptions.Compiled); // 10 o 13
        private static readonly Regex LogoRx = new(@"^[A-Za-z0-9_\-\/\.]{1,200}$", RegexOptions.Compiled);

        private (bool ok, string? msg) ValidarVM(AdminUpsertVm vm)
        {
            if (string.IsNullOrWhiteSpace(vm.Codigo) || string.IsNullOrWhiteSpace(vm.Nombre))
                return (false, "Selecciona un banco de la lista (código y nombre obligatorios).");

            if (!CodigoRx.IsMatch(vm.Codigo))
                return (false, "Código inválido. Usa minúsculas, números, '-' o '_' (2–50).");

            if (!Texto120Rx.IsMatch(vm.Nombre))
                return (false, "Nombre inválido (1–120, sin saltos de línea).");

            if (string.IsNullOrWhiteSpace(vm.Numero) || !NumeroRx.IsMatch(vm.Numero))
                return (false, "Número de cuenta inválido (solo dígitos, 6–20).");

            if (string.IsNullOrWhiteSpace(vm.Tipo) || !Texto40Rx.IsMatch(vm.Tipo))
                return (false, "Tipo de cuenta inválido.");

            if (!string.IsNullOrWhiteSpace(vm.Titular) && !Texto120Rx.IsMatch(vm.Titular!))
                return (false, "Titular inválido.");

            if (!string.IsNullOrWhiteSpace(vm.Ruc) && !RucRx.IsMatch(vm.Ruc!))
                return (false, "RUC/Cédula inválido(a).");

            if (!string.IsNullOrWhiteSpace(vm.LogoPath))
            {
                if (!LogoRx.IsMatch(vm.LogoPath!) || vm.LogoPath!.Contains("..") || vm.LogoPath!.Contains("://"))
                    return (false, "LogoPath inválido (ruta relativa, sin '..' ni protocolo).");
            }
            return (true, null);
        }

        // -----------------------------
        // Vista
        // -----------------------------
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            try
            {
                var list = (await _svc.GetAdminAsync())
                            .OrderBy(x => x.Nombre)
                            .ThenBy(x => x.Codigo)
                            .ToList();

                ViewBag.Cuentas = list;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cargando bancos del administrador");
                ViewBag.Cuentas = new List<CuentaBancaria>();
                TempData["MensajeError"] = "No se pudieron cargar las cuentas bancarias.";
            }

            ViewBag.MensajeExito = TempData["MensajeExito"];
            ViewBag.MensajeError = TempData["MensajeError"];
            ViewBag.ModelErrors = TempData["ModelErrors"];
            return View();
        }

        // -----------------------------
        // ViewModel (evitar over-posting)
        // -----------------------------
        public sealed class AdminUpsertVm
        {
            public string? OriginalCodigo { get; set; }

            [Required, MaxLength(50)] public string Codigo { get; set; } = "";
            [Required, MaxLength(120)] public string Nombre { get; set; } = "";
            [Required, MaxLength(20)] public string Numero { get; set; } = "";  // 6–20
            [Required, MaxLength(40)] public string Tipo { get; set; } = "Cuenta de Ahorros";

            [MaxLength(120)] public string? Titular { get; set; }
            [MaxLength(20)] public string? Ruc { get; set; }
            [MaxLength(200)] public string? LogoPath { get; set; }

            public bool Activo { get; set; } = true;
        }

        // -----------------------------
        // Acciones
        // -----------------------------
        [HttpPost("SaveAdmin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveAdmin([FromForm] AdminUpsertVm vm)
        {
            // 1) Normalización campos texto
            vm.OriginalCodigo = TrimOrNull(vm.OriginalCodigo);
            vm.Codigo = TrimOrEmpty(vm.Codigo).ToLowerInvariant();
            vm.Nombre = TrimOrEmpty(vm.Nombre);
            vm.Numero = TrimOrEmpty(vm.Numero);
            vm.Tipo = TrimOrEmpty(vm.Tipo);
            vm.Titular = TrimOrNull(vm.Titular);
            vm.Ruc = TrimOrNull(vm.Ruc);
            vm.LogoPath = TrimOrNull(vm.LogoPath);

            // 2) Fix binder checkbox "Activo" (on/true/1 → true; vacío → false)
            ModelState.Remove(nameof(AdminUpsertVm.Activo));
            if (Request.Form.TryGetValue(nameof(AdminUpsertVm.Activo), out var av))
            {
                var raw = (av.FirstOrDefault() ?? "").Trim().ToLowerInvariant();
                vm.Activo = raw is "true" or "on" or "1";
            }
            else vm.Activo = false;

            // 3) En altas, no queremos que OriginalCodigo invalide el ModelState
            ModelState.Remove(nameof(AdminUpsertVm.OriginalCodigo));

            // 4) Validación por DataAnnotations
            if (!ModelState.IsValid)
            {
                var errs = ModelErrorsToString();
                _logger.LogWarning("ModelState inválido en SaveAdmin: {Errs}", errs);
                TempData["MensajeError"] = "Datos inválidos en el formulario.";
                TempData["ModelErrors"] = errs;
                return RedirectToAction(nameof(Index));
            }

            // 5) Validación adicional de negocio
            var (ok, msg) = ValidarVM(vm);
            if (!ok)
            {
                TempData["MensajeError"] = msg!;
                TempData["ModelErrors"] = msg;
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var list = (await _svc.GetAdminAsync()).ToList();
                var nkey = NormKey(vm.Codigo);

                bool Duplicado(string key, CuentaBancaria? except = null)
                    => list.Any(x => !ReferenceEquals(x, except) && NormKey(x.Codigo) == key);

                if (string.IsNullOrEmpty(vm.OriginalCodigo))
                {
                    // ALTA
                    if (Duplicado(nkey))
                    {
                        TempData["MensajeError"] = "Ya existe una cuenta con ese código.";
                        return RedirectToAction(nameof(Index));
                    }

                    list.Add(new CuentaBancaria
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
                }
                else
                {
                    // EDICIÓN
                    var okey = NormKey(vm.OriginalCodigo);
                    var target = list.FirstOrDefault(x => NormKey(x.Codigo) == okey);
                    if (target is null)
                    {
                        TempData["MensajeError"] = "No se encontró la cuenta original para editar.";
                        return RedirectToAction(nameof(Index));
                    }

                    if (nkey != okey && Duplicado(nkey))
                    {
                        TempData["MensajeError"] = "El nuevo código ya pertenece a otra cuenta.";
                        return RedirectToAction(nameof(Index));
                    }

                    target.Codigo = vm.Codigo;
                    target.Nombre = vm.Nombre;
                    target.Numero = vm.Numero;
                    target.Tipo = vm.Tipo;
                    target.Titular = vm.Titular;
                    target.Ruc = vm.Ruc;
                    target.LogoPath = vm.LogoPath;
                    target.Activo = vm.Activo;
                }

                await _svc.SetAdminAsync(list);
                TempData["MensajeExito"] = "Cuenta guardada correctamente.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar cuenta bancaria (codigo: {Codigo})", vm.Codigo);
                TempData["MensajeError"] = "Ocurrió un error al guardar la cuenta bancaria.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost("DeleteAdmin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAdmin([FromForm][Required] string codigo)
        {
            var key = NormKey(codigo);
            if (string.IsNullOrEmpty(key))
            {
                TempData["MensajeError"] = "Código inválido.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var list = (await _svc.GetAdminAsync()).ToList();
                var idx = list.FindIndex(x => NormKey(x.Codigo) == key);

                if (idx >= 0)
                {
                    list.RemoveAt(idx);
                    await _svc.SetAdminAsync(list);
                    TempData["MensajeExito"] = "Cuenta eliminada.";
                }
                else
                {
                    TempData["MensajeError"] = "No se encontró la cuenta.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar cuenta bancaria (codigo: {Codigo})", codigo);
                TempData["MensajeError"] = "Ocurrió un error al eliminar la cuenta bancaria.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
