using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Simone.Data;
using Simone.Models;
using Simone.Services;
using Simone.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;
using static Microsoft.ApplicationInsights.MetricDimensionNames.TelemetryContext;


namespace Simone.Controllers
{
    public class CuentaController : Controller
    {
        private readonly UserManager<Usuario> _userManager;
        private readonly SignInManager<Usuario> _signInManager;
        private readonly RoleManager<Roles> _roleManager;
        private readonly CarritoService _carritoManager;
        private readonly LogService? _logService;

        //private readonly LoginLoggerService? _loginLogger;
        private readonly ILogger<CuentaController> _logger;
        private readonly TiendaDbContext _context;
        private LogService? logService;

        //private readonly LoginLoggerService loginLogger;

        public CuentaController(UserManager<Usuario> userManager,
                                SignInManager<Usuario> signInManager,
                                RoleManager<Roles> roleManager,
                                ILogger<CuentaController> logger,
                                CarritoService carrito,
                                LogService logService,
                                TiendaDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _logger = logger;
            _context = context;
            _carritoManager = carrito;
            _logService = logService;
            //_loginLogger = loginLogger; // Añade esta línea
        }

        /// <summary>
        /// Acción GET para mostrar la vista de inicio de sesión.
        /// Redirige al Home si el usuario ya está autenticado.
        /// </summary>
        /// <returns>Vista de inicio de sesión o redirección al Home si ya está autenticado.</returns>

// GET: /Cuenta/Login
[HttpGet]
    [AllowAnonymous]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Login()
    {
        // Si ya está autenticado, no muestres el login otra vez
        if (User?.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        // (Opcional) ID de solicitud si lo usas en la vista
        ViewData["RequestID"] = Guid.NewGuid().ToString();
        return View();
    }

    // POST: /Cuenta/Login
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);

        if (user == null)
        {
            _logger.LogWarning("Intento con usuario no registrado: {Email}", model.Email);
            TempData["MensajeError"] = "Correo o contraseña incorrectos.";
            return View(model);
        }

        if (!user.Activo)
        {
            _logger.LogWarning("Usuario inactivo: {Email}", model.Email);
            TempData["MensajeError"] = "Tu cuenta está desactivada. Contacta con soporte.";
            return View(model);
        }

        // Limpia cualquier cookie/estado previo del sign-in (clave para evitar el bucle)
        await _signInManager.SignOutAsync();

        // Autentica por UserName (en tu registro: UserName = Email)
        var result = await _signInManager.PasswordSignInAsync(
            user.UserName ?? user.Email,
            model.Password,
            model.RememberMe,
            lockoutOnFailure: true
        );

        if (result.Succeeded)
        {
            _logger.LogInformation("Inicio de sesión exitoso: {Email}", model.Email);
            await RegistrarLog(model.Email, true);
            if (_logService != null) await _logService.Registrar($"Inicio de sesión exitoso para {model.Email}");
            return RedirectToAction("Index", "Home");
        }

        if (result.IsLockedOut)
        {
            _logger.LogWarning("Usuario bloqueado por intentos fallidos: {Email}", model.Email);
            TempData["MensajeError"] = "Tu cuenta ha sido bloqueada por múltiples intentos fallidos. Intenta más tarde.";
            await RegistrarLog(model.Email, false);
            return View("Lockout");
        }

        if (result.IsNotAllowed)
        {
            _logger.LogWarning("Login no permitido para: {Email}", model.Email);
            TempData["MensajeError"] = "Tu cuenta no está habilitada para iniciar sesión.";
            return View(model);
        }

        if (result.RequiresTwoFactor)
        {
            TempData["MensajeError"] = "Se requiere un segundo factor de autenticación.";
            return View(model);
        }

        _logger.LogWarning("Contraseña incorrecta: {Email}", model.Email);
        TempData["MensajeError"] = "Correo o contraseña incorrectos.";
        await RegistrarLog(model.Email, false);
        if (_logService != null) await _logService.Registrar($"Intento de inicio de sesión fallido para {model.Email}");
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
                Exitoso = exitoso,
                DireccionIP = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
                Localizacion = "N/A",
                UserAgent = Request.Headers["User-Agent"].ToString()
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
       
        /// <returns>Redirige al Home si el registro es exitoso, o muestra los errores de registro.</returns>
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

                // Crea también el cliente asociado
                var cliente = new Cliente
                {
                    Nombre = usuario.NombreCompleto ?? usuario.UserName ?? "Sin nombre",
                    Email = usuario.Email,
                    Telefono = usuario.Telefono,
                    Direccion = usuario.Direccion,
                    FechaRegistro = DateTime.Now
                };
                _context.Clientes.Add(cliente);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Usuario registrado: {Email}", model.Email);
                await _signInManager.SignInAsync(usuario, isPersistent: false);
                await _logService.Registrar($"Se registró un nuevo usuario: {usuario.Email}");
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
            await _logService.Registrar("Cierre de sesión");
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
            usuarioDb.Ciudad = usuario.Ciudad;
            usuarioDb.Provincia = usuario.Provincia;

            _context.Update(usuarioDb);
            await _context.SaveChangesAsync();
            await _logService.Registrar($"Actualizó su perfil: {usuarioDb.Email}");
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
        [Authorize]
        public async Task<IActionResult> CambiarDireccion(string? returnUrl = null)
        {
            var u = await _userManager.GetUserAsync(User);
            if (u == null) return RedirectToAction("Login");

            ViewBag.ReturnUrl = returnUrl; // para volver a la pantalla que te llamó (checkout, perfil, etc.)
            return View(u);                // enviamos el propio Usuario a la vista
        }


        /// <summary>
        /// Acción POST que guarda la nueva dirección del usuario.
        /// </summary>
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarDireccion(
            string? id,              // se ignora: siempre actualizamos el usuario autenticado
            string? direccion,
            string? ciudad,
            string? provincia,
            string? referencia,
            string? telefono,
            string? returnUrl)
        {
            var u = await _userManager.GetUserAsync(User);
            if (u == null) return RedirectToAction("Login");

            u.Direccion = string.IsNullOrWhiteSpace(direccion) ? null : direccion.Trim();
            u.Ciudad = string.IsNullOrWhiteSpace(ciudad) ? null : ciudad.Trim();
            u.Provincia = string.IsNullOrWhiteSpace(provincia) ? null : provincia.Trim();
            u.Referencia = string.IsNullOrWhiteSpace(referencia) ? null : referencia.Trim();
            u.Telefono = string.IsNullOrWhiteSpace(telefono) ? null : telefono.Trim();

            _context.Update(u);
            await _context.SaveChangesAsync();
            if (_logService != null) await _logService.Registrar($"Actualizó dirección de envío: {u.Email}");

            TempData["MensajeExito"] = "Dirección guardada correctamente.";

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Perfil");
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
            await _logService.Registrar($"Solicitó recuperación de contraseña: {user.Email}");

            TempData["MensajeExito"] = "Te hemos enviado un enlace de recuperación (o revisa la consola).";
            return RedirectToAction("Login");
        }
    }
}
