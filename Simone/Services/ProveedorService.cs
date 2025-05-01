using Simone.Models;
using Simone.Data;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Simone.Services
{
    public class ProveedorService
    {
        private readonly TiendaDbContext _context;

        public ProveedorService(TiendaDbContext context)
        {
            _context = context;
        }

        // Agregar una nueva producto de manera asíncrona
        public async Task<bool> AddAsync(Proveedores proveedor)
        {
            try
            {
                await _context.Proveedores.AddAsync(proveedor); // Usamos AddAsync
                await _context.SaveChangesAsync(); // Guardar los cambios de manera asíncrona
                return true; // Retorna true si la producto se ha agregado correctamente
            }
            catch
            {
                return false; // Retorna false si hubo algún error
            }
        }

        // Obtener todas las productos de manera asíncrona
        public async Task<List<Proveedores>> GetAllAsync()
        {
            return await _context.Proveedores.ToListAsync(); // Usamos ToListAsync
        }

        // Obtener una producto por su ID de manera asíncrona
        public async Task<Proveedores> GetByIdAsync(int id)
        {
            return await _context.Proveedores.FindAsync(id); // Usamos FindAsync
        }

        // Actualizar una producto de manera asíncrona
        public async Task<bool> UpdateAsync(Proveedores producto)
        {
            try
            {
                _context.Proveedores.Update(producto); // Actualizar producto
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
                var producto = await _context.Proveedores.FindAsync(id); // Buscar la producto de manera asíncrona
                if (producto != null)
                {
                    _context.Proveedores.Remove(producto); // Eliminar producto
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
