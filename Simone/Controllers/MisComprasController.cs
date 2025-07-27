using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Simone.Data;
using Simone.Models;
using System.Linq;
using System.Threading.Tasks;

namespace Simone.Controllers
{
    [Authorize]
    public class MisComprasController : Controller
    {
        private readonly TiendaDbContext _context;
        private readonly UserManager<Usuario> _userManager;

        public MisComprasController(TiendaDbContext context, UserManager<Usuario> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // Mostrar historial de VENTAS del cliente autenticado
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToAction("Login", "Cuenta");

            // Buscar ventas donde el ClienteID sea el mismo que el usuario actual
            var ventas = await _context.Ventas
               .Where(v => v.ClienteID == user.Id)
                .OrderByDescending(v => v.FechaVenta)
                .ToListAsync();

            return View(ventas); // La vista debe ser fuertemente tipada a List<Ventas>
        }

        // Mostrar detalle de una venta específica
        public async Task<IActionResult> Detalle(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToAction("Login", "Cuenta");

            var venta = await _context.Ventas
                  .Include(v => v.DetalleVentas)   // ← El nombre correcto de la colección
                  .ThenInclude(dv => dv.Producto)
                  .FirstOrDefaultAsync(v => v.VentaID == id && v.ClienteID == user.Id);




            if (venta == null)
                return NotFound();

            return View(venta); // La vista debe ser fuertemente tipada a Ventas
        }
    }
}
