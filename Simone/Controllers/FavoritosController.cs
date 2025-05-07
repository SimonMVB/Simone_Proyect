using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Simone.Data;
using Simone.Models;
using System.Security.Claims;

namespace Simone.Controllers
{
    public class FavoritosController : Controller
    {
        private readonly TiendaDbContext _context;
        private readonly ILogger<FavoritosController> _logger;

        public FavoritosController(DbContext context, ILogger<FavoritosController> logger)
        {
            _context = (TiendaDbContext?)context;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("Usuario sin ID accedió a Favoritos.");
                    return RedirectToAction("Login", "Cuenta");
                }

                var productosFavoritos = await _context.Favoritos
                    .Where(f => f.UsuarioId == userId)
                    .Include(f => f.Producto)
                    .Select(f => f.Producto)
                    .AsNoTracking()
                    .ToListAsync();

                return View(productosFavoritos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar productos favoritos.");
                TempData["MensajeError"] = "Hubo un problema al cargar tus productos favoritos.";
                return RedirectToAction("Index", "Home");
            }
        }
    }
}
