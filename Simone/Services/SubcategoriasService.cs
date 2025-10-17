using Simone.Models;
using Simone.Data;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace Simone.Services
{
    public class SubcategoriasService
    {
        private readonly TiendaDbContext _context;
        private readonly ILogger<SubcategoriasService> _logger;

        public SubcategoriasService(TiendaDbContext context, ILogger<SubcategoriasService> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ---- Utils ----
        private static string NormalizeName(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            var trimmed = s.Trim();
            // Colapsa espacios múltiples
            return Regex.Replace(trimmed, @"\s+", " ");
        }

        // ============ CREATE ============
        /// <summary>
        /// Crea una subcategoría. Debe venir con VendedorID seteado desde el controlador.
        /// Enforce: NombreSubcategoria normalizado y unicidad por (VendedorID, CategoriaID, NombreSubcategoria).
        /// </summary>
        public async Task<bool> AddAsync(Subcategorias subcategoria, CancellationToken ct = default)
        {
            if (subcategoria == null) return false;

            subcategoria.NombreSubcategoria = NormalizeName(subcategoria.NombreSubcategoria);

            if (string.IsNullOrWhiteSpace(subcategoria.NombreSubcategoria)) return false;
            if (string.IsNullOrWhiteSpace(subcategoria.VendedorID)) return false;
            if (subcategoria.CategoriaID <= 0) return false;

            // Pre-chequeo de duplicado (coincide con índice único configurado en el DbContext)
            bool dup = await _context.Subcategorias
                .AsNoTracking()
                .AnyAsync(s =>
                    s.VendedorID == subcategoria.VendedorID &&
                    s.CategoriaID == subcategoria.CategoriaID &&
                    s.NombreSubcategoria == subcategoria.NombreSubcategoria, ct);

            if (dup) return false;

            try
            {
                await _context.Subcategorias.AddAsync(subcategoria, ct);
                await _context.SaveChangesAsync(ct);
                return true;
            }
            catch (DbUpdateException ex)
            {
                // 2601/2627 (índice único) u otros errores de BD
                _logger.LogWarning(ex, "No se pudo crear la subcategoría por conflicto/BD {@Subcat}", subcategoria);
                return false;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al crear subcategoría {@Subcat}", subcategoria);
                return false;
            }
        }

        // ============ READ ============
        /// <summary> Todas las subcategorías (solo para administración global). </summary>
        public async Task<List<Subcategorias>> GetAllAsync(CancellationToken ct = default)
        {
            return await _context.Subcategorias
                .AsNoTracking()
                .OrderBy(s => s.CategoriaID)
                .ThenBy(s => s.NombreSubcategoria)
                .Select(s => new Subcategorias
                {
                    SubcategoriaID = s.SubcategoriaID,
                    NombreSubcategoria = s.NombreSubcategoria,
                    CategoriaID = s.CategoriaID,
                    VendedorID = s.VendedorID
                })
                .ToListAsync(ct);
        }

        /// <summary> Subcategorías del vendedor. </summary>
        public async Task<List<Subcategorias>> GetAllByVendedorAsync(string vendedorId, CancellationToken ct = default)
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
                .ToListAsync(ct);
        }

        /// <summary> Subcategorías con la entidad Categoría cargada (sin filtro). </summary>
        public async Task<List<Subcategorias>> GetAllSubcategoriasWithCategoriaAsync(CancellationToken ct = default)
        {
            return await _context.Subcategorias
                .AsNoTracking()
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
                .AsSplitQuery()
                .ToListAsync(ct);
        }

        /// <summary> Subcategorías del vendedor con Categoría cargada. </summary>
        public async Task<List<Subcategorias>> GetAllByVendedorWithCategoriaAsync(string vendedorId, CancellationToken ct = default)
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
                .AsSplitQuery()
                .ToListAsync(ct);
        }

        /// <summary> Obtiene una subcategoría por ID (trackeada). </summary>
        public async Task<Subcategorias?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            return await _context.Subcategorias.FindAsync(new object[] { id }, ct);
        }

        /// <summary> Obtiene una subcategoría por ID sólo si pertenece al vendedor (trackeada). </summary>
        public async Task<Subcategorias?> GetByIdForVendedorAsync(int id, string vendedorId, CancellationToken ct = default)
        {
            return await _context.Subcategorias
                .FirstOrDefaultAsync(s => s.SubcategoriaID == id && s.VendedorID == vendedorId, ct);
        }

        /// <summary> Subcategorías por categoría (sin filtro de vendedor). </summary>
        public async Task<List<Subcategorias>> GetByCategoriaIdAsync(int categoriaID, CancellationToken ct = default)
        {
            return await _context.Subcategorias
                .AsNoTracking()
                .Where(s => s.CategoriaID == categoriaID)
                .OrderBy(s => s.NombreSubcategoria)
                .ToListAsync(ct);
        }

        /// <summary> Subcategorías por categoría y vendedor. </summary>
        public async Task<List<Subcategorias>> GetByCategoriaIdForVendedorAsync(int categoriaID, string vendedorId, CancellationToken ct = default)
        {
            return await _context.Subcategorias
                .AsNoTracking()
                .Where(s => s.CategoriaID == categoriaID && s.VendedorID == vendedorId)
                .OrderBy(s => s.NombreSubcategoria)
                .ToListAsync(ct);
        }

        // ============ UPDATE ============
        /// <summary>
        /// Actualiza una subcategoría. No permite cambiar VendedorID.
        /// Valida nombre normalizado y unicidad antes de guardar.
        /// </summary>
        public async Task<bool> UpdateAsync(Subcategorias subcategoria, CancellationToken ct = default)
        {
            if (subcategoria == null) return false;

            var nuevoNombre = NormalizeName(subcategoria.NombreSubcategoria);
            if (string.IsNullOrWhiteSpace(nuevoNombre)) return false;
            if (subcategoria.CategoriaID <= 0) return false;

            try
            {
                // Obtener dueño original y evitar cambios de VendedorID
                var original = await _context.Subcategorias
                    .AsNoTracking()
                    .Where(s => s.SubcategoriaID == subcategoria.SubcategoriaID)
                    .Select(s => new { s.VendedorID, s.CategoriaID })
                    .FirstOrDefaultAsync(ct);

                if (original == null) return false;

                // Enforce VendedorID original
                subcategoria.VendedorID = original.VendedorID;
                subcategoria.NombreSubcategoria = nuevoNombre;

                // Validar duplicados (con mismo vendedor y categoría)
                bool dup = await _context.Subcategorias
                    .AsNoTracking()
                    .AnyAsync(s =>
                        s.SubcategoriaID != subcategoria.SubcategoriaID &&
                        s.VendedorID == subcategoria.VendedorID &&
                        s.CategoriaID == subcategoria.CategoriaID &&
                        s.NombreSubcategoria == subcategoria.NombreSubcategoria, ct);

                if (dup) return false;

                _context.Subcategorias.Update(subcategoria);
                await _context.SaveChangesAsync(ct);
                return true;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogWarning(ex, "Conflicto/BD al actualizar subcategoría {SubcategoriaID}", subcategoria?.SubcategoriaID);
                return false;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al actualizar subcategoría {SubcategoriaID}", subcategoria?.SubcategoriaID);
                return false;
            }
        }

        // ============ DELETE ============
        /// <summary> Elimina por ID (sin verificar dueño). </summary>
        public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
        {
            try
            {
                var sub = await _context.Subcategorias.FindAsync(new object[] { id }, ct);
                if (sub == null) return false;

                _context.Subcategorias.Remove(sub);
                await _context.SaveChangesAsync(ct);
                return true;
            }
            catch (DbUpdateException ex)
            {
                // Probable FK con productos
                _logger.LogWarning(ex, "No se pudo eliminar subcategoría {SubcategoriaID} por dependencias.", id);
                return false;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al eliminar subcategoría {SubcategoriaID}", id);
                return false;
            }
        }

        /// <summary> Elimina por ID sólo si pertenece al vendedor. </summary>
        public async Task<bool> DeleteForVendedorAsync(int id, string vendedorId, CancellationToken ct = default)
        {
            try
            {
                var sub = await _context.Subcategorias
                    .FirstOrDefaultAsync(s => s.SubcategoriaID == id && s.VendedorID == vendedorId, ct);
                if (sub == null) return false;

                _context.Subcategorias.Remove(sub);
                await _context.SaveChangesAsync(ct);
                return true;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogWarning(ex, "No se pudo eliminar subcategoría {SubcategoriaID} (owner-check) por dependencias.", id);
                return false;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al eliminar subcategoría {SubcategoriaID} (owner-check)", id);
                return false;
            }
        }

        // ============ Extras útiles ============
        public async Task<bool> ExistsForVendorAsync(string vendedorId, int categoriaId, string nombreSubcategoria, CancellationToken ct = default)
        {
            nombreSubcategoria = NormalizeName(nombreSubcategoria);
            if (string.IsNullOrWhiteSpace(vendedorId) || string.IsNullOrWhiteSpace(nombreSubcategoria) || categoriaId <= 0)
                return false;

            return await _context.Subcategorias
                .AsNoTracking()
                .AnyAsync(s =>
                    s.VendedorID == vendedorId &&
                    s.CategoriaID == categoriaId &&
                    s.NombreSubcategoria == nombreSubcategoria, ct);
        }
    }
}
