using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Simone.Data;
using Simone.Models;

namespace Simone.Services
{
    #region Interface

    /// <summary>
    /// Servicio de gestión de categorías de productos con operaciones thread-safe
    /// Maneja toda la lógica de negocio relacionada con categorías
    /// Versión optimizada con mejores prácticas empresariales
    /// </summary>
    public interface ICategoriasService
    {
        #region CRUD Básico

        /// <summary>
        /// Agrega una nueva categoría
        /// </summary>
        /// <param name="categoria">Categoría a agregar</param>
        /// <param name="ct">Token de cancelación</param>
        /// <returns>True si se agregó correctamente, False si hubo error</returns>
        Task<bool> AddAsync(Categorias categoria, CancellationToken ct = default);

        /// <summary>
        /// Obtiene todas las categorías ordenadas por nombre
        /// </summary>
        /// <param name="ct">Token de cancelación</param>
        /// <returns>Lista de todas las categorías</returns>
        Task<List<Categorias>> GetAllAsync(CancellationToken ct = default);

        /// <summary>
        /// Obtiene una categoría por su ID
        /// </summary>
        /// <param name="id">ID de la categoría</param>
        /// <param name="ct">Token de cancelación</param>
        /// <returns>Categoría encontrada o null si no existe</returns>
        Task<Categorias> GetByIdAsync(int id, CancellationToken ct = default);

        /// <summary>
        /// Actualiza una categoría existente
        /// </summary>
        /// <param name="categoria">Categoría con datos actualizados</param>
        /// <param name="ct">Token de cancelación</param>
        /// <returns>True si se actualizó correctamente, False si hubo error</returns>
        Task<bool> UpdateAsync(Categorias categoria, CancellationToken ct = default);

        /// <summary>
        /// Elimina una categoría si no tiene dependencias
        /// </summary>
        /// <param name="id">ID de la categoría a eliminar</param>
        /// <param name="ct">Token de cancelación</param>
        /// <returns>True si se eliminó correctamente, False si hubo error o tiene dependencias</returns>
        Task<bool> DeleteAsync(int id, CancellationToken ct = default);

        #endregion

        #region Consultas

        /// <summary>
        /// Verifica si existe una categoría con el ID especificado
        /// </summary>
        /// <param name="id">ID de la categoría</param>
        /// <param name="ct">Token de cancelación</param>
        Task<bool> ExistsAsync(int id, CancellationToken ct = default);

        /// <summary>
        /// Verifica si un nombre de categoría ya está en uso
        /// </summary>
        /// <param name="nombre">Nombre a verificar</param>
        /// <param name="excludeId">ID de categoría a excluir (para updates)</param>
        /// <param name="ct">Token de cancelación</param>
        Task<bool> NombreExisteAsync(string nombre, int? excludeId = null, CancellationToken ct = default);

        /// <summary>
        /// Busca categorías por nombre (búsqueda parcial)
        /// </summary>
        /// <param name="searchTerm">Término de búsqueda</param>
        /// <param name="ct">Token de cancelación</param>
        Task<List<Categorias>> SearchByNameAsync(string searchTerm, CancellationToken ct = default);

        /// <summary>
        /// Obtiene categorías que tienen productos
        /// </summary>
        /// <param name="ct">Token de cancelación</param>
        Task<List<Categorias>> GetCategoriasConProductosAsync(CancellationToken ct = default);

        /// <summary>
        /// Obtiene categorías vacías (sin productos)
        /// </summary>
        /// <param name="ct">Token de cancelación</param>
        Task<List<Categorias>> GetCategoriasVaciasAsync(CancellationToken ct = default);

        #endregion

        #region Estadísticas

        /// <summary>
        /// Obtiene el conteo de productos por categoría
        /// </summary>
        /// <param name="ct">Token de cancelación</param>
        Task<Dictionary<int, int>> GetProductCountByCategoryAsync(CancellationToken ct = default);

        /// <summary>
        /// Obtiene categorías con el conteo de productos
        /// </summary>
        /// <param name="ct">Token de cancelación</param>
        Task<List<CategoriaConConteo>> GetCategoriasConConteoAsync(CancellationToken ct = default);

        /// <summary>
        /// Obtiene estadísticas generales de categorías
        /// </summary>
        /// <param name="ct">Token de cancelación</param>
        Task<CategoriasEstadisticas> GetEstadisticasAsync(CancellationToken ct = default);

