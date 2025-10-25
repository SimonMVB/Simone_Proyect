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

        // ============ CREATE ============
        /// <summary>
        /// Crea una subcategoría. Debe venir con VendedorID seteado desde el controlador.
        /// Enforce: NombreSubcategoria.Trim() y unicidad por (VendedorID, CategoriaID, NombreSubcategoria).
        /// </summary>
        public async Task<bool> AddAsync(Subcategorias subcategoria)
        {
            if (subcategoria == null) return false;

            subcategoria.NombreSubcategoria = (subcategoria.NombreSubcategoria ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(subcategoria.NombreSubcategoria)) return false;
            if (string.IsNullOrWhiteSpace(subcategoria.VendedorID)) return false;
            if (subcategoria.CategoriaID <= 0) return false;

            // Pre-chequeo de duplicado (evita exception por índice único)
            bool dup = await _context.Subcategorias
                .AsNoTracking()
                .AnyAsync(s =>
                    s.VendedorID == subcategoria.VendedorID &&
                    s.CategoriaID == subcategoria.CategoriaID &&
                    s.NombreSubcategoria == subcategoria.NombreSubcategoria);

            if (dup) return false;

            try
            {
                await _context.Subcategorias.AddAsync(subcategoria);
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false; // DB lanzará 2601/2627 si chocamos con el índice único
            }
        }

        // ============ READ ============
        /// <summary>
        /// Todas las subcategorías (sin filtro). Úsalo sólo para administración global.
        /// </summary>
        public async Task<List<Subcategorias>> GetAllAsync()
        {
            return await _context.Subcategorias
                .AsNoTracking()
                .Select(s => new Subcategorias
                {
                    SubcategoriaID = s.SubcategoriaID,
                    NombreSubcategoria = s.NombreSubcategoria,
                    CategoriaID = s.CategoriaID,
                    VendedorID = s.VendedorID
                })
                .ToListAsync();
        }

        /// <summary>
        /// Subcategorías del vendedor indicado.
        /// </summary>
        public async Task<List<Subcategorias>> GetAllByVendedorAsync(string vendedorId)
        {
            return await _context.Subcategorias
                .AsNoTracking()
                .Where(s => s.VendedorID == vendedorId)
                .OrderBy(s => s.CategoriaID)
                .ThenBy(s => s.NombreSubcategoria)
                .Select(s => new Subcategorias
                {
                    SubcategoriaID = s.SubcategoriaID,
                    NombreSubcategoria = s.NombreSubcategoria,
                    CategoriaID = s.CategoriaID,
                    VendedorID = s.VendedorID
                })
                .ToListAsync();
        }

        /// <summary>
        /// Subcategorías con la entidad Categoría cargada (sin filtro).
        /// </summary>
        public async Task<List<Subcategorias>> GetAllSubcategoriasWithCategoriaAsync()
        {
            return await _context.Subcategorias
                .AsNoTracking()
                .Include(s => s.Categoria)
                .Select(s => new Subcategorias
                {
                    SubcategoriaID = s.SubcategoriaID,
                    NombreSubcategoria = s.NombreSubcategoria,
                    CategoriaID = s.CategoriaID,
                    VendedorID = s.VendedorID,
                    Categoria = new Categorias
                    {
                        CategoriaID = s.Categoria.CategoriaID,
                        Nombre = s.Categoria.Nombre
                    }
                })
                .ToListAsync();
        }

        /// <summary>
        /// Subcategorías del vendedor con Categoría cargada.
        /// </summary>
        public async Task<List<Subcategorias>> GetAllByVendedorWithCategoriaAsync(string vendedorId)
        {
            return await _context.Subcategorias
                .AsNoTracking()
                .Where(s => s.VendedorID == vendedorId)
                .Include(s => s.Categoria)
                .OrderBy(s => s.CategoriaID)
                .ThenBy(s => s.NombreSubcategoria)
                .Select(s => new Subcategorias
                {
                    SubcategoriaID = s.SubcategoriaID,
                    NombreSubcategoria = s.NombreSubcategoria,
                    CategoriaID = s.CategoriaID,
                    VendedorID = s.VendedorID,
                    Categoria = new Categorias
                    {
                        CategoriaID = s.Categoria.CategoriaID,
                        Nombre = s.Categoria.Nombre
                    }
                })
                .ToListAsync();
        }

        /// <summary>
        /// Obtiene una subcategoría por ID (seguirá trackeada).
        /// </summary>
        public async Task<Subcategorias> GetByIdAsync(int id)
        {
            return await _context.Subcategorias.FindAsync(id);
        }

        /// <summary>
        /// Obtiene una subcategoría por ID sólo si pertenece al vendedor.
        /// </summary>
        public async Task<Subcategorias> GetByIdForVendedorAsync(int id, string vendedorId)
        {
            return await _context.Subcategorias
                .FirstOrDefaultAsync(s => s.SubcategoriaID == id && s.VendedorID == vendedorId);
        }

        /// <summary>
        /// Subcategorías por categoría (sin filtro de vendedor).
        /// </summary>
        public async Task<List<Subcategorias>> GetByCategoriaIdAsync(int categoriaID)
        {
            return await _context.Subcategorias
                .AsNoTracking()
                .Where(s => s.CategoriaID == categoriaID)
                .ToListAsync();
        }

        /// <summary>
        /// Subcategorías por categoría y vendedor.
        /// </summary>
        public async Task<List<Subcategorias>> GetByCategoriaIdForVendedorAsync(int categoriaID, string vendedorId)
        {
            return await _context.Subcategorias
                .AsNoTracking()
                .Where(s => s.CategoriaID == categoriaID && s.VendedorID == vendedorId)
                .OrderBy(s => s.NombreSubcategoria)
                .ToListAsync();
        }

        // ============ UPDATE ============
        /// <summary>
        /// Actualiza una subcategoría ya trackeada o adjunta el modelo y marca Modified.
        /// No permite cambiar VendedorID.
        /// </summary>
        public async Task<bool> UpdateAsync(Subcategorias subcategoria)
        {
            if (subcategoria == null) return false;

            subcategoria.NombreSubcategoria = (subcategoria.NombreSubcategoria ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(subcategoria.NombreSubcategoria)) return false;
            if (subcategoria.CategoriaID <= 0) return false;

            try
            {
                // Asegurar que no se intente cambiar el VendedorID por accidente:
                var original = await _context.Subcategorias
                    .AsNoTracking()
                    .Where(s => s.SubcategoriaID == subcategoria.SubcategoriaID)
                    .Select(s => new { s.VendedorID })
                    .FirstOrDefaultAsync();

                if (original == null) return false;
                subcategoria.VendedorID = original.VendedorID;

                _context.Subcategorias.Update(subcategoria);
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        // ============ DELETE ============
        /// <summary>
        /// Elimina por ID (sin verificar dueño). Usa EliminarForVendedorAsync si necesitas verificar propietario.
        /// </summary>
        public async Task<bool> DeleteAsync(int id)
        {
            try
            {
                var sub = await _context.Subcategorias.FindAsync(id);
                if (sub == null) return false;

                _context.Subcategorias.Remove(sub);
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Elimina por ID sólo si pertenece al vendedor.
        /// </summary>
        public async Task<bool> DeleteForVendedorAsync(int id, string vendedorId)
        {
            try
            {
                var sub = await _context.Subcategorias
                    .FirstOrDefaultAsync(s => s.SubcategoriaID == id && s.VendedorID == vendedorId);
                if (sub == null) return false;

                _context.Subcategorias.Remove(sub);
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
