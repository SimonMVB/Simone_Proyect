using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Simone.Data;

namespace Simone.ViewModels.Pagos
{
    public sealed record PagosDecision(
        bool EsMultiVendedor,
        string? VendedorIdUnico,
        IReadOnlyList<string> VendedoresIds
    );

    /// <summary>
    /// Detecta vendedores usando el UsuarioId (GUID) del Proveedor dueño del producto.
    /// </summary>
    public sealed class PagosResolver
    {
        private readonly TiendaDbContext _ctx;
        public PagosResolver(TiendaDbContext ctx) => _ctx = ctx;

        public async Task<PagosDecision> ResolverAsync(string usuarioId, CancellationToken ct = default)
        {
            var carrito = await _ctx.Carrito
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.UsuarioId == usuarioId, ct);

            if (carrito == null)
                return new PagosDecision(false, null, Array.Empty<string>());

            // Clave: traer el GUID del vendedor: Producto -> Proveedor -> UsuarioId
            // Tomamos el VendedorID (string) de cada producto en el carrito
            var vendedoresIds = await _ctx.CarritoDetalle
                .AsNoTracking()
                .Where(d => d.CarritoID == carrito.CarritoID)
                .Include(d => d.Producto) // nav a Producto
                .Select(d => d.Producto != null ? d.Producto.VendedorID : null)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct()
                .ToListAsync(ct);

            var esMulti = vendedoresIds.Count > 1;
            var unico = vendedoresIds.Count == 1 ? vendedoresIds[0] : null;

            return new PagosDecision(esMulti, unico, vendedoresIds);

        }
    }
}
