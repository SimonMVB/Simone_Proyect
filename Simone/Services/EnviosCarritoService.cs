using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Simone.Models;

namespace Simone.Services
{
    /// <summary>
    /// Calcula el costo de envío de un carrito agrupando por VENDEDOR.
    /// Reglas:
    ///  - Se cobra como máximo UNA tarifa por vendedor (no por ítem).
    ///  - Prioridad de resolución: (Provincia+Ciudad) -> (solo Provincia).
    ///  - Si un vendedor no tiene tarifa configurada para el destino, se marca en mensajes.
    /// </summary>
    public class EnviosCarritoService
    {
        private readonly EnviosResolver _resolver;

        public EnviosCarritoService(EnviosResolver resolver)
        {
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        }

        /// <summary>
        /// Calcula el envío total y por vendedor.
        /// </summary>
        /// <param name="vendedorIds">Colección de IDs de vendedores presentes en el carrito (puede traer repetidos; se hace Distinct).</param>
        /// <param name="provincia">Provincia destino.</param>
        /// <param name="ciudad">Ciudad destino (opcional).</param>
        /// <param name="ct">Token cancelación.</param>
        public async Task<EnvioResultado> CalcularAsync(
            IEnumerable<string> vendedorIds,
            string? provincia,
            string? ciudad,
            CancellationToken ct = default)
        {
            var result = new EnvioResultado();
            if (vendedorIds == null) return result;

            var prov = (provincia ?? string.Empty).Trim();
            var city = string.IsNullOrWhiteSpace(ciudad) ? null : ciudad!.Trim();

            foreach (var vendId in vendedorIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct())
            {
                ct.ThrowIfCancellationRequested();

                var costo = await _resolver.GetCostoAsync(vendId, prov, city, ct)
                                           .ConfigureAwait(false);

                if (costo.HasValue && costo.Value >= 0m)
                {
                    result.PorVendedor[vendId] = costo.Value;
                    result.TotalEnvio += costo.Value;
                }
                else
                {
                    result.PorVendedor[vendId] = 0m;
                    result.Mensajes.Add($"El vendedor {vendId} no tiene tarifa configurada para {prov}{(city != null ? $" / {city}" : "")}.");
                }
            }

            return result;
        }

        /// <summary>
        /// Helper: usa la dirección del usuario (Perfil) para calcular.
        /// </summary>
        public Task<EnvioResultado> CalcularParaUsuarioAsync(
            IEnumerable<string> vendedorIds,
            Usuario? usuario,
            CancellationToken ct = default)
        {
            var prov = usuario?.Provincia;
            var city = usuario?.Ciudad;
            return CalcularAsync(vendedorIds, prov, city, ct);
        }
    }

    /// <summary>
    /// Resultado del cálculo de envío.
    /// </summary>
    public sealed class EnvioResultado
    {
        /// <summary>Total a cobrar por concepto de envío (suma de cada vendedor).</summary>
        public decimal TotalEnvio { get; set; } = 0m;

        /// <summary>Detalle por vendedorId -> costo aplicado.</summary>
        public Dictionary<string, decimal> PorVendedor { get; } = new(StringComparer.Ordinal);

        /// <summary>Mensajes informativos (p.ej., vendedores sin tarifa).</summary>
        public List<string> Mensajes { get; } = new();
    }
}