        #endregion

        #region Validación

        /// <summary>
        /// Valida si una categoría puede ser eliminada
        /// </summary>
        /// <param name="id">ID de la categoría</param>
        /// <param name="ct">Token de cancelación</param>
        /// <returns>Tupla con resultado y mensaje de error si hay</returns>
        Task<(bool canDelete, string? reason)> CanDeleteAsync(int id, CancellationToken ct = default);

        #endregion
    }

    #endregion

    #region Implementation

    /// <summary>
    /// Implementación del servicio de categorías con operaciones thread-safe
    /// </summary>
    public class CategoriasService : ICategoriasService
    {
        #region Dependencias

        private readonly TiendaDbContext _context;
        private readonly ILogger<CategoriasService> _logger;

        #endregion

        #region Constantes - Mensajes de Log

        // Información
        private const string LOG_INFO_CATEGORIA_AGREGADA = "Categoría agregada. CategoriaId: {CategoriaId}, Nombre: {Nombre}";
        private const string LOG_INFO_CATEGORIA_ACTUALIZADA = "Categoría actualizada. CategoriaId: {CategoriaId}, Nombre: {Nombre}";
        private const string LOG_INFO_CATEGORIA_ELIMINADA = "Categoría eliminada. CategoriaId: {CategoriaId}, Nombre: {Nombre}";

        // Debug
        private const string LOG_DEBUG_OBTENER_TODAS = "Obteniendo todas las categorías";
        private const string LOG_DEBUG_OBTENER_POR_ID = "Obteniendo categoría. CategoriaId: {CategoriaId}";
        private const string LOG_DEBUG_BUSCAR_NOMBRE = "Buscando categorías con término: {SearchTerm}";
        private const string LOG_DEBUG_CONTEO_PRODUCTOS = "Obteniendo conteo de productos por categoría";
        private const string LOG_DEBUG_CATEGORIAS_CON_PRODUCTOS = "Obteniendo categorías con productos";
        private const string LOG_DEBUG_CATEGORIAS_VACIAS = "Obteniendo categorías vacías";
        private const string LOG_DEBUG_ESTADISTICAS = "Obteniendo estadísticas de categorías";
        private const string LOG_DEBUG_VERIFICAR_EXISTENCIA = "Verificando existencia de categoría. CategoriaId: {CategoriaId}";
        private const string LOG_DEBUG_VERIFICAR_NOMBRE = "Verificando si nombre existe. Nombre: {Nombre}, ExcludeId: {ExcludeId}";
        private const string LOG_DEBUG_PUEDE_ELIMINAR = "Validando si categoría puede eliminarse. CategoriaId: {CategoriaId}";
        private const string LOG_DEBUG_TRANSACCION_INICIADA = "Transacción iniciada. Operación: {Operacion}";
        private const string LOG_DEBUG_TRANSACCION_COMMIT = "Transacción confirmada. Operación: {Operacion}";
        private const string LOG_DEBUG_TRANSACCION_ROLLBACK = "Transacción revertida. Operación: {Operacion}";

        // Advertencias
        private const string LOG_WARN_CATEGORIA_NULL = "Intento de agregar/actualizar categoría null";
        private const string LOG_WARN_NOMBRE_VACIO = "Intento de agregar/actualizar categoría sin nombre. CategoriaId: {CategoriaId}";
        private const string LOG_WARN_NOMBRE_DUPLICADO_ADD = "Ya existe una categoría con el nombre: {Nombre}";
        private const string LOG_WARN_NOMBRE_DUPLICADO_UPDATE = "El nombre '{Nombre}' ya está en uso por otra categoría";
        private const string LOG_WARN_ID_INVALIDO = "ID de categoría inválido: {Id}";
        private const string LOG_WARN_CATEGORIA_NO_EXISTE = "Categoría no existe. CategoriaId: {CategoriaId}";
        private const string LOG_WARN_CATEGORIA_NO_ENCONTRADA = "Categoría no encontrada. CategoriaId: {CategoriaId}";
        private const string LOG_WARN_TIENE_PRODUCTOS = "No se puede eliminar categoría '{Nombre}' - tiene {Count} productos asociados";
        private const string LOG_WARN_TIENE_SUBCATEGORIAS = "No se puede eliminar categoría '{Nombre}' - tiene {Count} subcategorías asociadas";

