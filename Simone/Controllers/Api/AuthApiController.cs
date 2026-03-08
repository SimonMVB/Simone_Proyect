using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Simone.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Simone.Controllers.Api
{
    /// <summary>
    /// Autenticación para la app POS React Native.
    /// POST /api/v1/auth/login  →  devuelve JWT
    /// </summary>
    [ApiController]
    [Route("api/v1/auth")]
    public class AuthApiController : ControllerBase
    {
        private readonly UserManager<Usuario>  _userManager;
        private readonly SignInManager<Usuario> _signInManager;
        private readonly IConfiguration        _config;

        public AuthApiController(
            UserManager<Usuario>  userManager,
            SignInManager<Usuario> signInManager,
            IConfiguration        config)
        {
            _userManager  = userManager;
            _signInManager = signInManager;
            _config        = config;
        }

        // ── DTOs ─────────────────────────────────────────────────────────────

        public record LoginRequest(string Email, string Password);

        public record LoginResponse(
            string  Token,
            string  NombreCompleto,
            string  Email,
            string  Rol,
            int?    VendedorId,
            DateTime Expira);

        // ── POST /api/v1/auth/login ──────────────────────────────────────────

        /// <summary>
        /// Recibe email + contraseña, devuelve un JWT válido para llamadas POS.
        /// Solo pueden loguearse usuarios con rol Vendedor o Administrador.
        /// </summary>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
                return BadRequest(new { error = "Email y contraseña son requeridos." });

            var usuario = await _userManager.FindByEmailAsync(req.Email);
            if (usuario == null || !usuario.Activo)
                return Unauthorized(new { error = "Credenciales inválidas." });

            // Verificar contraseña (sin actualizar lockout en fallo = false)
            var result = await _signInManager.CheckPasswordSignInAsync(usuario, req.Password, lockoutOnFailure: true);

            if (result.IsLockedOut)
                return Unauthorized(new { error = "Cuenta bloqueada temporalmente. Intenta más tarde." });

            if (!result.Succeeded)
                return Unauthorized(new { error = "Credenciales inválidas." });

            // Solo permitir Vendedor o Administrador en la app POS
            var roles = await _userManager.GetRolesAsync(usuario);
            var rol   = roles.Contains("Administrador") ? "Administrador"
                      : roles.Contains("Vendedor")      ? "Vendedor"
                      : null;

            if (rol == null)
                return Unauthorized(new { error = "No tienes acceso a la app POS." });

            // Generar token
            var (token, expira) = GenerarJwt(usuario, rol);

            return Ok(new LoginResponse(
                Token:         token,
                NombreCompleto: usuario.NombreCompleto,
                Email:         usuario.Email ?? req.Email,
                Rol:           rol,
                VendedorId:    usuario.VendedorId,
                Expira:        expira));
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private (string token, DateTime expira) GenerarJwt(Usuario usuario, string rol)
        {
            var key     = _config["Jwt:SecretKey"] ?? "Simone-POS-Super-Secret-JWT-Key-2025-Ecuador-Mobile-App!";
            var issuer  = _config["Jwt:Issuer"]    ?? "SimoneAPI";
            var audience = _config["Jwt:Audience"] ?? "SimoneMobileApp";
            var minutos = int.TryParse(_config["Jwt:ExpirationMinutes"], out var m) ? m : 480;

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub,   usuario.Id),
                new(JwtRegisteredClaimNames.Email, usuario.Email ?? ""),
                new(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
                new(ClaimTypes.Name,               usuario.NombreCompleto),
                new(ClaimTypes.Role,               rol),
                new("vendedorId",                  usuario.VendedorId?.ToString() ?? ""),
            };

            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
            var creds      = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
            var expira     = DateTime.UtcNow.AddMinutes(minutos);

            var jwtToken = new JwtSecurityToken(
                issuer:             issuer,
                audience:           audience,
                claims:             claims,
                expires:            expira,
                signingCredentials: creds);

            return (new JwtSecurityTokenHandler().WriteToken(jwtToken), expira);
        }
    }
}
