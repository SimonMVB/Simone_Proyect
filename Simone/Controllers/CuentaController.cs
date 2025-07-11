using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Simone.Models;
using Simone.ViewModels;
using Simone.Data;
using Simone.Services;
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
        private readonly CarritoService _carritoManager;
        private readonly ILogger<CuentaController> _logger;
        private readonly TiendaDbContext _context;

        public CuentaController(UserManager<Usuario> userManager,
                                SignInManager<Usuario> signInManager,
                                RoleManager<Roles> roleManager,
                                ILogger<CuentaController> logger,
                                CarritoService carrito,
                                TiendaDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _logger = logger;
            _context = context;
            _carritoManager = carrito;
        }

        /// <summary>
        /// Acción GET para mostrar la vista de inicio de sesión.
        /// Redirige al Home si el usuario ya está autenticado.
        /// </summary>
        /// <returns>Vista de inicio de sesión o redirección al Home si ya está autenticado.</returns>
        [HttpGet]
        public IActionResult Login()
        {
            // Verifica si el usuario ya está autenticado
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }

            // Genera un ID único para la solicitud de inicio de sesión
            ViewData["RequestID"] = Guid.NewGuid().ToString();
            return View();
        }

        /// <summary>
        /// Acción POST que maneja el inicio de sesión de un usuario.
        /// Verifica las credenciales y redirige según el rol del usuario.
        /// </summary>
        /// <param name="model">Modelo de inicio de sesión que contiene el correo y la contraseña del usuario.</param>
        /// <returns>Redirige a la vista correspondiente si el inicio de sesión es exitoso o muestra un mensaje de error.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            // Verifica que el modelo sea válido
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);

            // Verifica si el usuario existe y si está activo
            if (user != null && user.Activo)
            {
                // Intenta iniciar sesión con las credenciales proporcionadas
                var result = await _signInManager.PasswordSignInAsync(user.UserName, model.Password, model.RememberMe, lockoutOnFailure: true);

                if (result.Succeeded)
                {
                    _logger.LogInformation("Usuario autenticado: {Email}", model.Email);
                    await RegistrarLog(model.Email, true);
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

            // Registrar el intento de login
            await RegistrarLog(model.Email, false);
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
        /// <returns>Vista de registro o redirección al Home si ya está autenticado.</returns>
        [HttpGet]
        public IActionResult Registrar()
        {
            // Si el usuario ya está autenticado, lo redirige al índice
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        /// <summary>
        /// Acción POST para registrar un nuevo usuario en el sistema.
        /// </summary>
        /// <param name="model">Modelo que contiene la información del usuario.</param>
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
                Direccion = model.Direccion,
                Telefono = model.Telefono,
                Referencia = model.Referencia,
            };

            var result = await _userManager.CreateAsync(usuario, model.Password);
            if (result.Succeeded)
            {
                // Asigna el rol "Cliente" por defecto
                await _userManager.AddToRoleAsync(usuario, "Cliente");
                await _carritoManager.AddAsync(usuario);

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
        public IActionResult AccesoDenegado() => View();

        /// <summary>
        /// Acción GET que muestra la vista del perfil del usuario.
        /// </summary>
        /// <returns>Vista con la información del perfil del usuario.</returns>
        [HttpGet]
        public async Task<IActionResult> Perfil()
        {
            // Obtiene el usuario actualmente autenticado
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                TempData["MensajeError"] = "No se encontró al usuario.";
                return RedirectToAction("Login");
            }

            // Obtiene los roles del usuario
            var roles = await _userManager.GetRolesAsync(usuario);
            ViewBag.RolUsuario = roles.FirstOrDefault() ?? "Sin rol";

            return View(usuario);
        }

        /// <summary>
        /// Acción POST para actualizar el perfil del usuario.
        /// </summary>
        /// <param name="usuario">Modelo con la nueva información del usuario.</param>
        /// <param name="ImagenPerfil">Imagen de perfil proporcionada por el usuario.</param>
        /// <returns>Redirige al perfil después de la actualización o muestra un error si la operación falla.</returns>
        [HttpPost]
        public async Task<IActionResult> ActualizarPerfil(Usuario usuario, IFormFile ImagenPerfil)
        {
            var usuarioDb = await _context.Usuarios.FindAsync(usuario.Id);
            if (usuarioDb == null) return NotFound();

            // Maneja la carga de la imagen de perfil
            if (ImagenPerfil != null && ImagenPerfil.Length > 0)
            {
                var rutaCarpeta = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/Perfiles");
                Directory.CreateDirectory(rutaCarpeta);

                var nombreArchivo = Guid.NewGuid().ToString() + Path.GetExtension(ImagenPerfil.FileName);
                var rutaCompleta = Path.Combine(rutaCarpeta, nombreArchivo);

                using (var stream = new FileStream(rutaCompleta, FileMode.Create))
                {
                    await ImagenPerfil.CopyToAsync(stream);
                }

                usuarioDb.FotoPerfil = "/images/Perfiles/" + nombreArchivo;
            }

            usuarioDb.NombreCompleto = usuario.NombreCompleto;
            usuarioDb.Telefono = usuario.Telefono;
            usuarioDb.Direccion = usuario.Direccion;
            usuarioDb.Referencia = usuario.Referencia;

            _context.Update(usuarioDb);
            await _context.SaveChangesAsync();

            TempData["MensajeExito"] = "Perfil actualizado correctamente.";
            return RedirectToAction("Perfil");
        }

        /// <summary>
        /// Acción GET que muestra la vista de olvidé mi contraseña.
        /// </summary>
        /// <returns>Vista para recuperar la contraseña.</returns>
        [HttpGet]
        public IActionResult OlvidePassword() => View();

        [HttpGet]
        public IActionResult CambiarDireccion() => View();

        /// <summary>
        /// Acción POST que guarda la nueva dirección del usuario.
        /// </summary>
        [HttpPost]
        public IActionResult GuardarDireccion(string direccionReferencia, string latitud, string longitud)
        {
            ViewBag.UserDireccion = direccionReferencia;
            TempData["MensajeExito"] = "Dirección guardada correctamente.";
            return RedirectToAction("Index", "Home");
        }

        /// <summary>
        /// Acción POST que maneja la recuperación de la contraseña.
        /// </summary>
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

            _logger.LogWarning("Token de recuperación para {Email}: {Link}", user.Email, callbackUrl);

            TempData["MensajeExito"] = "Te hemos enviado un enlace de recuperación (o revisa la consola).";
            return RedirectToAction("Login");
        }
    }
}
