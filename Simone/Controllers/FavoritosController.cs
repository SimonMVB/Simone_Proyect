using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Simone.Data;
using Simone.Models;

namespace Simone.Controllers
{
    /// <summary>
    /// Controlador de favoritos del usuario
    /// Permite guardar y gestionar productos favoritos
    /// Versión optimizada con mejores prácticas empresariales
    /// </summary>
    [Authorize]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class FavoritosController : Controller
    {
        #region Dependencias

        private readonly TiendaDbContext _context;
        private readonly ILogger<FavoritosController> _logger;

        #endregion

        #region Constantes

        // Claims
        private const string CLAIM_NAME_IDENTIFIER = ClaimTypes.NameIdentifier;

        // Mensajes
        private const string MSG_ERROR_NO_AUTENTICADO = "Usuario no autenticado.";
        private const string MSG_ERROR_PRODUCTO_NO_ENCONTRADO = "Producto no encontrado.";
        private const string MSG_ERROR_TOGGLE_FAVORITO = "Error al procesar el favorito.";
        private const string MSG_INFO_RECONCILIACION = "Reconciliando estado de favorito por concurrencia.";

        // Responses
        private const string RESPONSE_PROPERTY_MESSAGE = "message";
        private const string RESPONSE_PROPERTY_ES_FAVORITO = "esFavorito";

        #endregion

        #region Constructor

        /// <summary>
        /// Constructor con inyección de dependencias
        /// </summary>
        public FavoritosController(
            TiendaDbContext context,
            ILogger<FavoritosController> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Obtiene el ID del usuario actual
        /// </summary>
        private string? ObtenerUsuarioId() => User.FindFirstValue(CLAIM_NAME_IDENTIFIER);

        #endregion

        #region Listado de Favoritos

        /// <summary>
        /// GET: /Favoritos
        /// Lista de productos favoritos del usuario
        /// </summary>
        /// <param name="ct">Token de cancelación</param>
        /// <returns>Vista con lista de favoritos</returns>
        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken ct = default)
        {
            try
            {
                var userId = ObtenerUsuarioId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    _logger.LogWarning("Acceso a favoritos sin autenticación");
                    return Unauthorized();
                }

                _logger.LogInformation(
                    "Cargando favoritos. UsuarioId: {UsuarioId}",
                    userId);

                var favoritos = await _context.Favoritos
                    .AsNoTracking()
                    .Include(f => f.Producto)
                    .Where(f => f.UsuarioId == userId)
                    .OrderByDescending(f => f.FechaGuardado)
                    .ToListAsync(ct);

                _logger.LogDebug(
                    "Favoritos cargados. UsuarioId: {UsuarioId}, Count: {Count}",
                    userId,
                    favoritos.Count);

                return View(favoritos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar favoritos");
                return View(new List<Favorito>());
            }
        }

        #endregion

        #region Toggle Favorito

        /// <summary>
        /// POST: /Favoritos/Toggle/{id}
        /// Añade o quita un producto de favoritos
        /// </summary>
        /// <param name="id">ID del producto</param>
        /// <param name="ct">Token de cancelación</param>
        /// <returns>JSON con estado del favorito</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Produces("application/json")]
        public async Task<IActionResult> Toggle(
            [FromRoute] int id,
            CancellationToken ct = default)
        {
            try
            {
                var userId = ObtenerUsuarioId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    _logger.LogWarning(
                        "Intento de toggle favorito sin autenticación. ProductoId: {ProductoId}",
                        id);
                    return Unauthorized();
                }

                _logger.LogInformation(
                    "Toggle favorito solicitado. UsuarioId: {UsuarioId}, ProductoId: {ProductoId}",
                    userId,
                    id);

                // Validación temprana: evita FK error si el producto no existe
                var existeProducto = await _context.Productos
                    .AsNoTracking()
                    .AnyAsync(p => p.ProductoID == id, ct);

                if (!existeProducto)
                {
                    _logger.LogWarning(
                        "Intento de toggle favorito con producto inexistente. ProductoId: {ProductoId}",
                        id);

                    return NotFound(new { message = MSG_ERROR_PRODUCTO_NO_ENCONTRADO });
                }

                // Intento de localizar el favorito actual
                var existente = await _context.Favoritos
                    .FirstOrDefaultAsync(f => f.UsuarioId == userId && f.ProductoId == id, ct);

                if (existente != null)
                {
                    // ELIMINAR FAVORITO
                    return await EliminarFavoritoAsync(existente, userId, id, ct);
                }
                else
                {
                    // AÑADIR FAVORITO
                    return await AnadirFavoritoAsync(userId, id, ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error inesperado en Toggle. ProductoId: {ProductoId}",
                    id);

                return StatusCode(500, new { message = MSG_ERROR_TOGGLE_FAVORITO });
            }
        }

        #endregion

        #region Helpers - Toggle

        /// <summary>
        /// Elimina un favorito existente
        /// </summary>
        private async Task<IActionResult> EliminarFavoritoAsync(
            Favorito existente,
            string userId,
            int productoId,
            CancellationToken ct)
        {
            _context.Favoritos.Remove(existente);

            try
            {
                await _context.SaveChangesAsync(ct);

                _logger.LogInformation(
                    "Favorito eliminado. UsuarioId: {UsuarioId}, ProductoId: {ProductoId}",
                    userId,
                    productoId);

                return Json(new FavoritoResponse(false));
            }
            catch (DbUpdateException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Error al eliminar favorito (posible estado concurrente). UsuarioId: {UsuarioId}, ProductoId: {ProductoId}",
                    userId,
                    productoId);

                // Reconciliar estado real desde BD
                var aunExiste = await _context.Favoritos
                    .AsNoTracking()
                    .AnyAsync(f => f.UsuarioId == userId && f.ProductoId == productoId, ct);

                _logger.LogDebug(
                    "Estado reconciliado después de error. UsuarioId: {UsuarioId}, ProductoId: {ProductoId}, Existe: {Existe}",
                    userId,
                    productoId,
                    aunExiste);

                return Json(new FavoritoResponse(aunExiste));
            }
        }

