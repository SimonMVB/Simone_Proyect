using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Simone.Data;
using System.Threading.Tasks;
using System.Linq;

namespace Simone.Controllers
{
    [Authorize(Roles = "Administrador")]
    public class LogsController : Controller
    {
        private readonly TiendaDbContext _context;

        public LogsController(TiendaDbContext context)
        {
            _context = context;
        }

        // GET: /Logs
        public async Task<IActionResult> Index()
        {
            var logs = await _context.LogsActividad
                .Include(l => l.Usuario)
                .OrderByDescending(l => l.FechaHora)
                .Take(100) // limita a los 100 más recientes
                .ToListAsync();

            return View("~/Views/Panel/Logs.cshtml", logs);
        }
    }
}
