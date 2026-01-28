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
    /// Servicio de gestión de proveedores con operaciones CRUD y consultas avanzadas
    /// Versión optimizada con mejores prácticas empresariales
    /// </summary>
    public interface IProveedorService
    {
        #region CREATE

        /// <summary>
        /// Agrega un nuevo proveedor al sistema
        /// </summary>
        /// <param name="proveedor">Proveedor a agregar</param>
        /// <param name="ct">Token de cancelación</param>
        /// <returns>ID del proveedor creado</returns>
        Task<int> AddAsync(Proveedores proveedor, CancellationToken ct = default);

        /// <summary>
        /// Agrega múltiples proveedores en batch
        /// </summary>
        /// <param name="proveedores">Lista de proveedores</param>
        /// <param name="ct">Token de cancelación</param>
        Task<int> AddRangeAsync(IEnumerable<Proveedores> proveedores, CancellationToken ct = default);

        #endregion

        #region READ

        /// <summary>
        /// Obtiene todos los proveedores
        /// </summary>
        Task<List<Proveedores>> GetAllAsync(CancellationToken ct = default);

        /// <summary>
        /// Obtiene un proveedor por ID con tracking (para edición)
        /// </summary>
        Task<Proveedores?> GetByIdAsync(int id, CancellationToken ct = default);

        /// <summary>
        /// Obtiene un proveedor por ID en solo lectura
        /// </summary>
        Task<Proveedores?> GetByIdReadOnlyAsync(int id, CancellationToken ct = default);

        /// <summary>
        /// Verifica si existe un proveedor
        /// </summary>
        Task<bool> ExistsAsync(int id, CancellationToken ct = default);

        /// <summary>
        /// Busca proveedores por nombre
        /// </summary>
        Task<List<Proveedores>> SearchByNombreAsync(string searchTerm, CancellationToken ct = default);

        #endregion

        #region UPDATE

        /// <summary>
        /// Actualiza un proveedor existente
        /// </summary>
        Task<bool> UpdateAsync(Proveedores proveedor, CancellationToken ct = default);

        #endregion

        #region DELETE

        /// <summary>
        /// Elimina un proveedor si no tiene dependencias
        /// </summary>
        Task<bool> DeleteAsync(int id, CancellationToken ct = default);

        #endregion

        #region Business Logic

        /// <summary>
        /// Obtiene el conteo de productos por proveedor
        /// </summary>
        Task<Dictionary<int, int>> GetProductCountByProveedorAsync(CancellationToken ct = default);

        /// <summary>
        /// Obtiene estadísticas de proveedores
        /// </summary>
        Task<ProveedoresStats> GetStatsAsync(CancellationToken ct = default);

        #endregion

        #region Validation

        /// <summary>
        /// Verifica si un proveedor tiene productos asociados
        /// </summary>
        Task<bool> HasProductsAsync(int proveedorId, CancellationToken ct = default);

        /// <summary>
        /// Obtiene las dependencias de un proveedor
        /// </summary>
        Task<ProveedorDependencias> GetDependenciasAsync(int proveedorId, CancellationToken ct = default);

        #endregion
    }

    #endregion

    #region Implementation

    /// <summary>
    /// Implementación del servicio de proveedores con logging y validación robusta
    /// </summary>
    public class ProveedorService : IProveedorService
    {
        #region Dependencias

        private readonly TiendaDbContext _context;
        private readonly ILogger<ProveedorService> _logger;

        #endregion

        #region Constantes - Configuración

        private const int MIN_SEARCH_LENGTH = 2;

        #endregion

        #region Constantes - Mensajes de Log

        // Información
        private const string LOG_INFO_PROVEEDOR_AGREGADO = "Proveedor creado exitosamente. ProveedorId: {ProveedorId}, NombreProveedor: {NombreProveedor}";
        private const string LOG_INFO_PROVEEDORES_BATCH_AGREGADOS = "Proveedores guardados en batch. Count: {Count}, Guardados: {Saved}";
        private const string LOG_INFO_PROVEEDOR_ACTUALIZADO = "Proveedor actualizado. ProveedorId: {ProveedorId}, Changes: {Changes}";
        private const string LOG_INFO_PROVEEDOR_ELIMINADO = "Proveedor eliminado exitosamente. ProveedorId: {ProveedorId}";

        // Debug
        private const string LOG_DEBUG_OBTENIENDO_TODOS = "Obteniendo todos los proveedores";
        private const string LOG_DEBUG_OBTENIENDO_POR_ID = "Obteniendo proveedor {ProveedorId} con tracking: {Tracking}";
        private const string LOG_DEBUG_BUSCANDO_POR_NOMBRE = "Buscando proveedores por nombre. SearchTerm: {SearchTerm}";
        private const string LOG_DEBUG_AGREGANDO_PROVEEDOR = "Agregando nuevo proveedor. NombreProveedor: {NombreProveedor}";
        private const string LOG_DEBUG_AGREGANDO_BATCH = "Agregando {Count} proveedores en batch";
        private const string LOG_DEBUG_ACTUALIZANDO_PROVEEDOR = "Actualizando proveedor {ProveedorId}. NombreProveedor: {NombreProveedor}";
        private const string LOG_DEBUG_ELIMINANDO_PROVEEDOR = "Intentando eliminar proveedor {ProveedorId}";
        private const string LOG_DEBUG_OBTENIENDO_CONTEO_PRODUCTOS = "Obteniendo conteo de productos por proveedor";
        private const string LOG_DEBUG_OBTENIENDO_ESTADISTICAS = "Obteniendo estadísticas de proveedores";
        private const string LOG_DEBUG_VERIFICANDO_PRODUCTOS = "Verificando si proveedor {ProveedorId} tiene productos";
        private const string LOG_DEBUG_OBTENIENDO_DEPENDENCIAS = "Obteniendo dependencias del proveedor {ProveedorId}";

        // Advertencias
        private const string LOG_WARN_PROVEEDOR_NO_ENCONTRADO = "Proveedor no encontrado. ProveedorId: {ProveedorId}";
        private const string LOG_WARN_PROVEEDOR_CON_PRODUCTOS = "No se puede eliminar proveedor {ProveedorId} porque tiene {Count} productos asociados";
        private const string LOG_WARN_LISTA_PROVEEDORES_VACIA = "Lista de proveedores vacía en AddRangeAsync";
        private const string LOG_WARN_PROVEEDOR_NO_ACTUALIZADO = "Proveedor {ProveedorId} no se actualizó, no hubo cambios";
        private const string LOG_WARN_SEARCH_TERM_CORTO = "Término de búsqueda muy corto. SearchTerm: {SearchTerm}, Mínimo: {Min}";

        // Errores
        private const string LOG_ERROR_AGREGAR_PROVEEDOR = "Error al agregar proveedor. NombreProveedor: {NombreProveedor}";
        private const string LOG_ERROR_AGREGAR_BATCH = "Error al agregar proveedores en batch. Count: {Count}";
        private const string LOG_ERROR_ACTUALIZAR_PROVEEDOR = "Error al actualizar proveedor {ProveedorId}";
        private const string LOG_ERROR_ELIMINAR_PROVEEDOR = "Error al eliminar proveedor {ProveedorId}. Transacción revertida";
        private const string LOG_ERROR_OBTENER_ESTADISTICAS = "Error al obtener estadísticas de proveedores";
        private const string LOG_ERROR_VERIFICAR_PRODUCTOS = "Error al verificar productos del proveedor {ProveedorId}";
        private const string LOG_ERROR_CONCURRENCIA = "Error de concurrencia al actualizar proveedor {ProveedorId}";
        private const string LOG_ERROR_BASE_DATOS = "Error de base de datos. Operación: {Operacion}, ProveedorId: {ProveedorId}";

        #endregion

        #region Constantes - Mensajes de Excepción

        private const string EXC_PROVEEDOR_NULL = "El proveedor no puede ser nulo";
        private const string EXC_ID_INVALIDO = "El ID debe ser mayor a 0";
        private const string EXC_PROVEEDORES_LISTA_VACIA = "La lista de proveedores no puede estar vacía";
        private const string EXC_SEARCH_TERM_VACIO = "El término de búsqueda no puede estar vacío";

        #endregion

        #region Constructor

        /// <summary>
        /// Constructor del servicio de proveedores
        /// </summary>
        /// <param name="context">Contexto de base de datos</param>
        /// <param name="logger">Logger para registro de eventos</param>
        /// <exception cref="ArgumentNullException">Si alguna dependencia es null</exception>
        public ProveedorService(TiendaDbContext context, ILogger<ProveedorService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #endregion

        #region Helpers - Validación

        /// <summary>
        /// Valida que el proveedor no sea nulo
        /// </summary>
        private static void ValidateProveedor(Proveedores proveedor)
        {
            if (proveedor == null)
            {
                throw new ArgumentNullException(nameof(proveedor), EXC_PROVEEDOR_NULL);
            }
        }

        /// <summary>
        /// Valida que el ID sea válido
        /// </summary>
        private static void ValidateId(int id)
        {
            if (id <= 0)
            {
                throw new ArgumentException(EXC_ID_INVALIDO, nameof(id));
            }
        }

        /// <summary>
        /// Valida el término de búsqueda
        /// </summary>
        private void ValidateSearchTerm(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                throw new ArgumentException(EXC_SEARCH_TERM_VACIO, nameof(searchTerm));
            }

            if (searchTerm.Trim().Length < MIN_SEARCH_LENGTH)
            {
                _logger.LogWarning(LOG_WARN_SEARCH_TERM_CORTO, searchTerm, MIN_SEARCH_LENGTH);
            }
        }

        #endregion

        #region CREATE

        /// <inheritdoc />
        public async Task<int> AddAsync(Proveedores proveedor, CancellationToken ct = default)
        {
            ValidateProveedor(proveedor);

            try
            {
                _logger.LogDebug(LOG_DEBUG_AGREGANDO_PROVEEDOR, proveedor.NombreProveedor);

                await _context.Proveedores.AddAsync(proveedor, ct).ConfigureAwait(false);
                await _context.SaveChangesAsync(ct).ConfigureAwait(false);

                _logger.LogInformation(LOG_INFO_PROVEEDOR_AGREGADO, proveedor.ProveedorID, proveedor.NombreProveedor);
                return proveedor.ProveedorID;
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, LOG_ERROR_AGREGAR_PROVEEDOR, proveedor.NombreProveedor);
                throw new InvalidOperationException($"Error al guardar el proveedor: {dbEx.InnerException?.Message}", dbEx);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_AGREGAR_PROVEEDOR, proveedor.NombreProveedor);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<int> AddRangeAsync(IEnumerable<Proveedores> proveedores, CancellationToken ct = default)
        {
            if (proveedores == null || !proveedores.Any())
            {
                _logger.LogWarning(LOG_WARN_LISTA_PROVEEDORES_VACIA);
                throw new ArgumentException(EXC_PROVEEDORES_LISTA_VACIA, nameof(proveedores));
            }

            try
            {
                var proveedoresList = proveedores.ToList();
                _logger.LogDebug(LOG_DEBUG_AGREGANDO_BATCH, proveedoresList.Count);

                await _context.Proveedores.AddRangeAsync(proveedoresList, ct).ConfigureAwait(false);
                var saved = await _context.SaveChangesAsync(ct).ConfigureAwait(false);

                _logger.LogInformation(LOG_INFO_PROVEEDORES_BATCH_AGREGADOS, proveedoresList.Count, saved);
                return saved;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_AGREGAR_BATCH, proveedores.Count());
                throw;
            }
        }

        #endregion

        #region READ

        /// <inheritdoc />
        public async Task<List<Proveedores>> GetAllAsync(CancellationToken ct = default)
        {
            _logger.LogDebug(LOG_DEBUG_OBTENIENDO_TODOS);

            return await _context.Proveedores
                .AsNoTracking()
                .OrderBy(p => p.NombreProveedor)
                .ToListAsync(ct)
                .ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<Proveedores?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            ValidateId(id);

            _logger.LogDebug(LOG_DEBUG_OBTENIENDO_POR_ID, id, true);

            return await _context.Proveedores
                .FirstOrDefaultAsync(p => p.ProveedorID == id, ct)
                .ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<Proveedores?> GetByIdReadOnlyAsync(int id, CancellationToken ct = default)
        {
            ValidateId(id);

            _logger.LogDebug(LOG_DEBUG_OBTENIENDO_POR_ID, id, false);

            return await _context.Proveedores
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.ProveedorID == id, ct)
                .ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(int id, CancellationToken ct = default)
        {
            if (id <= 0) return false;

            return await _context.Proveedores
                .AsNoTracking()
                .AnyAsync(p => p.ProveedorID == id, ct)
                .ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<List<Proveedores>> SearchByNombreAsync(string searchTerm, CancellationToken ct = default)
        {
            ValidateSearchTerm(searchTerm);

            _logger.LogDebug(LOG_DEBUG_BUSCANDO_POR_NOMBRE, searchTerm);

            var term = searchTerm.ToLower();

            return await _context.Proveedores
                .AsNoTracking()
                .Where(p => p.NombreProveedor.ToLower().Contains(term) ||
                           (p.Contacto != null && p.Contacto.ToLower().Contains(term)) ||
                           (p.Telefono != null && p.Telefono.Contains(searchTerm)))
                .OrderBy(p => p.NombreProveedor)
                .ToListAsync(ct)
                .ConfigureAwait(false);
        }

        #endregion

        #region UPDATE

        /// <inheritdoc />
        public async Task<bool> UpdateAsync(Proveedores proveedor, CancellationToken ct = default)
        {
            ValidateProveedor(proveedor);

            try
            {
                _logger.LogDebug(LOG_DEBUG_ACTUALIZANDO_PROVEEDOR, proveedor.ProveedorID, proveedor.NombreProveedor);

                _context.Proveedores.Update(proveedor);
                var changes = await _context.SaveChangesAsync(ct).ConfigureAwait(false);

                if (changes > 0)
                {
                    _logger.LogInformation(LOG_INFO_PROVEEDOR_ACTUALIZADO, proveedor.ProveedorID, changes);
                    return true;
                }
                else
                {
                    _logger.LogWarning(LOG_WARN_PROVEEDOR_NO_ACTUALIZADO, proveedor.ProveedorID);
                    return false;
                }
            }
            catch (DbUpdateConcurrencyException concEx)
            {
                _logger.LogError(concEx, LOG_ERROR_CONCURRENCIA, proveedor.ProveedorID);
                throw new InvalidOperationException("El proveedor fue modificado por otro usuario. Recarga y vuelve a intentar.", concEx);
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, LOG_ERROR_BASE_DATOS, "Update", proveedor.ProveedorID);
                throw new InvalidOperationException($"Error al actualizar el proveedor: {dbEx.InnerException?.Message}", dbEx);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_ACTUALIZAR_PROVEEDOR, proveedor.ProveedorID);
                throw;
            }
        }

        #endregion

        #region DELETE

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
        {
            ValidateId(id);

            using var transaction = await _context.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

            try
            {
                _logger.LogDebug(LOG_DEBUG_ELIMINANDO_PROVEEDOR, id);

                var proveedor = await _context.Proveedores
                    .FirstOrDefaultAsync(p => p.ProveedorID == id, ct)
                    .ConfigureAwait(false);

                if (proveedor == null)
                {
                    _logger.LogWarning(LOG_WARN_PROVEEDOR_NO_ENCONTRADO, id);
                    return false;
                }

                // Verificar si tiene productos asociados
                var tieneProductos = await HasProductsAsync(id, ct).ConfigureAwait(false);

                if (tieneProductos)
                {
                    var productCount = await _context.Productos
                        .CountAsync(p => p.ProveedorID == id, ct)
                        .ConfigureAwait(false);

                    _logger.LogWarning(LOG_WARN_PROVEEDOR_CON_PRODUCTOS, id, productCount);
                    await transaction.RollbackAsync(ct).ConfigureAwait(false);
                    return false;
                }

                _context.Proveedores.Remove(proveedor);
                await _context.SaveChangesAsync(ct).ConfigureAwait(false);
                await transaction.CommitAsync(ct).ConfigureAwait(false);

                _logger.LogInformation(LOG_INFO_PROVEEDOR_ELIMINADO, id);
                return true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await transaction.RollbackAsync(ct).ConfigureAwait(false);
                _logger.LogError(ex, LOG_ERROR_ELIMINAR_PROVEEDOR, id);
                throw new InvalidOperationException($"Error al eliminar el proveedor: {ex.Message}", ex);
            }
        }

        #endregion

        #region Business Logic

        /// <inheritdoc />
        public async Task<Dictionary<int, int>> GetProductCountByProveedorAsync(CancellationToken ct = default)
        {
            _logger.LogDebug(LOG_DEBUG_OBTENIENDO_CONTEO_PRODUCTOS);

            return await _context.Productos
    .AsNoTracking()
    .Where(p => p.ProveedorID.HasValue && p.ProveedorID.Value > 0)  // ✅ Verifica que no sea null
    .GroupBy(p => p.ProveedorID.Value)  // ✅ Usa .Value para obtener int
    .Select(g => new { ProveedorId = g.Key, Count = g.Count() })
    .ToDictionaryAsync(x => x.ProveedorId, x => x.Count, ct)
    .ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<ProveedoresStats> GetStatsAsync(CancellationToken ct = default)
        {
            _logger.LogDebug(LOG_DEBUG_OBTENIENDO_ESTADISTICAS);

            try
            {
                var proveedores = await _context.Proveedores
                    .AsNoTracking()
                    .ToListAsync(ct)
                    .ConfigureAwait(false);

                var productCounts = await GetProductCountByProveedorAsync(ct).ConfigureAwait(false);

                var stats = new ProveedoresStats
                {
                    TotalProveedores = proveedores.Count
                };

                if (productCounts.Any())
                {
                    stats.ProveedoresConProductos = productCounts.Count;
                    stats.ProveedoresSinProductos = stats.TotalProveedores - stats.ProveedoresConProductos;
                    stats.ProductosPorProveedorPromedio = (decimal)productCounts.Values.Average();
                    stats.MaxProductosPorProveedor = productCounts.Values.Max();
                    stats.MinProductosPorProveedor = productCounts.Values.Min();

                    var proveedorConMasProductos = productCounts.OrderByDescending(x => x.Value).First();
                    var proveedor = proveedores.FirstOrDefault(p => p.ProveedorID == proveedorConMasProductos.Key);
                    stats.ProveedorConMasProductos = proveedor?.NombreProveedor;
                    stats.ProveedorConMasProductosCount = proveedorConMasProductos.Value;
                }
                else
                {
                    stats.ProveedoresSinProductos = stats.TotalProveedores;
                }

                return stats;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_OBTENER_ESTADISTICAS);
                throw;
            }
        }

        #endregion

        #region Validation

        /// <inheritdoc />
        public async Task<bool> HasProductsAsync(int proveedorId, CancellationToken ct = default)
        {
            ValidateId(proveedorId);

            try
            {
                _logger.LogDebug(LOG_DEBUG_VERIFICANDO_PRODUCTOS, proveedorId);

                return await _context.Productos
                    .AsNoTracking()
                    .AnyAsync(p => p.ProveedorID == proveedorId, ct)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_VERIFICAR_PRODUCTOS, proveedorId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<ProveedorDependencias> GetDependenciasAsync(int proveedorId, CancellationToken ct = default)
        {
            ValidateId(proveedorId);

            try
            {
                _logger.LogDebug(LOG_DEBUG_OBTENIENDO_DEPENDENCIAS, proveedorId);

                var dependencias = new ProveedorDependencias
                {
                    ProveedorId = proveedorId,
                    Productos = await _context.Productos
                        .AsNoTracking()
                        .CountAsync(p => p.ProveedorID == proveedorId, ct)
                        .ConfigureAwait(false)
                };

                return dependencias;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_VERIFICAR_PRODUCTOS, proveedorId);
                throw;
            }
        }

        #endregion
    }

    #endregion

    #region DTOs

    /// <summary>
    /// Estadísticas de proveedores
    /// </summary>
    public class ProveedoresStats
    {
        /// <summary>
        /// Total de proveedores
        /// </summary>
        public int TotalProveedores { get; set; }

        /// <summary>
        /// Proveedores con productos
        /// </summary>
        public int ProveedoresConProductos { get; set; }

        /// <summary>
        /// Proveedores sin productos
        /// </summary>
        public int ProveedoresSinProductos { get; set; }

        /// <summary>
        /// Promedio de productos por proveedor
        /// </summary>
        public decimal ProductosPorProveedorPromedio { get; set; }

        /// <summary>
        /// Máximo de productos en un proveedor
        /// </summary>
        public int MaxProductosPorProveedor { get; set; }

        /// <summary>
        /// Mínimo de productos en un proveedor
        /// </summary>
        public int MinProductosPorProveedor { get; set; }

        /// <summary>
        /// Nombre del proveedor con más productos
        /// </summary>
        public string? ProveedorConMasProductos { get; set; }

        /// <summary>
        /// Cantidad de productos del proveedor principal
        /// </summary>
        public int ProveedorConMasProductosCount { get; set; }

        /// <summary>
        /// Porcentaje de proveedores con productos
        /// </summary>
        public decimal PorcentajeConProductos =>
            TotalProveedores > 0 ? (decimal)ProveedoresConProductos / TotalProveedores * 100 : 0;
    }

    /// <summary>
    /// Dependencias de un proveedor
    /// </summary>
    public class ProveedorDependencias
    {
        /// <summary>
        /// ID del proveedor
        /// </summary>
        public int ProveedorId { get; set; }

        /// <summary>
        /// Cantidad de productos
        /// </summary>
        public int Productos { get; set; }

        /// <summary>
        /// Indica si tiene dependencias
        /// </summary>
        public bool TieneDependencias => Productos > 0;
    }

    #endregion
}