using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Simone.Data;
using Simone.Models;
using System;
using System.Linq;
using System.Threading;
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
        /// Historial de compras del usuario autenticado (con paginación opcional).
        /// </summary>
        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Index(int page = 1, int pageSize = 15, CancellationToken ct = default)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
            {
                TempData["MensajeError"] = "Debes iniciar sesión para ver tus compras.";
                return RedirectToAction("Login", "Cuenta");
            }

            // Saneamos paginación
            if (page < 1) page = 1;
            if (pageSize < 5) pageSize = 5;
            if (pageSize > 50) pageSize = 50;

            // Para el listado no necesitamos los detalles -> consulta más liviana
            var baseQuery = _context.Ventas
                .AsNoTracking()
                .Where(v => v.UsuarioId == userId)
                .OrderByDescending(v => v.FechaVenta);

            var total = await baseQuery.CountAsync(ct);

            var ventas = await baseQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            // Paginación para la vista (si decides mostrar controles)
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.Total = total;

            return View(ventas);
        }

        /// <summary>
        /// Detalle de una compra del usuario autenticado.
        /// </summary>
        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Detalle(int id, CancellationToken ct = default)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
            {
                TempData["MensajeError"] = "Debes iniciar sesión para ver el detalle.";
                return RedirectToAction("Login", "Cuenta");
            }

            var venta = await _context.Ventas
                .AsNoTracking()
                .Where(v => v.VentaID == id && v.UsuarioId == userId) // seguridad: solo propias
                .Include(v => v.DetalleVentas)
                    .ThenInclude(dv => dv.Producto)
#if NET5_0_OR_GREATER
                .AsSplitQuery()
#endif
                .FirstOrDefaultAsync(ct);

            if (venta == null)
            {
                TempData["MensajeError"] = "No se encontró la compra solicitada.";
                return RedirectToAction(nameof(Index));
            }

            return View(venta);
        }



    }
}
