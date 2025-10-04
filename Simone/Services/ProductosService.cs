using Simone.Models;
using Simone.Data;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Simone.Services
{
    public class ProductosService
    {
        private readonly TiendaDbContext _context;

        public ProductosService(TiendaDbContext context)
        {
            _context = context;
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
            catch
            {
                return false;
            }
        }

        // ==================== READ ====================

        /// <summary>
        /// Todos los productos (para admin). Incluye Categoria, Subcategoria y Proveedor.
        /// </summary>
        public async Task<List<Producto>> GetAllAsync()
        {
            return await _context.Productos
                                 .AsNoTracking()
                                 .Include(p => p.Categoria)
                                 .Include(p => p.Subcategoria)
                                 .Include(p => p.Proveedor)
                                 .ToListAsync();
        }

        /// <summary>
        /// Primeros N productos (orden natural por PK). Solo lectura.
        /// </summary>
        public async Task<List<Producto>> GetFirstAsync(int n)
        {
            return await _context.Productos
                                 .AsNoTracking()
                                 .OrderBy(p => p.ProductoID)
                                 .Take(n)
                                 .Include(p => p.Categoria)
                                 .Include(p => p.Subcategoria)
                                 .Include(p => p.Proveedor)
                                 .ToListAsync();
        }

        /// <summary>
        /// Un producto por ID, con relaciones principales.
        /// </summary>
        public async Task<Producto> GetByIdAsync(int id)
        {
            return await _context.Productos
                                 .Include(p => p.Categoria)
                                 .Include(p => p.Subcategoria)
                                 .Include(p => p.Proveedor)
                                 .FirstOrDefaultAsync(p => p.ProductoID == id);
        }

        /// <summary>
        /// Por categoría (cualquier vendedor). Solo lectura.
        /// </summary>
        public async Task<List<Producto>> GetByCategoryID(int categoriaID)
        {
            return await _context.Productos
                                 .AsNoTracking()
                                 .Where(p => p.CategoriaID == categoriaID)
                                 .Include(p => p.Categoria)
                                 .Include(p => p.Subcategoria)
                                 .Include(p => p.Proveedor)
                                 .ToListAsync();
        }

        /// <summary>
        /// Por vendedor (solo sus productos). Solo lectura.
        /// </summary>
        public async Task<List<Producto>> GetByVendedorID(string vendedorID)
        {
            return await _context.Productos
                                 .AsNoTracking()
                                 .Where(p => p.VendedorID == vendedorID)
                                 .Include(p => p.Categoria)
                                 .Include(p => p.Subcategoria)
                                 .Include(p => p.Proveedor)
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
            catch
            {
                return false;
            }
        }

        // ==================== DELETE ====================
        public async Task<bool> DeleteAsync(int id)
        {
            try
            {
                var producto = await _context.Productos.FindAsync(id);
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
                    return false;
                }

                _context.Productos.Remove(producto);
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
