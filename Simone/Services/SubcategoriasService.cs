using Simone.Models;
using Simone.Data;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Simone.Services
{
    public class SubcategoriasService
    {
        private readonly TiendaDbContext _context;

        public SubcategoriasService(TiendaDbContext context)
        {
            _context = context;
        }

        // Agregar una nueva categoría de manera asíncrona
        public async Task<bool> AddAsync(Subcategorias subcategorias)
        {
            try
            {
                await _context.Subcategorias.AddAsync(subcategorias); // Usamos AddAsync
                await _context.SaveChangesAsync(); // Guardar los cambios de manera asíncrona
                return true; // Retorna true si la categoría se ha agregado correctamente
            }
            catch
            {
                return false; // Retorna false si hubo algún error
            }
        }

        // Obtener todas las categorías de manera asíncrona
        public async Task<List<Subcategorias>> GetAllAsync()
        {
            return await _context.Subcategorias
            .Select(s => new Subcategorias
            {
                SubcategoriaID = s.SubcategoriaID,
                NombreSubcategoria = s.NombreSubcategoria,
                CategoriaID = s.Categoria.CategoriaID,
            })
            .ToListAsync(); // Usamos ToListAsync
        }

        // Obtener una categoría por su ID de manera asíncrona
        public async Task<Subcategorias> GetByIdAsync(int id)
        {
            return await _context.Subcategorias.FindAsync(id); // Usamos FindAsync
        }

        // Actualizar una categoría de manera asíncrona
        public async Task<bool> UpdateAsync(Subcategorias subcategorias)
        {
            try
            {
                _context.Subcategorias.Update(subcategorias); // Actualizar categoría
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
                var subcategorias = await _context.Subcategorias.FindAsync(id); // Buscar la categoría de manera asíncrona
                if (subcategorias != null)
                {
                    _context.Subcategorias.Remove(subcategorias); // Eliminar categoría
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

        public async Task<List<Subcategorias>> GetAllSubcategoriasWithCategoriaAsync()
        {
            // Use Include to load the related Categoria
            return await _context.Subcategorias
                                .Include(s => s.Categoria) // Include related Categoria entity
                                .Select(s => new Subcategorias
                                {
                                    SubcategoriaID = s.SubcategoriaID,
                                    NombreSubcategoria = s.NombreSubcategoria,
                                    Categoria = new Categorias
                                    {
                                        CategoriaID = s.Categoria.CategoriaID,
                                        Nombre = s.Categoria.Nombre
                                    }
                                })
                                .ToListAsync(); // Asynchronously get the list
        }

    }
}