        /// <summary>
        /// Añade un nuevo favorito
        /// </summary>
        private async Task<IActionResult> AnadirFavoritoAsync(
            string userId,
            int productoId,
            CancellationToken ct)
        {
            var favorito = new Favorito
            {
                UsuarioId = userId,
                ProductoId = productoId
                // FechaGuardado: default en BD con GETUTCDATE()
            };

            _context.Favoritos.Add(favorito);

            try
            {
                await _context.SaveChangesAsync(ct);

                _logger.LogInformation(
                    "Favorito añadido. UsuarioId: {UsuarioId}, ProductoId: {ProductoId}",
                    userId,
                    productoId);

                return Json(new FavoritoResponse(true));
            }
            catch (DbUpdateException ex)
            {
                // Puede ocurrir si hubo un toggle concurrente y saltó el índice único (UsuarioId, ProductoId)
                _logger.LogInformation(
                    ex,
                    "Conflicto de unicidad al agregar favorito. Reconciliando estado. UsuarioId: {UsuarioId}, ProductoId: {ProductoId}",
                    userId,
                    productoId);

                var yaExiste = await _context.Favoritos
                    .AsNoTracking()
                    .AnyAsync(f => f.UsuarioId == userId && f.ProductoId == productoId, ct);

                _logger.LogDebug(
                    "Estado reconciliado después de conflicto. UsuarioId: {UsuarioId}, ProductoId: {ProductoId}, Existe: {Existe}",
                    userId,
                    productoId,
                    yaExiste);

                return Json(new FavoritoResponse(yaExiste));
            }
        }

        #endregion

        #region Records

        /// <summary>
        /// Respuesta del toggle de favorito
        /// </summary>
        private record FavoritoResponse(bool esFavorito);

        #endregion
    }
}