        // Errores
        private const string LOG_ERROR_AGREGAR = "Error al agregar categoría. Nombre: {Nombre}";
        private const string LOG_ERROR_OBTENER_TODAS = "Error al obtener todas las categorías";
        private const string LOG_ERROR_OBTENER_POR_ID = "Error al obtener categoría. CategoriaId: {CategoriaId}";
        private const string LOG_ERROR_ACTUALIZAR = "Error al actualizar categoría. CategoriaId: {CategoriaId}";
        private const string LOG_ERROR_ELIMINAR = "Error al eliminar categoría. CategoriaId: {CategoriaId}";
        private const string LOG_ERROR_BUSCAR = "Error al buscar categorías con término: {SearchTerm}";
        private const string LOG_ERROR_CONTEO_PRODUCTOS = "Error al obtener conteo de productos por categoría";
        private const string LOG_ERROR_CATEGORIAS_CON_CONTEO = "Error al obtener categorías con conteo";
        private const string LOG_ERROR_CATEGORIAS_CON_PRODUCTOS = "Error al obtener categorías con productos";
        private const string LOG_ERROR_CATEGORIAS_VACIAS = "Error al obtener categorías vacías";
        private const string LOG_ERROR_ESTADISTICAS = "Error al obtener estadísticas de categorías";
        private const string LOG_ERROR_VERIFICAR_EXISTENCIA = "Error al verificar existencia de categoría. CategoriaId: {CategoriaId}";
        private const string LOG_ERROR_VERIFICAR_NOMBRE = "Error al verificar si nombre existe. Nombre: {Nombre}";
        private const string LOG_ERROR_PUEDE_ELIMINAR = "Error al validar si categoría puede eliminarse. CategoriaId: {CategoriaId}";
        private const string LOG_ERROR_DB_UPDATE = "Error de base de datos en operación: {Operacion}";
        private const string LOG_ERROR_DB_CONCURRENCY = "Error de concurrencia en operación: {Operacion}";

        #endregion

        #region Constantes - Mensajes de Error

        private const string ERR_CATEGORIA_NULL = "La categoría no puede ser nula";
        private const string ERR_NOMBRE_VACIO = "El nombre de la categoría no puede estar vacío";
        private const string ERR_ID_INVALIDO = "El ID de la categoría debe ser mayor que cero";
        private const string ERR_NOMBRE_DUPLICADO = "Ya existe una categoría con el nombre '{0}'";
        private const string ERR_CATEGORIA_NO_EXISTE = "La categoría no existe";
        private const string ERR_TIENE_PRODUCTOS = "No se puede eliminar la categoría porque tiene {0} producto(s) asociado(s)";
        private const string ERR_TIENE_SUBCATEGORIAS = "No se puede eliminar la categoría porque tiene {0} subcategoría(s) asociada(s)";
        private const string ERR_SEARCH_TERM_VACIO = "El término de búsqueda no puede estar vacío";

        #endregion

        #region Constructor

