using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Simone.Data;
using Simone.Models;

namespace Simone.Services
{
    /// <summary>
    /// Servicio para la gestión de subcategorías de productos con soporte multi-vendedor
    /// </summary>
    public class SubcategoriasService
    {
        private readonly TiendaDbContext _context;
        private readonly ILogger<SubcategoriasService> _logger;

        public SubcategoriasService(TiendaDbContext context, ILogger<SubcategoriasService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region CREATE

        /// <summary>
        /// Crea una subcategoría. Debe venir con VendedorID seteado desde el controlador.
        /// Enforce: NombreSubcategoria.Trim() y unicidad por (VendedorID, CategoriaID, NombreSubcategoria).
        /// </summary>
        public async Task<bool> AddAsync(Subcategorias subcategoria)
        {
            if (subcategoria == null)
            {
                _logger.LogWarning("Intento de agregar subcategoría null");
                return false;
            }

            subcategoria.NombreSubcategoria = (subcategoria.NombreSubcategoria ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(subcategoria.NombreSubcategoria))
            {
                _logger.LogWarning("Intento de agregar subcategoría sin nombre");
                return false;
            }

            if (string.IsNullOrWhiteSpace(subcategoria.VendedorID))
            {
                _logger.LogWarning("Intento de agregar subcategoría sin VendedorID");
                return false;
            }

            if (subcategoria.CategoriaID <= 0)
            {
                _logger.LogWarning("Intento de agregar subcategoría con CategoriaID inválido: {CategoriaId}",
                    subcategoria.CategoriaID);
                return false;
            }

            try
            {
                _logger.LogInformation("Agregando subcategoría '{Nombre}' para vendedor {VendedorId} en categoría {CategoriaId}",
                    subcategoria.NombreSubcategoria, subcategoria.VendedorID, subcategoria.CategoriaID);

                // Pre-chequeo de duplicado (evita exception por índice único)
                bool dup = await _context.Subcategorias
                    .AsNoTracking()
                    .AnyAsync(s =>
                        s.VendedorID == subcategoria.VendedorID &&
                        s.CategoriaID == subcategoria.CategoriaID &&
                        s.NombreSubcategoria == subcategoria.NombreSubcategoria);

                if (dup)
                {
                    _logger.LogWarning("Ya existe subcategoría '{Nombre}' para vendedor {VendedorId} en categoría {CategoriaId}",
                        subcategoria.NombreSubcategoria, subcategoria.VendedorID, subcategoria.CategoriaID);
                    return false;
                }

                await _context.Subcategorias.AddAsync(subcategoria);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Subcategoría {SubcategoriaId} - '{Nombre}' creada exitosamente",
                    subcategoria.SubcategoriaID, subcategoria.NombreSubcategoria);
                return true;
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Error de BD al agregar subcategoría '{Nombre}'",
                    subcategoria.NombreSubcategoria);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al agregar subcategoría '{Nombre}'",
                    subcategoria.NombreSubcategoria);
                return false;
            }
        }

        #endregion

        #region READ - Basic

        /// <summary>
        /// Todas las subcategorías (sin filtro). Úsalo sólo para administración global.
        /// </summary>
        public async Task<List<Subcategorias>> GetAllAsync()
        {
            try
            {
                _logger.LogDebug("Obteniendo todas las subcategorías");

                return await _context.Subcategorias
                    .AsNoTracking()
                    .OrderBy(s => s.CategoriaID)
                    .ThenBy(s => s.NombreSubcategoria)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener todas las subcategorías");
                return new List<Subcategorias>();
            }
        }

        /// <summary>
        /// Subcategorías del vendedor indicado.
        /// </summary>
        public async Task<List<Subcategorias>> GetAllByVendedorAsync(string vendedorId)
        {
            if (string.IsNullOrWhiteSpace(vendedorId))
            {
                _logger.LogWarning("GetAllByVendedorAsync llamado con vendedorId vacío");
                return new List<Subcategorias>();
            }

            try
            {
                _logger.LogDebug("Obteniendo subcategorías del vendedor {VendedorId}", vendedorId);

                return await _context.Subcategorias
                    .AsNoTracking()
                    .Where(s => s.VendedorID == vendedorId)
                    .OrderBy(s => s.CategoriaID)
                    .ThenBy(s => s.NombreSubcategoria)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener subcategorías del vendedor {VendedorId}", vendedorId);
                return new List<Subcategorias>();
            }
        }

        /// <summary>
        /// Subcategorías con la entidad Categoría cargada (sin filtro).
        /// </summary>
        public async Task<List<Subcategorias>> GetAllSubcategoriasWithCategoriaAsync()
        {
            try
            {
                _logger.LogDebug("Obteniendo todas las subcategorías con categoría");

                return await _context.Subcategorias
                    .AsNoTracking()
                    .Include(s => s.Categoria)
                    .OrderBy(s => s.CategoriaID)
                    .ThenBy(s => s.NombreSubcategoria)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener subcategorías con categoría");
                return new List<Subcategorias>();
            }
        }

        /// <summary>
        /// Subcategorías del vendedor con Categoría cargada.
        /// </summary>
        public async Task<List<Subcategorias>> GetAllByVendedorWithCategoriaAsync(string vendedorId)
        {
            if (string.IsNullOrWhiteSpace(vendedorId))
            {
                _logger.LogWarning("GetAllByVendedorWithCategoriaAsync llamado con vendedorId vacío");
                return new List<Subcategorias>();
            }

            try
            {
                _logger.LogDebug("Obteniendo subcategorías del vendedor {VendedorId} con categoría", vendedorId);

                return await _context.Subcategorias
                    .AsNoTracking()
                    .Where(s => s.VendedorID == vendedorId)
                    .Include(s => s.Categoria)
                    .OrderBy(s => s.CategoriaID)
                    .ThenBy(s => s.NombreSubcategoria)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener subcategorías del vendedor {VendedorId} con categoría", vendedorId);
                return new List<Subcategorias>();
            }
        }

        /// <summary>
        /// Obtiene una subcategoría por ID (seguirá trackeada).
        /// </summary>
        public async Task<Subcategorias> GetByIdAsync(int id)
        {
            if (id <= 0)
            {
                _logger.LogWarning("GetByIdAsync llamado con ID inválido: {Id}", id);
                return null;
            }

            try
            {
                _logger.LogDebug("Obteniendo subcategoría {SubcategoriaId}", id);
                return await _context.Subcategorias.FindAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener subcategoría {SubcategoriaId}", id);
                return null;
            }
        }

        /// <summary>
        /// Obtiene una subcategoría por ID sólo si pertenece al vendedor.
        /// </summary>
        public async Task<Subcategorias> GetByIdForVendedorAsync(int id, string vendedorId)
        {
            if (id <= 0 || string.IsNullOrWhiteSpace(vendedorId))
            {
                _logger.LogWarning("GetByIdForVendedorAsync llamado con parámetros inválidos: Id={Id}, VendedorId={VendedorId}",
                    id, vendedorId);
                return null;
            }

            try
            {
                _logger.LogDebug("Obteniendo subcategoría {SubcategoriaId} para vendedor {VendedorId}", id, vendedorId);

                return await _context.Subcategorias
                    .FirstOrDefaultAsync(s => s.SubcategoriaID == id && s.VendedorID == vendedorId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener subcategoría {SubcategoriaId} para vendedor {VendedorId}",
                    id, vendedorId);
                return null;
            }
        }

        #endregion

        #region READ - By Filters

        /// <summary>
        /// Subcategorías por categoría (sin filtro de vendedor).
        /// </summary>
        public async Task<List<Subcategorias>> GetByCategoriaIdAsync(int categoriaID)
        {
            if (categoriaID <= 0)
            {
                _logger.LogWarning("GetByCategoriaIdAsync llamado con CategoriaID inválido: {CategoriaId}", categoriaID);
                return new List<Subcategorias>();
            }

            try
            {
                _logger.LogDebug("Obteniendo subcategorías de categoría {CategoriaId}", categoriaID);

                return await _context.Subcategorias
                    .AsNoTracking()
                    .Where(s => s.CategoriaID == categoriaID)
                    .OrderBy(s => s.NombreSubcategoria)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener subcategorías de categoría {CategoriaId}", categoriaID);
                return new List<Subcategorias>();
            }
        }

        /// <summary>
        /// Subcategorías por categoría y vendedor.
        /// </summary>
        public async Task<List<Subcategorias>> GetByCategoriaIdForVendedorAsync(int categoriaID, string vendedorId)
        {
            if (categoriaID <= 0 || string.IsNullOrWhiteSpace(vendedorId))
            {
                _logger.LogWarning("GetByCategoriaIdForVendedorAsync llamado con parámetros inválidos: CategoriaId={CategoriaId}, VendedorId={VendedorId}",
                    categoriaID, vendedorId);
                return new List<Subcategorias>();
            }

            try
            {
                _logger.LogDebug("Obteniendo subcategorías de categoría {CategoriaId} para vendedor {VendedorId}",
                    categoriaID, vendedorId);

                return await _context.Subcategorias
                    .AsNoTracking()
                    .Where(s => s.CategoriaID == categoriaID && s.VendedorID == vendedorId)
                    .OrderBy(s => s.NombreSubcategoria)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener subcategorías de categoría {CategoriaId} para vendedor {VendedorId}",
                    categoriaID, vendedorId);
                return new List<Subcategorias>();
            }
        }

        #endregion

        #region UPDATE

        /// <summary>
        /// Actualiza una subcategoría ya trackeada o adjunta el modelo y marca Modified.
        /// No permite cambiar VendedorID.
        /// </summary>
        public async Task<bool> UpdateAsync(Subcategorias subcategoria)
        {
            if (subcategoria == null)
            {
                _logger.LogWarning("Intento de actualizar subcategoría null");
                return false;
            }

            subcategoria.NombreSubcategoria = (subcategoria.NombreSubcategoria ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(subcategoria.NombreSubcategoria))
            {
                _logger.LogWarning("Intento de actualizar subcategoría {SubcategoriaId} sin nombre",
                    subcategoria.SubcategoriaID);
                return false;
            }

            if (subcategoria.CategoriaID <= 0)
            {
                _logger.LogWarning("Intento de actualizar subcategoría {SubcategoriaId} con CategoriaID inválido",
                    subcategoria.SubcategoriaID);
                return false;
            }

            try
            {
                _logger.LogInformation("Actualizando subcategoría {SubcategoriaId} - '{Nombre}'",
                    subcategoria.SubcategoriaID, subcategoria.NombreSubcategoria);

                // Asegurar que no se intente cambiar el VendedorID por accidente
                var original = await _context.Subcategorias
                    .AsNoTracking()
                    .Where(s => s.SubcategoriaID == subcategoria.SubcategoriaID)
                    .Select(s => new { s.VendedorID })
                    .FirstOrDefaultAsync();

                if (original == null)
                {
                    _logger.LogWarning("Subcategoría {SubcategoriaId} no encontrada para actualizar",
                        subcategoria.SubcategoriaID);
                    return false;
                }

                subcategoria.VendedorID = original.VendedorID;

                _context.Subcategorias.Update(subcategoria);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Subcategoría {SubcategoriaId} actualizada exitosamente",
                    subcategoria.SubcategoriaID);
                return true;
            }
            catch (DbUpdateConcurrencyException concEx)
            {
                _logger.LogError(concEx, "Error de concurrencia al actualizar subcategoría {SubcategoriaId}",
                    subcategoria.SubcategoriaID);
                return false;
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Error de BD al actualizar subcategoría {SubcategoriaId}",
                    subcategoria.SubcategoriaID);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al actualizar subcategoría {SubcategoriaId}",
                    subcategoria.SubcategoriaID);
                return false;
            }
        }

        #endregion

        #region DELETE

        /// <summary>
        /// Elimina por ID (sin verificar dueño). Usa DeleteForVendedorAsync si necesitas verificar propietario.
        /// ✅ MEJORADO: Ahora verifica si tiene productos asociados antes de eliminar
        /// </summary>
        public async Task<bool> DeleteAsync(int id)
        {
            if (id <= 0)
            {
                _logger.LogWarning("Intento de eliminar subcategoría con ID inválido: {Id}", id);
                return false;
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _logger.LogInformation("Intentando eliminar subcategoría {SubcategoriaId}", id);

                var sub = await _context.Subcategorias.FindAsync(id);
                if (sub == null)
                {
                    _logger.LogWarning("Subcategoría {SubcategoriaId} no encontrada", id);
                    return false;
                }

                // ✅ CRÍTICO: Verificar si tiene productos asociados
                var tieneProductos = await _context.Productos
                    .AsNoTracking()
                    .AnyAsync(p => p.SubcategoriaID == id);

                if (tieneProductos)
                {
                    _logger.LogWarning("No se puede eliminar subcategoría {SubcategoriaId} - '{Nombre}' porque tiene productos asociados",
                        id, sub.NombreSubcategoria);
                    await transaction.RollbackAsync();
                    return false;
                }

                _context.Subcategorias.Remove(sub);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Subcategoría {SubcategoriaId} - '{Nombre}' eliminada exitosamente",
                    id, sub.NombreSubcategoria);
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error al eliminar subcategoría {SubcategoriaId}. Transacción revertida", id);
                return false;
            }
        }

        /// <summary>
        /// Elimina por ID sólo si pertenece al vendedor.
        /// ✅ MEJORADO: Ahora verifica si tiene productos asociados antes de eliminar
        /// </summary>
        public async Task<bool> DeleteForVendedorAsync(int id, string vendedorId)
        {
            if (id <= 0 || string.IsNullOrWhiteSpace(vendedorId))
            {
                _logger.LogWarning("DeleteForVendedorAsync llamado con parámetros inválidos: Id={Id}, VendedorId={VendedorId}",
                    id, vendedorId);
                return false;
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _logger.LogInformation("Intentando eliminar subcategoría {SubcategoriaId} para vendedor {VendedorId}",
                    id, vendedorId);

                var sub = await _context.Subcategorias
                    .FirstOrDefaultAsync(s => s.SubcategoriaID == id && s.VendedorID == vendedorId);

                if (sub == null)
                {
                    _logger.LogWarning("Subcategoría {SubcategoriaId} no encontrada para vendedor {VendedorId}",
                        id, vendedorId);
                    return false;
                }

                // ✅ CRÍTICO: Verificar si tiene productos asociados
                var tieneProductos = await _context.Productos
                    .AsNoTracking()
                    .AnyAsync(p => p.SubcategoriaID == id && p.VendedorID == vendedorId);

                if (tieneProductos)
                {
                    _logger.LogWarning("No se puede eliminar subcategoría {SubcategoriaId} - '{Nombre}' del vendedor {VendedorId} porque tiene productos asociados",
                        id, sub.NombreSubcategoria, vendedorId);
                    await transaction.RollbackAsync();
                    return false;
                }

                _context.Subcategorias.Remove(sub);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Subcategoría {SubcategoriaId} - '{Nombre}' eliminada exitosamente para vendedor {VendedorId}",
                    id, sub.NombreSubcategoria, vendedorId);
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error al eliminar subcategoría {SubcategoriaId} para vendedor {VendedorId}. Transacción revertida",
                    id, vendedorId);
                return false;
            }
        }

        #endregion

        #region Métodos Útiles Adicionales

        /// <summary>
        /// Verifica si existe una subcategoría con el ID especificado
        /// </summary>
        public async Task<bool> ExistsAsync(int id)
        {
            if (id <= 0) return false;

            try
            {
                return await _context.Subcategorias
                    .AsNoTracking()
                    .AnyAsync(s => s.SubcategoriaID == id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al verificar existencia de subcategoría {SubcategoriaId}", id);
                return false;
            }
        }

        /// <summary>
        /// Busca subcategorías por nombre (búsqueda parcial)
        /// </summary>
        public async Task<List<Subcategorias>> SearchByNameAsync(string searchTerm, string vendedorId = null)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return string.IsNullOrWhiteSpace(vendedorId)
                    ? await GetAllAsync()
                    : await GetAllByVendedorAsync(vendedorId);
            }

            try
            {
                _logger.LogDebug("Buscando subcategorías con término: {SearchTerm}", searchTerm);

                var query = _context.Subcategorias.AsNoTracking();

                if (!string.IsNullOrWhiteSpace(vendedorId))
                    query = query.Where(s => s.VendedorID == vendedorId);

                var term = searchTerm.ToLower();
                return await query
                    .Where(s => s.NombreSubcategoria.ToLower().Contains(term))
                    .OrderBy(s => s.NombreSubcategoria)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al buscar subcategorías con término '{SearchTerm}'", searchTerm);
                return new List<Subcategorias>();
            }
        }

        /// <summary>
        /// Obtiene el conteo de productos por subcategoría
        /// </summary>
        public async Task<Dictionary<int, int>> GetProductCountBySubcategoryAsync(string vendedorId = null)
        {
            try
            {
                _logger.LogDebug("Obteniendo conteo de productos por subcategoría");

                var query = _context.Productos.AsNoTracking();

                if (!string.IsNullOrWhiteSpace(vendedorId))
                    query = query.Where(p => p.VendedorID == vendedorId);

                // ✅ CORREGIDO: Sin .HasValue ni .Value
                return await query
                    .Where(p => p.SubcategoriaID != null && p.SubcategoriaID > 0)  // ✅ Verificación simple
                    .GroupBy(p => p.SubcategoriaID)                                 // ✅ Sin .Value
                    .Select(g => new { SubcategoriaId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.SubcategoriaId, x => x.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener conteo de productos por subcategoría");
                return new Dictionary<int, int>();
            }
        }

        /// <summary>
        /// Obtiene subcategorías con el conteo de productos
        /// </summary>
        public async Task<List<SubcategoriaConConteo>> GetSubcategoriasConConteoAsync(string vendedorId = null)
        {
            try
            {
                _logger.LogDebug("Obteniendo subcategorías con conteo de productos");

                var subcategorias = string.IsNullOrWhiteSpace(vendedorId)
                    ? await GetAllAsync()
                    : await GetAllByVendedorAsync(vendedorId);

                var productCounts = await GetProductCountBySubcategoryAsync(vendedorId);

                return subcategorias.Select(s => new SubcategoriaConConteo
                {
                    SubcategoriaID = s.SubcategoriaID,
                    NombreSubcategoria = s.NombreSubcategoria,
                    CategoriaID = s.CategoriaID,
                    VendedorID = s.VendedorID,
                    CantidadProductos = productCounts.ContainsKey(s.SubcategoriaID)
                        ? productCounts[s.SubcategoriaID]
                        : 0
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener subcategorías con conteo");
                return new List<SubcategoriaConConteo>();
            }
        }

        /// <summary>
        /// Obtiene subcategorías que tienen productos
        /// </summary>
        public async Task<List<Subcategorias>> GetSubcategoriasConProductosAsync(string vendedorId = null)
        {
            try
            {
                _logger.LogDebug("Obteniendo subcategorías con productos");

                var query = _context.Subcategorias.AsNoTracking();

                if (!string.IsNullOrWhiteSpace(vendedorId))
                    query = query.Where(s => s.VendedorID == vendedorId);

                return await query
                    .Where(s => _context.Productos.Any(p => p.SubcategoriaID == s.SubcategoriaID))
                    .OrderBy(s => s.NombreSubcategoria)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener subcategorías con productos");
                return new List<Subcategorias>();
            }
        }

        /// <summary>
        /// Obtiene subcategorías vacías (sin productos)
        /// </summary>
        public async Task<List<Subcategorias>> GetSubcategoriasVaciasAsync(string vendedorId = null)
        {
            try
            {
                _logger.LogDebug("Obteniendo subcategorías vacías");

                var query = _context.Subcategorias.AsNoTracking();

                if (!string.IsNullOrWhiteSpace(vendedorId))
                    query = query.Where(s => s.VendedorID == vendedorId);

                return await query
                    .Where(s => !_context.Productos.Any(p => p.SubcategoriaID == s.SubcategoriaID))
                    .OrderBy(s => s.NombreSubcategoria)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener subcategorías vacías");
                return new List<Subcategorias>();
            }
        }

        #endregion
    }

    #region DTOs

    /// <summary>
    /// DTO para subcategoría con conteo de productos
    /// </summary>
    public class SubcategoriaConConteo
    {
        public int SubcategoriaID { get; set; }
        public string NombreSubcategoria { get; set; }
        public int CategoriaID { get; set; }
        public string VendedorID { get; set; }
        public int CantidadProductos { get; set; }
    }

    #endregion
}