using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Simone.Models;
using Simone.Data;
using Simone.Configuration;
using Simone.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Cfg = Simone.Configuration;

namespace Simone.Controllers
{
    // Admin también puede ingresar para ayudar a un vendedor
    [Authorize(Roles = "Administrador,Vendedor")]
    public class VendedorController : Controller
    {
        private readonly UserManager<Usuario> _userManager;
        private readonly RoleManager<Roles> _roleManager;
        private readonly ILogger<VendedorController> _logger;
        private readonly TiendaDbContext _context;
        private readonly IBancosConfigService _bancos;

        public VendedorController(
            UserManager<Usuario> userManager,
            RoleManager<Roles> roleManager,
            ILogger<VendedorController> logger,
            TiendaDbContext context,
            IBancosConfigService bancos)
        {
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _bancos = bancos ?? throw new ArgumentNullException(nameof(bancos));
        }

        // =========================
        // Productos (existente)
        // =========================
        public IActionResult Productos() => View();
        public IActionResult AnadirProducto() => View();

        // =========================
        // Mis Cuentas Bancarias (NUEVO)
        // =========================

        // VM que el vendedor puede editar (sin Codigo/LogoPath)
        public sealed class BancoUpsertVm
        {
            public string? Numero { get; set; } = string.Empty;
            public string? Nombre { get; set; } = string.Empty;          // Banco
            public string? Tipo { get; set; } = "Cuenta de Ahorros";     // Texto corto
            public string? Titular { get; set; }                         // Opcional
            public string? Ruc { get; set; }                             // Opcional (10 o 13 dígitos)
            public bool Activo { get; set; } = true;
        }

        // Reglas básicas (alineadas con AdminBancosController)
        private static readonly Regex NumeroCuentaRegex = new(@"^[0-9]{6,20}$", RegexOptions.Compiled);
        private static readonly Regex Texto40Regex = new(@"^.{1,40}$", RegexOptions.Compiled);
        private static readonly Regex Texto120Regex = new(@"^.{1,120}$", RegexOptions.Compiled);
        private static readonly Regex RucRegex = new(@"^\d{10}(\d{3})?$", RegexOptions.Compiled);

        private static string TOrEmpty(string? s) => (s ?? string.Empty).Trim();
        private static string? TOrNull(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        [HttpGet]
        public async Task<IActionResult> Bancos()
        {
            var uid = _userManager.GetUserId(User)!;

            // Carga JSON del vendedor: App_Data/bancos-proveedor-{uid}.json
            var cuentas = (await _bancos.GetByProveedorAsync(uid))?.ToList()
                ?? new List<Cfg.CuentaBancaria>();

            // Mensajes
            ViewBag.MensajeExito = TempData["MensajeExito"];
            ViewBag.MensajeError = TempData["MensajeError"];

            return View(cuentas);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarBanco([FromForm] BancoUpsertVm vm)
        {
            var uid = _userManager.GetUserId(User)!;

            // Normalizar
            vm.Numero = TOrEmpty(vm.Numero);
            vm.Nombre = TOrEmpty(vm.Nombre);
            vm.Tipo = TOrEmpty(vm.Tipo);
            vm.Titular = TOrNull(vm.Titular);
            vm.Ruc = TOrNull(vm.Ruc);

            // Validar
            if (string.IsNullOrWhiteSpace(vm.Numero) || !NumeroCuentaRegex.IsMatch(vm.Numero))
            {
                TempData["MensajeError"] = "Número de cuenta inválido (solo dígitos, 6-20).";
                return RedirectToAction(nameof(Bancos));
            }
            if (string.IsNullOrWhiteSpace(vm.Nombre) || !Texto120Regex.IsMatch(vm.Nombre))
            {
                TempData["MensajeError"] = "Nombre de banco inválido (máximo 120).";
                return RedirectToAction(nameof(Bancos));
            }
            if (string.IsNullOrWhiteSpace(vm.Tipo) || !Texto40Regex.IsMatch(vm.Tipo))
            {
                TempData["MensajeError"] = "Tipo de cuenta inválido.";
                return RedirectToAction(nameof(Bancos));
            }
            if (!string.IsNullOrWhiteSpace(vm.Titular) && !Texto120Regex.IsMatch(vm.Titular))
            {
                TempData["MensajeError"] = "Titular inválido (máximo 120).";
                return RedirectToAction(nameof(Bancos));
            }
            if (!string.IsNullOrWhiteSpace(vm.Ruc) && !RucRegex.IsMatch(vm.Ruc))
            {
                TempData["MensajeError"] = "RUC/Cédula inválido (10 o 13 dígitos).";
                return RedirectToAction(nameof(Bancos));
            }

            // Upsert por Número
            try
            {
                var cuentas = (await _bancos.GetByProveedorAsync(uid))?.ToList() ?? new List<Cfg.CuentaBancaria>();
                var existente = cuentas.FirstOrDefault(c => string.Equals(c.Numero, vm.Numero, StringComparison.Ordinal));

                if (existente == null)
                {
                    cuentas.Add(new Cfg.CuentaBancaria
                    {
                        // Codigo/LogoPath NO los edita el vendedor
                        Nombre = vm.Nombre,
                        Numero = vm.Numero,
                        Tipo = vm.Tipo,
                        Titular = vm.Titular,
                        Ruc = vm.Ruc,
                        Activo = vm.Activo
                    });
                }
                else
                {
                    existente.Nombre = vm.Nombre;
                    existente.Tipo = vm.Tipo;
                    existente.Titular = vm.Titular;
                    existente.Ruc = vm.Ruc;
                    existente.Activo = vm.Activo;
                }

                // Guardar archivo del vendedor
                await _bancos.SetByProveedorAsync(uid, cuentas);

                TempData["MensajeExito"] = "Cuenta guardada.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error guardando cuenta bancaria del vendedor {uid}", uid);
                TempData["MensajeError"] = "No se pudo guardar la cuenta.";
            }

            return RedirectToAction(nameof(Bancos));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarBanco([FromForm] string numero)
        {
            var uid = _userManager.GetUserId(User)!;

            try
            {
                var cuentas = (await _bancos.GetByProveedorAsync(uid))?.ToList() ?? new List<Cfg.CuentaBancaria>();
                cuentas = cuentas.Where(c => !string.Equals(c.Numero, numero, StringComparison.Ordinal)).ToList();

                await _bancos.SetByProveedorAsync(uid, cuentas);

                TempData["MensajeExito"] = "Cuenta eliminada.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error eliminando cuenta bancaria (uid: {uid}, numero: {numero})", uid, numero);
                TempData["MensajeError"] = "No se pudo eliminar la cuenta.";
            }

            return RedirectToAction(nameof(Bancos));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleBanco([FromForm] string numero)
        {
            var uid = _userManager.GetUserId(User)!;

            try
            {
                var cuentas = (await _bancos.GetByProveedorAsync(uid))?.ToList() ?? new List<Cfg.CuentaBancaria>();
                var cta = cuentas.FirstOrDefault(c => string.Equals(c.Numero, numero, StringComparison.Ordinal));
                if (cta != null) cta.Activo = !cta.Activo;

                await _bancos.SetByProveedorAsync(uid, cuentas);

                TempData["MensajeExito"] = "Estado actualizado.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error alternando estado de cuenta bancaria (uid: {uid}, numero: {numero})", uid, numero);
                TempData["MensajeError"] = "No se pudo actualizar el estado.";
            }

            return RedirectToAction(nameof(Bancos));
        }
    }
}
