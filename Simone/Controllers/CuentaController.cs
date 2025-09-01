using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Simone.Data;
using Simone.Models;
using Simone.Services;
using Simone.ViewModels;

namespace Simone.Controllers
{
    public class CuentaController : Controller
    {
        private readonly UserManager<Usuario> _userManager;
        private readonly SignInManager<Usuario> _signInManager;
        private readonly RoleManager<Roles> _roleManager;
        private readonly CarritoService _carritoManager;
        private readonly LogService? _logService;
        private readonly ILogger<CuentaController> _logger;
        private readonly TiendaDbContext _context;

        public CuentaController(
            UserManager<Usuario> userManager,
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
        }

        // GET: /Cuenta/Login
        [HttpGet]
        [AllowAnonymous]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Login(string? returnUrl = null, CancellationToken ct = default)
        {
            // Limpia cookie externa (clave para evitar bucles después de fallos)
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            await _signInManager.SignOutAsync();

            if (User?.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");

            ViewData["ReturnUrl"] = returnUrl;
            ViewData["RequestID"] = Guid.NewGuid().ToString();
            return View();
        }

        // POST: /Cuenta/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null, CancellationToken ct = default)
        {
            // Mantener ViewData en todos los casos de retorno a la vista
            ViewData["ReturnUrl"] = returnUrl;
            ViewData["RequestID"] = Guid.NewGuid().ToString();

            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user is null)
            {
                _logger.LogWarning("Intento con usuario no registrado: {Email}", model.Email);
                TempData["MensajeError"] = "Correo o contraseña incorrectos.";
                await RegistrarLog(model.Email, false, ct);
                return View(model);
            }

            if (!user.Activo)
            {
                _logger.LogWarning("Usuario inactivo: {Email}", model.Email);
                TempData["MensajeError"] = "Tu cuenta está desactivada. Contacta con soporte.";
                return View(model);
            }

            // NO limpiar cookies aquí - solo se debe hacer en el GET
            var result = await _signInManager.PasswordSignInAsync(
                user.UserName ?? user.Email,
                model.Password,
                model.RememberMe,
                lockoutOnFailure: true
            );

            if (result.Succeeded)
            {
                await _userManager.ResetAccessFailedCountAsync(user);
                _logger.LogInformation("Inicio de sesión exitoso: {Email}", model.Email);
                await RegistrarLog(model.Email, true, ct);

                // Limpiar cookies solo después de login exitoso
                await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

                if (_logService != null)
                    await _logService.Registrar($"Login OK {model.Email}");

                if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return Redirect(returnUrl);

                return RedirectToAction("Index", "Home");
            }

            if (result.IsLockedOut)
            {
                _logger.LogWarning("Usuario bloqueado por intentos fallidos: {Email}", model.Email);
                TempData["MensajeError"] = "Cuenta bloqueada por múltiples intentos fallidos. Intenta más tarde.";
                await RegistrarLog(model.Email, false, ct);
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

            _logger.LogWarning("Credenciales inválidas para {Email}", model.Email);
            TempData["MensajeError"] = "Correo o contraseña incorrectos.";
            await RegistrarLog(model.Email, false, ct);

            if (_logService != null)
                await _logService.Registrar($"Login FAIL {model.Email}");

            return View(model);
        }
        // Registro
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Registrar()
        {
            if (User?.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Registrar(RegistroViewModel model, CancellationToken ct = default)
        {
            if (!ModelState.IsValid)
                return View(model);

            var rol = await _roleManager.FindByNameAsync("Cliente"); // puede ser null si aún no seed-easte
            var usuario = new Usuario
            {
                NombreCompleto = model.Nombre,
                Email = model.Email,
                UserName = model.Email,
                EmailConfirmed = true,      // según tu política
                FechaRegistro = DateTime.UtcNow,
                Activo = true,
                RolID = rol?.Id,   // evita NRE si rol no existe aún
                Direccion = model.Direccion,
                Telefono = model.Telefono,
                Referencia = model.Referencia,
            };

            var result = await _userManager.CreateAsync(usuario, model.Password);
            if (result.Succeeded)
            {
                if (rol != null) await _userManager.AddToRoleAsync(usuario, "Cliente");
                await _carritoManager.AddAsync(usuario);

                var cliente = new Cliente
                {
                    Nombre = usuario.NombreCompleto ?? usuario.UserName ?? "Sin nombre",
                    Email = usuario.Email,
                    Telefono = usuario.Telefono,
                    Direccion = usuario.Direccion,
                    FechaRegistro = DateTime.UtcNow
                };

                _context.Clientes.Add(cliente);
                await _context.SaveChangesAsync(ct);

                _logger.LogInformation("Usuario registrado: {Email}", model.Email);
                await _signInManager.SignInAsync(usuario, isPersistent: false);
                if (_logService != null) await _logService.Registrar($"Nuevo registro {usuario.Email}");
                return RedirectToAction("Index", "Home");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
                _logger.LogError("Error al registrar {Email}: {Error}", model.Email, error.Description);
            }

            return View(model);
        }

        // Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout(CancellationToken ct)
        {
            await _signInManager.SignOutAsync();
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            _logger.LogInformation("Sesión cerrada.");
            if (_logService != null) await _logService.Registrar("Logout");
            return RedirectToAction("Login", "Cuenta");
        }

        // Perfil (GET)
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Perfil(CancellationToken ct)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                TempData["MensajeError"] = "No se encontró al usuario.";
                return RedirectToAction("Login");
            }

            var roles = await _userManager.GetRolesAsync(usuario);
            ViewBag.RolUsuario = roles.FirstOrDefault() ?? "Sin rol";
            return View(usuario);
        }

        // Perfil (POST) – actualizar datos + imagen
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarPerfil(Usuario usuario, IFormFile? ImagenPerfil, CancellationToken ct)
        {
            var usuarioDb = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == usuario.Id, ct);
            if (usuarioDb == null) return NotFound();

            // Upload seguro
            if (ImagenPerfil != null && ImagenPerfil.Length > 0)
            {
                if (ImagenPerfil.Length > 2_000_000)
                {
                    TempData["MensajeError"] = "La imagen supera 2MB.";
                    return RedirectToAction("Perfil");
                }

                var ext = Path.GetExtension(ImagenPerfil.FileName).ToLowerInvariant();
                var permitidas = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                if (!permitidas.Contains(ext))
                {
                    TempData["MensajeError"] = "Formato de imagen no permitido.";
                    return RedirectToAction("Perfil");
                }

                var rutaCarpeta = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "Perfiles");
                Directory.CreateDirectory(rutaCarpeta);

                var nombreArchivo = $"{Guid.NewGuid():N}{ext}";
                var rutaCompleta = Path.Combine(rutaCarpeta, nombreArchivo);
                await using (var stream = new FileStream(rutaCompleta, FileMode.Create))
                    await ImagenPerfil.CopyToAsync(stream, ct);

                usuarioDb.FotoPerfil = "/images/Perfiles/" + nombreArchivo;
            }

