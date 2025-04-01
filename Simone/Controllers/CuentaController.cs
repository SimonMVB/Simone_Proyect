using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Simone.Models;
using Simone.ViewModels;
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

        public CuentaController(UserManager<Usuario> userManager, SignInManager<Usuario> signInManager, ILogger<CuentaController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
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

            // Intentar iniciar sesión usando el email y contraseña proporcionados
            var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);

            if (result.Succeeded)
            {
                _logger.LogInformation("El usuario {Email} inició sesión correctamente.", model.Email);
                return RedirectToAction("Index", "Home");
            }
            else if (result.IsLockedOut)
            {
                _logger.LogWarning("La cuenta del usuario {Email} se encuentra bloqueada.", model.Email);
                return View("Lockout");
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Inicio de sesión inválido.");
                _logger.LogWarning("Intento fallido de inicio de sesión para {Email}.", model.Email);
                return View(model);
            }
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
