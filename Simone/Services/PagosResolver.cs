using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Simone.Data;

namespace Simone.Services
{
    // Resultado para decidir si mostrar cuentas del vendedor o del admin.
    public sealed class CarritoVendedorInfo
    {
        public bool EsMultiVendedor { get; init; }
        public string? VendedorIdUnico { get; init; }
        public IReadOnlyList<string> VendedoresIds { get; init; } = Array.Empty<string>();
        public int TiendasCount => VendedoresIds?.Count ?? 0;
    }

    /// Resolver: ¿el carrito del usuario mezcla varias tiendas?
    public sealed class PagosResolver
    {
        private readonly TiendaDbContext _db;
        public PagosResolver(TiendaDbContext db) => _db = db;

        /// Obtiene los VendedorID distintos del carrito actual (o de todos si no se pasa carritoId).
        public async Task<CarritoVendedorInfo> ResolverAsync(
            string usuarioId,
            int? carritoId = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(usuarioId))
                throw new ArgumentException("usuarioId es requerido.", nameof(usuarioId));

            // Base: detalles de carritos del usuario
            IQueryable<Simone.Models.CarritoDetalle> query = _db.CarritoDetalle
                .AsNoTracking()
                .Where(cd => cd.Carrito != null && cd.Carrito.UsuarioId == usuarioId);

            if (carritoId.HasValue)
                query = query.Where(cd => cd.CarritoID == carritoId.Value);

            // Solo cantidades válidas
            query = query.Where(cd => cd.Cantidad > 0);

            // Proyección directa y normalización en memoria
            var vendedores = await query
                .Where(cd => cd.Producto != null && cd.Producto.VendedorID != null && cd.Producto.VendedorID != "")
                .Select(cd => cd.Producto!.VendedorID!) // seguro por el Where anterior
                .ToListAsync(ct);

            var vendedoresNorm = vendedores
                .Select(v => v.Trim())
                .Where(v => v.Length > 0)
                .Distinct(StringComparer.Ordinal) // sensible a mayúsculas/minúsculas
                .ToList();

            if (vendedoresNorm.Count == 1)
            {
                return new CarritoVendedorInfo
                {
                    EsMultiVendedor = false,
                    VendedorIdUnico = vendedoresNorm[0],
                    VendedoresIds = vendedoresNorm
                };
            }

            return new CarritoVendedorInfo
            {
                EsMultiVendedor = vendedoresNorm.Count > 1,
                VendedorIdUnico = null,
                VendedoresIds = vendedoresNorm
            };
        }
    }
}
