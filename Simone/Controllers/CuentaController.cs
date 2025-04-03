using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Simone.Models;
using Simone.ViewModels;
using Simone.Data;
using System;
using System.Threading.Tasks;
using System.Linq;

namespace Simone.Controllers
{
    /// <summary>
    /// Controlador para gestionar la autenticación y registro de usuarios.
    /// </summary>
    public class CuentaController : Controller
    {
        private readonly UserManager<Usuario> _userManager;
        private readonly SignInManager<Usuario> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ILogger<CuentaController> _logger;
        private readonly TiendaDbContext _context;

        public CuentaController(UserManager<Usuario> userManager, SignInManager<Usuario> signInManager, RoleManager<IdentityRole> roleManager, ILogger<CuentaController> logger, TiendaDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _logger = logger;
            _context = context;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            bool loginExitoso = false;

            if (user != null)
            {
                var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);

                loginExitoso = result.Succeeded;

                if (result.Succeeded)
                {
                    _logger.LogInformation("El usuario {Email} inició sesión correctamente.", model.Email);
                    await RegistrarLog(model.Email, true);

                    var roles = await _userManager.GetRolesAsync(user);
                    if (roles.Contains("Administrador"))
                        return RedirectToAction("Panel", "Admin");
                    else if (roles.Contains("Empleado"))
                        return RedirectToAction("Dashboard", "Empleado");
                    else
                        return RedirectToAction("Index", "Home");
                }
                else if (result.IsLockedOut)
                {
                    _logger.LogWarning("La cuenta del usuario {Email} se encuentra bloqueada.", model.Email);
                    await RegistrarLog(model.Email, false);
                    return View("Lockout");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Inicio de sesión inválido.");
                    _logger.LogWarning("Intento fallido de inicio de sesión para {Email}.", model.Email);
                }
            }
            else
            {
                ModelState.AddModelError(string.Empty, "El usuario no existe.");
                _logger.LogWarning("Intento de inicio con usuario inexistente: {Email}.", model.Email);
            }

            await RegistrarLog(model.Email, loginExitoso);
            return View(model);
        }

        private async Task RegistrarLog(string email, bool exitoso)
        {
            var log = new LogIniciosSesion
            {
                Usuario = email,
                FechaInicio = DateTime.Now,
                Exitoso = exitoso
            };

            _context.LogIniciosSesion.Add(log);
            await _context.SaveChangesAsync();
        }

        [HttpGet]
        public IActionResult Registrar()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Registro(RegistroViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var usuario = new Usuario
            {
                NombreUsuario = model.Nombre,
                Email = model.Email,
                UserName = model.Email,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(usuario, model.Password);

            if (result.Succeeded)
            {
                // Crear roles si no existen
                if (!await _roleManager.RoleExistsAsync("Administrador"))
                    await _roleManager.CreateAsync(new IdentityRole("Administrador"));
                if (!await _roleManager.RoleExistsAsync("Empleado"))
                    await _roleManager.CreateAsync(new IdentityRole("Empleado"));
                if (!await _roleManager.RoleExistsAsync("Comprador"))
                    await _roleManager.CreateAsync(new IdentityRole("Comprador"));

                // Asignar rol de Comprador por defecto
                await _userManager.AddToRoleAsync(usuario, "Comprador");

                _logger.LogInformation("El usuario {Email} se registró correctamente.", model.Email);
                await _signInManager.SignInAsync(usuario, isPersistent: false);
                return RedirectToAction("Index", "Home");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
                _logger.LogError("Error al registrar el usuario {Email}: {Error}", model.Email, error.Description);
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            _logger.LogInformation("El usuario cerró sesión.");
            return RedirectToAction("Index", "Home");
        }
    }
}
