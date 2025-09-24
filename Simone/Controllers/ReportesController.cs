using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Simone.Data;
using Simone.ViewModels.Reportes;
using Simone.Models; // para DetalleVenta
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Simone.Controllers
{
    [Authorize(Roles = "Administrador")]
    [Route("Panel")] 
    public class ReportesController : Controller
    {
        private readonly TiendaDbContext _context;

        public ReportesController(TiendaDbContext context) => _context = context;

        // GET /Panel/Reportes
        [HttpGet("Reportes")]
        public async Task<IActionResult> Reportes(CancellationToken ct)
        {
            // Métricas
            var totalVentas = await _context.Ventas.CountAsync(ct);
            var totalIngresos = await _context.Ventas.SumAsync(v => (decimal?)v.Total, ct) ?? 0m;
            var productosVendidos = await _context.DetalleVentas.SumAsync(dv => (int?)dv.Cantidad, ct) ?? 0;

            var desde = DateTime.UtcNow.AddMonths(-1);
            var clientesNuevos = await _context.Users
                .OfType<Simone.Models.Usuario>()
                .CountAsync(u => u.FechaRegistro >= desde, ct);

            ViewBag.TotalVentas = totalVentas;
            ViewBag.TotalIngresos = totalIngresos;
            ViewBag.ProductosVendidos = productosVendidos;
            ViewBag.ClientesNuevos = clientesNuevos;

            // Listado de ventas recientes → CompradorResumenVM
            var vm = await _context.Ventas
                .AsNoTracking()
                .Include(v => v.Usuario)
                .OrderByDescending(v => v.FechaVenta)
                .Take(50)
                .Select(v => new CompradorResumenVM
                {
                    VentaID = v.VentaID,
                    UsuarioId = v.UsuarioId,
                    Nombre = v.Usuario == null || string.IsNullOrWhiteSpace(v.Usuario.NombreCompleto)
                                    ? "(sin usuario)"
                                    : v.Usuario.NombreCompleto,
                    Email = v.Usuario == null ? null : v.Usuario.Email,
                    Telefono = v.Usuario == null ? null : (v.Usuario.Telefono ?? v.Usuario.PhoneNumber),
                    Fecha = v.FechaVenta,
                    Estado = v.Estado ?? string.Empty,
                    MetodoPago = v.MetodoPago ?? string.Empty,
                    Total = v.Total,
                    FotoPerfil = v.Usuario == null ? null : v.Usuario.FotoPerfil
                })
                .ToListAsync(ct);

            // Apuntamos explícitamente a la vista física (tu carpeta actual)
            return View("~/Views/Reportes/Reportes.cshtml", vm);
        }

        // GET /Panel/VentaDetalle/{id}
        [HttpGet("VentaDetalle/{id:int}", Name = "Panel_VentaDetalle")]
        public async Task<IActionResult> VentaDetalle(int id, CancellationToken ct)
        {
            var v = await _context.Ventas
                .AsNoTracking()
                .Include(x => x.Usuario)
                .Include(x => x.DetalleVentas).ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(x => x.VentaID == id, ct);

            if (v == null) return NotFound();

            // Dirección mostrable (fallback desde el perfil)
            var direcciones = new List<DireccionVM>();
            if (v.Usuario != null && !string.IsNullOrWhiteSpace(v.Usuario.Direccion))
            {
                direcciones.Add(new DireccionVM
                {
                    Calle = v.Usuario.Direccion,
                    Ciudad = v.Usuario.Ciudad,
                    EstadoProvincia = v.Usuario.Provincia,
                    CodigoPostal = v.Usuario.CodigoPostal,
                    TelefonoContacto = v.Usuario.Telefono ?? v.Usuario.PhoneNumber,
                    // usamos la fecha de la venta para que la vista pueda ordenar/fallback
                    FechaRegistro = v.FechaVenta
                });
            }

            var vm = new VentaDetalleVM
            {
                // Pago / depósito (Usuario)
                Banco = null, // si luego guardas banco en otra entidad/campo, mapéalo aquí
                Depositante = v.Usuario?.NombreDepositante,
                ComprobanteUrl = v.Usuario?.FotoComprobanteDeposito,

                // Venta
                VentaID = v.VentaID,
                Fecha = v.FechaVenta,
                Estado = v.Estado ?? string.Empty,
                MetodoPago = v.MetodoPago ?? string.Empty,
                Total = v.Total,

                // Persona
                UsuarioId = v.UsuarioId ?? v.Usuario?.Id ?? string.Empty,
                Nombre = v.Usuario?.NombreCompleto ?? "(sin usuario)",
                Email = v.Usuario?.Email,
                Telefono = v.Usuario?.Telefono ?? v.Usuario?.PhoneNumber,
                Direccion = v.Usuario?.Direccion,

                // Fallback perfil
                PerfilCiudad = v.Usuario?.Ciudad,
                PerfilProvincia = v.Usuario?.Provincia,
                PerfilReferencia = v.Usuario?.Referencia,

                Direcciones = direcciones,

                // Detalles
                Detalles = (v.DetalleVentas ?? new List<DetalleVentas>())
    .OrderBy(d => d.DetalleVentaID) // si tu PK se llama distinto, ajusta aquí
    .Select(d => new DetalleFilaVM
    {
        Producto = d.Producto?.Nombre
                   ?? (d.ProductoID != 0 ? $"#{d.ProductoID}" : "(producto)"),
        Cantidad = d.Cantidad,
        // Si Subtotal es decimal? usamos fallback con PrecioUnitario; si es decimal no-null, puedes dejar solo d.Subtotal
        Subtotal = d.Subtotal.HasValue ? d.Subtotal.Value : d.PrecioUnitario * d.Cantidad
    })
    .ToList()
            };

            // Apuntamos explícitamente a la vista física (tu carpeta actual)
            return View("~/Views/Reportes/VentaDetalle.cshtml", vm);
        }

        // POST /Panel/MarcarEnviada
        [HttpPost("MarcarEnviada")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarcarEnviada(int id, CancellationToken ct)
        {
            var venta = await _context.Ventas.FirstOrDefaultAsync(v => v.VentaID == id, ct);
            if (venta == null) return NotFound();

            venta.Estado = "Enviado";
            await _context.SaveChangesAsync(ct);

            // Si vino por AJAX, devolvemos JSON para que la vista recargue
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { ok = true, estado = venta.Estado });

            TempData["MensajeExito"] = "Venta marcada como enviada.";
            return RedirectToAction(nameof(Reportes));
        }
    }
}