        /// <summary>
        /// Constructor del servicio de categorías
        /// </summary>
        /// <param name="context">Contexto de base de datos</param>
        /// <param name="logger">Logger para registro de eventos</param>
        /// <exception cref="ArgumentNullException">Si alguna dependencia es null</exception>
        public CategoriasService(TiendaDbContext context, ILogger<CategoriasService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #endregion

        #region Helpers - Validación de Argumentos

        /// <summary>
        /// Valida que la categoría no sea nula
        /// </summary>
        private static void ValidateCategoria(Categorias categoria)
        {
            if (categoria == null)
            {
                throw new ArgumentNullException(nameof(categoria), ERR_CATEGORIA_NULL);
            }
        }

        /// <summary>
        /// Valida que el nombre de la categoría no esté vacío
        /// </summary>
        private void ValidateNombre(Categorias categoria)
        {
            if (string.IsNullOrWhiteSpace(categoria?.Nombre))
            {
                _logger.LogWarning(LOG_WARN_NOMBRE_VACIO, categoria?.CategoriaID ?? 0);
                throw new ArgumentException(ERR_NOMBRE_VACIO, nameof(categoria));
            }
        }

        /// <summary>
        /// Valida que el ID sea mayor que cero
        /// </summary>
        private void ValidateId(int id)
        {
            if (id <= 0)
            {
                _logger.LogWarning(LOG_WARN_ID_INVALIDO, id);
                throw new ArgumentException(ERR_ID_INVALIDO, nameof(id));
            }
        }

        #endregion

        #region Helpers - Validación de Negocio

        /// <summary>
        /// Verifica si un nombre ya existe (para agregar)
        /// </summary>
        private async Task<bool> NombreYaExisteAsync(string nombre, CancellationToken ct = default)
        {
            var existe = await _context.Categorias
                .AsNoTracking()
                .AnyAsync(c => c.Nombre.ToLower() == nombre.ToLower(), ct)
                .ConfigureAwait(false);

            if (existe)
            {
                _logger.LogWarning(LOG_WARN_NOMBRE_DUPLICADO_ADD, nombre);
            }

            return existe;
        }

        /// <summary>
        /// Verifica si un nombre está duplicado para otra categoría (para actualizar)
        /// </summary>
        private async Task<bool> NombreDuplicadoParaOtraAsync(int categoriaId, string nombre, CancellationToken ct = default)
        {
            var duplicado = await _context.Categorias
                .AsNoTracking()
                .AnyAsync(c => c.CategoriaID != categoriaId &&
                              c.Nombre.ToLower() == nombre.ToLower(), ct)
                .ConfigureAwait(false);

            if (duplicado)
            {
                _logger.LogWarning(LOG_WARN_NOMBRE_DUPLICADO_UPDATE, nombre);
            }

            return duplicado;
        }

        /// <summary>
        /// Cuenta productos asociados a una categoría
        /// </summary>
        private async Task<int> ContarProductosAsync(int categoriaId, CancellationToken ct = default)
        {
            return await _context.Productos
                .AsNoTracking()
                .CountAsync(p => p.CategoriaID == categoriaId, ct)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Cuenta subcategorías asociadas a una categoría
        /// </summary>
        private async Task<int> ContarSubcategoriasAsync(int categoriaId, CancellationToken ct = default)
        {
            return await _context.Subcategorias
                .AsNoTracking()
                .CountAsync(s => s.CategoriaID == categoriaId, ct)
                .ConfigureAwait(false);
        }

        #endregion

        #region CRUD Básico

        /// <inheritdoc />
        public async Task<bool> AddAsync(Categorias categoria, CancellationToken ct = default)
        {
            ValidateCategoria(categoria);
            ValidateNombre(categoria);

            try
            {
                _logger.LogInformation("Agregando nueva categoría. Nombre: {Nombre}", categoria.Nombre);

                // Verificar nombre duplicado
                if (await NombreYaExisteAsync(categoria.Nombre, ct).ConfigureAwait(false))
                {
                    return false;
                }

                await _context.Categorias.AddAsync(categoria, ct).ConfigureAwait(false);
                await _context.SaveChangesAsync(ct).ConfigureAwait(false);

                _logger.LogInformation(LOG_INFO_CATEGORIA_AGREGADA,
                    categoria.CategoriaID, categoria.Nombre);
                return true;
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, LOG_ERROR_DB_UPDATE, "AddCategoria");
                return false;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_AGREGAR, categoria.Nombre);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<List<Categorias>> GetAllAsync(CancellationToken ct = default)
        {
            try
            {
                _logger.LogDebug(LOG_DEBUG_OBTENER_TODAS);

                return await _context.Categorias
                    .AsNoTracking()
                    .OrderBy(c => c.Nombre)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_OBTENER_TODAS);
                return new List<Categorias>();
            }
        }

        /// <inheritdoc />
        public async Task<Categorias> GetByIdAsync(int id, CancellationToken ct = default)
        {
            if (id <= 0)
            {
                _logger.LogWarning(LOG_WARN_ID_INVALIDO, id);
                return null;
            }

            try
            {
                _logger.LogDebug(LOG_DEBUG_OBTENER_POR_ID, id);

                return await _context.Categorias
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.CategoriaID == id, ct)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_OBTENER_POR_ID, id);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<bool> UpdateAsync(Categorias categoria, CancellationToken ct = default)
        {
            ValidateCategoria(categoria);
            ValidateNombre(categoria);

            try
            {
                _logger.LogInformation("Actualizando categoría. CategoriaId: {CategoriaId}, Nombre: {Nombre}",
                    categoria.CategoriaID, categoria.Nombre);

                // Verificar existencia
                var existe = await ExistsAsync(categoria.CategoriaID, ct).ConfigureAwait(false);
                if (!existe)
                {
                    _logger.LogWarning(LOG_WARN_CATEGORIA_NO_EXISTE, categoria.CategoriaID);
                    return false;
                }

                // Verificar nombre duplicado
                if (await NombreDuplicadoParaOtraAsync(categoria.CategoriaID, categoria.Nombre, ct)
                    .ConfigureAwait(false))
                {
                    return false;
                }

                _context.Categorias.Update(categoria);
                await _context.SaveChangesAsync(ct).ConfigureAwait(false);

                _logger.LogInformation(LOG_INFO_CATEGORIA_ACTUALIZADA,
                    categoria.CategoriaID, categoria.Nombre);
                return true;
            }
            catch (DbUpdateConcurrencyException concEx)
            {
                _logger.LogError(concEx, LOG_ERROR_DB_CONCURRENCY, "UpdateCategoria");
                return false;
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, LOG_ERROR_DB_UPDATE, "UpdateCategoria");
                return false;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_ACTUALIZAR, categoria.CategoriaID);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
        {
            ValidateId(id);

            await using var transaction = await _context.Database
                .BeginTransactionAsync(ct)
                .ConfigureAwait(false);

            try
            {
                _logger.LogInformation("Intentando eliminar categoría. CategoriaId: {CategoriaId}", id);
                _logger.LogDebug(LOG_DEBUG_TRANSACCION_INICIADA, "DeleteCategoria");

                var categoria = await _context.Categorias
                    .FirstOrDefaultAsync(c => c.CategoriaID == id, ct)
                    .ConfigureAwait(false);

                if (categoria == null)
                {
                    _logger.LogWarning(LOG_WARN_CATEGORIA_NO_ENCONTRADA, id);
                    return false;
                }

                // Verificar productos asociados
                var countProductos = await ContarProductosAsync(id, ct).ConfigureAwait(false);
                if (countProductos > 0)
                {
                    _logger.LogWarning(LOG_WARN_TIENE_PRODUCTOS, categoria.Nombre, countProductos);
                    await transaction.RollbackAsync(ct).ConfigureAwait(false);
                    return false;
                }

                // Verificar subcategorías asociadas
                var countSubcategorias = await ContarSubcategoriasAsync(id, ct).ConfigureAwait(false);
                if (countSubcategorias > 0)
                {
                    _logger.LogWarning(LOG_WARN_TIENE_SUBCATEGORIAS, categoria.Nombre, countSubcategorias);
                    await transaction.RollbackAsync(ct).ConfigureAwait(false);
                    return false;
                }

                _context.Categorias.Remove(categoria);
                await _context.SaveChangesAsync(ct).ConfigureAwait(false);
                await transaction.CommitAsync(ct).ConfigureAwait(false);

                _logger.LogInformation(LOG_INFO_CATEGORIA_ELIMINADA, id, categoria.Nombre);
                _logger.LogDebug(LOG_DEBUG_TRANSACCION_COMMIT, "DeleteCategoria");

                return true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await transaction.RollbackAsync(ct).ConfigureAwait(false);
                _logger.LogDebug(LOG_DEBUG_TRANSACCION_ROLLBACK, "DeleteCategoria");
                _logger.LogError(ex, LOG_ERROR_ELIMINAR, id);
                return false;
            }
        }

        #endregion

        #region Consultas

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(int id, CancellationToken ct = default)
        {
            if (id <= 0) return false;

            try
            {
                _logger.LogDebug(LOG_DEBUG_VERIFICAR_EXISTENCIA, id);

                return await _context.Categorias
                    .AsNoTracking()
                    .AnyAsync(c => c.CategoriaID == id, ct)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_VERIFICAR_EXISTENCIA, id);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> NombreExisteAsync(string nombre, int? excludeId = null, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(nombre))
            {
                return false;
            }

            try
            {
                _logger.LogDebug(LOG_DEBUG_VERIFICAR_NOMBRE, nombre, excludeId);

                var nombreLower = nombre.ToLower();

                if (excludeId.HasValue)
                {
                    return await _context.Categorias
                        .AsNoTracking()
                        .AnyAsync(c => c.CategoriaID != excludeId.Value &&
                                      c.Nombre.ToLower() == nombreLower, ct)
                        .ConfigureAwait(false);
                }
                else
                {
                    return await _context.Categorias
                        .AsNoTracking()
                        .AnyAsync(c => c.Nombre.ToLower() == nombreLower, ct)
                        .ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_VERIFICAR_NOMBRE, nombre);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<List<Categorias>> SearchByNameAsync(string searchTerm, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return await GetAllAsync(ct).ConfigureAwait(false);
            }

            try
            {
                _logger.LogDebug(LOG_DEBUG_BUSCAR_NOMBRE, searchTerm);

                var term = searchTerm.ToLower();
                return await _context.Categorias
                    .AsNoTracking()
                    .Where(c => c.Nombre.ToLower().Contains(term))
                    .OrderBy(c => c.Nombre)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_BUSCAR, searchTerm);
                return new List<Categorias>();
            }
        }

        /// <inheritdoc />
        public async Task<List<Categorias>> GetCategoriasConProductosAsync(CancellationToken ct = default)
        {
            try
            {
                _logger.LogDebug(LOG_DEBUG_CATEGORIAS_CON_PRODUCTOS);

                return await _context.Categorias
                    .AsNoTracking()
                    .Where(c => _context.Productos.Any(p => p.CategoriaID == c.CategoriaID))
                    .OrderBy(c => c.Nombre)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_CATEGORIAS_CON_PRODUCTOS);
                return new List<Categorias>();
            }
        }

