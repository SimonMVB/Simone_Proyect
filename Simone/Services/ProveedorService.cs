// Services/ProveedorService.cs
using Simone.Models;
using Simone.Data;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Simone.Services
{
    public class ProveedorService
    {
        private readonly TiendaDbContext _context;
        private readonly ILogger<ProveedorService> _logger;

        public ProveedorService(TiendaDbContext context, ILogger<ProveedorService> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ==================== CREATE ====================
        public async Task<bool> AddAsync(Proveedores proveedor, CancellationToken ct = default)
        {
            if (proveedor == null) return false;

            proveedor.NombreProveedor = (proveedor.NombreProveedor ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(proveedor.NombreProveedor)) return false;

            try
            {
                await _context.Proveedores.AddAsync(proveedor, ct);
                await _context.SaveChangesAsync(ct);
                return true;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogWarning(ex, "Conflicto/BD al crear proveedor {@Proveedor}", proveedor);
                return false;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al crear proveedor {@Proveedor}", proveedor);
                return false;
            }
        }

        // ==================== READ ====================
        public async Task<List<Proveedores>> GetAllAsync(CancellationToken ct = default)
        {
            return await _context.Proveedores
                .AsNoTracking()
                .OrderBy(p => p.NombreProveedor)
                .ToListAsync(ct);
        }

        public async Task<Proveedores?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            // FindAsync usa la PK y es más eficiente
            return await _context.Proveedores.FindAsync(new object[] { id }, ct);
        }

        // ==================== UPDATE ====================
        public async Task<bool> UpdateAsync(Proveedores proveedor, CancellationToken ct = default)
        {
            if (proveedor == null) return false;

            proveedor.NombreProveedor = (proveedor.NombreProveedor ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(proveedor.NombreProveedor)) return false;

            try
            {
                _context.Proveedores.Update(proveedor);
                await _context.SaveChangesAsync(ct);
                return true;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogWarning(ex, "Conflicto/BD al actualizar proveedor {ProveedorID}", proveedor?.ProveedorID);
                return false;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al actualizar proveedor {ProveedorID}", proveedor?.ProveedorID);
                return false;
            }
        }

        // ==================== DELETE ====================
        public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
        {
            try
            {
                var proveedor = await _context.Proveedores.FindAsync(new object[] { id }, ct);
                if (proveedor == null) return false;

                // Evita borrar si hay productos que referencian este proveedor
                bool tieneProductos = await _context.Productos
                    .AsNoTracking()
                    .AnyAsync(p => p.ProveedorID == id, ct);

                if (tieneProductos)
                {
                    _logger.LogWarning("No se elimina Proveedor {ProveedorID}: tiene productos asociados.", id);
                    return false;
                }

                _context.Proveedores.Remove(proveedor);
                await _context.SaveChangesAsync(ct);
                return true;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogWarning(ex, "No se pudo eliminar proveedor {ProveedorID} por dependencias.", id);
                return false;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al eliminar proveedor {ProveedorID}", id);
                return false;
            }
        }
    }
}
