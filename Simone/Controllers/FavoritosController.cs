using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PayPalCheckoutSdk.Orders;
using Simone.Data;
using Simone.Models;
using System.Security.Claims;

namespace Simone.Controllers
{
    [Authorize]
    public class FavoritosController : Controller
    {
        private readonly TiendaDbContext _context;

        public FavoritosController(TiendaDbContext context)
        {
            _context = context;
        }

        // GET: /Favoritos
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var favoritos = await _context.Favoritos
                .Include(f => f.Producto)
                .Where(f => f.UsuarioId == userId)
                .ToListAsync();

            return View(favoritos);
        }

        // POST: /Favoritos/Toggle/5
        [HttpPost]
        public async Task<IActionResult> Toggle(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var existente = await _context.Favoritos
                .FirstOrDefaultAsync(f => f.UsuarioId == userId && f.ProductoId == id);

            if (existente != null)
            {
                _context.Favoritos.Remove(existente);
                await _context.SaveChangesAsync();
                return Json(new { esFavorito = false });
            }
            else
            {
                var favorito = new Favorito
                {
                    UsuarioId = userId,
                    ProductoId = id,
                    FechaGuardado = DateTime.UtcNow
                };
                _context.Favoritos.Add(favorito);
                await _context.SaveChangesAsync();
                return Json(new { esFavorito = true });
            }
        }
    }
}
