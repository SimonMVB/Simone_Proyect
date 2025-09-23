using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Simone.Data;
using Simone.ViewModels.Reportes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Simone.Controllers
{
    [Authorize(Roles = "Administrador")]
    [Route("Panel")] // prefijo de todas las rutas de este controller
    public class ReportesController : Controller
    {
        private readonly TiendaDbContext _context;

        public ReportesController(TiendaDbContext context) => _context = context;

        // GET /Panel/Reportes
        [HttpGet("Reportes")]
        public async Task<IActionResult> Reportes(CancellationToken ct)
        {
            // métricas
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

            // listado de ventas recientes → CompradorResumenVM
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

            // devolvemos explícitamente la vista "Reportes"
            return View("Reportes", vm);
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

            // Dirección mostrable (armada desde el perfil)
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
                    FechaRegistro = v.FechaVenta
                });
            }

            var vm = new VentaDetalleVM
            {
                // Pago / depósito (desde perfil)
                Banco = null, // si en el futuro guardas banco, mapéalo aquí
                Depositante = v.Usuario?.NombreDepositante,
                ComprobanteUrl = v.Usuario?.FotoComprobanteDeposito,

                // Datos venta
                VentaID = v.VentaID,
                Fecha = v.FechaVenta,
                Estado = v.Estado ?? string.Empty,
                MetodoPago = v.MetodoPago ?? string.Empty,
                Total = v.Total,

                // Persona
                UsuarioId = v.UsuarioId,
                Nombre = v.Usuario?.NombreCompleto ?? "(sin usuario)",
                Email = v.Usuario?.Email,
                Telefono = v.Usuario?.Telefono ?? v.Usuario?.PhoneNumber,
                Direccion = v.Usuario?.Direccion,

                // Fallback perfil
                PerfilCiudad = v.Usuario?.Ciudad,
                PerfilProvincia = v.Usuario?.Provincia,
                PerfilReferencia = v.Usuario?.Referencia,

                Direcciones = direcciones,

                // Detalle de líneas
                Detalles = v.DetalleVentas
                    .OrderBy(d => d.DetalleVentaID)
                    .Select(d => new DetalleFilaVM
                    {
                        Producto = d.Producto?.Nombre ?? $"#{d.ProductoID}",
                        Cantidad = d.Cantidad,
                        Subtotal = (decimal)d.Subtotal
                    })
                    .ToList()
            };

            return View(vm); // Vista: Views/Reportes/VentaDetalle.cshtml
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

            // Si vino por AJAX, devolvemos JSON para actualizar la fila en el listado
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { ok = true, estado = venta.Estado });

            TempData["MensajeExito"] = "Venta marcada como enviada.";
            return RedirectToAction(nameof(Reportes));
        }
    }
}
