using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Simone.Models;
using Simone.ViewModels;
using Simone.Data;
using System;
using System.Threading.Tasks;

namespace Simone.Controllers
{
    /// <summary>
    /// Controlador para gestionar la autenticación y registro de usuarios.
    /// </summary>
    public class CuentaController : Controller
    {
        private readonly UserManager<Usuario> _userManager;
        private readonly SignInManager<Usuario> _signInManager;
        private readonly ILogger<CuentaController> _logger;
        private readonly TiendaDbContext _context;

        public CuentaController(UserManager<Usuario> userManager, SignInManager<Usuario> signInManager, ILogger<CuentaController> logger, TiendaDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            _context = context;
        }

        /// <summary>
        /// Muestra la vista de inicio de sesión.
        /// </summary>
        /// <returns>Vista de Login</returns>
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        /// <summary>
        /// Procesa el inicio de sesión.
        /// </summary>
        /// <param name="model">Modelo de Login</param>
        /// <returns>Redirige a la página principal o muestra errores</returns>
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

        /// <summary>
        /// Muestra la vista de registro de usuario.
        /// </summary>
        /// <returns>Vista de Registro</returns>
        [HttpGet]
        public IActionResult Registrar()
        {
            return View();
        }

        /// <summary>
        /// Procesa el registro de un nuevo usuario.
        /// </summary>
        /// <param name="model">Modelo de Registro</param>
        /// <returns>Redirige a la página principal o muestra errores</returns>
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

        /// <summary>
        /// Procesa el cierre de sesión del usuario.
        /// </summary>
        /// <returns>Redirige a la página principal</returns>
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

