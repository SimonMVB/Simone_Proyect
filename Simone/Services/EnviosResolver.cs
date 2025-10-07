using System;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Simone.Configuration;

namespace Simone.Services
{
    /// <summary>
    /// Resuelve la tarifa de envío para un vendedor dado un destino (Provincia / Ciudad).
    /// Prioridad: Ciudad > Provincia. Si no hay regla del vendedor, intenta las de Admin.
    /// </summary>
    public class EnviosResolver
    {
        private readonly IEnviosConfigService _envios;
        private readonly ILogger<EnviosResolver> _logger;

        public EnviosResolver(IEnviosConfigService envios, ILogger<EnviosResolver> logger)
        {
            _envios = envios ?? throw new ArgumentNullException(nameof(envios));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Devuelve la tarifa (USD) para el vendedor y destino indicado, o null si no hay regla.
        /// </summary>
        public async Task<decimal?> GetTarifaAsync(
            string vendedorId,
            string provincia,
            string? ciudad,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(vendedorId) || string.IsNullOrWhiteSpace(provincia))
                return null;

            var p = N(provincia);
            var c = N(ciudad);

            try
            {
                // Reglas del vendedor (solo activas)
                var reglasVend = (await _envios.GetByProveedorAsync(vendedorId, ct))?
                                    .Where(r => r.Activo).ToList()
                                 ?? new List<TarifaEnvioRegla>();

                // 1) Coincidencia por ciudad
                if (!string.IsNullOrEmpty(c))
                {
                    var rc = reglasVend.FirstOrDefault(r =>
                        N(r.Provincia) == p &&
                        !string.IsNullOrWhiteSpace(r.Ciudad) &&
                        N(r.Ciudad) == c
                    );
                    if (rc != null) return rc.Precio;
                }

                // 2) Coincidencia por provincia
                var rp = reglasVend.FirstOrDefault(r =>
                    N(r.Provincia) == p && string.IsNullOrWhiteSpace(r.Ciudad));
                if (rp != null) return rp.Precio;

                // 3) Fallback Admin (opcional)
                var reglasAdmin = (await _envios.GetAdminAsync(ct))?
                                    .Where(r => r.Activo).ToList()
                                 ?? new List<TarifaEnvioRegla>();

                if (!string.IsNullOrEmpty(c))
                {
                    var ac = reglasAdmin.FirstOrDefault(r =>
                        N(r.Provincia) == p &&
                        !string.IsNullOrWhiteSpace(r.Ciudad) &&
                        N(r.Ciudad) == c
                    );
                    if (ac != null) return ac.Precio;
                }

                var ap = reglasAdmin.FirstOrDefault(r =>
                    N(r.Provincia) == p && string.IsNullOrWhiteSpace(r.Ciudad));
                if (ap != null) return ap.Precio;

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error resolviendo tarifa: vendedor={V}, provincia={P}, ciudad={C}",
                    vendedorId, provincia, ciudad);
                return null;
            }
        }

        /// <summary>
        /// Alias por compatibilidad: tu código llama a GetCostoAsync.
        /// </summary>
        public Task<decimal?> GetCostoAsync(
            string vendedorId,
            string provincia,
            string? ciudad,
            CancellationToken ct = default)
            => GetTarifaAsync(vendedorId, provincia, ciudad, ct);

        // Normaliza (trim + lower + sin acentos) para comparar de forma robusta.
        private static string N(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            var t = s.Trim().ToLowerInvariant();
            var norm = t.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(norm.Length);
            foreach (var ch in norm)
            {
                var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (cat != UnicodeCategory.NonSpacingMark) sb.Append(ch);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
