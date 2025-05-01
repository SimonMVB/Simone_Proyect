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
    public class CuentaController : Controller
    {
        private readonly UserManager<Usuario> _userManager;
        private readonly SignInManager<Usuario> _signInManager;
        private readonly RoleManager<Roles> _roleManager;
        private readonly ILogger<CuentaController> _logger;
        private readonly TiendaDbContext _context;

        // Constructor que inyecta las dependencias necesarias
        public CuentaController(UserManager<Usuario> userManager,
                                SignInManager<Usuario> signInManager,
                                RoleManager<Roles> roleManager,
                                ILogger<CuentaController> logger,
                                TiendaDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _logger = logger;
            _context = context;
        }

        /// <summary>
        /// Acción GET para mostrar la vista de inicio de sesión.
        /// Redirige al Home si el usuario ya está autenticado.
        /// </summary>
        /// <returns>Vista de inicio de sesión o redirección a Home.</returns>
        [HttpGet]
        public IActionResult Login()
        {
            bool sesionIniciada = User.Identity.IsAuthenticated;

            // Si el usuario ya está autenticado, redirige al índice.
            if (sesionIniciada)
            {
                return RedirectToAction("Index", "Home");
            }

            // Genera un ID único para la solicitud de inicio de sesión
            var requestId = Guid.NewGuid().ToString();
            ViewData["RequestID"] = requestId;

            return View();
        }

        /// <summary>
        /// Acción POST que maneja el inicio de sesión de un usuario.
        /// Verifica las credenciales y redirige según el rol del usuario.
        /// </summary>
        /// <param name="model">Modelo de inicio de sesión que contiene el correo y la contraseña del usuario.</param>
        /// <returns>Redirige a distintas vistas según el rol del usuario o muestra un error.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            bool loginExitoso = false;

            if (user != null && user.Activo)
            {
                var result = await _signInManager.PasswordSignInAsync(user.UserName, model.Password, model.RememberMe, lockoutOnFailure: true);
                loginExitoso = result.Succeeded;

                if (loginExitoso)
                {
                    _logger.LogInformation("Usuario autenticado: {Email}", model.Email);
                    await RegistrarLog(model.Email, true);

                    return RedirectToAction("Index", "Home");
                }

                // Si la cuenta está bloqueada
                if (result.IsLockedOut)
                {
                    _logger.LogWarning("Cuenta bloqueada: {Email}", model.Email);
                    await RegistrarLog(model.Email, false);
                    return View("Lockout");
                }

                _logger.LogWarning("Credenciales incorrectas: {Email}", model.Email);
                ModelState.AddModelError(string.Empty, "Email o contraseña incorrectos.");
            }
            else
            {
                _logger.LogWarning("Intento de login con usuario inexistente o inactivo: {Email}", model.Email);
                ModelState.AddModelError(string.Empty, "El usuario no existe o está inactivo.");
            }

            // Registrar el intento de login
            await RegistrarLog(model.Email, loginExitoso);
            return View(model);
        }

        /// <summary>
        /// Registra los intentos de inicio de sesión en la base de datos.
        /// </summary>
        /// <param name="email">El correo del usuario que intentó iniciar sesión.</param>
        /// <param name="exitoso">Indica si el inicio de sesión fue exitoso.</param>
        /// <returns>Un task que completa la operación de guardar el log en la base de datos.</returns>
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
        /// Acción GET para mostrar la vista de registro de un nuevo usuario.
        /// Redirige al Home si el usuario ya está autenticado.
        /// </summary>
        /// <returns>Vista de registro o redirección a Home.</returns>
        [HttpGet]
        public IActionResult Registrar()
        {
            bool sesionIniciada = User.Identity.IsAuthenticated;
            
            // Si el usuario ya está autenticado, redirige al índice.
            if (sesionIniciada)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        /// <summary>
        /// Acción POST para registrar un nuevo usuario en el sistema.
        /// </summary>
        /// <param name="model">Modelo de registro que contiene la información del usuario.</param>
        /// <returns>Redirige al Home si el registro es exitoso, o muestra los errores de registro.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Registrar(RegistroViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            // Verifica si el rol "Cliente" existe
            var rol = await _roleManager.FindByNameAsync("Cliente");

            var usuario = new Usuario
            {
                NombreCompleto = model.Nombre,
                Email = model.Email,
                UserName = model.Email,
                EmailConfirmed = true,
                FechaRegistro = DateTime.Now,
                Activo = true,
                RolID = rol.Id,
            };

            var result = await _userManager.CreateAsync(usuario, model.Password);

            if (result.Succeeded)
            {
                // Asigna el rol "Cliente" por defecto
                await _userManager.AddToRoleAsync(usuario, "Cliente");

                _logger.LogInformation("Usuario registrado: {Email}", model.Email);
                await _signInManager.SignInAsync(usuario, isPersistent: false);
                return RedirectToAction("Index", "Home");
            }

            // Registrar cualquier error durante el registro
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
                _logger.LogError("Error al registrar {Email}: {Error}", model.Email, error.Description);
            }

            return View(model);
        }

        /// <summary>
        /// Acción POST para cerrar la sesión de un usuario.
        /// </summary>
        /// <returns>Redirige al login después de cerrar la sesión.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            _logger.LogInformation("Sesión cerrada.");
            return RedirectToAction("Login", "Cuenta");
        }

        /// <summary>
        /// Acción GET que muestra la vista de acceso denegado.
        /// </summary>
        /// <returns>Vista de acceso denegado.</returns>
        [HttpGet]
        public IActionResult AccesoDenegado()
        {
            return View();
        }

        /// <summary>
        /// Acción GET que muestra la vista del perfil del usuario.
        /// </summary>
        /// <returns>Vista con la información del perfil del usuario.</returns>
        [HttpGet]
        public async Task<IActionResult> Perfil()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                TempData["MensajeError"] = "No se encontró al usuario.";
                return RedirectToAction("Login");
            } else {
                TempData["MensajeExito"] = "Usuario encontrado exitosamente.";
            }

            var roles = await _userManager.GetRolesAsync(usuario);
            ViewBag.RolUsuario = roles.FirstOrDefault() ?? "Sin rol";
            ViewBag.Usuario = usuario;

            return View();
        }

        /// <summary>
        /// Acción POST que maneja la actualización del perfil del usuario.
        /// </summary>
        /// <param name="model">Modelo con la nueva información del usuario.</param>
        /// <returns>Redirige al perfil después de la actualización o muestra un error si la operación falla.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Perfil(Usuario model)
        {
            if (!ModelState.IsValid)
            {
                TempData["MensajeError"] = "Corrige los errores antes de guardar.";
                return View(model);
            }

            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                TempData["MensajeError"] = "No se encontró el usuario.";
                return RedirectToAction("Login");
            }

            // Solo se permite editar el NombreCompleto por seguridad
            usuario.NombreCompleto = model.NombreCompleto;

            var result = await _userManager.UpdateAsync(usuario);

            if (result.Succeeded)
            {
                TempData["MensajeExito"] = "Tu perfil ha sido actualizado correctamente.";
            }
            else
            {
                TempData["MensajeError"] = "Ocurrió un error al guardar los cambios.";
            }

            var roles = await _userManager.GetRolesAsync(usuario);
            ViewBag.RolUsuario = roles.FirstOrDefault() ?? "Sin rol";
            ViewData["Usuario"] = usuario;

            return View();
        }

        /// <summary>
        /// Acción GET que muestra la vista de olvidé mi contraseña.
        /// </summary>
        /// <returns>Vista para recuperar la contraseña.</returns>
        [HttpGet]
        public IActionResult OlvidePassword()
        {
            return View();
        }

        /// <summary>
        /// Acción POST que maneja la recuperación de la contraseña.
        /// </summary>
        /// <param name="model">Modelo que contiene el correo del usuario.</param>
        /// <returns>Redirige al login después de enviar el enlace de recuperación.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OlvidePassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null || !(await _userManager.IsEmailConfirmedAsync(user)))
            {
                TempData["MensajeExito"] = "Si el correo existe, se ha enviado un enlace de recuperación.";
                return RedirectToAction("Login");
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var callbackUrl = Url.Action("ResetPassword", "Cuenta", new { email = user.Email, token = token }, protocol: HttpContext.Request.Scheme);

            // 🔧 Aquí deberías enviar un correo real
            _logger.LogWarning("Token de recuperación para {Email}: {Link}", user.Email, callbackUrl);

            TempData["MensajeExito"] = "Te hemos enviado un enlace de recuperación (o revisa la consola).";
            return RedirectToAction("Login");
        }
    }
}
