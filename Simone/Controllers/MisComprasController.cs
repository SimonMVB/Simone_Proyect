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

        /// <summary>
        /// Muestra el historial de ventas del usuario autenticado
        /// </summary>
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                TempData["MensajeError"] = "Debes iniciar sesión para ver tus compras.";
                return RedirectToAction("Login", "Cuenta");
            }

            var cliente = await _context.Clientes.FirstOrDefaultAsync(c => c.Email == user.Email);
            if (cliente == null)
            {
                TempData["MensajeError"] = "No se encontró información de cliente vinculada a tu cuenta.";
                return RedirectToAction("Index", "Home");
            }

            var ventas = await _context.Ventas
                .Where(v => v.ClienteID == cliente.ClienteID)
                .Include(v => v.DetalleVentas)
                .ThenInclude(dv => dv.Producto)
                .OrderByDescending(v => v.FechaVenta)
                .ToListAsync();

            return View(ventas);
        }

        /// <summary>
        /// Muestra el detalle de una venta específica del usuario autenticado
        /// </summary>
        public async Task<IActionResult> Detalle(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                TempData["MensajeError"] = "Debes iniciar sesión para ver el detalle.";
                return RedirectToAction("Login", "Cuenta");
            }

            var cliente = await _context.Clientes.FirstOrDefaultAsync(c => c.Email == user.Email);
            if (cliente == null)
            {
                TempData["MensajeError"] = "No se encontró información de cliente vinculada a tu cuenta.";
                return RedirectToAction("Index", "Home");
            }

            var venta = await _context.Ventas
                .Include(v => v.DetalleVentas)
                    .ThenInclude(dv => dv.Producto)
                .FirstOrDefaultAsync(v => v.VentaID == id && v.ClienteID == cliente.ClienteID);

            if (venta == null)
            {
                TempData["MensajeError"] = "No se encontró la venta solicitada.";
                return RedirectToAction("Index");
            }

            return View(venta);
        }
    }
}
