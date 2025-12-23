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
    /// Servicio para la gestión de categorías de productos
    /// </summary>
    public class CategoriasService
    {
        private readonly TiendaDbContext _context;
        private readonly ILogger<CategoriasService> _logger;

        public CategoriasService(TiendaDbContext context, ILogger<CategoriasService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region CRUD Básico

        /// <summary>
        /// Agrega una nueva categoría
        /// </summary>
        /// <param name="categoria">Categoría a agregar</param>
        /// <returns>True si se agregó correctamente, False si hubo error</returns>
        public async Task<bool> AddAsync(Categorias categoria)
        {
            if (categoria == null)
            {
                _logger.LogWarning("Intento de agregar categoría null");
                return false;
            }

            if (string.IsNullOrWhiteSpace(categoria.Nombre))
            {
                _logger.LogWarning("Intento de agregar categoría sin nombre");
                return false;
            }

            try
            {
                _logger.LogInformation("Agregando nueva categoría: {Nombre}", categoria.Nombre);

                // Verificar si ya existe una categoría con el mismo nombre
                var existe = await _context.Categorias
                    .AsNoTracking()
                    .AnyAsync(c => c.Nombre.ToLower() == categoria.Nombre.ToLower());

                if (existe)
                {
                    _logger.LogWarning("Ya existe una categoría con el nombre '{Nombre}'", categoria.Nombre);
                    return false;
                }

                await _context.Categorias.AddAsync(categoria);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Categoría {CategoriaId} - '{Nombre}' agregada exitosamente",
                    categoria.CategoriaID, categoria.Nombre);
                return true;
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Error de base de datos al agregar categoría '{Nombre}'", categoria.Nombre);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al agregar categoría '{Nombre}'", categoria.Nombre);
                return false;
            }
        }

        /// <summary>
        /// Obtiene todas las categorías
        /// </summary>
        /// <returns>Lista de todas las categorías</returns>
        public async Task<List<Categorias>> GetAllAsync()
        {
            try
            {
                _logger.LogDebug("Obteniendo todas las categorías");

                return await _context.Categorias
                    .AsNoTracking()
                    .OrderBy(c => c.Nombre)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener todas las categorías");
                return new List<Categorias>();
            }
        }

        /// <summary>
        /// Obtiene una categoría por su ID
        /// </summary>
        /// <param name="id">ID de la categoría</param>
        /// <returns>Categoría encontrada o null si no existe</returns>
        public async Task<Categorias> GetByIdAsync(int id)
        {
            if (id <= 0)
            {
                _logger.LogWarning("Intento de obtener categoría con ID inválido: {Id}", id);
                return null;
            }

            try
            {
                _logger.LogDebug("Obteniendo categoría {CategoriaId}", id);

                return await _context.Categorias
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.CategoriaID == id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener categoría {CategoriaId}", id);
                return null;
            }
        }

        /// <summary>
        /// Actualiza una categoría existente
        /// </summary>
        /// <param name="categoria">Categoría con datos actualizados</param>
        /// <returns>True si se actualizó correctamente, False si hubo error</returns>
        public async Task<bool> UpdateAsync(Categorias categoria)
        {
            if (categoria == null)
            {
                _logger.LogWarning("Intento de actualizar categoría null");
                return false;
            }

            if (string.IsNullOrWhiteSpace(categoria.Nombre))
            {
                _logger.LogWarning("Intento de actualizar categoría {CategoriaId} sin nombre", categoria.CategoriaID);
                return false;
            }

            try
            {
                _logger.LogInformation("Actualizando categoría {CategoriaId} - '{Nombre}'",
                    categoria.CategoriaID, categoria.Nombre);

                // Verificar si existe
                var existe = await _context.Categorias
                    .AsNoTracking()
                    .AnyAsync(c => c.CategoriaID == categoria.CategoriaID);

                if (!existe)
                {
                    _logger.LogWarning("Categoría {CategoriaId} no existe", categoria.CategoriaID);
                    return false;
                }

                // Verificar si el nuevo nombre ya está en uso por otra categoría
                var nombreDuplicado = await _context.Categorias
                    .AsNoTracking()
                    .AnyAsync(c => c.CategoriaID != categoria.CategoriaID &&
                                   c.Nombre.ToLower() == categoria.Nombre.ToLower());

                if (nombreDuplicado)
                {
                    _logger.LogWarning("El nombre '{Nombre}' ya está en uso por otra categoría", categoria.Nombre);
                    return false;
                }

                _context.Categorias.Update(categoria);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Categoría {CategoriaId} actualizada exitosamente", categoria.CategoriaID);
                return true;
            }
            catch (DbUpdateConcurrencyException concEx)
            {
                _logger.LogError(concEx, "Error de concurrencia al actualizar categoría {CategoriaId}",
                    categoria.CategoriaID);
                return false;
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Error de base de datos al actualizar categoría {CategoriaId}",
                    categoria.CategoriaID);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al actualizar categoría {CategoriaId}",
                    categoria.CategoriaID);
                return false;
            }
        }

        /// <summary>
        /// Elimina una categoría si no tiene productos asociados
        /// </summary>
        /// <param name="id">ID de la categoría a eliminar</param>
        /// <returns>True si se eliminó correctamente, False si hubo error o tiene dependencias</returns>
        public async Task<bool> DeleteAsync(int id)
        {
            if (id <= 0)
            {
                _logger.LogWarning("Intento de eliminar categoría con ID inválido: {Id}", id);
                return false;
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _logger.LogInformation("Intentando eliminar categoría {CategoriaId}", id);

                var categoria = await _context.Categorias.FindAsync(id);
                if (categoria == null)
                {
                    _logger.LogWarning("Categoría {CategoriaId} no encontrada", id);
                    return false;
                }

                // ✅ CRÍTICO: Verificar si tiene productos asociados
                var tieneProductos = await _context.Productos
                    .AsNoTracking()
                    .AnyAsync(p => p.CategoriaID == id);

                if (tieneProductos)
                {
                    _logger.LogWarning("No se puede eliminar categoría {CategoriaId} - '{Nombre}' porque tiene productos asociados",
                        id, categoria.Nombre);
                    await transaction.RollbackAsync();
                    return false;
                }

                // ✅ Verificar si tiene subcategorías asociadas
                var tieneSubcategorias = await _context.Subcategorias
                    .AsNoTracking()
                    .AnyAsync(s => s.CategoriaID == id);

                if (tieneSubcategorias)
                {
                    _logger.LogWarning("No se puede eliminar categoría {CategoriaId} - '{Nombre}' porque tiene subcategorías asociadas",
                        id, categoria.Nombre);
                    await transaction.RollbackAsync();
                    return false;
                }

                _context.Categorias.Remove(categoria);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Categoría {CategoriaId} - '{Nombre}' eliminada exitosamente",
                    id, categoria.Nombre);
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error al eliminar categoría {CategoriaId}. Transacción revertida", id);
                return false;
            }
        }

        #endregion

        #region Métodos Útiles Adicionales

        /// <summary>
        /// Verifica si existe una categoría con el ID especificado
        /// </summary>
        public async Task<bool> ExistsAsync(int id)
        {
            if (id <= 0) return false;

            try
            {
                return await _context.Categorias
                    .AsNoTracking()
                    .AnyAsync(c => c.CategoriaID == id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al verificar existencia de categoría {CategoriaId}", id);
                return false;
            }
        }

        /// <summary>
        /// Busca categorías por nombre (búsqueda parcial)
        /// </summary>
        public async Task<List<Categorias>> SearchByNameAsync(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return await GetAllAsync();

            try
            {
                _logger.LogDebug("Buscando categorías con término: {SearchTerm}", searchTerm);

                var term = searchTerm.ToLower();
                return await _context.Categorias
                    .AsNoTracking()
                    .Where(c => c.Nombre.ToLower().Contains(term))
                    .OrderBy(c => c.Nombre)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al buscar categorías con término '{SearchTerm}'", searchTerm);
                return new List<Categorias>();
            }
        }

        /// <summary>
        /// Obtiene el conteo de productos por categoría
        /// </summary>
        public async Task<Dictionary<int, int>> GetProductCountByCategoryAsync()
        {
            try
            {
                _logger.LogDebug("Obteniendo conteo de productos por categoría");

                return await _context.Productos
                    .AsNoTracking()
                    .GroupBy(p => p.CategoriaID)
                    .Select(g => new { CategoriaId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.CategoriaId, x => x.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener conteo de productos por categoría");
                return new Dictionary<int, int>();
            }
        }

        /// <summary>
        /// Obtiene categorías con el conteo de productos
        /// </summary>
        public async Task<List<CategoriaConConteo>> GetCategoriasConConteoAsync()
        {
            try
            {
                _logger.LogDebug("Obteniendo categorías con conteo de productos");

                var categorias = await _context.Categorias
                    .AsNoTracking()
                    .OrderBy(c => c.Nombre)
                    .ToListAsync();

                var productCounts = await GetProductCountByCategoryAsync();

                return categorias.Select(c => new CategoriaConConteo
                {
                    CategoriaID = c.CategoriaID,
                    Nombre = c.Nombre,
                    CantidadProductos = productCounts.ContainsKey(c.CategoriaID)
                        ? productCounts[c.CategoriaID]
                        : 0
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener categorías con conteo");
                return new List<CategoriaConConteo>();
            }
        }

        /// <summary>
        /// Obtiene categorías que tienen productos
        /// </summary>
        public async Task<List<Categorias>> GetCategoriasConProductosAsync()
        {
            try
            {
                _logger.LogDebug("Obteniendo categorías con productos");

                return await _context.Categorias
                    .AsNoTracking()
                    .Where(c => _context.Productos.Any(p => p.CategoriaID == c.CategoriaID))
                    .OrderBy(c => c.Nombre)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener categorías con productos");
                return new List<Categorias>();
            }
        }

        /// <summary>
        /// Obtiene categorías vacías (sin productos)
        /// </summary>
        public async Task<List<Categorias>> GetCategoriasVaciasAsync()
        {
            try
            {
                _logger.LogDebug("Obteniendo categorías vacías");

                return await _context.Categorias
                    .AsNoTracking()
                    .Where(c => !_context.Productos.Any(p => p.CategoriaID == c.CategoriaID))
                    .OrderBy(c => c.Nombre)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener categorías vacías");
                return new List<Categorias>();
            }
        }

        #endregion
    }

    #region DTOs

    /// <summary>
    /// DTO para categoría con conteo de productos
    /// </summary>
    public class CategoriaConConteo
    {
        public int CategoriaID { get; set; }
        public string Nombre { get; set; }
        public int CantidadProductos { get; set; }
    }

    #endregion
}