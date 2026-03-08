using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Simone.Services;

namespace Simone.Controllers
{
    [Authorize(Roles = "Administrador")]
    [Route("FinanzasAdmin")]
    public class FinanzasAdminController : Controller
    {
        private readonly FinanzasAdminService _svc;
        private readonly ILogger<FinanzasAdminController> _logger;

        public FinanzasAdminController(
            FinanzasAdminService svc,
            ILogger<FinanzasAdminController> logger)
        {
            _svc = svc;
            _logger = logger;
        }

        // ── Helpers de fecha ─────────────────────────────────────────────────
        private static (DateTime Desde, DateTime Hasta) ParseFechas(
            string? desde, string? hasta, string? periodo)
        {
            // Si viene rango custom
            if (DateTime.TryParse(desde, out var d) && DateTime.TryParse(hasta, out var h))
                return (d.ToUniversalTime(), h.AddDays(1).AddTicks(-1).ToUniversalTime());

            // Períodos predefinidos
            var ahora = DateTime.UtcNow;
            return periodo switch
            {
                "hoy" => (ahora.Date, ahora),
                "semana" => (ahora.AddDays(-7).Date, ahora),
                "mes" => (new DateTime(ahora.Year, ahora.Month, 1, 0, 0, 0, DateTimeKind.Utc), ahora),
                "trimestre" => (ahora.AddMonths(-3).Date, ahora),
                "anio" => (new DateTime(ahora.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc), ahora),
                _ => (ahora.AddDays(-30).Date, ahora)   // default: últimos 30 días
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // GET /FinanzasAdmin  →  Dashboard
        // ─────────────────────────────────────────────────────────────────────
        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index(
            string? desde, string? hasta, string? periodo = "mes")
        {
            var (d, h) = ParseFechas(desde, hasta, periodo);

            var kpis = await _svc.GetDashboardKPIsAsync(d, h);
            var alertas = await _svc.GetAlertasAsync();

            ViewBag.Desde = d.ToString("yyyy-MM-dd");
            ViewBag.Hasta = h.ToString("yyyy-MM-dd");
            ViewBag.Periodo = periodo;
            ViewBag.Alertas = alertas;

            return View(kpis);
        }

        // ─────────────────────────────────────────────────────────────────────
        // GET /FinanzasAdmin/Ingresos
        // ─────────────────────────────────────────────────────────────────────
        [HttpGet("Ingresos")]
        public async Task<IActionResult> Ingresos(int? anio)
        {
            var year = anio ?? DateTime.UtcNow.Year;
            var datos = await _svc.GetIngresosPorMesAsync(year);

            ViewBag.Anio = year;
            ViewBag.Anios = Enumerable.Range(DateTime.UtcNow.Year - 3, 4).Reverse().ToList();
            return View(datos);
        }

        // ─────────────────────────────────────────────────────────────────────
        // GET /FinanzasAdmin/Vendedores
        // ─────────────────────────────────────────────────────────────────────
        [HttpGet("Vendedores")]
        public async Task<IActionResult> Vendedores(
            string? desde, string? hasta, string? periodo = "mes",
            string? vendedorId = null, string? busqueda = null)
        {
            var (d, h) = ParseFechas(desde, hasta, periodo);

            var filtro = new FiltroFinanzas
            {
                Desde = d,
                Hasta = h,
                VendedorId = vendedorId,
                Busqueda = busqueda
            };

            var datos = await _svc.GetVendedoresAsync(filtro);

            ViewBag.Desde = d.ToString("yyyy-MM-dd");
            ViewBag.Hasta = h.ToString("yyyy-MM-dd");
            ViewBag.Periodo = periodo;
            ViewBag.Busqueda = busqueda;
            return View(datos);
        }

        // ─────────────────────────────────────────────────────────────────────
        // GET /FinanzasAdmin/Productos
        // ─────────────────────────────────────────────────────────────────────
        [HttpGet("Productos")]
        public async Task<IActionResult> Productos(
            string? desde, string? hasta, string? periodo = "mes",
            string? vendedorId = null, string? busqueda = null, int top = 20)
        {
            var (d, h) = ParseFechas(desde, hasta, periodo);

            var filtro = new FiltroFinanzas
            {
                Desde = d,
                Hasta = h,
                VendedorId = vendedorId,
                Busqueda = busqueda
            };

            var datos = await _svc.GetProductosMasVendidosAsync(filtro, top);

            ViewBag.Desde = d.ToString("yyyy-MM-dd");
            ViewBag.Hasta = h.ToString("yyyy-MM-dd");
            ViewBag.Periodo = periodo;
            ViewBag.Top = top;
            return View(datos);
        }

        // ─────────────────────────────────────────────────────────────────────
        // GET /FinanzasAdmin/Clientes
        // ─────────────────────────────────────────────────────────────────────
        [HttpGet("Clientes")]
        public async Task<IActionResult> Clientes(
            string? desde, string? hasta, string? periodo = "mes",
            string? busqueda = null, string? filtroTipo = null)
        {
            var (d, h) = ParseFechas(desde, hasta, periodo);

            var filtro = new FiltroFinanzas
            {
                Desde = d,
                Hasta = h,
                Busqueda = busqueda
            };

            var datos = await _svc.GetClientesAsync(filtro);

            // Filtro adicional: nuevos vs recurrentes
            if (filtroTipo == "nuevos")
                datos = datos.Where(c => !c.EsRecurrente).ToList();
            else if (filtroTipo == "recurrentes")
                datos = datos.Where(c => c.EsRecurrente).ToList();
            else if (filtroTipo == "inactivos")
                datos = datos.Where(c => c.DiasDesdeUltimaCompra > 30).ToList();

            ViewBag.Desde = d.ToString("yyyy-MM-dd");
            ViewBag.Hasta = h.ToString("yyyy-MM-dd");
            ViewBag.Periodo = periodo;
            ViewBag.FiltroTipo = filtroTipo;
            ViewBag.Busqueda = busqueda;
            return View(datos);
        }

        // ─────────────────────────────────────────────────────────────────────
        // GET /FinanzasAdmin/Ventas
        // ─────────────────────────────────────────────────────────────────────
        [HttpGet("Ventas")]
        public async Task<IActionResult> Ventas(
            string? desde, string? hasta, string? periodo = "mes",
            string? estado = null, string? vendedorId = null,
            string? busqueda = null, int pagina = 1)
        {
            var (d, h) = ParseFechas(desde, hasta, periodo);

            var filtro = new FiltroFinanzas
            {
                Desde = d,
                Hasta = h,
                Estado = estado,
                VendedorId = vendedorId,
                Busqueda = busqueda,
                Pagina = pagina,
                PorPagina = 25
            };

            var resultado = await _svc.GetVentasAsync(filtro);

            ViewBag.Desde = d.ToString("yyyy-MM-dd");
            ViewBag.Hasta = h.ToString("yyyy-MM-dd");
            ViewBag.Periodo = periodo;
            ViewBag.Estado = estado;
            ViewBag.VendedorId = vendedorId;
            ViewBag.Busqueda = busqueda;
            ViewBag.Pagina = pagina;
            return View(resultado);
        }

        // ─────────────────────────────────────────────────────────────────────
        // GET /FinanzasAdmin/DetalleVenta/5
        // ─────────────────────────────────────────────────────────────────────
        [HttpGet("DetalleVenta/{id:int}")]
        public async Task<IActionResult> DetalleVenta(int id)
        {
            var venta = await _svc.GetDetalleVentaAsync(id);
            if (venta == null) return NotFound();
            return View(venta);
        }

        // ─────────────────────────────────────────────────────────────────────
        // GET /FinanzasAdmin/ExportarVentas  → CSV
        // ─────────────────────────────────────────────────────────────────────
        [HttpGet("ExportarVentas")]
        public async Task<IActionResult> ExportarVentas(
            string? desde, string? hasta, string? periodo = "mes", string? estado = null)
        {
            var (d, h) = ParseFechas(desde, hasta, periodo);

            var filtro = new FiltroFinanzas
            {
                Desde = d,
                Hasta = h,
                Estado = estado,
                PorPagina = 9999
            };

            var resultado = await _svc.GetVentasAsync(filtro);

            // Escapa un valor CSV: comillas, y neutraliza prefijos de fórmula (CSV injection)
            static string EscapeCsv(string? value)
            {
                if (string.IsNullOrEmpty(value)) return "\"\"";
                if (value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
                    value = "'" + value;
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }

            var csv = new System.Text.StringBuilder();
            csv.AppendLine("VentaID,Cliente,Email,Fecha,Estado,MetodoPago,Items,Total,Vendedores");

            foreach (var v in resultado.Items)
            {
                csv.AppendLine(string.Join(",",
                    v.VentaId,
                    EscapeCsv(v.NombreCliente),
                    EscapeCsv(v.EmailCliente),
                    EscapeCsv(v.FechaVenta.ToString("yyyy-MM-dd HH:mm")),
                    EscapeCsv(v.Estado),
                    EscapeCsv(v.MetodoPago),
                    v.CantidadItems,
                    v.Total.ToString("F2"),
                    EscapeCsv(string.Join("|", v.Vendedores))
                ));
            }

            var bytes = System.Text.Encoding.UTF8.GetPreamble()
                .Concat(System.Text.Encoding.UTF8.GetBytes(csv.ToString()))
                .ToArray();

            return File(bytes, "text/csv",
                $"ventas_{d:yyyyMMdd}_{h:yyyyMMdd}.csv");
        }
    }
}
