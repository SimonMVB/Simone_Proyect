using Simone.Models;
using Simone.Data;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Simone.Services
{
    public class CategoriasService
    {
        private readonly TiendaDbContext _context;

        public CategoriasService(TiendaDbContext context)
        {
            _context = context;
        }

        // Agregar una nueva categoría de manera asíncrona
        public async Task<bool> AddAsync(Categorias categoria)
        {
            try
            {
                await _context.Categorias.AddAsync(categoria); // Usamos AddAsync
                await _context.SaveChangesAsync(); // Guardar los cambios de manera asíncrona
                return true; // Retorna true si la categoría se ha agregado correctamente
            }
            catch
            {
                return false; // Retorna false si hubo algún error
            }
        }

        // Obtener todas las categorías de manera asíncrona
        public async Task<List<Categorias>> GetAllAsync()
        {
            return await _context.Categorias.ToListAsync(); // Usamos ToListAsync
        }

        // Obtener una categoría por su ID de manera asíncrona
        public async Task<Categorias> GetByIdAsync(int id)
        {
            return await _context.Categorias.FindAsync(id); // Usamos FindAsync
        }

        // Actualizar una categoría de manera asíncrona
        public async Task<bool> UpdateAsync(Categorias categoria)
        {
            try
            {
                _context.Categorias.Update(categoria); // Actualizar categoría
                await _context.SaveChangesAsync(); // Guardar los cambios de manera asíncrona
                return true; // Retorna true si la categoría se actualiza correctamente
            }
            catch
            {
                return false; // Retorna false si hubo algún error
            }
        }

        // Eliminar una categoría de manera asíncrona
        public async Task<bool> DeleteAsync(int id)
        {
            try
            {
                var categoria = await _context.Categorias.FindAsync(id); // Buscar la categoría de manera asíncrona
                if (categoria != null)
                {
                    _context.Categorias.Remove(categoria); // Eliminar categoría
                    await _context.SaveChangesAsync(); // Guardar cambios de manera asíncrona
                    return true; // Retorna true si la categoría se elimina correctamente
                }
                return false; // Retorna false si no se encuentra la categoría
            }
            catch
            {
                return false; // Retorna false si hubo algún error
            }
        }
    }
}
