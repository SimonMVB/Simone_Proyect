using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Simone.Data;
using Simone.Models;

namespace Simone.Services
{
    /// <summary>
    /// Servicio para gestión completa de productos con optimizaciones de performance,
    /// transacciones, validaciones y métodos avanzados de consulta.
    /// </summary>
    public class ProductosService
    {
        private readonly TiendaDbContext _context;
        private readonly ILogger<ProductosService> _logger;

        // Constantes de configuración
        private const int DEFAULT_PAGE_SIZE = 20;
        private const int MAX_PAGE_SIZE = 100;

        public ProductosService(TiendaDbContext context, ILogger<ProductosService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region Query Helpers

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

        /// <summary>
        /// Agrega un nuevo producto al sistema
        /// </summary>
        /// <exception cref="ArgumentNullException">Si el producto es null</exception>
        /// <exception cref="InvalidOperationException">Si hay error al guardar</exception>
        public async Task<int> AddAsync(Producto producto)
        {
            if (producto == null)
            {
                throw new ArgumentNullException(nameof(producto));
            }

            try
            {
                _logger.LogInformation("Agregando nuevo producto: {Nombre}", producto.Nombre);

                await _context.Productos.AddAsync(producto);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Producto {ProductoId} creado exitosamente", producto.ProductoID);
                return producto.ProductoID;
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Error de base de datos al agregar producto {Nombre}", producto.Nombre);
                throw new InvalidOperationException($"Error al guardar el producto: {dbEx.InnerException?.Message}", dbEx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al agregar producto {Nombre}", producto.Nombre);
                throw;
            }
        }

        /// <summary>
        /// Agrega múltiples productos en batch (más eficiente)
        /// </summary>
        public async Task<int> AddRangeAsync(IEnumerable<Producto> productos)
        {
            if (productos == null || !productos.Any())
            {
                throw new ArgumentException("La lista de productos no puede estar vacía", nameof(productos));
            }

            try
            {
                var productosList = productos.ToList();
                _logger.LogInformation("Agregando {Count} productos en batch", productosList.Count);

                await _context.Productos.AddRangeAsync(productosList);
                var saved = await _context.SaveChangesAsync();

                _logger.LogInformation("{Saved} productos guardados exitosamente", saved);
                return saved;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al agregar productos en batch");
                throw;
            }
        }

        #endregion

        #region READ - Basic

        /// <summary>
        /// Obtiene todos los productos (para admin).
        /// Incluye Categoria, Subcategoria, Proveedor y Variantes.
        /// Solo lectura (AsNoTracking).
        /// </summary>
        /// <remarks>
        /// ⚠️ ADVERTENCIA: Este método puede retornar muchos registros.
        /// Considerar usar GetPagedAsync para mejor performance.
        /// </remarks>
        public async Task<List<Producto>> GetAllAsync()
        {
            _logger.LogDebug("Obteniendo todos los productos");

            return await BaseQuery(withVariantes: true, tracking: false)
                .OrderBy(p => p.Nombre)
                .ToListAsync();
        }

        /// <summary>
        /// Obtiene los primeros N productos ordenados por ID
        /// </summary>
        public async Task<List<Producto>> GetFirstAsync(int count)
        {
            if (count <= 0)
            {
                throw new ArgumentException("El conteo debe ser mayor a 0", nameof(count));
            }

            _logger.LogDebug("Obteniendo primeros {Count} productos", count);

            return await BaseQuery(withVariantes: true, tracking: false)
                .OrderBy(p => p.ProductoID)
                .Take(count)
                .ToListAsync();
        }

        /// <summary>
        /// Obtiene un producto por ID con tracking (para edición)
        /// </summary>
        public async Task<Producto?> GetByIdAsync(int id)
        {
            if (id <= 0)
            {
                throw new ArgumentException("ID debe ser mayor a 0", nameof(id));
            }

            _logger.LogDebug("Obteniendo producto {ProductoId} con tracking", id);

            return await BaseQuery(withVariantes: true, tracking: true)
                .FirstOrDefaultAsync(p => p.ProductoID == id);
        }

        /// <summary>
        /// Obtiene un producto por ID en solo-lectura (para vistas/detalles)
        /// </summary>
        public async Task<Producto?> GetByIdReadOnlyAsync(int id)
        {
            if (id <= 0)
            {
                throw new ArgumentException("ID debe ser mayor a 0", nameof(id));
            }

            _logger.LogDebug("Obteniendo producto {ProductoId} en modo solo lectura", id);

            return await BaseQuery(withVariantes: true, tracking: false)
                .FirstOrDefaultAsync(p => p.ProductoID == id);
        }

        /// <summary>
        /// Verifica si existe un producto con el ID especificado
        /// </summary>
        public async Task<bool> ExistsAsync(int id)
        {
            if (id <= 0) return false;

            return await _context.Productos
                .AsNoTracking()
                .AnyAsync(p => p.ProductoID == id);
        }

        #endregion

        #region READ - By Filters

        /// <summary>
        /// Obtiene productos por categoría
        /// </summary>
        public async Task<List<Producto>> GetByCategoryIdAsync(int categoriaId)
        {
            if (categoriaId <= 0)
            {
                throw new ArgumentException("CategoriaID debe ser mayor a 0", nameof(categoriaId));
            }

            _logger.LogDebug("Obteniendo productos de categoría {CategoriaId}", categoriaId);

            return await BaseQuery(withVariantes: true, tracking: false)
                .Where(p => p.CategoriaID == categoriaId)
                .OrderBy(p => p.Nombre)
                .ToListAsync();
        }

        /// <summary>
        /// Obtiene productos por subcategoría
        /// </summary>
        public async Task<List<Producto>> GetBySubcategoryIdAsync(int subcategoriaId)
        {
            if (subcategoriaId <= 0)
            {
                throw new ArgumentException("SubcategoriaID debe ser mayor a 0", nameof(subcategoriaId));
            }

            _logger.LogDebug("Obteniendo productos de subcategoría {SubcategoriaId}", subcategoriaId);

            return await BaseQuery(withVariantes: true, tracking: false)
                .Where(p => p.SubcategoriaID == subcategoriaId)
                .OrderBy(p => p.Nombre)
                .ToListAsync();
        }

        /// <summary>
        /// Obtiene productos por vendedor
        /// </summary>
        public async Task<List<Producto>> GetByVendedorIdAsync(string vendedorId)
        {
            if (string.IsNullOrWhiteSpace(vendedorId))
            {
                throw new ArgumentException("VendedorID no puede estar vacío", nameof(vendedorId));
            }

            _logger.LogDebug("Obteniendo productos del vendedor {VendedorId}", vendedorId);

            return await BaseQuery(withVariantes: true, tracking: false)
                .Where(p => p.VendedorID == vendedorId)
                .OrderByDescending(p => p.ProductoID)
                .ToListAsync();
        }

        /// <summary>
        /// Obtiene productos con stock disponible (stock > 0)
        /// </summary>
        public async Task<List<Producto>> GetWithStockAsync()
        {
            _logger.LogDebug("Obteniendo productos con stock disponible");

            return await BaseQuery(withVariantes: true, tracking: false)
                .Where(p => p.Stock > 0)
                .OrderBy(p => p.Nombre)
                .ToListAsync();
        }

        /// <summary>
        /// Obtiene productos con stock bajo (configurable)
        /// </summary>
        public async Task<List<Producto>> GetLowStockAsync(int threshold = 10)
        {
            _logger.LogDebug("Obteniendo productos con stock bajo (umbral: {Threshold})", threshold);

            return await BaseQuery(withVariantes: true, tracking: false)
                .Where(p => p.Stock > 0 && p.Stock <= threshold)
                .OrderBy(p => p.Stock)
                .ThenBy(p => p.Nombre)
                .ToListAsync();
        }

        /// <summary>
        /// Obtiene productos sin stock
        /// </summary>
        public async Task<List<Producto>> GetOutOfStockAsync()
        {
            _logger.LogDebug("Obteniendo productos sin stock");

            return await BaseQuery(withVariantes: true, tracking: false)
                .Where(p => p.Stock <= 0)
                .OrderBy(p => p.Nombre)
                .ToListAsync();
        }

        #endregion

        #region READ - Advanced (Search & Pagination)

        /// <summary>
        /// Búsqueda avanzada de productos con múltiples criterios
        /// </summary>
        /// <param name="searchTerm">Término de búsqueda (busca en nombre, descripción, marca)</param>
        /// <param name="categoriaId">Filtro por categoría (opcional)</param>
        /// <param name="subcategoriaId">Filtro por subcategoría (opcional)</param>
        /// <param name="minPrice">Precio mínimo (opcional)</param>
        /// <param name="maxPrice">Precio máximo (opcional)</param>
        /// <param name="inStock">Solo productos con stock (opcional)</param>
        public async Task<List<Producto>> SearchAsync(
            string? searchTerm = null,
            int? categoriaId = null,
            int? subcategoriaId = null,
            decimal? minPrice = null,
            decimal? maxPrice = null,
            bool? inStock = null)
        {
            _logger.LogDebug("Búsqueda de productos con filtros: Term={SearchTerm}, Cat={CatId}, SubCat={SubCatId}",
                searchTerm, categoriaId, subcategoriaId);

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
                .ToListAsync();
        }

        /// <summary>
        /// Obtiene productos paginados con ordenamiento
        /// </summary>
        /// <param name="pageNumber">Número de página (base 1)</param>
        /// <param name="pageSize">Tamaño de página (máximo 100)</param>
        /// <param name="orderBy">Expresión de ordenamiento</param>
        /// <param name="ascending">Dirección del ordenamiento</param>
        public async Task<PagedResult<Producto>> GetPagedAsync(
            int pageNumber = 1,
            int pageSize = DEFAULT_PAGE_SIZE,
            Expression<Func<Producto, object>>? orderBy = null,
            bool ascending = true)
        {
            if (pageNumber < 1)
            {
                throw new ArgumentException("El número de página debe ser mayor o igual a 1", nameof(pageNumber));
            }

            if (pageSize < 1 || pageSize > MAX_PAGE_SIZE)
            {
                throw new ArgumentException($"El tamaño de página debe estar entre 1 y {MAX_PAGE_SIZE}", nameof(pageSize));
            }

            _logger.LogDebug("Obteniendo página {PageNumber} con tamaño {PageSize}", pageNumber, pageSize);

            var query = BaseQuery(withVariantes: true, tracking: false);

            // Contar total ANTES de paginar
            var totalCount = await query.CountAsync();

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
                .ToListAsync();

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

        /// <summary>
        /// Actualiza un producto existente
        /// </summary>
        /// <exception cref="ArgumentNullException">Si el producto es null</exception>
        /// <exception cref="InvalidOperationException">Si hay error al actualizar</exception>
        public async Task<bool> UpdateAsync(Producto producto)
        {
            if (producto == null)
            {
                throw new ArgumentNullException(nameof(producto));
            }

            try
            {
                _logger.LogInformation("Actualizando producto {ProductoId}: {Nombre}", producto.ProductoID, producto.Nombre);

                _context.Productos.Update(producto);
                var changes = await _context.SaveChangesAsync();

                _logger.LogInformation("Producto {ProductoId} actualizado ({Changes} cambios)", producto.ProductoID, changes);
                return changes > 0;
            }
            catch (DbUpdateConcurrencyException concEx)
            {
                _logger.LogError(concEx, "Error de concurrencia al actualizar producto {ProductoId}", producto.ProductoID);
                throw new InvalidOperationException("El producto fue modificado por otro usuario. Recarga y vuelve a intentar.", concEx);
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Error de base de datos al actualizar producto {ProductoId}", producto.ProductoID);
                throw new InvalidOperationException($"Error al actualizar el producto: {dbEx.InnerException?.Message}", dbEx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al actualizar producto {ProductoId}", producto.ProductoID);
                throw;
            }
        }

        #endregion

        #region DELETE

        /// <summary>
        /// Elimina un producto si no tiene dependencias.
        /// Usa transacción para garantizar consistencia.
        /// </summary>
        /// <param name="id">ID del producto a eliminar</param>
        /// <returns>True si se eliminó, False si tiene dependencias o no existe</returns>
        /// <exception cref="InvalidOperationException">Si hay error al eliminar</exception>
        public async Task<bool> DeleteAsync(int id)
        {
            if (id <= 0)
            {
                throw new ArgumentException("ID debe ser mayor a 0", nameof(id));
            }

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _logger.LogInformation("Intentando eliminar producto {ProductoId}", id);

                // Cargar producto con variantes
                var producto = await _context.Productos
                    .Include(p => p.Variantes)
                    .FirstOrDefaultAsync(p => p.ProductoID == id);

                if (producto == null)
                {
                    _logger.LogWarning("Producto {ProductoId} no encontrado", id);
                    return false;
                }

                // ✅ OPTIMIZACIÓN: Verificar todas las dependencias en UNA SOLA query
                var hasDependencies = await HasDependenciesAsync(id);

                if (hasDependencies)
                {
                    _logger.LogWarning("No se puede eliminar producto {ProductoId} porque tiene dependencias en ventas/pedidos/carrito/reseñas", id);
                    await transaction.RollbackAsync();
                    return false;
                }

                // Eliminar variantes si existen (por si no hay cascade delete)
                if (producto.Variantes != null && producto.Variantes.Any())
                {
                    _logger.LogDebug("Eliminando {Count} variantes del producto {ProductoId}", producto.Variantes.Count, id);
                    _context.ProductoVariantes.RemoveRange(producto.Variantes);
                }

                // Eliminar producto
                _context.Productos.Remove(producto);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Producto {ProductoId} eliminado exitosamente", id);
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error al eliminar producto {ProductoId}. Transacción revertida", id);
                throw new InvalidOperationException($"Error al eliminar el producto: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Verifica si un producto tiene dependencias que impiden su eliminación
        /// </summary>
        private async Task<bool> HasDependenciesAsync(int productoId)
        {
            // ✅ OPTIMIZACIÓN: Una sola query que verifica todas las dependencias
            return await _context.DetalleVentas.AnyAsync(d => d.ProductoID == productoId) ||
                   await _context.DetallesPedido.AnyAsync(d => d.ProductoID == productoId) ||
                   await _context.CarritoDetalle.AnyAsync(d => d.ProductoID == productoId) ||
                   await _context.Reseñas.AnyAsync(r => r.ProductoID == productoId);
        }

        #endregion

        #region Business Logic

        /// <summary>
        /// Recalcula y actualiza los campos agregados del producto desde sus variantes.
        /// Stock total = suma de stock de variantes
        /// PrecioVenta = mínimo precio de variantes
        /// Usa transacción para garantizar consistencia.
        /// </summary>
        public async Task<bool> RecomputeFromVariantsAsync(int productoId)
        {
            if (productoId <= 0)
            {
                throw new ArgumentException("ProductoID debe ser mayor a 0", nameof(productoId));
            }

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _logger.LogInformation("Recalculando agregados desde variantes para producto {ProductoId}", productoId);

                // Cargar variantes y producto en una sola operación
                var producto = await _context.Productos
                    .Include(p => p.Variantes)
                    .FirstOrDefaultAsync(p => p.ProductoID == productoId);

                if (producto == null)
                {
                    _logger.LogWarning("Producto {ProductoId} no encontrado", productoId);
                    return false;
                }

                var variantes = producto.Variantes?.ToList() ?? new List<ProductoVariante>();

                if (!variantes.Any())
                {
                    _logger.LogDebug("Producto {ProductoId} no tiene variantes, no se recalcula", productoId);
                    await transaction.CommitAsync();
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
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Producto {ProductoId} recalculado: Stock={Stock}, Precio={Precio}",
                        productoId, stockTotal, precioMinimo);
                }
                else
                {
                    _logger.LogDebug("Producto {ProductoId} ya está actualizado", productoId);
                }

                await transaction.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error al recalcular agregados para producto {ProductoId}. Transacción revertida", productoId);
                throw new InvalidOperationException($"Error al recalcular agregados: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Obtiene el conteo de productos por categoría
        /// </summary>
        public async Task<Dictionary<int, int>> GetCountByCategoryAsync()
        {
            _logger.LogDebug("Obteniendo conteo de productos por categoría");

            return await _context.Productos
                .AsNoTracking()
                .GroupBy(p => p.CategoriaID)
                .Select(g => new { CategoriaId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.CategoriaId, x => x.Count);
        }

        /// <summary>
        /// Obtiene estadísticas básicas de productos
        /// </summary>
        public async Task<ProductosStats> GetStatsAsync()
        {
            _logger.LogDebug("Obteniendo estadísticas de productos");

            var productos = await _context.Productos
                .AsNoTracking()
                .ToListAsync();

            return new ProductosStats
            {
                TotalProductos = productos.Count,
                ConStock = productos.Count(p => p.Stock > 0),
                SinStock = productos.Count(p => p.Stock <= 0),
                StockBajo = productos.Count(p => p.Stock > 0 && p.Stock <= 10),
                PrecioPromedio = productos.Any() ? productos.Average(p => p.PrecioVenta) : 0,
                PrecioMinimo = productos.Any() ? productos.Min(p => p.PrecioVenta) : 0,
                PrecioMaximo = productos.Any() ? productos.Max(p => p.PrecioVenta) : 0,
                StockTotal = productos.Sum(p => p.Stock)
            };
        }

        #endregion
    }

    #region DTOs

    /// <summary>
    /// Resultado paginado genérico
    /// </summary>
    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new List<T>();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
        public bool HasPreviousPage => PageNumber > 1;
        public bool HasNextPage => PageNumber < TotalPages;
    }

    /// <summary>
    /// Estadísticas de productos
    /// </summary>
    public class ProductosStats
    {
        public int TotalProductos { get; set; }
        public int ConStock { get; set; }
        public int SinStock { get; set; }
        public int StockBajo { get; set; }
        public decimal PrecioPromedio { get; set; }
        public decimal PrecioMinimo { get; set; }
        public decimal PrecioMaximo { get; set; }
        public int StockTotal { get; set; }
    }

    #endregion
}
