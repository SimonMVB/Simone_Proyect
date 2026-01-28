using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
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
    /// Servicio de gestión completa de productos con optimizaciones enterprise
    /// Proporciona operaciones CRUD, búsqueda avanzada, paginación y estadísticas
    /// Versión optimizada con mejores prácticas empresariales
    /// </summary>
    public interface IProductosService
    {
        #region CREATE

        /// <summary>
        /// Agrega un nuevo producto al sistema
        /// </summary>
        /// <param name="producto">Producto a agregar</param>
        /// <param name="ct">Token de cancelación</param>
        /// <returns>ID del producto creado</returns>
        Task<int> AddAsync(Producto producto, CancellationToken ct = default);

        /// <summary>
        /// Agrega múltiples productos en batch (más eficiente)
        /// </summary>
        /// <param name="productos">Lista de productos a agregar</param>
        /// <param name="ct">Token de cancelación</param>
        /// <returns>Cantidad de productos guardados</returns>
        Task<int> AddRangeAsync(IEnumerable<Producto> productos, CancellationToken ct = default);

        #endregion

        #region READ - Basic

        /// <summary>
        /// Obtiene todos los productos (solo lectura)
        /// </summary>
        Task<List<Producto>> GetAllAsync(CancellationToken ct = default);

        /// <summary>
        /// Obtiene los primeros N productos ordenados por ID
        /// </summary>
        Task<List<Producto>> GetFirstAsync(int count, CancellationToken ct = default);

        /// <summary>
        /// Obtiene un producto por ID con tracking (para edición)
        /// </summary>
        Task<Producto?> GetByIdAsync(int id, CancellationToken ct = default);

        /// <summary>
        /// Obtiene un producto por ID en solo-lectura (para vistas/detalles)
        /// </summary>
        Task<Producto?> GetByIdReadOnlyAsync(int id, CancellationToken ct = default);

        /// <summary>
        /// Verifica si existe un producto con el ID especificado
        /// </summary>
        Task<bool> ExistsAsync(int id, CancellationToken ct = default);

        #endregion

        #region READ - By Filters

        /// <summary>
        /// Obtiene productos por categoría
        /// </summary>
        Task<List<Producto>> GetByCategoryIdAsync(int categoriaId, CancellationToken ct = default);

        /// <summary>
        /// Obtiene productos por subcategoría
        /// </summary>
        Task<List<Producto>> GetBySubcategoryIdAsync(int subcategoriaId, CancellationToken ct = default);

        /// <summary>
        /// Obtiene productos por vendedor
        /// </summary>
        Task<List<Producto>> GetByVendedorIdAsync(string vendedorId, CancellationToken ct = default);

        /// <summary>
        /// Obtiene productos con stock disponible (stock > 0)
        /// </summary>
        Task<List<Producto>> GetWithStockAsync(CancellationToken ct = default);

        /// <summary>
        /// Obtiene productos con stock bajo (configurable)
        /// </summary>
        Task<List<Producto>> GetLowStockAsync(int threshold = 10, CancellationToken ct = default);

        /// <summary>
        /// Obtiene productos sin stock
        /// </summary>
        Task<List<Producto>> GetOutOfStockAsync(CancellationToken ct = default);

        #endregion

        #region READ - Advanced

        /// <summary>
        /// Búsqueda avanzada de productos con múltiples criterios
        /// </summary>
        Task<List<Producto>> SearchAsync(
            string? searchTerm = null,
            int? categoriaId = null,
            int? subcategoriaId = null,
            decimal? minPrice = null,
            decimal? maxPrice = null,
            bool? inStock = null,
            CancellationToken ct = default);

        /// <summary>
        /// Obtiene productos paginados con ordenamiento
        /// </summary>
        Task<PagedResult<Producto>> GetPagedAsync(
            int pageNumber = 1,
            int pageSize = 20,
            Expression<Func<Producto, object>>? orderBy = null,
            bool ascending = true,
            CancellationToken ct = default);

        #endregion

        #region UPDATE

        /// <summary>
        /// Actualiza un producto existente
        /// </summary>
        Task<bool> UpdateAsync(Producto producto, CancellationToken ct = default);

        /// <summary>
        /// Actualiza el stock de un producto
        /// </summary>
        Task<bool> UpdateStockAsync(int productoId, int nuevoStock, CancellationToken ct = default);

        /// <summary>
        /// Actualiza el precio de un producto
        /// </summary>
        Task<bool> UpdatePrecioAsync(int productoId, decimal nuevoPrecio, CancellationToken ct = default);

        #endregion

        #region DELETE

        /// <summary>
        /// Elimina un producto si no tiene dependencias
        /// </summary>
        Task<bool> DeleteAsync(int id, CancellationToken ct = default);

        /// <summary>
        /// Elimina múltiples productos en batch
        /// </summary>
        Task<(int eliminados, int fallidos)> DeleteRangeAsync(IEnumerable<int> ids, CancellationToken ct = default);

        #endregion

        #region Business Logic

        /// <summary>
        /// Recalcula y actualiza los campos agregados del producto desde sus variantes
        /// </summary>
        Task<bool> RecomputeFromVariantsAsync(int productoId, CancellationToken ct = default);

        /// <summary>
        /// Obtiene el conteo de productos por categoría
        /// </summary>
        Task<Dictionary<int, int>> GetCountByCategoryAsync(CancellationToken ct = default);

        /// <summary>
        /// Obtiene estadísticas básicas de productos
        /// </summary>
        Task<ProductosStats> GetStatsAsync(CancellationToken ct = default);

        /// <summary>
        /// Obtiene estadísticas detalladas de productos
        /// </summary>
        Task<ProductosStatsDetalladas> GetStatsDetalladasAsync(CancellationToken ct = default);

        #endregion

        #region Validation

        /// <summary>
        /// Verifica si un producto tiene dependencias que impiden su eliminación
        /// </summary>
        Task<bool> HasDependenciesAsync(int productoId, CancellationToken ct = default);

        /// <summary>
        /// Obtiene las dependencias de un producto
        /// </summary>
        Task<ProductoDependencias> GetDependenciasAsync(int productoId, CancellationToken ct = default);

        #endregion
    }

    #endregion

    #region Implementation

    /// <summary>
    /// Implementación del servicio de productos con logging y validación robusta
    /// </summary>
    public class ProductosService : IProductosService
    {
        #region Dependencias

        private readonly TiendaDbContext _context;
        private readonly ILogger<ProductosService> _logger;

        #endregion

        #region Constantes - Configuración

        private const int DEFAULT_PAGE_SIZE = 20;
        private const int MAX_PAGE_SIZE = 100;
        private const int MIN_PAGE_NUMBER = 1;
        private const int DEFAULT_LOW_STOCK_THRESHOLD = 10;
        private const int MIN_STOCK_VALUE = 0;
        private const decimal MIN_PRICE_VALUE = 0m;

        #endregion

        #region Constantes - Mensajes de Log

        // Información
        private const string LOG_INFO_PRODUCTO_AGREGADO = "Producto creado exitosamente. ProductoId: {ProductoId}, Nombre: {Nombre}";
        private const string LOG_INFO_PRODUCTOS_BATCH_AGREGADOS = "Productos guardados en batch. Count: {Count}, Guardados: {Saved}";
        private const string LOG_INFO_PRODUCTO_ACTUALIZADO = "Producto actualizado. ProductoId: {ProductoId}, Changes: {Changes}";
        private const string LOG_INFO_PRODUCTO_ELIMINADO = "Producto eliminado exitosamente. ProductoId: {ProductoId}";
        private const string LOG_INFO_STOCK_ACTUALIZADO = "Stock actualizado. ProductoId: {ProductoId}, StockAnterior: {StockAnterior}, StockNuevo: {StockNuevo}";
        private const string LOG_INFO_PRECIO_ACTUALIZADO = "Precio actualizado. ProductoId: {ProductoId}, PrecioAnterior: {PrecioAnterior:C}, PrecioNuevo: {PrecioNuevo:C}";
        private const string LOG_INFO_PRODUCTO_RECALCULADO = "Producto recalculado desde variantes. ProductoId: {ProductoId}, Stock: {Stock}, Precio: {Precio:C}";
        private const string LOG_INFO_BATCH_DELETE_COMPLETADO = "Eliminación batch completada. Total: {Total}, Eliminados: {Eliminados}, Fallidos: {Fallidos}";

        // Debug
        private const string LOG_DEBUG_OBTENIENDO_TODOS = "Obteniendo todos los productos";
        private const string LOG_DEBUG_OBTENIENDO_PRIMEROS = "Obteniendo primeros {Count} productos";
        private const string LOG_DEBUG_OBTENIENDO_POR_ID = "Obteniendo producto {ProductoId} con tracking: {Tracking}";
        private const string LOG_DEBUG_OBTENIENDO_POR_CATEGORIA = "Obteniendo productos de categoría {CategoriaId}";
        private const string LOG_DEBUG_OBTENIENDO_POR_SUBCATEGORIA = "Obteniendo productos de subcategoría {SubcategoriaId}";
        private const string LOG_DEBUG_OBTENIENDO_POR_VENDEDOR = "Obteniendo productos del vendedor {VendedorId}";
        private const string LOG_DEBUG_OBTENIENDO_CON_STOCK = "Obteniendo productos con stock disponible";
        private const string LOG_DEBUG_OBTENIENDO_STOCK_BAJO = "Obteniendo productos con stock bajo. Umbral: {Threshold}";
        private const string LOG_DEBUG_OBTENIENDO_SIN_STOCK = "Obteniendo productos sin stock";
        private const string LOG_DEBUG_BUSQUEDA_AVANZADA = "Búsqueda avanzada. Term: {SearchTerm}, Cat: {CatId}, SubCat: {SubCatId}, MinPrice: {MinPrice}, MaxPrice: {MaxPrice}, InStock: {InStock}";
        private const string LOG_DEBUG_PAGINACION = "Obteniendo página {PageNumber} con tamaño {PageSize}";
        private const string LOG_DEBUG_AGREGANDO_PRODUCTO = "Agregando nuevo producto. Nombre: {Nombre}";
        private const string LOG_DEBUG_AGREGANDO_BATCH = "Agregando {Count} productos en batch";
        private const string LOG_DEBUG_ACTUALIZANDO_PRODUCTO = "Actualizando producto {ProductoId}. Nombre: {Nombre}";
        private const string LOG_DEBUG_ELIMINANDO_PRODUCTO = "Intentando eliminar producto {ProductoId}";
        private const string LOG_DEBUG_ELIMINANDO_VARIANTES = "Eliminando {Count} variantes del producto {ProductoId}";
        private const string LOG_DEBUG_RECALCULANDO_AGREGADOS = "Recalculando agregados desde variantes. ProductoId: {ProductoId}";
        private const string LOG_DEBUG_PRODUCTO_YA_ACTUALIZADO = "Producto {ProductoId} ya está actualizado, no se requieren cambios";
        private const string LOG_DEBUG_OBTENIENDO_CONTEO_CATEGORIAS = "Obteniendo conteo de productos por categoría";
        private const string LOG_DEBUG_OBTENIENDO_ESTADISTICAS = "Obteniendo estadísticas de productos";
        private const string LOG_DEBUG_VERIFICANDO_DEPENDENCIAS = "Verificando dependencias del producto {ProductoId}";
        private const string LOG_DEBUG_OBTENIENDO_DEPENDENCIAS = "Obteniendo dependencias detalladas del producto {ProductoId}";

        // Advertencias
        private const string LOG_WARN_PRODUCTO_NO_ENCONTRADO = "Producto no encontrado. ProductoId: {ProductoId}";
        private const string LOG_WARN_PRODUCTO_CON_DEPENDENCIAS = "No se puede eliminar producto {ProductoId} porque tiene dependencias. Ventas: {Ventas}, Pedidos: {Pedidos}, Carrito: {Carrito}, Reseñas: {Reseñas}";
        private const string LOG_WARN_PRODUCTO_SIN_VARIANTES = "Producto {ProductoId} no tiene variantes, no se recalcula";
        private const string LOG_WARN_LISTA_PRODUCTOS_VACIA = "Lista de productos vacía en AddRangeAsync";
        private const string LOG_WARN_LISTA_IDS_VACIA = "Lista de IDs vacía en DeleteRangeAsync";
        private const string LOG_WARN_PRODUCTO_NO_ACTUALIZADO = "Producto {ProductoId} no se actualizó, no hubo cambios";
        private const string LOG_WARN_STOCK_NEGATIVO = "Intento de actualizar stock a valor negativo. ProductoId: {ProductoId}, Stock: {Stock}";
        private const string LOG_WARN_PRECIO_NEGATIVO = "Intento de actualizar precio a valor negativo. ProductoId: {ProductoId}, Precio: {Precio}";

        // Errores
        private const string LOG_ERROR_AGREGAR_PRODUCTO = "Error al agregar producto. Nombre: {Nombre}";
        private const string LOG_ERROR_AGREGAR_BATCH = "Error al agregar productos en batch. Count: {Count}";
        private const string LOG_ERROR_ACTUALIZAR_PRODUCTO = "Error al actualizar producto {ProductoId}";
        private const string LOG_ERROR_ACTUALIZAR_STOCK = "Error al actualizar stock del producto {ProductoId}";
        private const string LOG_ERROR_ACTUALIZAR_PRECIO = "Error al actualizar precio del producto {ProductoId}";
        private const string LOG_ERROR_ELIMINAR_PRODUCTO = "Error al eliminar producto {ProductoId}. Transacción revertida";
        private const string LOG_ERROR_RECALCULAR_AGREGADOS = "Error al recalcular agregados para producto {ProductoId}. Transacción revertida";
        private const string LOG_ERROR_CONCURRENCIA = "Error de concurrencia al actualizar producto {ProductoId}";
        private const string LOG_ERROR_BASE_DATOS = "Error de base de datos. Operación: {Operacion}, ProductoId: {ProductoId}";
        private const string LOG_ERROR_OBTENER_ESTADISTICAS = "Error al obtener estadísticas de productos";
        private const string LOG_ERROR_VERIFICAR_DEPENDENCIAS = "Error al verificar dependencias del producto {ProductoId}";

        #endregion

        #region Constantes - Mensajes de Excepción

        private const string EXC_PRODUCTO_NULL = "El producto no puede ser nulo";
        private const string EXC_ID_INVALIDO = "El ID debe ser mayor a 0";
        private const string EXC_COUNT_INVALIDO = "El conteo debe ser mayor a 0";
        private const string EXC_VENDEDOR_ID_VACIO = "El VendedorID no puede estar vacío";
        private const string EXC_PAGE_NUMBER_INVALIDO = "El número de página debe ser mayor o igual a {0}";
        private const string EXC_PAGE_SIZE_INVALIDO = "El tamaño de página debe estar entre 1 y {0}";
        private const string EXC_PRODUCTOS_LISTA_VACIA = "La lista de productos no puede estar vacía";
        private const string EXC_IDS_LISTA_VACIA = "La lista de IDs no puede estar vacía";
        private const string EXC_STOCK_NEGATIVO = "El stock no puede ser negativo";
        private const string EXC_PRECIO_NEGATIVO = "El precio no puede ser negativo";

        #endregion

        #region Constructor

        /// <summary>
        /// Constructor del servicio de productos
        /// </summary>
        /// <param name="context">Contexto de base de datos</param>
        /// <param name="logger">Logger para registro de eventos</param>
        /// <exception cref="ArgumentNullException">Si alguna dependencia es null</exception>
        public ProductosService(TiendaDbContext context, ILogger<ProductosService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #endregion

        #region Helpers - Validación

        /// <summary>
        /// Valida que el producto no sea nulo
        /// </summary>
        private static void ValidateProducto(Producto producto)
        {
            if (producto == null)
            {
                throw new ArgumentNullException(nameof(producto), EXC_PRODUCTO_NULL);
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
        /// Valida que el vendedorId no esté vacío
        /// </summary>
        private static void ValidateVendedorId(string vendedorId)
        {
            if (string.IsNullOrWhiteSpace(vendedorId))
            {
                throw new ArgumentException(EXC_VENDEDOR_ID_VACIO, nameof(vendedorId));
            }
        }

        /// <summary>
        /// Valida que el count sea válido
        /// </summary>
        private static void ValidateCount(int count)
        {
            if (count <= 0)
            {
                throw new ArgumentException(EXC_COUNT_INVALIDO, nameof(count));
            }
        }

        /// <summary>
        /// Valida parámetros de paginación
        /// </summary>
        private static void ValidatePaginationParams(int pageNumber, int pageSize)
        {
            if (pageNumber < MIN_PAGE_NUMBER)
            {
                throw new ArgumentException(
                    string.Format(EXC_PAGE_NUMBER_INVALIDO, MIN_PAGE_NUMBER),
                    nameof(pageNumber));
            }

            if (pageSize < 1 || pageSize > MAX_PAGE_SIZE)
            {
                throw new ArgumentException(
                    string.Format(EXC_PAGE_SIZE_INVALIDO, MAX_PAGE_SIZE),
                    nameof(pageSize));
            }
        }

        /// <summary>
        /// Valida que el stock no sea negativo
        /// </summary>
        private void ValidateStock(int stock, int productoId)
        {
            if (stock < MIN_STOCK_VALUE)
            {
                _logger.LogWarning(LOG_WARN_STOCK_NEGATIVO, productoId, stock);
                throw new ArgumentException(EXC_STOCK_NEGATIVO, nameof(stock));
            }
        }

        /// <summary>
        /// Valida que el precio no sea negativo
        /// </summary>
        private void ValidatePrecio(decimal precio, int productoId)
        {
            if (precio < MIN_PRICE_VALUE)
            {
                _logger.LogWarning(LOG_WARN_PRECIO_NEGATIVO, productoId, precio);
                throw new ArgumentException(EXC_PRECIO_NEGATIVO, nameof(precio));
            }
        }

        #endregion

        #region Helpers - Query Building

        /// <summary>
        /// Query base con relaciones típicas del dominio.
        /// Usa AsSplitQuery para evitar cartesian explosion.
        /// </summary>
        /// <param name="withVariantes">Si debe incluir las variantes del producto</param>
        /// <param name="tracking">Si debe hacer tracking de cambios (false para solo lectura)</param>
        private IQueryable<Producto> BaseQuery(bool withVariantes = true, bool tracking = true)
        {
            IQueryable<Producto> query = _context.Productos
                .Include(p => p.Categoria)
                .Include(p => p.Subcategoria)
                .Include(p => p.Proveedor);

            if (withVariantes)
            {
                query = query.Include(p => p.Variantes);
            }

            // Evita cartesian explosion con múltiples Includes
            query = query.AsSplitQuery();

            if (!tracking)
            {
                query = query.AsNoTracking();
            }

            return query;
        }

        #endregion

        #region CREATE

        /// <inheritdoc />
        public async Task<int> AddAsync(Producto producto, CancellationToken ct = default)
        {
            ValidateProducto(producto);

            try
            {
                _logger.LogDebug(LOG_DEBUG_AGREGANDO_PRODUCTO, producto.Nombre);

                await _context.Productos.AddAsync(producto, ct).ConfigureAwait(false);
                await _context.SaveChangesAsync(ct).ConfigureAwait(false);

                _logger.LogInformation(LOG_INFO_PRODUCTO_AGREGADO, producto.ProductoID, producto.Nombre);
                return producto.ProductoID;
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, LOG_ERROR_AGREGAR_PRODUCTO, producto.Nombre);
                throw new InvalidOperationException($"Error al guardar el producto: {dbEx.InnerException?.Message}", dbEx);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_AGREGAR_PRODUCTO, producto.Nombre);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<int> AddRangeAsync(IEnumerable<Producto> productos, CancellationToken ct = default)
        {
            if (productos == null || !productos.Any())
            {
                _logger.LogWarning(LOG_WARN_LISTA_PRODUCTOS_VACIA);
                throw new ArgumentException(EXC_PRODUCTOS_LISTA_VACIA, nameof(productos));
            }

            try
            {
                var productosList = productos.ToList();
                _logger.LogDebug(LOG_DEBUG_AGREGANDO_BATCH, productosList.Count);

                await _context.Productos.AddRangeAsync(productosList, ct).ConfigureAwait(false);
                var saved = await _context.SaveChangesAsync(ct).ConfigureAwait(false);

                _logger.LogInformation(LOG_INFO_PRODUCTOS_BATCH_AGREGADOS, productosList.Count, saved);
                return saved;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_AGREGAR_BATCH, productos.Count());
                throw;
            }
        }

        #endregion

        #region READ - Basic

        /// <inheritdoc />
        public async Task<List<Producto>> GetAllAsync(CancellationToken ct = default)
        {
            _logger.LogDebug(LOG_DEBUG_OBTENIENDO_TODOS);

            return await BaseQuery(withVariantes: true, tracking: false)
                .OrderBy(p => p.Nombre)
                .ToListAsync(ct)
                .ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<List<Producto>> GetFirstAsync(int count, CancellationToken ct = default)
        {
            ValidateCount(count);

            _logger.LogDebug(LOG_DEBUG_OBTENIENDO_PRIMEROS, count);

            return await BaseQuery(withVariantes: true, tracking: false)
                .OrderBy(p => p.ProductoID)
                .Take(count)
                .ToListAsync(ct)
                .ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<Producto?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            ValidateId(id);

            _logger.LogDebug(LOG_DEBUG_OBTENIENDO_POR_ID, id, true);

            return await BaseQuery(withVariantes: true, tracking: true)
                .FirstOrDefaultAsync(p => p.ProductoID == id, ct)
                .ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<Producto?> GetByIdReadOnlyAsync(int id, CancellationToken ct = default)
        {
            ValidateId(id);

            _logger.LogDebug(LOG_DEBUG_OBTENIENDO_POR_ID, id, false);

            return await BaseQuery(withVariantes: true, tracking: false)
                .FirstOrDefaultAsync(p => p.ProductoID == id, ct)
                .ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(int id, CancellationToken ct = default)
        {
            if (id <= 0) return false;

            return await _context.Productos
                .AsNoTracking()
                .AnyAsync(p => p.ProductoID == id, ct)
                .ConfigureAwait(false);
        }

        #endregion

        #region READ - By Filters

        /// <inheritdoc />
        public async Task<List<Producto>> GetByCategoryIdAsync(int categoriaId, CancellationToken ct = default)
        {
            ValidateId(categoriaId);

            _logger.LogDebug(LOG_DEBUG_OBTENIENDO_POR_CATEGORIA, categoriaId);

            return await BaseQuery(withVariantes: true, tracking: false)
                .Where(p => p.CategoriaID == categoriaId)
                .OrderBy(p => p.Nombre)
                .ToListAsync(ct)
                .ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<List<Producto>> GetBySubcategoryIdAsync(int subcategoriaId, CancellationToken ct = default)
        {
            ValidateId(subcategoriaId);

            _logger.LogDebug(LOG_DEBUG_OBTENIENDO_POR_SUBCATEGORIA, subcategoriaId);

            return await BaseQuery(withVariantes: true, tracking: false)
                .Where(p => p.SubcategoriaID == subcategoriaId)
                .OrderBy(p => p.Nombre)
                .ToListAsync(ct)
                .ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<List<Producto>> GetByVendedorIdAsync(string vendedorId, CancellationToken ct = default)
        {
            ValidateVendedorId(vendedorId);

            _logger.LogDebug(LOG_DEBUG_OBTENIENDO_POR_VENDEDOR, vendedorId);

            return await BaseQuery(withVariantes: true, tracking: false)
                .Where(p => p.VendedorID == vendedorId)
                .OrderByDescending(p => p.ProductoID)
                .ToListAsync(ct)
                .ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<List<Producto>> GetWithStockAsync(CancellationToken ct = default)
        {
            _logger.LogDebug(LOG_DEBUG_OBTENIENDO_CON_STOCK);

            return await BaseQuery(withVariantes: true, tracking: false)
                .Where(p => p.Stock > 0)
                .OrderBy(p => p.Nombre)
                .ToListAsync(ct)
                .ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<List<Producto>> GetLowStockAsync(int threshold = DEFAULT_LOW_STOCK_THRESHOLD, CancellationToken ct = default)
        {
            _logger.LogDebug(LOG_DEBUG_OBTENIENDO_STOCK_BAJO, threshold);

            return await BaseQuery(withVariantes: true, tracking: false)
                .Where(p => p.Stock > 0 && p.Stock <= threshold)
                .OrderBy(p => p.Stock)
                .ThenBy(p => p.Nombre)
                .ToListAsync(ct)
                .ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<List<Producto>> GetOutOfStockAsync(CancellationToken ct = default)
        {
            _logger.LogDebug(LOG_DEBUG_OBTENIENDO_SIN_STOCK);

            return await BaseQuery(withVariantes: true, tracking: false)
                .Where(p => p.Stock <= 0)
                .OrderBy(p => p.Nombre)
                .ToListAsync(ct)
                .ConfigureAwait(false);
        }

        #endregion

        #region READ - Advanced

        /// <inheritdoc />
        public async Task<List<Producto>> SearchAsync(
            string? searchTerm = null,
            int? categoriaId = null,
            int? subcategoriaId = null,
            decimal? minPrice = null,
            decimal? maxPrice = null,
            bool? inStock = null,
            CancellationToken ct = default)
        {
            _logger.LogDebug(LOG_DEBUG_BUSQUEDA_AVANZADA,
                searchTerm, categoriaId, subcategoriaId, minPrice, maxPrice, inStock);

            var query = BaseQuery(withVariantes: true, tracking: false);

            // Filtro de búsqueda por texto
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.ToLower();
                query = query.Where(p =>
                    p.Nombre.ToLower().Contains(term) ||
                    (p.Descripcion != null && p.Descripcion.ToLower().Contains(term)) ||
                    (p.Marca != null && p.Marca.ToLower().Contains(term)));
            }

            // Filtros por categoría/subcategoría
            if (categoriaId.HasValue && categoriaId.Value > 0)
            {
                query = query.Where(p => p.CategoriaID == categoriaId.Value);
            }

            if (subcategoriaId.HasValue && subcategoriaId.Value > 0)
            {
                query = query.Where(p => p.SubcategoriaID == subcategoriaId.Value);
            }

            // Filtros por rango de precio
            if (minPrice.HasValue && minPrice.Value > 0)
            {
                query = query.Where(p => p.PrecioVenta >= minPrice.Value);
            }

            if (maxPrice.HasValue && maxPrice.Value > 0)
            {
                query = query.Where(p => p.PrecioVenta <= maxPrice.Value);
            }

            // Filtro de stock
            if (inStock.HasValue)
            {
                query = inStock.Value
                    ? query.Where(p => p.Stock > 0)
                    : query.Where(p => p.Stock <= 0);
            }

            return await query
                .OrderBy(p => p.Nombre)
                .ToListAsync(ct)
                .ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<PagedResult<Producto>> GetPagedAsync(
            int pageNumber = MIN_PAGE_NUMBER,
            int pageSize = DEFAULT_PAGE_SIZE,
            Expression<Func<Producto, object>>? orderBy = null,
            bool ascending = true,
            CancellationToken ct = default)
        {
            ValidatePaginationParams(pageNumber, pageSize);

            _logger.LogDebug(LOG_DEBUG_PAGINACION, pageNumber, pageSize);

            var query = BaseQuery(withVariantes: true, tracking: false);

            // Contar total ANTES de paginar
            var totalCount = await query.CountAsync(ct).ConfigureAwait(false);

            // Aplicar ordenamiento
            if (orderBy != null)
            {
                query = ascending
                    ? query.OrderBy(orderBy)
                    : query.OrderByDescending(orderBy);
            }
            else
            {
                query = query.OrderBy(p => p.ProductoID);
            }

            // Aplicar paginación
            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            return new PagedResult<Producto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        #endregion

        #region UPDATE

        /// <inheritdoc />
        public async Task<bool> UpdateAsync(Producto producto, CancellationToken ct = default)
        {
            ValidateProducto(producto);

            try
            {
                _logger.LogDebug(LOG_DEBUG_ACTUALIZANDO_PRODUCTO, producto.ProductoID, producto.Nombre);

                _context.Productos.Update(producto);
                var changes = await _context.SaveChangesAsync(ct).ConfigureAwait(false);

                if (changes > 0)
                {
                    _logger.LogInformation(LOG_INFO_PRODUCTO_ACTUALIZADO, producto.ProductoID, changes);
                    return true;
                }
                else
                {
                    _logger.LogWarning(LOG_WARN_PRODUCTO_NO_ACTUALIZADO, producto.ProductoID);
                    return false;
                }
            }
            catch (DbUpdateConcurrencyException concEx)
            {
                _logger.LogError(concEx, LOG_ERROR_CONCURRENCIA, producto.ProductoID);
                throw new InvalidOperationException("El producto fue modificado por otro usuario. Recarga y vuelve a intentar.", concEx);
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, LOG_ERROR_BASE_DATOS, "Update", producto.ProductoID);
                throw new InvalidOperationException($"Error al actualizar el producto: {dbEx.InnerException?.Message}", dbEx);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_ACTUALIZAR_PRODUCTO, producto.ProductoID);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> UpdateStockAsync(int productoId, int nuevoStock, CancellationToken ct = default)
        {
            ValidateId(productoId);
            ValidateStock(nuevoStock, productoId);

            try
            {
                var producto = await _context.Productos
                    .FirstOrDefaultAsync(p => p.ProductoID == productoId, ct)
                    .ConfigureAwait(false);

                if (producto == null)
                {
                    _logger.LogWarning(LOG_WARN_PRODUCTO_NO_ENCONTRADO, productoId);
                    return false;
                }

                var stockAnterior = producto.Stock;
                producto.Stock = nuevoStock;

                await _context.SaveChangesAsync(ct).ConfigureAwait(false);

                _logger.LogInformation(LOG_INFO_STOCK_ACTUALIZADO, productoId, stockAnterior, nuevoStock);
                return true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_ACTUALIZAR_STOCK, productoId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> UpdatePrecioAsync(int productoId, decimal nuevoPrecio, CancellationToken ct = default)
        {
            ValidateId(productoId);
            ValidatePrecio(nuevoPrecio, productoId);

            try
            {
                var producto = await _context.Productos
                    .FirstOrDefaultAsync(p => p.ProductoID == productoId, ct)
                    .ConfigureAwait(false);

                if (producto == null)
                {
                    _logger.LogWarning(LOG_WARN_PRODUCTO_NO_ENCONTRADO, productoId);
                    return false;
                }

                var precioAnterior = producto.PrecioVenta;
                producto.PrecioVenta = nuevoPrecio;

                await _context.SaveChangesAsync(ct).ConfigureAwait(false);

                _logger.LogInformation(LOG_INFO_PRECIO_ACTUALIZADO, productoId, precioAnterior, nuevoPrecio);
                return true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_ACTUALIZAR_PRECIO, productoId);
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
                _logger.LogDebug(LOG_DEBUG_ELIMINANDO_PRODUCTO, id);

                // Cargar producto con variantes
                var producto = await _context.Productos
                    .Include(p => p.Variantes)
                    .FirstOrDefaultAsync(p => p.ProductoID == id, ct)
                    .ConfigureAwait(false);

                if (producto == null)
                {
                    _logger.LogWarning(LOG_WARN_PRODUCTO_NO_ENCONTRADO, id);
                    return false;
                }

                // Verificar dependencias
                var dependencias = await GetDependenciasAsync(id, ct).ConfigureAwait(false);

                if (dependencias.TieneDependencias)
                {
                    _logger.LogWarning(LOG_WARN_PRODUCTO_CON_DEPENDENCIAS,
                        id, dependencias.Ventas, dependencias.Pedidos, dependencias.Carrito, dependencias.Reseñas);
                    await transaction.RollbackAsync(ct).ConfigureAwait(false);
                    return false;
                }

                // Eliminar variantes si existen
                if (producto.Variantes != null && producto.Variantes.Any())
                {
                    _logger.LogDebug(LOG_DEBUG_ELIMINANDO_VARIANTES, producto.Variantes.Count, id);
                    _context.ProductoVariantes.RemoveRange(producto.Variantes);
                }

                // Eliminar producto
                _context.Productos.Remove(producto);
                await _context.SaveChangesAsync(ct).ConfigureAwait(false);
                await transaction.CommitAsync(ct).ConfigureAwait(false);

                _logger.LogInformation(LOG_INFO_PRODUCTO_ELIMINADO, id);
                return true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await transaction.RollbackAsync(ct).ConfigureAwait(false);
                _logger.LogError(ex, LOG_ERROR_ELIMINAR_PRODUCTO, id);
                throw new InvalidOperationException($"Error al eliminar el producto: {ex.Message}", ex);
            }
        }

        /// <inheritdoc />
        public async Task<(int eliminados, int fallidos)> DeleteRangeAsync(IEnumerable<int> ids, CancellationToken ct = default)
        {
            if (ids == null || !ids.Any())
            {
                _logger.LogWarning(LOG_WARN_LISTA_IDS_VACIA);
                throw new ArgumentException(EXC_IDS_LISTA_VACIA, nameof(ids));
            }

            var idsList = ids.ToList();
            var eliminados = 0;
            var fallidos = 0;

            foreach (var id in idsList)
            {
                try
                {
                    var result = await DeleteAsync(id, ct).ConfigureAwait(false);
                    if (result)
                        eliminados++;
                    else
                        fallidos++;
                }
                catch
                {
                    fallidos++;
                }
            }

            _logger.LogInformation(LOG_INFO_BATCH_DELETE_COMPLETADO, idsList.Count, eliminados, fallidos);
            return (eliminados, fallidos);
        }

        #endregion

        #region Business Logic

        /// <inheritdoc />
        public async Task<bool> RecomputeFromVariantsAsync(int productoId, CancellationToken ct = default)
        {
            ValidateId(productoId);

            using var transaction = await _context.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

            try
            {
                _logger.LogDebug(LOG_DEBUG_RECALCULANDO_AGREGADOS, productoId);

                // Cargar variantes y producto
                var producto = await _context.Productos
                    .Include(p => p.Variantes)
                    .FirstOrDefaultAsync(p => p.ProductoID == productoId, ct)
                    .ConfigureAwait(false);

                if (producto == null)
                {
                    _logger.LogWarning(LOG_WARN_PRODUCTO_NO_ENCONTRADO, productoId);
                    return false;
                }

                var variantes = producto.Variantes?.ToList() ?? new List<ProductoVariante>();

                if (!variantes.Any())
                {
                    _logger.LogWarning(LOG_WARN_PRODUCTO_SIN_VARIANTES, productoId);
                    await transaction.CommitAsync(ct).ConfigureAwait(false);
                    return true;
                }

                // Recalcular stock total
                var stockTotal = variantes.Sum(v => v.Stock);

                // Recalcular precio mínimo
                var precioMinimo = variantes
                    .Where(v => v.PrecioVenta.HasValue && v.PrecioVenta.Value > 0)
                    .Select(v => v.PrecioVenta!.Value)
                    .DefaultIfEmpty(0m)
                    .Min();

                // Actualizar solo si cambió
                var changed = false;
                if (producto.Stock != stockTotal)
                {
                    producto.Stock = stockTotal;
                    changed = true;
                }

                if (producto.PrecioVenta != precioMinimo)
                {
                    producto.PrecioVenta = precioMinimo;
                    changed = true;
                }

                if (changed)
                {
                    _context.Productos.Update(producto);
                    await _context.SaveChangesAsync(ct).ConfigureAwait(false);
                    _logger.LogInformation(LOG_INFO_PRODUCTO_RECALCULADO, productoId, stockTotal, precioMinimo);
                }
                else
                {
                    _logger.LogDebug(LOG_DEBUG_PRODUCTO_YA_ACTUALIZADO, productoId);
                }

                await transaction.CommitAsync(ct).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await transaction.RollbackAsync(ct).ConfigureAwait(false);
                _logger.LogError(ex, LOG_ERROR_RECALCULAR_AGREGADOS, productoId);
                throw new InvalidOperationException($"Error al recalcular agregados: {ex.Message}", ex);
            }
        }

        /// <inheritdoc />
        public async Task<Dictionary<int, int>> GetCountByCategoryAsync(CancellationToken ct = default)
        {
            _logger.LogDebug(LOG_DEBUG_OBTENIENDO_CONTEO_CATEGORIAS);

            return await _context.Productos
                .AsNoTracking()
                .GroupBy(p => p.CategoriaID)
                .Select(g => new { CategoriaId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.CategoriaId, x => x.Count, ct)
                .ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<ProductosStats> GetStatsAsync(CancellationToken ct = default)
        {
            _logger.LogDebug(LOG_DEBUG_OBTENIENDO_ESTADISTICAS);

            try
            {
                var productos = await _context.Productos
                    .AsNoTracking()
                    .ToListAsync(ct)
                    .ConfigureAwait(false);

                return new ProductosStats
                {
                    TotalProductos = productos.Count,
                    ConStock = productos.Count(p => p.Stock > 0),
                    SinStock = productos.Count(p => p.Stock <= 0),
                    StockBajo = productos.Count(p => p.Stock > 0 && p.Stock <= DEFAULT_LOW_STOCK_THRESHOLD),
                    PrecioPromedio = productos.Any() ? productos.Average(p => p.PrecioVenta) : 0,
                    PrecioMinimo = productos.Any() ? productos.Min(p => p.PrecioVenta) : 0,
                    PrecioMaximo = productos.Any() ? productos.Max(p => p.PrecioVenta) : 0,
                    StockTotal = productos.Sum(p => p.Stock)
                };
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_OBTENER_ESTADISTICAS);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<ProductosStatsDetalladas> GetStatsDetalladasAsync(CancellationToken ct = default)
        {
            _logger.LogDebug(LOG_DEBUG_OBTENIENDO_ESTADISTICAS);

            try
            {
                var productos = await _context.Productos
                    .AsNoTracking()
                    .ToListAsync(ct)
                    .ConfigureAwait(false);

                var stats = new ProductosStatsDetalladas
                {
                    TotalProductos = productos.Count,
                    ConStock = productos.Count(p => p.Stock > 0),
                    SinStock = productos.Count(p => p.Stock <= 0),
                    StockBajo = productos.Count(p => p.Stock > 0 && p.Stock <= DEFAULT_LOW_STOCK_THRESHOLD),
                    PrecioPromedio = productos.Any() ? productos.Average(p => p.PrecioVenta) : 0,
                    PrecioMinimo = productos.Any() ? productos.Min(p => p.PrecioVenta) : 0,
                    PrecioMaximo = productos.Any() ? productos.Max(p => p.PrecioVenta) : 0,
                    StockTotal = productos.Sum(p => p.Stock)
                };

                // Estadísticas adicionales
                if (productos.Any())
                {
                    stats.PrecioMediana = CalcularMediana(productos.Select(p => p.PrecioVenta).OrderBy(p => p).ToList());
                    stats.StockPromedio = productos.Any() ? (decimal)productos.Average(p => p.Stock) : 0;
                    stats.ProductosPorEncimaPrecioPromedio = productos.Count(p => p.PrecioVenta > stats.PrecioPromedio);
                    stats.ProductosPorDebajoPrecioPromedio = productos.Count(p => p.PrecioVenta < stats.PrecioPromedio);
                }

                return stats;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_OBTENER_ESTADISTICAS);
                throw;
            }
        }

        /// <summary>
        /// Calcula la mediana de una lista de valores
        /// </summary>
        private static decimal CalcularMediana(List<decimal> valores)
        {
            if (!valores.Any()) return 0;

            var count = valores.Count;
            if (count % 2 == 0)
            {
                return (valores[count / 2 - 1] + valores[count / 2]) / 2m;
            }
            else
            {
                return valores[count / 2];
            }
        }

        #endregion

        #region Validation

        /// <inheritdoc />
        public async Task<bool> HasDependenciesAsync(int productoId, CancellationToken ct = default)
        {
            ValidateId(productoId);

            try
            {
                _logger.LogDebug(LOG_DEBUG_VERIFICANDO_DEPENDENCIAS, productoId);

                return await _context.DetalleVentas.AnyAsync(d => d.ProductoID == productoId, ct).ConfigureAwait(false) ||
                       await _context.DetallesPedido.AnyAsync(d => d.ProductoID == productoId, ct).ConfigureAwait(false) ||
                       await _context.CarritoDetalle.AnyAsync(d => d.ProductoID == productoId, ct).ConfigureAwait(false) ||
                       await _context.Reseñas.AnyAsync(r => r.ProductoID == productoId, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_VERIFICAR_DEPENDENCIAS, productoId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<ProductoDependencias> GetDependenciasAsync(int productoId, CancellationToken ct = default)
        {
            ValidateId(productoId);

            try
            {
                _logger.LogDebug(LOG_DEBUG_OBTENIENDO_DEPENDENCIAS, productoId);

                var dependencias = new ProductoDependencias
                {
                    ProductoId = productoId,
                    Ventas = await _context.DetalleVentas.CountAsync(d => d.ProductoID == productoId, ct).ConfigureAwait(false),
                    Pedidos = await _context.DetallesPedido.CountAsync(d => d.ProductoID == productoId, ct).ConfigureAwait(false),
                    Carrito = await _context.CarritoDetalle.CountAsync(d => d.ProductoID == productoId, ct).ConfigureAwait(false),
                    Reseñas = await _context.Reseñas.CountAsync(r => r.ProductoID == productoId, ct).ConfigureAwait(false)
                };

                return dependencias;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_VERIFICAR_DEPENDENCIAS, productoId);
                throw;
            }
        }

        #endregion
    }

    #endregion

    #region DTOs

    /// <summary>
    /// Resultado paginado genérico
    /// </summary>
    public class PagedResult<T>
    {
        /// <summary>
        /// Items de la página actual
        /// </summary>
        public List<T> Items { get; set; } = new List<T>();

        /// <summary>
        /// Total de items en todas las páginas
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// Número de página actual (base 1)
        /// </summary>
        public int PageNumber { get; set; }

        /// <summary>
        /// Tamaño de página
        /// </summary>
        public int PageSize { get; set; }

        /// <summary>
        /// Total de páginas
        /// </summary>
        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);

        /// <summary>
        /// Indica si hay página anterior
        /// </summary>
        public bool HasPreviousPage => PageNumber > 1;

        /// <summary>
        /// Indica si hay página siguiente
        /// </summary>
        public bool HasNextPage => PageNumber < TotalPages;

        /// <summary>
        /// Indica si es la primera página
        /// </summary>
        public bool IsFirstPage => PageNumber == 1;

        /// <summary>
        /// Indica si es la última página
        /// </summary>
        public bool IsLastPage => PageNumber == TotalPages;
    }

    /// <summary>
    /// Estadísticas básicas de productos
    /// </summary>
    public class ProductosStats
    {
        /// <summary>
        /// Total de productos
        /// </summary>
        public int TotalProductos { get; set; }

        /// <summary>
        /// Productos con stock disponible
        /// </summary>
        public int ConStock { get; set; }

        /// <summary>
        /// Productos sin stock
        /// </summary>
        public int SinStock { get; set; }

        /// <summary>
        /// Productos con stock bajo
        /// </summary>
        public int StockBajo { get; set; }

        /// <summary>
        /// Precio promedio
        /// </summary>
        public decimal PrecioPromedio { get; set; }

        /// <summary>
        /// Precio mínimo
        /// </summary>
        public decimal PrecioMinimo { get; set; }

        /// <summary>
        /// Precio máximo
        /// </summary>
        public decimal PrecioMaximo { get; set; }

        /// <summary>
        /// Stock total
        /// </summary>
        public int StockTotal { get; set; }

        /// <summary>
        /// Porcentaje de productos con stock
        /// </summary>
        public decimal PorcentajeConStock =>
            TotalProductos > 0 ? (decimal)ConStock / TotalProductos * 100 : 0;

        /// <summary>
        /// Porcentaje de productos sin stock
        /// </summary>
        public decimal PorcentajeSinStock =>
            TotalProductos > 0 ? (decimal)SinStock / TotalProductos * 100 : 0;
    }

    /// <summary>
    /// Estadísticas detalladas de productos
    /// </summary>
    public class ProductosStatsDetalladas : ProductosStats
    {
        /// <summary>
        /// Precio mediana
        /// </summary>
        public decimal PrecioMediana { get; set; }

        /// <summary>
        /// Stock promedio
        /// </summary>
        public decimal StockPromedio { get; set; }

        /// <summary>
        /// Productos por encima del precio promedio
        /// </summary>
        public int ProductosPorEncimaPrecioPromedio { get; set; }

        /// <summary>
        /// Productos por debajo del precio promedio
        /// </summary>
        public int ProductosPorDebajoPrecioPromedio { get; set; }
    }

    /// <summary>
    /// Dependencias de un producto
    /// </summary>
    public class ProductoDependencias
    {
        /// <summary>
        /// ID del producto
        /// </summary>
        public int ProductoId { get; set; }

        /// <summary>
        /// Cantidad en ventas
        /// </summary>
        public int Ventas { get; set; }

        /// <summary>
        /// Cantidad en pedidos
        /// </summary>
        public int Pedidos { get; set; }

        /// <summary>
        /// Cantidad en carritos
        /// </summary>
        public int Carrito { get; set; }

        /// <summary>
        /// Cantidad de reseñas
        /// </summary>
        public int Reseñas { get; set; }

        /// <summary>
        /// Total de dependencias
        /// </summary>
        public int Total => Ventas + Pedidos + Carrito + Reseñas;

        /// <summary>
        /// Indica si tiene dependencias
        /// </summary>
        public bool TieneDependencias => Total > 0;
    }

    #endregion
}