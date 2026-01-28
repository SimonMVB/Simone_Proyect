using System;
using System.Collections.Generic;
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
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Simone.Data;
using Simone.Models;
using Simone.Services;
using Simone.ViewModels;

namespace Simone.Controllers
{
    /// <summary>
    /// Controlador de autenticación y gestión de perfil de usuario
    /// Versión optimizada con mejores prácticas empresariales
    /// </summary>
    [AutoValidateAntiforgeryToken]
    public class CuentaController : Controller
    {
        #region Dependencias

        private readonly UserManager<Usuario> _userManager;
        private readonly SignInManager<Usuario> _signInManager;
        private readonly RoleManager<Roles> _roleManager;
        private readonly CarritoService _carritoManager;
        private readonly LogService? _logService;
        private readonly ILogger<CuentaController> _logger;
        private readonly TiendaDbContext _context;
        private readonly IMemoryCache _cache;

        #endregion

        #region Constantes

        // Archivos - Imágenes de perfil
        private const long MAX_IMAGEN_BYTES = 5 * 1024 * 1024; // 5MB
        private const int MAX_IMAGEN_MB = 5;

        private static readonly HashSet<string> EXTENSIONES_PERMITIDAS = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp"
        };

        // Roles
        private const string ROL_CLIENTE = "Cliente";
        private const string ROL_SIN_ASIGNAR = "Sin rol";

        // Rutas
        private const string RUTA_IMAGENES_PERFILES = "/images/Perfiles/";
        private const string CARPETA_PERFILES = "images/Perfiles";
        private const string CARPETA_WWWROOT = "wwwroot";

        // Mensajes
        private const string MSG_ERROR_CREDENCIALES = "Correo o contraseña incorrectos.";
        private const string MSG_ERROR_CUENTA_INACTIVA = "Tu cuenta está desactivada. Contacta con soporte.";
        private const string MSG_ERROR_CUENTA_BLOQUEADA = "Cuenta bloqueada por múltiples intentos fallidos. Intenta más tarde.";
        private const string MSG_ERROR_CUENTA_NO_PERMITIDA = "Tu cuenta no está habilitada para iniciar sesión.";
        private const string MSG_ERROR_USUARIO_NO_ENCONTRADO = "No se encontró al usuario.";
        private const string MSG_ERROR_IMAGEN_GRANDE = "La imagen supera el tamaño máximo (5MB).";
        private const string MSG_ERROR_FORMATO_IMAGEN = "Formato de imagen no permitido.";
        private const string MSG_ERROR_GUARDAR_IMAGEN = "No se pudo guardar la imagen.";
        private const string MSG_EXITO_PERFIL_ACTUALIZADO = "Perfil actualizado correctamente.";
        private const string MSG_EXITO_DIRECCION_GUARDADA = "Dirección guardada correctamente.";
        private const string MSG_EXITO_PASSWORD_CAMBIADO = "Contraseña actualizada correctamente.";
        private const string MSG_EXITO_RECUPERACION_ENVIADA = "Te hemos enviado un enlace de recuperación (revisa tu correo).";
        private const string MSG_EXITO_RECUPERACION_GENERICO = "Si el correo existe, se ha enviado un enlace de recuperación.";

        // Headers
        private const string HEADER_USER_AGENT = "User-Agent";

        // Cache
        private const string CACHE_KEY_ROL_CLIENTE = "Rol_Cliente";
        private static readonly TimeSpan CACHE_DURATION_ROLES = TimeSpan.FromHours(1);

        // Validación de cédula
        private const string REGEX_CEDULA = @"^\d{10}$";
        private const int CEDULA_LONGITUD = 10;

        // Direcciones IP
        private const string IP_LOCALHOST = "127.0.0.1";
        private const string LOCALIZACION_DEFAULT = "N/A";

        #endregion

        #region Constructor

