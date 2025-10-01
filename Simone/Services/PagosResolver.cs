using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Simone.Data;

namespace Simone.Services
{
    /// <summary>
    /// Resultado mínimo para decidir si mostrar cuentas del vendedor o del administrador.
    /// </summary>
    public sealed class CarritoVendedorInfo
    {
        public bool EsMultiVendedor { get; init; }
        public string? VendedorIdUnico { get; init; }
        public IReadOnlyList<string> VendedoresIds { get; init; } = Array.Empty<string>();
        public int TiendasCount => VendedoresIds?.Count ?? 0;
    }

    /// <summary>
    /// Resolver de lógica: ¿el carrito del usuario mezcla varias tiendas?
    /// Usa CarritoDetalle -> Producto para detectar los VendedorID involucrados.
    /// </summary>
    public sealed class PagosResolver
    {
        private readonly TiendaDbContext _db;

        public PagosResolver(TiendaDbContext db) => _db = db;

        /// <summary>
        /// Obtiene los VendedorID distintos presentes en el carrito del usuario.
        /// Si <paramref name="carritoId"/> es null, evalúa todos los carritos del usuario
        /// (con el esquema actual, normalmente será uno).
        /// </summary>
        /// <param name="usuarioId">Usuario propietario del carrito.</param>
        /// <param name="carritoId">Opcional, para restringir a un carrito concreto.</param>
        public async Task<CarritoVendedorInfo> ResolverAsync(
            string usuarioId,
            int? carritoId = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(usuarioId))
                throw new ArgumentException("usuarioId es requerido.", nameof(usuarioId));

            // Base: detalles del/los carritos del usuario (sin tracking).
            IQueryable<Models.CarritoDetalle> query = _db.CarritoDetalle
                .AsNoTracking()
                .Where(cd => cd.Carrito != null && cd.Carrito.UsuarioId == usuarioId);

            if (carritoId.HasValue)
                query = query.Where(cd => cd.CarritoID == carritoId.Value);

            // Filtra cantidades no positivas si existieran (defensivo).
            query = query.Where(cd => cd.Cantidad > 0);

            // Proyecta el VendedorID del producto.
            var vendedoresRaw = await query
                .Include(cd => cd.Producto)
                .Select(cd => cd.Producto!.VendedorID)
                .Where(v => v != null && v != "")
                .ToListAsync(ct);

            // Limpieza: trim y distinct estable.
            var vendedores = vendedoresRaw
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v!.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (vendedores.Count == 1)
            {
                return new CarritoVendedorInfo
                {
                    EsMultiVendedor = false,
                    VendedorIdUnico = vendedores[0],
                    VendedoresIds = vendedores
                };
            }

            return new CarritoVendedorInfo
            {
                EsMultiVendedor = vendedores.Count > 1,
                VendedorIdUnico = null,
                VendedoresIds = vendedores
            };
        }
    }
}
