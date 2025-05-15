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

        // Agregar una nueva proveedor de manera asíncrona
        public async Task<bool> AddAsync(Proveedores proveedor)
        {
            try
            {
                await _context.Proveedores.AddAsync(proveedor); // Usamos AddAsync
                await _context.SaveChangesAsync(); // Guardar los cambios de manera asíncrona
                return true; // Retorna true si el proveedor se ha agregado correctamente
            }
            catch
            {
                return false; // Retorna false si hubo algún error
            }
        }

        // Obtener todos los proveedores de manera asíncrona
        public async Task<List<Proveedores>> GetAllAsync()
        {
            return await _context.Proveedores.ToListAsync(); // Usamos ToListAsync
        }

        // Obtener una proveedor por su ID de manera asíncrona
        public async Task<Proveedores> GetByIdAsync(int id)
        {
            return await _context.Proveedores.FindAsync(id); // Usamos FindAsync
        }

        // Actualizar una proveedor de manera asíncrona
        public async Task<bool> UpdateAsync(Proveedores proveedores)
        {
            try
            {
                _context.Proveedores.Update(proveedores); // Actualizar proveedor
                await _context.SaveChangesAsync(); // Guardar los cambios de manera asíncrona
                return true; // Retorna true si el proveedor se actualiza correctamente
            }
            catch
            {
                return false; // Retorna false si hubo algún error
            }
        }

        // Eliminar un proveedor de manera asíncrona
        public async Task<bool> DeleteAsync(int id)
        {
            try
            {
                var proveedor = await _context.Proveedores.FindAsync(id); // Buscar el proveedor de manera asíncrona
                if (proveedor != null)
                {
                    _context.Proveedores.Remove(proveedor); // Eliminar proveedor
                    await _context.SaveChangesAsync(); // Guardar cambios de manera asíncrona
                    return true; // Retorna true si el proveedor se elimina correctamente
                }
                return false; // Retorna false si no se encuentra el proveedor
            }
            catch
            {
                return false; // Retorna false si hubo algún error
            }
        }
    }
}