        /// <inheritdoc />
        public async Task<List<Categorias>> GetCategoriasVaciasAsync(CancellationToken ct = default)
        {
            try
            {
                _logger.LogDebug(LOG_DEBUG_CATEGORIAS_VACIAS);

                return await _context.Categorias
                    .AsNoTracking()
                    .Where(c => !_context.Productos.Any(p => p.CategoriaID == c.CategoriaID))
                    .OrderBy(c => c.Nombre)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_CATEGORIAS_VACIAS);
                return new List<Categorias>();
            }
        }

        #endregion

        #region Estadísticas

        /// <inheritdoc />
        public async Task<Dictionary<int, int>> GetProductCountByCategoryAsync(CancellationToken ct = default)
        {
            try
            {
                _logger.LogDebug(LOG_DEBUG_CONTEO_PRODUCTOS);

                return await _context.Productos
                    .AsNoTracking()
                    .GroupBy(p => p.CategoriaID)
                    .Select(g => new { CategoriaId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.CategoriaId, x => x.Count, ct)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_CONTEO_PRODUCTOS);
                return new Dictionary<int, int>();
            }
        }

        /// <inheritdoc />
        public async Task<List<CategoriaConConteo>> GetCategoriasConConteoAsync(CancellationToken ct = default)
        {
            try
            {
                _logger.LogDebug("Obteniendo categorías con conteo de productos");

                var categorias = await _context.Categorias
                    .AsNoTracking()
                    .OrderBy(c => c.Nombre)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);

                var productCounts = await GetProductCountByCategoryAsync(ct).ConfigureAwait(false);

                return categorias.Select(c => new CategoriaConConteo
                {
                    CategoriaID = c.CategoriaID,
                    Nombre = c.Nombre,
                    CantidadProductos = productCounts.ContainsKey(c.CategoriaID)
                        ? productCounts[c.CategoriaID]
                        : 0
                }).ToList();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_CATEGORIAS_CON_CONTEO);
                return new List<CategoriaConConteo>();
            }
        }

        /// <inheritdoc />
        public async Task<CategoriasEstadisticas> GetEstadisticasAsync(CancellationToken ct = default)
        {
            try
            {
                _logger.LogDebug(LOG_DEBUG_ESTADISTICAS);

                var totalCategorias = await _context.Categorias
                    .AsNoTracking()
                    .CountAsync(ct)
                    .ConfigureAwait(false);

                var categoriasConProductos = await _context.Categorias
                    .AsNoTracking()
                    .CountAsync(c => _context.Productos.Any(p => p.CategoriaID == c.CategoriaID), ct)
                    .ConfigureAwait(false);

                var categoriasVacias = totalCategorias - categoriasConProductos;

                var totalProductos = await _context.Productos
                    .AsNoTracking()
                    .CountAsync(ct)
                    .ConfigureAwait(false);

                var promedioProductosPorCategoria = totalCategorias > 0
                    ? (decimal)totalProductos / totalCategorias
                    : 0m;

                var categoriaConMasProductos = await _context.Categorias
                    .AsNoTracking()
                    .Select(c => new
                    {
                        c.CategoriaID,
                        c.Nombre,
                        Count = _context.Productos.Count(p => p.CategoriaID == c.CategoriaID)
                    })
                    .OrderByDescending(x => x.Count)
                    .FirstOrDefaultAsync(ct)
                    .ConfigureAwait(false);

                return new CategoriasEstadisticas
                {
                    TotalCategorias = totalCategorias,
                    CategoriasConProductos = categoriasConProductos,
                    CategoriasVacias = categoriasVacias,
                    TotalProductos = totalProductos,
                    PromedioProductosPorCategoria = promedioProductosPorCategoria,
                    CategoriaConMasProductosId = categoriaConMasProductos?.CategoriaID,
                    CategoriaConMasProductosNombre = categoriaConMasProductos?.Nombre,
                    MaxProductosEnCategoria = categoriaConMasProductos?.Count ?? 0
                };
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_ESTADISTICAS);
                return new CategoriasEstadisticas();
            }
        }

        #endregion

        #region Validación

        /// <inheritdoc />
        public async Task<(bool canDelete, string? reason)> CanDeleteAsync(int id, CancellationToken ct = default)
        {
            if (id <= 0)
            {
                return (false, ERR_ID_INVALIDO);
            }

            try
            {
                _logger.LogDebug(LOG_DEBUG_PUEDE_ELIMINAR, id);

                var categoria = await GetByIdAsync(id, ct).ConfigureAwait(false);
                if (categoria == null)
                {
                    return (false, ERR_CATEGORIA_NO_EXISTE);
                }

                // Verificar productos
                var countProductos = await ContarProductosAsync(id, ct).ConfigureAwait(false);
                if (countProductos > 0)
                {
                    return (false, string.Format(ERR_TIENE_PRODUCTOS, countProductos));
                }

                // Verificar subcategorías
                var countSubcategorias = await ContarSubcategoriasAsync(id, ct).ConfigureAwait(false);
                if (countSubcategorias > 0)
                {
                    return (false, string.Format(ERR_TIENE_SUBCATEGORIAS, countSubcategorias));
                }

                return (true, null);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_PUEDE_ELIMINAR, id);
                return (false, "Error al validar la categoría");
            }
        }

        #endregion
    }

    #endregion

    #region DTOs

    /// <summary>
    /// DTO para categoría con conteo de productos
    /// </summary>
    public class CategoriaConConteo
    {
        /// <summary>
        /// ID de la categoría
        /// </summary>
        public int CategoriaID { get; set; }

        /// <summary>
        /// Nombre de la categoría
        /// </summary>
        public string Nombre { get; set; }

        /// <summary>
        /// Cantidad de productos en esta categoría
        /// </summary>
        public int CantidadProductos { get; set; }
    }

    /// <summary>
    /// DTO para estadísticas generales de categorías
    /// </summary>
    public class CategoriasEstadisticas
    {
        /// <summary>
        /// Total de categorías en el sistema
        /// </summary>
        public int TotalCategorias { get; set; }

        /// <summary>
        /// Categorías que tienen al menos un producto
        /// </summary>
        public int CategoriasConProductos { get; set; }

        /// <summary>
        /// Categorías sin productos
        /// </summary>
        public int CategoriasVacias { get; set; }

        /// <summary>
        /// Total de productos en el sistema
        /// </summary>
        public int TotalProductos { get; set; }

        /// <summary>
        /// Promedio de productos por categoría
        /// </summary>
        public decimal PromedioProductosPorCategoria { get; set; }

        /// <summary>
        /// ID de la categoría con más productos
        /// </summary>
        public int? CategoriaConMasProductosId { get; set; }

        /// <summary>
        /// Nombre de la categoría con más productos
        /// </summary>
        public string CategoriaConMasProductosNombre { get; set; }

        /// <summary>
        /// Cantidad máxima de productos en una categoría
        /// </summary>
        public int MaxProductosEnCategoria { get; set; }
    }

    #endregion
}