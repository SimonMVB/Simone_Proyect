using Simone.Models;
using Simone.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Simone.Services
{
    public class ProductosService
    {
        private readonly TiendaDbContext _context;
        private readonly ILogger<ProductosService> _logger;

        public ProductosService(TiendaDbContext context, ILogger<ProductosService> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ===== Helpers de consulta =====
        /// <summary>
        /// Crea una consulta base con relaciones y configuración de tracking.
        /// </summary>
        private IQueryable<Producto> BaseQuery(bool withVariantes = true, bool asTracking = false)
        {
            IQueryable<Producto> q = _context.Productos
                .Include(p => p.Categoria)
                .Include(p => p.Subcategoria)
                .Include(p => p.Proveedor);

            if (withVariantes)
                q = q.Include(p => p.Variantes);

            q = q.AsSplitQuery(); // Evita "cartesian explosion" con múltiples includes

            return asTracking ? q : q.AsNoTracking();
        }

        // ==================== CREATE ====================
        public async Task<bool> AddAsync(Producto producto, CancellationToken ct = default)
        {
            try
            {
                if (producto == null) throw new ArgumentNullException(nameof(producto));

                await _context.Productos.AddAsync(producto, ct);
                await _context.SaveChangesAsync(ct);
                return true;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error de base de datos al crear Producto {@Producto}", producto);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado en AddAsync de Producto");
                return false;
            }
        }

        // ==================== READ ====================

        /// <summary>
        /// Todos los productos (para admin). Incluye Categoria, Subcategoria, Proveedor y Variantes.
        /// Solo lectura (AsNoTracking).
        /// </summary>
        public async Task<List<Producto>> GetAllAsync(CancellationToken ct = default)
        {
            return await BaseQuery(withVariantes: true, asTracking: false)
                .OrderBy(p => p.Nombre)
                .ToListAsync(ct);
        }

        /// <summary>
        /// Primeros N productos, ordenados por PK.
        /// </summary>
        public async Task<List<Producto>> GetFirstAsync(int n, CancellationToken ct = default)
        {
            if (n <= 0) return new List<Producto>();
            return await BaseQuery(withVariantes: true, asTracking: false)
                .OrderBy(p => p.ProductoID)
                .Take(n)
                .ToListAsync(ct);
        }

        /// <summary>
        /// Un producto por ID (con tracking), incluye relaciones y Variantes.
        /// </summary>
        public async Task<Producto?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            if (id <= 0) return null;
            return await BaseQuery(withVariantes: true, asTracking: true)
                .FirstOrDefaultAsync(p => p.ProductoID == id, ct);
        }

        /// <summary>
        /// Un producto por ID (solo lectura), incluye relaciones y Variantes.
        /// </summary>
        public async Task<Producto?> GetByIdReadOnlyAsync(int id, CancellationToken ct = default)
        {
            if (id <= 0) return null;
            return await BaseQuery(withVariantes: true, asTracking: false)
                .FirstOrDefaultAsync(p => p.ProductoID == id, ct);
        }

        /// <summary>
        /// Productos por categoría (cualquier vendedor). Solo lectura.
        /// </summary>
        public async Task<List<Producto>> GetByCategoryID(int categoriaID, CancellationToken ct = default)
        {
            if (categoriaID <= 0) return new List<Producto>();
            return await BaseQuery(withVariantes: true, asTracking: false)
                .Where(p => p.CategoriaID == categoriaID)
                .OrderBy(p => p.Nombre)
                .ToListAsync(ct);
        }

        /// <summary>
        /// Productos por vendedor. Solo lectura.
        /// </summary>
        public async Task<List<Producto>> GetByVendedorID(string vendedorID, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(vendedorID)) return new List<Producto>();
            return await BaseQuery(withVariantes: true, asTracking: false)
                .Where(p => p.VendedorID == vendedorID)
                .OrderBy(p => p.Nombre)
                .ToListAsync(ct);
        }

        /// <summary>
        /// Búsqueda simple con paginación opcional. Devuelve (items, total).
        /// </summary>
        public async Task<(List<Producto> items, int total)> GetPagedAsync(
            string? vendedorID = null,
            int? categoriaID = null,
            string? texto = null,
            int page = 1,
            int pageSize = 20,
            CancellationToken ct = default)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 200);

            var q = BaseQuery(withVariantes: true, asTracking: false);

            if (!string.IsNullOrWhiteSpace(vendedorID))
                q = q.Where(p => p.VendedorID == vendedorID);

            if (categoriaID.HasValue && categoriaID.Value > 0)
                q = q.Where(p => p.CategoriaID == categoriaID.Value);

            if (!string.IsNullOrWhiteSpace(texto))
            {
                var t = texto.Trim().ToLower();
                q = q.Where(p =>
                    p.Nombre.ToLower().Contains(t) ||
                    (p.Marca != null && p.Marca.ToLower().Contains(t)) ||
                    (p.Descripcion != null && p.Descripcion.ToLower().Contains(t)));
            }

            var total = await q.CountAsync(ct);
            var items = await q.OrderBy(p => p.Nombre)
                               .Skip((page - 1) * pageSize)
                               .Take(pageSize)
                               .ToListAsync(ct);

            return (items, total);
        }

        // ==================== UPDATE ====================
        public async Task<bool> UpdateAsync(Producto producto, CancellationToken ct = default)
        {
            try
            {
                if (producto == null) throw new ArgumentNullException(nameof(producto));

                _context.Productos.Update(producto);
                await _context.SaveChangesAsync(ct);
                return true;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Conflicto de concurrencia al actualizar Producto {ProductoID}", producto?.ProductoID);
                return false;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error de base de datos al actualizar Producto {ProductoID}", producto?.ProductoID);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado en UpdateAsync de Producto {ProductoID}", producto?.ProductoID);
                return false;
            }
        }

        // ==================== DELETE ====================
        public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
        {
            if (id <= 0) return false;

            // Transacción: borrar variantes y luego el producto
            await using var trx = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                var producto = await _context.Productos
                    .Include(p => p.Variantes)
                    .FirstOrDefaultAsync(p => p.ProductoID == id, ct);

                if (producto == null) return false;

                // Dependencias duras que impedirían el borrado
                bool tieneDependencias =
                    await _context.DetalleVentas.AnyAsync(d => d.ProductoID == id, ct) ||
                    await _context.DetallesPedido.AnyAsync(d => d.ProductoID == id, ct) ||
                    await _context.CarritoDetalle.AnyAsync(d => d.ProductoID == id, ct) ||
                    await _context.Reseñas.AnyAsync(r => r.ProductoID == id, ct);

                if (tieneDependencias)
                {
                    // Si en el futuro implementas SoftDelete, este es el lugar
                    _logger.LogWarning("No se elimina Producto {ProductoID}: dependencias en ventas/pedidos/carrito/reseñas", id);
                    await trx.RollbackAsync(ct);
                    return false;
                }

                if (producto.Variantes != null && producto.Variantes.Count > 0)
                    _context.ProductoVariantes.RemoveRange(producto.Variantes);

                _context.Productos.Remove(producto);

                await _context.SaveChangesAsync(ct);
                await trx.CommitAsync(ct);
                return true;
            }
            catch (DbUpdateException ex)
            {
                await trx.RollbackAsync(ct);
                _logger.LogError(ex, "Error de base de datos al eliminar Producto {ProductoID}", id);
                return false;
            }
            catch (Exception ex)
            {
                await trx.RollbackAsync(ct);
                _logger.LogError(ex, "Error inesperado en DeleteAsync de Producto {ProductoID}", id);
                return false;
            }
        }

        // ==================== Utilitarios de dominio ====================

        /// <summary>
        /// Recalcula y guarda en el producto los agregados a partir de sus variantes:
        /// Stock total = suma de stock variantes; PrecioVenta = mínimo Var.PrecioVenta.
        /// No crea ni borra variantes, solo refresca los campos del producto base.
        /// </summary>
        public async Task<bool> RecomputeFromVariantsAsync(int productoID, CancellationToken ct = default)
        {
            try
            {
                var vars = await _context.ProductoVariantes
                    .AsNoTracking()
                    .Where(v => v.ProductoID == productoID)
                    .ToListAsync(ct);

                var producto = await _context.Productos.FirstOrDefaultAsync(p => p.ProductoID == productoID, ct);
                if (producto == null) return false;

                if (vars.Count == 0)
                {
                    // Sin variantes: no tocamos Talla/Color/PrecioVenta/Stock (caso simple)
                    return true;
                }

                // Stock total
                producto.Stock = vars.Sum(v => v.Stock);

                // PrecioVenta del producto = mínimo precio de variante (ignorando nulos)
                var precios = vars
                    .Select(v => v.PrecioVenta)   // decimal?
                    .Where(p => p.HasValue)       // filtra nulos
                    .Select(p => p!.Value);       // decimal

                decimal minPrice = precios.Any() ? precios.Min() : 0m;
                producto.PrecioVenta = minPrice;


                _context.Productos.Update(producto);
                await _context.SaveChangesAsync(ct);
                return true;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error de base de datos al recomputar agregados para Producto {ProductoID}", productoID);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al recomputar agregados desde variantes para Producto {ProductoID}", productoID);
                return false;
            }
        }

        /// <summary>
        /// Comprueba si un producto tiene dependencias que bloquean su eliminación.
        /// </summary>
        public async Task<bool> HasHardDependenciesAsync(int productoID, CancellationToken ct = default)
        {
            if (productoID <= 0) return false;

            return await _context.DetalleVentas.AnyAsync(d => d.ProductoID == productoID, ct)
                || await _context.DetallesPedido.AnyAsync(d => d.ProductoID == productoID, ct)
                || await _context.CarritoDetalle.AnyAsync(d => d.ProductoID == productoID, ct)
                || await _context.Reseñas.AnyAsync(r => r.ProductoID == productoID, ct);
        }
    }
}
