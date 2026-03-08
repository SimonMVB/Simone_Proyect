using Simone.Data;
using Simone.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Simone.Services
{
    public class LogService
    {
        private readonly TiendaDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<LogService> _logger;

        public LogService(TiendaDbContext context, IHttpContextAccessor accessor, ILogger<LogService> logger)
        {
            _context = context;
            _httpContextAccessor = accessor;
            _logger = logger;
        }

        public async Task Registrar(string accion)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null) return;

            var user = httpContext.User;
            var usuarioID = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var ip = httpContext.Connection.RemoteIpAddress?.ToString();

            if (string.IsNullOrEmpty(usuarioID))
            {
                // Acción de usuario anónimo — visible en logs de aplicación pero no en BD
                _logger.LogDebug("Acción anónima no registrada en BD. Acción: {Accion}, IP: {IP}", accion, ip);
                return;
            }

            try
            {
                var log = new LogActividad
                {
                    UsuarioID = usuarioID,
                    Accion = accion,
                    IP = ip
                };

                _context.LogsActividad.Add(log);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // El fallo de auditoría NO debe interrumpir el flujo de la aplicación
                _logger.LogWarning(ex, "No se pudo registrar actividad. Acción: {Accion}, UsuarioId: {UserId}", accion, usuarioID);
            }
        }
    }
}