            // Solo campos permitidos
            usuarioDb.NombreCompleto = usuario.NombreCompleto;
            usuarioDb.Telefono = string.IsNullOrWhiteSpace(usuario.Telefono) ? null : usuario.Telefono.Trim();
            usuarioDb.Direccion = string.IsNullOrWhiteSpace(usuario.Direccion) ? null : usuario.Direccion.Trim();
            usuarioDb.Referencia = string.IsNullOrWhiteSpace(usuario.Referencia) ? null : usuario.Referencia.Trim();
            usuarioDb.Ciudad = string.IsNullOrWhiteSpace(usuario.Ciudad) ? null : usuario.Ciudad.Trim();
            usuarioDb.Provincia = string.IsNullOrWhiteSpace(usuario.Provincia) ? null : usuario.Provincia.Trim();

            var res = await _userManager.UpdateAsync(usuarioDb);
            if (!res.Succeeded)
            {
                TempData["MensajeError"] = string.Join("; ", res.Errors.Select(e => e.Description));
                return RedirectToAction("Perfil");
            }

            if (_logService != null) await _logService.Registrar($"Actualizó su perfil: {usuarioDb.Email}");
            TempData["MensajeExito"] = "Perfil actualizado correctamente.";
            return RedirectToAction("Perfil");
        }

        // Dirección de envío (GET)
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> CambiarDireccion(string? returnUrl = null, CancellationToken ct = default)
        {
            var u = await _userManager.GetUserAsync(User);
            if (u == null) return RedirectToAction("Login");

            ViewBag.ReturnUrl = returnUrl;
            return View(u);
        }

        // Dirección de envío (POST)
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarDireccion(
            string? id, string? direccion, string? ciudad, string? provincia,
            string? referencia, string? telefono, string? returnUrl, CancellationToken ct)
        {
            var u = await _userManager.GetUserAsync(User);
            if (u == null) return RedirectToAction("Login");

            u.Direccion = string.IsNullOrWhiteSpace(direccion) ? null : direccion.Trim();
            u.Ciudad = string.IsNullOrWhiteSpace(ciudad) ? null : ciudad.Trim();
            u.Provincia = string.IsNullOrWhiteSpace(provincia) ? null : provincia.Trim();
            u.Referencia = string.IsNullOrWhiteSpace(referencia) ? null : referencia.Trim();
            u.Telefono = string.IsNullOrWhiteSpace(telefono) ? null : telefono.Trim();

            var res = await _userManager.UpdateAsync(u);
            if (!res.Succeeded)
            {
                TempData["MensajeError"] = string.Join("; ", res.Errors.Select(e => e.Description));
                return RedirectToAction("Perfil");
            }

            if (_logService != null) await _logService.Registrar($"Actualizó dirección de envío: {u.Email}");
            TempData["MensajeExito"] = "Dirección guardada correctamente.";

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Perfil");
        }

        // Olvidé la contraseña
        [HttpGet]
        [AllowAnonymous]
        public IActionResult OlvidePassword() => View();

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OlvidePassword(ForgotPasswordViewModel model, CancellationToken ct = default)
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
            var callbackUrl = Url.Action("ResetPassword", "Cuenta",
                new { email = user.Email, token }, protocol: HttpContext.Request.Scheme);

            _logger.LogInformation("Solicitud de recuperación para {Email}", user.Email);
            if (_logService != null) await _logService.Registrar($"Reset password solicitado {user.Email}");

            TempData["MensajeExito"] = "Te hemos enviado un enlace de recuperación (revisa tu correo).";
            return RedirectToAction("Login");
        }

        // --- Helper de logging de inicios de sesión ---
        private async Task RegistrarLog(string email, bool exitoso, CancellationToken ct)
        {
            var log = new LogIniciosSesion
            {
                Usuario = email,
                FechaInicio = DateTime.UtcNow,
                Exitoso = exitoso,
                DireccionIP = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
                Localizacion = "N/A",
                UserAgent = Request.Headers["User-Agent"].ToString()
            };
            _context.LogIniciosSesion.Add(log);
            await _context.SaveChangesAsync(ct);
        }
    }
}