        public CuentaController(
            UserManager<Usuario> userManager,
            SignInManager<Usuario> signInManager,
            RoleManager<Roles> roleManager,
            ILogger<CuentaController> logger,
            CarritoService carrito,
            LogService logService,
            TiendaDbContext context,
            IMemoryCache cache)
        {
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _signInManager = signInManager ?? throw new ArgumentNullException(nameof(signInManager));
            _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _carritoManager = carrito ?? throw new ArgumentNullException(nameof(carrito));
            _logService = logService; // Puede ser null
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        #endregion

        #region Login

        /// <summary>
        /// GET: /Cuenta/Login
        /// Muestra el formulario de inicio de sesión
        /// </summary>
        /// <param name="returnUrl">URL de retorno después del login</param>
        /// <param name="ct">Token de cancelación</param>
        [HttpGet]
        [AllowAnonymous]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Login(string? returnUrl = null, CancellationToken ct = default)
        {
            try
            {
                // Limpia cualquier cookie de autenticación externa
                await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

                if (User?.Identity?.IsAuthenticated == true)
                {
                    _logger.LogDebug("Usuario ya autenticado redirigido desde Login");
                    return RedirectToAction("Index", "Home");
                }

                ViewData["ReturnUrl"] = returnUrl;
                ViewData["RequestID"] = Guid.NewGuid().ToString();

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar vista de Login");
                return View();
            }
        }

        /// <summary>
        /// POST: /Cuenta/Login
        /// Procesa el inicio de sesión
        /// </summary>
        [HttpPost]
        [AllowAnonymous]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Login(
            LoginViewModel model,
            string? returnUrl = null,
            CancellationToken ct = default)
        {
            ViewData["ReturnUrl"] = returnUrl;
            ViewData["RequestID"] = Guid.NewGuid().ToString();

            if (!ModelState.IsValid)
            {
                _logger.LogDebug("Login con ModelState inválido");
                return View(model);
            }

            try
            {
                var user = await _userManager.FindByEmailAsync(model.Email);

                if (user is null)
                {
                    _logger.LogWarning(
                        "Intento de login con email no registrado. Email: {Email}",
                        model.Email);

                    TempData["MensajeError"] = MSG_ERROR_CREDENCIALES;
                    await RegistrarLogAsync(model.Email, false, ct);

                    if (_logService != null)
                        await _logService.Registrar($"Login FAIL {model.Email} (no existe)");

                    return View(model);
                }

                if (!user.Activo)
                {
                    _logger.LogWarning(
                        "Login de usuario inactivo. Email: {Email}, UserId: {UserId}",
                        model.Email,
                        user.Id);

                    TempData["MensajeError"] = MSG_ERROR_CUENTA_INACTIVA;
                    await RegistrarLogAsync(model.Email, false, ct);

                    return View(model);
                }

                var result = await _signInManager.PasswordSignInAsync(
                    user.UserName ?? user.Email,
                    model.Password,
                    model.RememberMe,
                    lockoutOnFailure: true);

                if (result.Succeeded)
                {
                    await _userManager.ResetAccessFailedCountAsync(user);

                    _logger.LogInformation(
                        "Login exitoso. Email: {Email}, UserId: {UserId}, RememberMe: {RememberMe}",
                        model.Email,
                        user.Id,
                        model.RememberMe);

                    await RegistrarLogAsync(model.Email, true, ct);

                    if (_logService != null)
                        await _logService.Registrar($"Login OK {model.Email}");

                    // Limpia cookie externa residual
                    await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

                    if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                        return Redirect(returnUrl);

                    return RedirectToAction("Index", "Home");
                }

                if (result.IsLockedOut)
                {
                    _logger.LogWarning(
                        "Cuenta bloqueada por intentos fallidos. Email: {Email}, UserId: {UserId}",
                        model.Email,
                        user.Id);

                    TempData["MensajeError"] = MSG_ERROR_CUENTA_BLOQUEADA;
                    await RegistrarLogAsync(model.Email, false, ct);

                    return View("Lockout");
                }

                if (result.IsNotAllowed)
                {
                    _logger.LogWarning(
                        "Login no permitido. Email: {Email}, UserId: {UserId}",
                        model.Email,
                        user.Id);

                    TempData["MensajeError"] = MSG_ERROR_CUENTA_NO_PERMITIDA;
                    return View(model);
                }

                if (result.RequiresTwoFactor)
                {
                    _logger.LogInformation(
                        "Se requiere autenticación de dos factores. Email: {Email}",
                        model.Email);

                    TempData["MensajeError"] = "Se requiere un segundo factor de autenticación.";
                    return View(model);
                }

                // Credenciales inválidas
                _logger.LogWarning(
                    "Credenciales inválidas. Email: {Email}",
                    model.Email);

                TempData["MensajeError"] = MSG_ERROR_CREDENCIALES;
                await RegistrarLogAsync(model.Email, false, ct);

                if (_logService != null)
                    await _logService.Registrar($"Login FAIL {model.Email}");

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al procesar login. Email: {Email}", model.Email);
                TempData["MensajeError"] = "Error inesperado al iniciar sesión. Intenta nuevamente.";
                return View(model);
            }
        }

        #endregion

        #region Registro

        /// <summary>
        /// GET: /Cuenta/Registrar
        /// Muestra el formulario de registro
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Registrar()
        {
            if (User?.Identity?.IsAuthenticated == true)
            {
                _logger.LogDebug("Usuario ya autenticado redirigido desde Registrar");
                return RedirectToAction("Index", "Home");
            }

            return View();
        }

        /// <summary>
        /// POST: /Cuenta/Registrar
        /// Procesa el registro de nuevo usuario
        /// </summary>
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Registrar(
            RegistroViewModel model,
            CancellationToken ct = default)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogDebug("Registro con ModelState inválido");
                return View(model);
            }

