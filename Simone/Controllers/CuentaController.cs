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
    [AutoValidateAntiforgeryToken]
    public class CuentaController : Controller
    {
        private static readonly string[] _extPermitidas = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        private const long _maxImagenBytes = 5 * 1024 * 1024; // 5MB

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

        // ======= LOGIN =======

        [HttpGet]
        [AllowAnonymous]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Login(string? returnUrl = null, CancellationToken ct = default)
        {
            // Limpia cualquier cookie de autenticación externa
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            if (User?.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");

            ViewData["ReturnUrl"] = returnUrl;
            ViewData["RequestID"] = Guid.NewGuid().ToString();
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null, CancellationToken ct = default)
        {
            ViewData["ReturnUrl"] = returnUrl;
            ViewData["RequestID"] = Guid.NewGuid().ToString();

            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user is null)
            {
                _logger.LogWarning("Intento de login con email no registrado: {Email}", model.Email);
                TempData["MensajeError"] = "Correo o contraseña incorrectos.";
                await RegistrarLog(model.Email, false, ct);
                if (_logService != null) await _logService.Registrar($"Login FAIL {model.Email} (no existe)");
                return View(model);
            }

            if (!user.Activo)
            {
                _logger.LogWarning("Login de usuario inactivo: {Email}", model.Email);
                TempData["MensajeError"] = "Tu cuenta está desactivada. Contacta con soporte.";
                return View(model);
            }

            var result = await _signInManager.PasswordSignInAsync(
                user.UserName ?? user.Email,
                model.Password,
                model.RememberMe,
                lockoutOnFailure: true
            );

            if (result.Succeeded)
            {
                await _userManager.ResetAccessFailedCountAsync(user);
                _logger.LogInformation("Login OK: {Email}", model.Email);
                await RegistrarLog(model.Email, true, ct);
                if (_logService != null) await _logService.Registrar($"Login OK {model.Email}");

                // Limpia cookie externa residual
                await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

                if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return Redirect(returnUrl);

                return RedirectToAction("Index", "Home");
            }

            if (result.IsLockedOut)
            {
                _logger.LogWarning("Cuenta bloqueada por intentos fallidos: {Email}", model.Email);
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
            if (_logService != null) await _logService.Registrar($"Login FAIL {model.Email}");

            return View(model);
        }

        // ======= REGISTRO =======

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
        public async Task<IActionResult> Registrar(RegistroViewModel model, CancellationToken ct = default)
        {
            if (!ModelState.IsValid)
                return View(model);

            // Evita registro duplicado
            var existente = await _userManager.FindByEmailAsync(model.Email);
            if (existente != null)
            {
                ModelState.AddModelError(nameof(model.Email), "Este correo ya está registrado.");
                return View(model);
            }

            var rolCliente = await _roleManager.FindByNameAsync("Cliente"); // puede ser null si aún no seed-easte
            var usuario = new Usuario
            {
                NombreCompleto = model.Nombre,
                Email = model.Email,
                UserName = model.Email,
                EmailConfirmed = true, // Ajusta según tu política
                FechaRegistro = DateTime.UtcNow,
                Activo = true,
                RolID = rolCliente?.Id,
                Direccion = string.IsNullOrWhiteSpace(model.Direccion) ? null : model.Direccion.Trim(),
                Telefono = string.IsNullOrWhiteSpace(model.Telefono) ? null : model.Telefono.Trim(),
                Referencia = string.IsNullOrWhiteSpace(model.Referencia) ? null : model.Referencia.Trim(),
                Ciudad = string.IsNullOrWhiteSpace(model.Ciudad) ? null : model.Ciudad.Trim(),
                Provincia = string.IsNullOrWhiteSpace(model.Provincia) ? null : model.Provincia.Trim(),
            };

            var result = await _userManager.CreateAsync(usuario, model.Password);
            if (result.Succeeded)
            {
                if (rolCliente != null) await _userManager.AddToRoleAsync(usuario, "Cliente");
                await _carritoManager.AddAsync(usuario);

                _logger.LogInformation("Usuario registrado: {Email}", model.Email);
                if (_logService != null) await _logService.Registrar($"Nuevo registro {usuario.Email}");

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

        // ======= LOGOUT =======

        [HttpPost]
        public async Task<IActionResult> Logout(CancellationToken ct)
        {
            await _signInManager.SignOutAsync();
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            _logger.LogInformation("Sesión cerrada.");
            if (_logService != null) await _logService.Registrar("Logout");
            return RedirectToAction("Login", "Cuenta");
        }

        // ======= PERFIL =======

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

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> ActualizarPerfil(
            [FromForm] Usuario usuario,
            IFormFile? ImagenPerfil,
            [FromForm] bool? EliminarFoto,
            CancellationToken ct)
        {
            var usuarioDb = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == usuario.Id, ct);
            if (usuarioDb == null) return NotFound();

            // Eliminar foto si el usuario lo solicitó
            if (EliminarFoto == true && !string.IsNullOrEmpty(usuarioDb.FotoPerfil))
            {
                TryDeleteProfileImage(usuarioDb.FotoPerfil);
                usuarioDb.FotoPerfil = null;
            }

            // Subida segura de nueva imagen
            if (ImagenPerfil != null && ImagenPerfil.Length > 0)
            {
                if (ImagenPerfil.Length > _maxImagenBytes)
                {
                    TempData["MensajeError"] = "La imagen supera el tamaño máximo (5MB).";
                    return RedirectToAction(nameof(Perfil));
                }

                var ext = Path.GetExtension(ImagenPerfil.FileName).ToLowerInvariant();
                if (!_extPermitidas.Contains(ext) || string.IsNullOrWhiteSpace(ImagenPerfil.ContentType) || !ImagenPerfil.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    TempData["MensajeError"] = "Formato de imagen no permitido.";
                    return RedirectToAction(nameof(Perfil));
                }

                // Carpeta destino
                var carpeta = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "Perfiles");
                Directory.CreateDirectory(carpeta);

                // Borra anterior si existe
                if (!string.IsNullOrEmpty(usuarioDb.FotoPerfil))
                    TryDeleteProfileImage(usuarioDb.FotoPerfil);

                var nombreArchivo = $"{Guid.NewGuid():N}{ext}";
                var rutaFisica = Path.Combine(carpeta, nombreArchivo);

                try
                {
                    await using var stream = new FileStream(rutaFisica, FileMode.Create);
                    await ImagenPerfil.CopyToAsync(stream, ct);
                    usuarioDb.FotoPerfil = "/images/Perfiles/" + nombreArchivo;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error guardando imagen de perfil");
                    TempData["MensajeError"] = "No se pudo guardar la imagen.";
                    return RedirectToAction(nameof(Perfil));
                }
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
                return RedirectToAction(nameof(Perfil));
            }

            if (_logService != null) await _logService.Registrar($"Actualizó su perfil: {usuarioDb.Email}");
            TempData["MensajeExito"] = "Perfil actualizado correctamente.";
            return RedirectToAction(nameof(Perfil));
        }

        // ======= DIRECCIÓN DE ENVÍO =======

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> CambiarDireccion(string? returnUrl = null, CancellationToken ct = default)
        {
            var u = await _userManager.GetUserAsync(User);
            if (u == null) return RedirectToAction("Login");

            ViewBag.ReturnUrl = returnUrl;
            return View(u);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> GuardarDireccion(
            string? direccion, string? ciudad, string? provincia,
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
                return RedirectToAction(nameof(Perfil));
            }

            if (_logService != null) await _logService.Registrar($"Actualizó dirección de envío: {u.Email}");
            TempData["MensajeExito"] = "Dirección guardada correctamente.";

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction(nameof(Perfil));
        }

        // ======= OLVIDÉ LA CONTRASEÑA =======

        [HttpGet]
        [AllowAnonymous]
        public IActionResult OlvidePassword() => View();

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> OlvidePassword(ForgotPasswordViewModel model, CancellationToken ct = default)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null || !(await _userManager.IsEmailConfirmedAsync(user)))
            {
                TempData["MensajeExito"] = "Si el correo existe, se ha enviado un enlace de recuperación.";
                return RedirectToAction(nameof(Login));
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var callbackUrl = Url.Action("ResetPassword", "Cuenta",
                new { email = user.Email, token }, protocol: HttpContext.Request.Scheme);

            _logger.LogInformation("Solicitud de recuperación para {Email}", user.Email);
            if (_logService != null) await _logService.Registrar($"Reset password solicitado {user.Email}");

            // Aquí iría el envío del correo con callbackUrl.
            TempData["MensajeExito"] = "Te hemos enviado un enlace de recuperación (revisa tu correo).";
            return RedirectToAction(nameof(Login));
        }

        // ======= CAMBIAR CONTRASEÑA (AJAX, sin redirecciones) =======

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CambiarPassword([FromForm] string CurrentPassword, [FromForm] string NewPassword, [FromForm] string ConfirmPassword)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) return Unauthorized(new { error = "Sesión expirada. Ingresa nuevamente." });

            if (string.IsNullOrWhiteSpace(CurrentPassword) ||
                string.IsNullOrWhiteSpace(NewPassword) ||
                string.IsNullOrWhiteSpace(ConfirmPassword))
                return BadRequest(new { error = "Todos los campos son obligatorios." });

            if (NewPassword != ConfirmPassword)
                return BadRequest(new { error = "Las contraseñas no coinciden." });

            // Validación de política de contraseñas
            foreach (var v in _userManager.PasswordValidators)
            {
                var vr = await v.ValidateAsync(_userManager, user, NewPassword);
                if (!vr.Succeeded)
                    return BadRequest(new { errors = vr.Errors.Select(e => e.Description).ToArray() });
            }

            var result = await _userManager.ChangePasswordAsync(user, CurrentPassword, NewPassword);
            if (!result.Succeeded)
                return BadRequest(new { errors = result.Errors.Select(e => e.Description).ToArray() });

            if (_logService != null) await _logService.Registrar($"Password cambiado: {user.Email}");
            return Ok(new { ok = true, message = "Contraseña actualizada correctamente." });
        }

        // ======= HELPERS =======

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

        private void TryDeleteProfileImage(string? relativePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(relativePath)) return;
                // Solo archivos dentro de /images/Perfiles
                if (!relativePath.Replace('\\', '/').StartsWith("/images/Perfiles/", StringComparison.OrdinalIgnoreCase))
                    return;

                var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(fullPath))
                    System.IO.File.Delete(fullPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo eliminar la imagen anterior del perfil.");
            }
        }
    }
}
