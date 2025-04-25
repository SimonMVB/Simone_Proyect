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

        // GET: /Cuenta/Login
        [HttpGet]
        public IActionResult Login()
        {
            bool sesionIniciada = User.Identity.IsAuthenticated;
            // Si ya hay sesion iniciada
            if (sesionIniciada)
            {
                // Envia el usuario al index.
                return RedirectToAction("Index", "Home");
            }

            var requestId = Guid.NewGuid().ToString();
            ViewData["RequestID"] = requestId;

            return View();
        }

        // POST: /Cuenta/Login
        /// <summary>
        /// Maneja el inicio de sesion de un usuario.
        /// Este metodo revisa las credenciales del usuario y si son validas lo redirige de acuerdo a su rol.
        /// </summary>
        /// <param name="model">El model que contiene los parametros de correo y contrasena del usuario</param>
        /// <returns>Redirecciona a distintas vistas de acuerdo a el rol del usuario.</returns>
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

                    var roles = await _userManager.GetRolesAsync(user);

                    if (roles.Contains("Administrador"))
                        return RedirectToAction("Panel", "Admin");

                    if (roles.Contains("Empleado"))
                        return RedirectToAction("Dashboard", "Empleado");

                    return RedirectToAction("Index", "Home");
                }

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

        // GET: /Cuenta/Registrar
        [HttpGet]
        public IActionResult Registrar()
        {
            bool sesionIniciada = User.Identity.IsAuthenticated;
            // Si ya hay sesion iniciada
            if (sesionIniciada)
            {
                // Envia el usuario al index.
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        // POST: /Cuenta/Registrar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Registrar(RegistroViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

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
                // Asignar rol por defecto
                await _userManager.AddToRoleAsync(usuario, "Cliente");

                _logger.LogInformation("Usuario registrado: {Email}", model.Email);
                await _signInManager.SignInAsync(usuario, isPersistent: false);
                return RedirectToAction("Index", "Home");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
                _logger.LogError("Error al registrar {Email}: {Error}", model.Email, error.Description);
            }

            return View(model);
        }

        // POST: /Cuenta/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            _logger.LogInformation("Sesión cerrada.");
            return RedirectToAction("Login", "Cuenta");
        }

        // GET: /Cuenta/AccesoDenegado
        [HttpGet]
        public IActionResult AccesoDenegado()
        {
            return View();
        }
        // GET: /Cuenta/Perfil
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
            ViewData["Usuario"] = usuario;

            return View();
        }

        // POST: /Cuenta/Perfil
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

            // Solo se puede editar NombreCompleto por seguridad
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
        [HttpGet]
        public IActionResult OlvidePassword()
        {
            return View();
        }
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


