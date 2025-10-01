using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Simone.Data;

namespace Simone.ViewModels.Pagos
{
    // DTO de resultado
    public sealed record PagosDecision(
        bool EsMultiVendedor,
        string? VendedorIdUnico,
        IReadOnlyList<string> VendedoresIds
    );

    /// <summary>
    /// Determina si el carrito del usuario tiene productos de una o varias "tiendas".
    /// En este proyecto tomamos "tienda" = Proveedor del producto (Producto.ProveedorID).
    /// NO crea tablas ni columnas nuevas: usa únicamente lo que ya existe.
    /// </summary>
    public sealed class PagosResolver
    {
        private readonly TiendaDbContext _ctx;

        public PagosResolver(TiendaDbContext ctx) => _ctx = ctx;

        public async Task<PagosDecision> ResolverAsync(string usuarioId, CancellationToken ct = default)
        {
            // Carrito del usuario (si no hay, devolvemos mono-tienda vacío)
            var carrito = await _ctx.Carrito
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.UsuarioId == usuarioId, ct);

            if (carrito == null)
                return new PagosDecision(false, null, Array.Empty<string>());

            // Tomamos el ProveedorID de cada producto en el carrito
            var vendedores = await _ctx.CarritoDetalle
                .AsNoTracking()
                .Where(d => d.CarritoID == carrito.CarritoID)
                .Include(d => d.Producto)
                .Select(d => d.Producto != null ? d.Producto.ProveedorID : (int?)null)
                .Where(pid => pid.HasValue)
                .Select(pid => pid!.Value)
                .Distinct()
                .ToListAsync(ct);

            // Normalizamos a string (la vista espera string)
            var vendedoresIds = vendedores.Select(v => v.ToString()).ToList();
            var esMulti = vendedoresIds.Count > 1;
            var unico = vendedoresIds.Count == 1 ? vendedoresIds[0] : null;

            return new PagosDecision(esMulti, unico, vendedoresIds);
        }
    }
}
