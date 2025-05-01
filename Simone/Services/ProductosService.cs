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

        // Agregar una nueva producto de manera asíncrona
        public async Task<bool> AddAsync(Producto producto)
        {
            try
            {
                await _context.Productos.AddAsync(producto); // Usamos AddAsync
                await _context.SaveChangesAsync(); // Guardar los cambios de manera asíncrona
                return true; // Retorna true si la producto se ha agregado correctamente
            }
            catch
            {
                return false; // Retorna false si hubo algún error
            }
        }

        // Obtener todas las productos de manera asíncrona
        public async Task<List<Producto>> GetAllAsync()
        {
            return await _context.Productos.ToListAsync(); // Usamos ToListAsync
        }

        // Obtener varios productos de manera asíncrona
        public async Task<List<Producto>> GetFirstAsync()
        {
            return await _context.Productos.
            Take(50).
            ToListAsync(); // Usamos ToListAsync
        }

        // Obtener una producto por su ID de manera asíncrona
        public async Task<Producto> GetByIdAsync(int id)
        {
            return await _context.Productos.FindAsync(id); // Usamos FindAsync
        }

        // Actualizar una producto de manera asíncrona
        public async Task<bool> UpdateAsync(Producto producto)
        {
            try
            {
                _context.Productos.Update(producto); // Actualizar producto
                await _context.SaveChangesAsync(); // Guardar los cambios de manera asíncrona
                return true; // Retorna true si la producto se actualiza correctamente
            }
            catch
            {
                return false; // Retorna false si hubo algún error
            }
        }

        // Eliminar un producto de manera asíncrona
        public async Task<bool> DeleteAsync(int id)
        {
            try
            {
                var producto = await _context.Productos.FindAsync(id); // Buscar la producto de manera asíncrona
                if (producto != null)
                {
                    _context.Productos.Remove(producto); // Eliminar producto
                    await _context.SaveChangesAsync(); // Guardar cambios de manera asíncrona
                    return true; // Retorna true si el producto se elimina correctamente
                }
                return false; // Retorna false si no se encuentra el producto
            }
            catch
            {
                return false; // Retorna false si hubo algún error
            }
        }
    }
}
