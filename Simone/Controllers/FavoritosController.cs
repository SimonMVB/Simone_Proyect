using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Simone.Data;
using Simone.Models;

namespace Simone.Controllers
{
    [Authorize]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class FavoritosController : Controller
    {
        private readonly TiendaDbContext _context;
        private readonly ILogger<FavoritosController> _logger;

        public FavoritosController(TiendaDbContext context, ILogger<FavoritosController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: /Favoritos
        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();

            var favoritos = await _context.Favoritos
                .AsNoTracking()
                .Include(f => f.Producto)
                .Where(f => f.UsuarioId == userId)
                .OrderByDescending(f => f.FechaGuardado)
                .ToListAsync(ct);

            return View(favoritos);
        }

        // POST: /Favoritos/Toggle/5
        // Si es AJAX, incluye token anti-CSRF en la vista (_RequestVerificationToken)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Produces("application/json")]
        public async Task<IActionResult> Toggle([FromRoute] int id, CancellationToken ct)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();

            // Validación temprana: evita FK error si el producto no existe
            var existeProducto = await _context.Productos
                .AsNoTracking()
                .AnyAsync(p => p.ProductoID == id, ct);

            if (!existeProducto)
                return NotFound(new { message = "Producto no encontrado." });

            // Intento de localizar el favorito actual
            var existente = await _context.Favoritos
                .FirstOrDefaultAsync(f => f.UsuarioId == userId && f.ProductoId == id, ct);

            if (existente != null)
            {
                _context.Favoritos.Remove(existente);
                try
                {
                    await _context.SaveChangesAsync(ct);
                    return Json(new { esFavorito = false });
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogWarning(ex, "Error al eliminar favorito (posible estado concurrente). Usuario {UserId}, Producto {ProdId}", userId, id);
                    // Reconciliar estado real desde BD
                    var aunExiste = await _context.Favoritos
                        .AsNoTracking()
                        .AnyAsync(f => f.UsuarioId == userId && f.ProductoId == id, ct);
                    return Json(new { esFavorito = aunExiste });
                }
            }
            else
            {
                var favorito = new Favorito
                {
                    UsuarioId = userId,
                    ProductoId = id
                    // FechaGuardado: default en BD con GETUTCDATE()
                };

                _context.Favoritos.Add(favorito);
                try
                {
                    await _context.SaveChangesAsync(ct);
                    return Json(new { esFavorito = true });
                }
                catch (DbUpdateException ex)
                {
                    // Puede ocurrir si hubo un toggle concurrente y saltó el índice único (UsuarioId, ProductoId)
                    _logger.LogInformation(ex, "Conflicto de unicidad al agregar favorito. Reconciliando estado. Usuario {UserId}, Producto {ProdId}", userId, id);

                    var yaExiste = await _context.Favoritos
                        .AsNoTracking()
                        .AnyAsync(f => f.UsuarioId == userId && f.ProductoId == id, ct);

                    return Json(new { esFavorito = yaExiste });
                }
            }
        }
    }
}