            try
            {
                // Verificar duplicados
                var existente = await _userManager.FindByEmailAsync(model.Email);
                if (existente != null)
                {
                    _logger.LogWarning(
                        "Intento de registro con email duplicado. Email: {Email}",
                        model.Email);

                    ModelState.AddModelError(nameof(model.Email), "Este correo ya está registrado.");
                    return View(model);
                }

                // Obtener rol Cliente con cache
                var rolCliente = await ObtenerRolClienteConCacheAsync();

                var usuario = new Usuario
                {
                    NombreCompleto = model.Nombre,
                    Email = model.Email,
                    UserName = model.Email,
                    EmailConfirmed = true,
                    FechaRegistro = DateTime.UtcNow,
                    Activo = true,
                    Cedula = model.Cedula,
                    RolID = rolCliente?.Id,
                    Direccion = NormalizeStringOrNull(model.Direccion),
                    Telefono = NormalizeStringOrNull(model.Telefono),
                    Referencia = NormalizeStringOrNull(model.Referencia),
                    Ciudad = NormalizeStringOrNull(model.Ciudad),
                    Provincia = NormalizeStringOrNull(model.Provincia),
                };

                var result = await _userManager.CreateAsync(usuario, model.Password);

                if (result.Succeeded)
                {
                    if (rolCliente != null)
                        await _userManager.AddToRoleAsync(usuario, ROL_CLIENTE);

                    await _carritoManager.AddAsync(usuario);

                    _logger.LogInformation(
                        "Usuario registrado exitosamente. Email: {Email}, UserId: {UserId}",
                        model.Email,
                        usuario.Id);

                    if (_logService != null)
                        await _logService.Registrar($"Nuevo registro {usuario.Email}");

                    await _signInManager.SignInAsync(usuario, isPersistent: false);
                    return RedirectToAction("Index", "Home");
                }

                // Errores al crear usuario
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                    _logger.LogWarning(
                        "Error al registrar usuario. Email: {Email}, Error: {Error}",
                        model.Email,
                        error.Description);
                }

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al procesar registro. Email: {Email}", model.Email);
                ModelState.AddModelError(string.Empty, "Error inesperado al crear la cuenta.");
                return View(model);
            }
        }

        #endregion

        #region Logout

        /// <summary>
        /// POST: /Cuenta/Logout
        /// Cierra la sesión del usuario
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Logout(CancellationToken ct)
        {
            try
            {
                var userEmail = User?.Identity?.Name;

                await _signInManager.SignOutAsync();
                await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

                _logger.LogInformation(
                    "Sesión cerrada. Usuario: {Email}",
                    userEmail ?? "Desconocido");

                if (_logService != null)
                    await _logService.Registrar($"Logout {userEmail}");

                return RedirectToAction(nameof(Login));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cerrar sesión");
                return RedirectToAction(nameof(Login));
            }
        }

        #endregion

        #region Perfil

        /// <summary>
        /// GET: /Cuenta/Perfil
        /// Muestra el perfil del usuario autenticado
        /// </summary>
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Perfil(CancellationToken ct)
        {
            try
            {
                var usuario = await _userManager.GetUserAsync(User);
                if (usuario == null)
                {
                    _logger.LogWarning("Usuario no encontrado al cargar perfil");
                    TempData["MensajeError"] = MSG_ERROR_USUARIO_NO_ENCONTRADO;
                    return RedirectToAction(nameof(Login));
                }

                var roles = await _userManager.GetRolesAsync(usuario);
                ViewBag.RolUsuario = roles.FirstOrDefault() ?? ROL_SIN_ASIGNAR;

                _logger.LogDebug(
                    "Perfil cargado. UserId: {UserId}, Email: {Email}",
                    usuario.Id,
                    usuario.Email);

                return View(usuario);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar perfil");
                TempData["MensajeError"] = "Error al cargar el perfil.";
                return RedirectToAction("Index", "Home");
            }
        }

        /// <summary>
        /// POST: /Cuenta/ActualizarPerfil
        /// Actualiza el perfil del usuario
        /// </summary>
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> ActualizarPerfil(
            [FromForm] Usuario usuario,
            IFormFile? ImagenPerfil,
            [FromForm] bool? EliminarFoto,
            CancellationToken ct)
        {
            try
            {
                var usuarioDb = await _userManager.Users
                    .FirstOrDefaultAsync(u => u.Id == usuario.Id, ct);

                if (usuarioDb == null)
                {
                    _logger.LogWarning(
                        "Usuario no encontrado al actualizar perfil. UserId: {UserId}",
                        usuario.Id);
                    return NotFound();
                }

                // Eliminar foto si se solicitó
                if (EliminarFoto == true && !string.IsNullOrEmpty(usuarioDb.FotoPerfil))
                {
                    _logger.LogInformation(
                        "Eliminando foto de perfil. UserId: {UserId}, Foto: {Foto}",
                        usuarioDb.Id,
                        usuarioDb.FotoPerfil);

                    TryDeleteProfileImage(usuarioDb.FotoPerfil);
                    usuarioDb.FotoPerfil = null;
                }

                // Subir nueva imagen
                if (ImagenPerfil != null && ImagenPerfil.Length > 0)
                {
                    var validacionImagen = ValidarImagenPerfil(ImagenPerfil);
                    if (!validacionImagen.IsValid)
                    {
                        _logger.LogWarning(
                            "Imagen de perfil inválida. UserId: {UserId}, Error: {Error}",
                            usuarioDb.Id,
                            validacionImagen.ErrorMessage);

                        TempData["MensajeError"] = validacionImagen.ErrorMessage;
                        return RedirectToAction(nameof(Perfil));
                    }

                    var resultado = await GuardarImagenPerfilAsync(
                        ImagenPerfil,
                        usuarioDb.FotoPerfil,
                        ct);

                    if (!resultado.Exitoso)
                    {
                        _logger.LogError(
                            "Error al guardar imagen de perfil. UserId: {UserId}",
                            usuarioDb.Id);

                        TempData["MensajeError"] = MSG_ERROR_GUARDAR_IMAGEN;
                        return RedirectToAction(nameof(Perfil));
                    }

                    usuarioDb.FotoPerfil = resultado.RutaRelativa;
                }

                // Validar y asignar cédula
                var validacionCedula = ValidarCedula(usuario.Cedula, usuarioDb.Id);
                if (!validacionCedula.IsValid)
                {
                    _logger.LogWarning(
                        "Cédula inválida. UserId: {UserId}, Cedula: {Cedula}, Error: {Error}",
                        usuarioDb.Id,
                        usuario.Cedula,
                        validacionCedula.ErrorMessage);

                    TempData["MensajeError"] = validacionCedula.ErrorMessage;
                    return RedirectToAction(nameof(Perfil));
                }

                // Verificar unicidad de cédula si se proporcionó
                if (!string.IsNullOrWhiteSpace(validacionCedula.CedulaNormalizada))
                {
                    var cedulaDuplicada = await _userManager.Users
                        .AsNoTracking()
                        .AnyAsync(u => u.Id != usuarioDb.Id &&
                                      u.Cedula == validacionCedula.CedulaNormalizada,
                                 ct);

                    if (cedulaDuplicada)
                    {
                        _logger.LogWarning(
                            "Intento de usar cédula duplicada. UserId: {UserId}, Cedula: {Cedula}",
                            usuarioDb.Id,
                            validacionCedula.CedulaNormalizada);

                        TempData["MensajeError"] = "La cédula ingresada ya está registrada en otra cuenta.";
                        return RedirectToAction(nameof(Perfil));
                    }
                }

                usuarioDb.Cedula = validacionCedula.CedulaNormalizada;

                // Actualizar campos permitidos
                usuarioDb.NombreCompleto = usuario.NombreCompleto;
                usuarioDb.Telefono = NormalizeStringOrNull(usuario.Telefono);
                usuarioDb.Direccion = NormalizeStringOrNull(usuario.Direccion);
                usuarioDb.Referencia = NormalizeStringOrNull(usuario.Referencia);
                usuarioDb.Ciudad = NormalizeStringOrNull(usuario.Ciudad);
                usuarioDb.Provincia = NormalizeStringOrNull(usuario.Provincia);

                var res = await _userManager.UpdateAsync(usuarioDb);
                if (!res.Succeeded)
                {
                    var errores = string.Join("; ", res.Errors.Select(e => e.Description));
                    _logger.LogError(
                        "Error al actualizar perfil. UserId: {UserId}, Errores: {Errores}",
                        usuarioDb.Id,
                        errores);

                    TempData["MensajeError"] = errores;
                    return RedirectToAction(nameof(Perfil));
                }

                _logger.LogInformation(
                    "Perfil actualizado. UserId: {UserId}, Email: {Email}",
                    usuarioDb.Id,
                    usuarioDb.Email);

                if (_logService != null)
                    await _logService.Registrar($"Actualizó su perfil: {usuarioDb.Email}");

                TempData["MensajeExito"] = MSG_EXITO_PERFIL_ACTUALIZADO;
                return RedirectToAction(nameof(Perfil));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar perfil. UserId: {UserId}", usuario.Id);
                TempData["MensajeError"] = "Error inesperado al actualizar el perfil.";
                return RedirectToAction(nameof(Perfil));
            }
        }

        #endregion

        #region Dirección de Envío

        /// <summary>
        /// GET: /Cuenta/CambiarDireccion
        /// Muestra el formulario para cambiar dirección de envío
        /// </summary>
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> CambiarDireccion(
            string? returnUrl = null,
            CancellationToken ct = default)
        {
            try
            {
                var usuario = await _userManager.GetUserAsync(User);
                if (usuario == null)
                {
                    _logger.LogWarning("Usuario no encontrado al cargar CambiarDireccion");
                    return RedirectToAction(nameof(Login));
                }

                ViewBag.ReturnUrl = returnUrl;
                return View(usuario);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar CambiarDireccion");
                return RedirectToAction(nameof(Login));
            }
        }

        /// <summary>
        /// POST: /Cuenta/GuardarDireccion
        /// Guarda la dirección de envío del usuario
        /// </summary>
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> GuardarDireccion(
            string? direccion,
            string? ciudad,
            string? provincia,
            string? referencia,
            string? telefono,
            string? returnUrl,
            CancellationToken ct)
        {
            try
            {
                var usuario = await _userManager.GetUserAsync(User);
                if (usuario == null)
                {
                    _logger.LogWarning("Usuario no encontrado al guardar dirección");
                    return RedirectToAction(nameof(Login));
                }

                usuario.Direccion = NormalizeStringOrNull(direccion);
                usuario.Ciudad = NormalizeStringOrNull(ciudad);
                usuario.Provincia = NormalizeStringOrNull(provincia);
                usuario.Referencia = NormalizeStringOrNull(referencia);
                usuario.Telefono = NormalizeStringOrNull(telefono);

                var res = await _userManager.UpdateAsync(usuario);
                if (!res.Succeeded)
                {
                    var errores = string.Join("; ", res.Errors.Select(e => e.Description));
                    _logger.LogError(
                        "Error al guardar dirección. UserId: {UserId}, Errores: {Errores}",
                        usuario.Id,
                        errores);

                    TempData["MensajeError"] = errores;
                    return RedirectToAction(nameof(Perfil));
                }

                _logger.LogInformation(
                    "Dirección actualizada. UserId: {UserId}, Provincia: {Provincia}, Ciudad: {Ciudad}",
                    usuario.Id,
                    provincia,
                    ciudad);

                if (_logService != null)
                    await _logService.Registrar($"Actualizó dirección de envío: {usuario.Email}");

                TempData["MensajeExito"] = MSG_EXITO_DIRECCION_GUARDADA;

                if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return Redirect(returnUrl);

                return RedirectToAction(nameof(Perfil));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar dirección");
                TempData["MensajeError"] = "Error inesperado al guardar la dirección.";
                return RedirectToAction(nameof(Perfil));
            }
        }

        #endregion

        #region Recuperación de Contraseña

        /// <summary>
        /// GET: /Cuenta/OlvidePassword
        /// Muestra el formulario de recuperación de contraseña
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public IActionResult OlvidePassword() => View();

        /// <summary>
        /// POST: /Cuenta/OlvidePassword
        /// Procesa la solicitud de recuperación de contraseña
        /// </summary>
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> OlvidePassword(
            ForgotPasswordViewModel model,
            CancellationToken ct = default)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogDebug("OlvidePassword con ModelState inválido");
                return View(model);
            }

            try
            {
                var user = await _userManager.FindByEmailAsync(model.Email);

                if (user == null || !(await _userManager.IsEmailConfirmedAsync(user)))
                {
                    _logger.LogInformation(
                        "Solicitud de recuperación para email no existente o no confirmado. Email: {Email}",
                        model.Email);

                    // No revelar si el email existe o no
                    TempData["MensajeExito"] = MSG_EXITO_RECUPERACION_GENERICO;
                    return RedirectToAction(nameof(Login));
                }

                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var callbackUrl = Url.Action(
                    "ResetPassword",
                    "Cuenta",
                    new { email = user.Email, token },
                    protocol: HttpContext.Request.Scheme);

                _logger.LogInformation(
                    "Solicitud de recuperación de contraseña. Email: {Email}, UserId: {UserId}",
                    user.Email,
                    user.Id);

                if (_logService != null)
                    await _logService.Registrar($"Reset password solicitado {user.Email}");

                // TODO: Aquí iría el envío del correo con callbackUrl
                // await _emailService.SendPasswordResetEmailAsync(user.Email, callbackUrl);

                TempData["MensajeExito"] = MSG_EXITO_RECUPERACION_ENVIADA;
                return RedirectToAction(nameof(Login));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al procesar recuperación de contraseña. Email: {Email}", model.Email);
                TempData["MensajeError"] = "Error al procesar la solicitud.";
                return View(model);
            }
        }

        #endregion

        #region Cambiar Contraseña

        /// <summary>
        /// POST: /Cuenta/CambiarPassword
        /// Cambia la contraseña del usuario (AJAX)
        /// </summary>
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CambiarPassword(
            [FromForm] string CurrentPassword,
            [FromForm] string NewPassword,
            [FromForm] string ConfirmPassword)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user is null)
                {
                    _logger.LogWarning("CambiarPassword: usuario no encontrado");
                    return Unauthorized(new { error = "Sesión expirada. Ingresa nuevamente." });
                }

                // Validaciones básicas
                if (string.IsNullOrWhiteSpace(CurrentPassword) ||
                    string.IsNullOrWhiteSpace(NewPassword) ||
                    string.IsNullOrWhiteSpace(ConfirmPassword))
                {
                    return BadRequest(new { error = "Todos los campos son obligatorios." });
                }

                if (NewPassword != ConfirmPassword)
                {
                    return BadRequest(new { error = "Las contraseñas no coinciden." });
                }

                // Validar política de contraseñas
                foreach (var validator in _userManager.PasswordValidators)
                {
                    var validationResult = await validator.ValidateAsync(_userManager, user, NewPassword);
                    if (!validationResult.Succeeded)
                    {
                        var errores = validationResult.Errors.Select(e => e.Description).ToArray();
                        _logger.LogWarning(
                            "Password no cumple políticas. UserId: {UserId}, Errores: {Errores}",
                            user.Id,
                            string.Join(", ", errores));

                        return BadRequest(new { errors = errores });
                    }
                }

                var result = await _userManager.ChangePasswordAsync(user, CurrentPassword, NewPassword);
                if (!result.Succeeded)
                {
                    var errores = result.Errors.Select(e => e.Description).ToArray();
                    _logger.LogWarning(
                        "Error al cambiar contraseña. UserId: {UserId}, Errores: {Errores}",
                        user.Id,
                        string.Join(", ", errores));

                    return BadRequest(new { errors = errores });
                }

                _logger.LogInformation(
                    "Contraseña cambiada exitosamente. UserId: {UserId}, Email: {Email}",
                    user.Id,
                    user.Email);

                if (_logService != null)
                    await _logService.Registrar($"Password cambiado: {user.Email}");

                return Ok(new { ok = true, message = MSG_EXITO_PASSWORD_CAMBIADO });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cambiar contraseña");
                return StatusCode(500, new { error = "Error inesperado al cambiar la contraseña." });
            }
        }

        #endregion

        #region Helpers - Cache

        /// <summary>
        /// Obtiene el rol Cliente con cache
        /// </summary>
        private async Task<Roles?> ObtenerRolClienteConCacheAsync()
        {
            return await _cache.GetOrCreateAsync(
                CACHE_KEY_ROL_CLIENTE,
                async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = CACHE_DURATION_ROLES;
                    _logger.LogDebug("Cargando rol Cliente desde BD (cache miss)");
                    return await _roleManager.FindByNameAsync(ROL_CLIENTE);
                });
        }

        #endregion

        #region Helpers - Validación

        /// <summary>
        /// Valida una imagen de perfil
        /// </summary>
        private ValidationResult ValidarImagenPerfil(IFormFile imagen)
        {
            if (imagen.Length > MAX_IMAGEN_BYTES)
            {
                return ValidationResult.Failure(MSG_ERROR_IMAGEN_GRANDE);
            }

            var extension = Path.GetExtension(imagen.FileName).ToLowerInvariant();

            if (!EXTENSIONES_PERMITIDAS.Contains(extension) ||
                string.IsNullOrWhiteSpace(imagen.ContentType) ||
                !imagen.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return ValidationResult.Failure(MSG_ERROR_FORMATO_IMAGEN);
            }

            return ValidationResult.Success();
        }

        /// <summary>
        /// Valida una cédula ecuatoriana
        /// </summary>
        private ValidationResult<string?> ValidarCedula(string? cedula, string userId)
        {
            var cedulaNormalizada = NormalizeStringOrNull(cedula);

            if (cedulaNormalizada == null)
            {
                return ValidationResult<string?>.Success(null);
            }

            if (!System.Text.RegularExpressions.Regex.IsMatch(cedulaNormalizada, REGEX_CEDULA))
            {
                return ValidationResult<string?>.Failure(
                    $"La cédula debe tener exactamente {CEDULA_LONGITUD} dígitos numéricos.");
            }

            return ValidationResult<string?>.Success(cedulaNormalizada);
        }

        #endregion

        #region Helpers - Archivos

        /// <summary>
        /// Guarda una imagen de perfil y retorna la ruta relativa
        /// </summary>
        private async Task<ResultadoGuardadoImagen> GuardarImagenPerfilAsync(
            IFormFile imagen,
            string? imagenAnterior,
            CancellationToken ct)
        {
            try
            {
                var carpeta = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    CARPETA_WWWROOT,
                    CARPETA_PERFILES);

                Directory.CreateDirectory(carpeta);

                // Eliminar imagen anterior si existe
                if (!string.IsNullOrEmpty(imagenAnterior))
                {
                    TryDeleteProfileImage(imagenAnterior);
                }

                var extension = Path.GetExtension(imagen.FileName).ToLowerInvariant();
                var nombreArchivo = $"{Guid.NewGuid():N}{extension}";
                var rutaFisica = Path.Combine(carpeta, nombreArchivo);

                await using var stream = new FileStream(rutaFisica, FileMode.Create);
                await imagen.CopyToAsync(stream, ct);

                var rutaRelativa = RUTA_IMAGENES_PERFILES + nombreArchivo;

                _logger.LogInformation(
                    "Imagen de perfil guardada. Ruta: {Ruta}, Tamaño: {Size} bytes",
                    rutaRelativa,
                    imagen.Length);

                return new ResultadoGuardadoImagen(true, rutaRelativa);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar imagen de perfil");
                return new ResultadoGuardadoImagen(false, null);
            }
        }

        /// <summary>
        /// Intenta eliminar una imagen de perfil de forma segura
        /// </summary>
        private void TryDeleteProfileImage(string? relativePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(relativePath))
                    return;

                // Solo archivos dentro de /images/Perfiles
                var normalizedPath = relativePath.Replace('\\', '/');
                if (!normalizedPath.StartsWith(RUTA_IMAGENES_PERFILES, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning(
                        "Intento de eliminar archivo fuera de carpeta permitida: {Path}",
                        relativePath);
                    return;
                }

                var fullPath = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    CARPETA_WWWROOT,
                    relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

                if (System.IO.File.Exists(fullPath))
                {
                    System.IO.File.Delete(fullPath);
                    _logger.LogDebug("Imagen de perfil eliminada: {Path}", fullPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo eliminar imagen de perfil: {Path}", relativePath);
            }
        }

        #endregion

        #region Helpers - Logging

        /// <summary>
        /// Registra un intento de inicio de sesión
        /// </summary>
        private async Task RegistrarLogAsync(string email, bool exitoso, CancellationToken ct)
        {
            try
            {
                var userAgent = Request.Headers.TryGetValue(HEADER_USER_AGENT, out var ua)
                    ? ua.ToString()
                    : string.Empty;

                var log = new LogIniciosSesion
                {
                    Usuario = email,
                    FechaInicio = DateTime.UtcNow,
                    Exitoso = exitoso,
                    DireccionIP = HttpContext.Connection.RemoteIpAddress?.ToString() ?? IP_LOCALHOST,
                    Localizacion = LOCALIZACION_DEFAULT,
                    UserAgent = userAgent
                };

                _context.LogIniciosSesion.Add(log);
                await _context.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar log de inicio de sesión. Email: {Email}", email);
            }
        }

        #endregion

        #region Helpers - Normalización

        /// <summary>
        /// Normaliza un string: trim y null si está vacío
        /// </summary>
        private static string? NormalizeStringOrNull(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var trimmed = value.Trim();
            return string.IsNullOrEmpty(trimmed) ? null : trimmed;
        }

        #endregion

        #region Records - Resultados

        /// <summary>
        /// Resultado de validación
        /// </summary>
        private record ValidationResult(bool IsValid, string? ErrorMessage)
        {
            public static ValidationResult Success() => new(true, null);
            public static ValidationResult Failure(string message) => new(false, message);
        }

        /// <summary>
        /// Resultado de validación con valor
        /// </summary>
        private record ValidationResult<T>(bool IsValid, T? Value, string? ErrorMessage)
        {
            public static ValidationResult<T> Success(T value) => new(true, value, null);
            public static ValidationResult<T> Failure(string message) => new(false, default, message);

            public string? CedulaNormalizada => Value as string;
        }

        /// <summary>
        /// Resultado de guardado de imagen
        /// </summary>
        private record ResultadoGuardadoImagen(bool Exitoso, string? RutaRelativa);

        #endregion
    }
}