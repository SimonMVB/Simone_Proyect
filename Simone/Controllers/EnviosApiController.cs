using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Simone.Models;
using Simone.Services;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Simone.Controllers
{
    /// <summary>
    /// API para estimar costos de envío del carrito actual,
    /// basado en las reglas por vendedor definidas en /Vendedor/Envios.
    /// No modifica DB; solo calcula y devuelve JSON.
    /// </summary>
    [Authorize(Roles = "Administrador,Vendedor,Cliente")]
    [Route("api/envios")]
    [ApiController]
    public class EnviosApiController : ControllerBase
    {
        private readonly ILogger<EnviosApiController> _logger;
        private readonly UserManager<Usuario> _userManager;
        private readonly IEnviosConfigService _envios;

        public EnviosApiController(
            ILogger<EnviosApiController> logger,
            UserManager<Usuario> userManager,
            IEnviosConfigService envios)
        {
            _logger = logger;
            _userManager = userManager;
            _envios = envios;
        }

        private const string SessionCartKey = "Carrito";

        private sealed class SessionCartItem
        {
            public int ProductoID { get; set; }
            public int Cantidad { get; set; }
            public decimal PrecioUnitario { get; set; }
            public string VendedorID { get; set; } = string.Empty;
        }

        private static string K(string? s) => (s ?? string.Empty).Trim().ToLowerInvariant();

        private List<SessionCartItem> ReadCartFromSession()
        {
            try
            {
                var raw = HttpContext.Session.GetString(SessionCartKey);
                if (string.IsNullOrWhiteSpace(raw)) return new List<SessionCartItem>();
                var list = JsonSerializer.Deserialize<List<SessionCartItem>>(raw) ?? new List<SessionCartItem>();
                return list;
            }
            catch
            {
                return new List<SessionCartItem>();
            }
        }

        private async Task<(string? Provincia, string? Ciudad)> GetBuyerLocationAsync()
        {
            var uid = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(uid)) return (null, null);
            var u = await _userManager.FindByIdAsync(uid);
            return (u?.Provincia, u?.Ciudad);
        }

        private async Task<decimal> ResolverTarifaAsync(string vendedorId, string? prov, string? city)
        {
            if (string.IsNullOrWhiteSpace(vendedorId) || string.IsNullOrWhiteSpace(prov))
                return 0m;

            var reglas = await _envios.GetByProveedorAsync(vendedorId);

            // 1) Regla específica por ciudad (si existe y activa)
            var ciudadMatch = reglas.FirstOrDefault(r =>
                r.Activo &&
                K(r.Provincia) == K(prov) &&
                !string.IsNullOrWhiteSpace(r.Ciudad) &&
                K(r.Ciudad) == K(city));

            if (ciudadMatch != null) return ciudadMatch.Precio;

            // 2) Regla por provincia (Ciudad vacía)
            var provMatch = reglas.FirstOrDefault(r =>
                r.Activo &&
                K(r.Provincia) == K(prov) &&
                string.IsNullOrWhiteSpace(r.Ciudad));

            if (provMatch != null) return provMatch.Precio;

            // 3) Sin regla => 0 (default)
            return 0m;
        }

        [HttpGet("estimar")]
        public async Task<IActionResult> Estimar()
        {
            var (prov, city) = await GetBuyerLocationAsync();
            if (string.IsNullOrWhiteSpace(prov))
                return Ok(new { totalEnvio = 0m, detalle = new object[0], warning = "El usuario no tiene provincia configurada en su perfil." });

            var cart = ReadCartFromSession();
            if (cart.Count == 0)
                return Ok(new { totalEnvio = 0m, detalle = new object[0] });

            var porVendor = cart
                .GroupBy(i => i.VendedorID)
                .Select(g => new { VendedorID = g.Key, ItemCount = g.Sum(x => x.Cantidad) })
                .ToList();

            var detalle = new List<object>();
            decimal total = 0m;

            foreach (var g in porVendor)
            {
                var tarifa = await ResolverTarifaAsync(g.VendedorID, prov, city);
                total += tarifa;
                detalle.Add(new
                {
                    vendedorId = g.VendedorID,
                    provincia = prov,
                    ciudad = city,
                    precio = tarifa,
                    items = g.ItemCount
                });
            }

            return Ok(new
            {
                totalEnvio = total,
                detalle
            });
        }
    }
}
