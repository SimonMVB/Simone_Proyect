using Simone.Models;
using Simone.Data;
using System.Collections.Generic;
using System.Linq;
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
        // Base con relaciones típicas del dominio
        // Base con relaciones
        private IQueryable<Producto> BaseQuery(bool withVariantes = true)
        {
            IQueryable<Producto> q = _context.Productos
                .Include(p => p.Categoria)
                .Include(p => p.Subcategoria)
                .Include(p => p.Proveedor);

            if (withVariantes)
                q = q.Include(p => p.Variantes);

            // Evita cartesian explosion con múltiples Includes
            return q.AsSplitQuery();
        }

        // ==================== CREATE ====================
        public async Task<bool> AddAsync(Producto producto)
        {
            try
            {
                await _context.Productos.AddAsync(producto);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error en AddAsync de Producto");
                return false;
            }
        }

        // ==================== READ ====================

        /// <summary>
        /// Todos los productos (para admin). Incluye Categoria, Subcategoria, Proveedor y Variantes.
        /// Solo lectura (AsNoTracking).
        /// </summary>
        public async Task<List<Producto>> GetAllAsync()
        {
            return await BaseQuery(withVariantes: true)
                .AsNoTracking()
                .ToListAsync();
        }

        /// <summary>
        /// Primeros N productos (orden natural por PK). Solo lectura (AsNoTracking).
        /// Incluye Categoria, Subcategoria, Proveedor y Variantes.
        /// </summary>
        public async Task<List<Producto>> GetFirstAsync(int n)
        {
            return await BaseQuery(withVariantes: true)
                .AsNoTracking()
                .OrderBy(p => p.ProductoID)
                .Take(n)
                .ToListAsync();
        }

        /// <summary>
        /// Un producto por ID, con relaciones principales y Variantes (con tracking).
        /// </summary>
        public async Task<Producto> GetByIdAsync(int id)
        {
            return await BaseQuery(withVariantes: true)
                .FirstOrDefaultAsync(p => p.ProductoID == id);
        }

        /// <summary>
        /// Un producto por ID en solo-lectura (AsNoTracking), incluye relaciones y Variantes.
        /// Útil para vistas/detalles sin edición.
        /// </summary>
        public async Task<Producto> GetByIdReadOnlyAsync(int id)
        {
            return await BaseQuery(withVariantes: true)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.ProductoID == id);
        }

        /// <summary>
        /// Por categoría (cualquier vendedor). Solo lectura.
        /// Incluye Categoria, Subcategoria, Proveedor y Variantes.
        /// </summary>
        public async Task<List<Producto>> GetByCategoryID(int categoriaID)
        {
            return await BaseQuery(withVariantes: true)
                .AsNoTracking()
                .Where(p => p.CategoriaID == categoriaID)
                .ToListAsync();
        }

        /// <summary>
        /// Por vendedor (solo sus productos). Solo lectura.
        /// Incluye Categoria, Subcategoria, Proveedor y Variantes.
        /// </summary>
        public async Task<List<Producto>> GetByVendedorID(string vendedorID)
        {
            return await BaseQuery(withVariantes: true)
                .AsNoTracking()
                .Where(p => p.VendedorID == vendedorID)
                .ToListAsync();
        }

        // ==================== UPDATE ====================
        public async Task<bool> UpdateAsync(Producto producto)
        {
            try
            {
                _context.Productos.Update(producto);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error en UpdateAsync de Producto {ProductoID}", producto?.ProductoID);
                return false;
            }
        }

        // ==================== DELETE ====================
        public async Task<bool> DeleteAsync(int id)
        {
            try
            {
                var producto = await _context.Productos
                    .Include(p => p.Variantes)
                    .FirstOrDefaultAsync(p => p.ProductoID == id);

                if (producto == null) return false;

                // Dependencias duras que impedirían el borrado
                bool tieneDependencias =
                    await _context.DetalleVentas.AnyAsync(d => d.ProductoID == id) ||
                    await _context.DetallesPedido.AnyAsync(d => d.ProductoID == id) ||
                    await _context.CarritoDetalle.AnyAsync(d => d.ProductoID == id) ||
                    await _context.Reseñas.AnyAsync(r => r.ProductoID == id);

                if (tieneDependencias)
                {
                    // Si más adelante implementas SoftDelete, aquí sería el lugar.
                    _logger.LogWarning("No se elimina Producto {ProductoID} por dependencias en ventas/pedidos/carrito/reseñas", id);
                    return false;
                }

                // Si la FK de variantes no tiene cascade delete, aseguremos limpiar explícitamente
                if (producto.Variantes != null && producto.Variantes.Count > 0)
                {
                    _context.ProductoVariantes.RemoveRange(producto.Variantes);
                }

                _context.Productos.Remove(producto);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error en DeleteAsync de Producto {ProductoID}", id);
                return false;
            }
        }

        // ==================== Utilitarios opcionales de dominio ====================

        /// <summary>
        /// Recalcula y guarda en el producto los agregados a partir de sus variantes:
        /// Stock total = suma de stock variantes; PrecioVenta = mínimo Var.PrecioVenta.
        /// No crea ni borra variantes, solo refresca los campos del producto base.
        /// </summary>
        public async Task<bool> RecomputeFromVariantsAsync(int productoID)
        {
            try
            {
                var vars = await _context.ProductoVariantes
                    .AsNoTracking()
                    .Where(v => v.ProductoID == productoID)
                    .ToListAsync();

                var producto = await _context.Productos.FirstOrDefaultAsync(p => p.ProductoID == productoID);
                if (producto == null) return false;

                if (vars.Count == 0)
                {
                    // Sin variantes: no tocamos Talla/Color/PrecioVenta/Stock (caso simple)
                    return true;
                }

                producto.Stock = vars.Sum(v => v.Stock);
                // PrecioVenta del producto = mínimo precio de variante (consistente con el controlador)
                var minPrice = vars.Where(v => v.PrecioVenta.HasValue).Select(v => v.PrecioVenta.Value).DefaultIfEmpty(0m).Min();
                producto.PrecioVenta = minPrice;

                _context.Productos.Update(producto);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al recomputar agregados desde variantes para Producto {ProductoID}", productoID);
                return false;
            }
        }
    }
}
