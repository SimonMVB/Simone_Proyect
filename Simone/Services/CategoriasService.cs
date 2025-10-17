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
    public class CategoriasService
    {
        private readonly TiendaDbContext _context;
        private readonly ILogger<CategoriasService> _logger;

        public CategoriasService(TiendaDbContext context, ILogger<CategoriasService> logger)
        {
            _context = context;
            _logger = logger;
        }

        // --- Utils ---
        private static string NormalizeName(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            var trimmed = s.Trim();
            // Colapsa espacios múltiples en uno solo
            return Regex.Replace(trimmed, @"\s+", " ");
        }

        // ==================== CREATE ====================
        public async Task<bool> AddAsync(Categorias categoria, CancellationToken ct = default)
        {
            try
            {
                if (categoria == null) return false;

                categoria.Nombre = NormalizeName(categoria.Nombre);
                if (string.IsNullOrWhiteSpace(categoria.Nombre)) return false;

                // Evita duplicados por nombre (confía en la collation de la BD para case-insensitive)
                bool exists = await _context.Categorias
                    .AnyAsync(c => c.Nombre == categoria.Nombre, ct);

                if (exists) return false;

                await _context.Categorias.AddAsync(categoria, ct);
                await _context.SaveChangesAsync(ct);
                return true;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error de BD al crear categoría {@Categoria}", categoria);
                return false;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al crear categoría {@Categoria}", categoria);
                return false;
            }
        }

        // ==================== READ ====================

        public async Task<List<Categorias>> GetAllAsync(CancellationToken ct = default)
        {
            return await _context.Categorias
                .AsNoTracking()
                .OrderBy(c => c.Nombre)
                .Select(c => new Categorias
                {
                    CategoriaID = c.CategoriaID,
                    Nombre = c.Nombre
                })
                .ToListAsync(ct);
        }

        public async Task<Categorias?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            // tracked (útil para edición)
            return await _context.Categorias.FindAsync(new object[] { id }, ct);
        }

        public async Task<Categorias?> GetByIdReadOnlyAsync(int id, CancellationToken ct = default)
        {
            // solo lectura
            return await _context.Categorias
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CategoriaID == id, ct);
        }

        // ==================== UPDATE ====================
        public async Task<bool> UpdateAsync(Categorias categoria, CancellationToken ct = default)
        {
            try
            {
                if (categoria == null) return false;

                var existing = await _context.Categorias
                    .FirstOrDefaultAsync(c => c.CategoriaID == categoria.CategoriaID, ct);

                if (existing == null) return false;

                var nuevoNombre = NormalizeName(categoria.Nombre);
                if (string.IsNullOrWhiteSpace(nuevoNombre)) return false;

                // Evita duplicados con otros IDs
                bool dup = await _context.Categorias
                    .AnyAsync(c => c.CategoriaID != categoria.CategoriaID && c.Nombre == nuevoNombre, ct);

                if (dup) return false;

                existing.Nombre = nuevoNombre;

                await _context.SaveChangesAsync(ct);
                return true;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error de BD al actualizar categoría {CategoriaID}", categoria?.CategoriaID);
                return false;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al actualizar categoría {CategoriaID}", categoria?.CategoriaID);
                return false;
            }
        }

        // ==================== DELETE ====================
        public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
        {
            try
            {
                var categoria = await _context.Categorias
                    .FirstOrDefaultAsync(c => c.CategoriaID == id, ct);

                if (categoria == null) return false;

                _context.Categorias.Remove(categoria);
                await _context.SaveChangesAsync(ct);
                return true;
            }
            catch (DbUpdateException ex)
            {
                // Suele ocurrir si hay Subcategorias/Productos asociados (FK Restrict)
                _logger.LogWarning(ex, "No se pudo eliminar categoría {CategoriaID} por dependencias.", id);
                return false;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al eliminar categoría {CategoriaID}", id);
                return false;
            }
        }

        // ==================== Extras útiles (opcionales) ====================
        public async Task<bool> ExistsByNameAsync(string nombre, CancellationToken ct = default)
        {
            nombre = NormalizeName(nombre);
            if (string.IsNullOrWhiteSpace(nombre)) return false;

            return await _context.Categorias
                .AsNoTracking()
                .AnyAsync(c => c.Nombre == nombre, ct);
        }
    }
}
