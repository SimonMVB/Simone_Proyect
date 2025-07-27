using Simone.Data;
using Simone.Models;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Simone.Services
{
    public class LogService
    {
        private readonly TiendaDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public LogService(TiendaDbContext context, IHttpContextAccessor accessor)
        {
            _context = context;
            _httpContextAccessor = accessor;
        }

        public async Task Registrar(string accion)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var user = httpContext.User;

            var usuarioID = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var ip = httpContext.Connection.RemoteIpAddress?.ToString();

            if (string.IsNullOrEmpty(usuarioID)) return; // No registrar si no está logueado

            var log = new LogActividad
            {
                UsuarioID = usuarioID,
                Accion = accion,
                IP = ip
            };

            _context.LogsActividad.Add(log);
            await _context.SaveChangesAsync();
        }
    }
}
