using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Simone.Data;
using Simone.ViewModels.Reportes;
using Simone.Models; // Ventas, DetalleVentas, Producto, Usuario, Devoluciones
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;

namespace Simone.Controllers
{
    [Authorize(Roles = "Administrador")]
    [Route("Panel")]
    public class ReportesController : Controller
    {
        private readonly TiendaDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<ReportesController> _logger;

        public ReportesController(
            TiendaDbContext context,
            IWebHostEnvironment env,
            ILogger<ReportesController> logger)
        {
            _context = context;
            _env = env;
            _logger = logger;
        }

        // ----------------------- Helpers (comprobante/metadata) -----------------------

        private string UploadsFolderAbs()
            => Path.Combine(_env.WebRootPath, "uploads", "comprobantes");

        /// <summary>
        /// Busca el comprobante guardado como "venta-{id}.ext" (jpg/png/pdf) y retorna URL relativa.
        /// </summary>
        private string? BuscarComprobanteUrl(int ventaId)
        {
            var folder = UploadsFolderAbs();
            if (!Directory.Exists(folder)) return null;

            var files = Directory.GetFiles(folder, $"venta-{ventaId}.*", SearchOption.TopDirectoryOnly);
            if (files.Length == 0) return null;

            var rel = Path.GetRelativePath(_env.WebRootPath, files[0]).Replace("\\", "/");
            return "/" + rel;
        }

        /// <summary>
        /// Intenta leer el nombre del depositante desde un archivo de metadatos:
        /// 1) JSON: { "depositante": "Nombre Apellido" } en venta-{id}.meta.json
        /// 2) TXT:  contenido plano                       en venta-{id}.txt
        /// </summary>
        private string? BuscarDepositante(int ventaId)
        {
            var folder = UploadsFolderAbs();
            if (!Directory.Exists(folder)) return null;

            var jsonMeta = Path.Combine(folder, $"venta-{ventaId}.meta.json");
            if (System.IO.File.Exists(jsonMeta))
            {
                try
                {
                    using var doc = JsonDocument.Parse(System.IO.File.ReadAllText(jsonMeta));
                    if (doc.RootElement.TryGetProperty("depositante", out var el))
                    {
                        var val = el.GetString();
                        if (!string.IsNullOrWhiteSpace(val)) return val!.Trim();
                    }
                }
                catch { /* ignorar parse errors */ }
            }

            var txtMeta = Path.Combine(folder, $"venta-{ventaId}.txt");
            if (System.IO.File.Exists(txtMeta))
            {
                var val = System.IO.File.ReadAllText(txtMeta).Trim();
                if (!string.IsNullOrWhiteSpace(val)) return val;
            }

            return null;
        }

        private void GuardarMetaDeposito(int ventaId, string? depositante, string? banco)
        {
            var folder = UploadsFolderAbs();
            Directory.CreateDirectory(folder);

            var meta = new
            {
                depositante = string.IsNullOrWhiteSpace(depositante) ? null : depositante.Trim(),
                banco = string.IsNullOrWhiteSpace(banco) ? null : banco.Trim(),
                ts = DateTime.UtcNow
            };

            var metaPath = Path.Combine(folder, $"venta-{ventaId}.meta.json");
            var json = JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = false });
            System.IO.File.WriteAllText(metaPath, json);
        }

        // ------------------------------- Reportes (listado y detalle) -------------------------------

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
                .OfType<Usuario>()
                .CountAsync(u => u.FechaRegistro >= desde, ct);

            ViewBag.TotalVentas = totalVentas;
            ViewBag.TotalIngresos = totalIngresos;
            ViewBag.ProductosVendidos = productosVendidos;
            ViewBag.ClientesNuevos = clientesNuevos;

            // Top 50 ventas recientes
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

            var comprobanteUrl = BuscarComprobanteUrl(v.VentaID);
            var depositante = BuscarDepositante(v.VentaID);

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
                Banco = null,
                Depositante = depositante,
                ComprobanteUrl = comprobanteUrl,

                VentaID = v.VentaID,
                Fecha = v.FechaVenta,
                Estado = v.Estado ?? string.Empty,
                MetodoPago = v.MetodoPago ?? string.Empty,
                Total = v.Total,

                UsuarioId = v.UsuarioId ?? v.Usuario?.Id ?? string.Empty,
                Nombre = v.Usuario?.NombreCompleto ?? "(sin usuario)",
                Email = v.Usuario?.Email,
                Telefono = v.Usuario?.Telefono ?? v.Usuario?.PhoneNumber,
                Direccion = v.Usuario?.Direccion,

                PerfilCiudad = v.Usuario?.Ciudad,
                PerfilProvincia = v.Usuario?.Provincia,
                PerfilReferencia = v.Usuario?.Referencia,

                Direcciones = direcciones,

                Detalles = (v.DetalleVentas ?? new List<DetalleVentas>())
                    .OrderBy(d => d.DetalleVentaID)
                    .Select(d => new DetalleFilaVM
                    {
                        Producto = d.Producto?.Nombre
                                   ?? (d.ProductoID != 0 ? $"#{d.ProductoID}" : "(producto)"),
                        Cantidad = d.Cantidad,
                        Subtotal = d.Subtotal.HasValue ? d.Subtotal.Value : d.PrecioUnitario * d.Cantidad
                    })
                    .ToList()
            };

            return View("~/Views/Reportes/VentaDetalle.cshtml", vm);
        }

        // ------------------------------- Acciones rápidas -------------------------------

        // POST /Panel/MarcarEnviada
        [HttpPost("MarcarEnviada")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarcarEnviada(int id, CancellationToken ct)
        {
            var venta = await _context.Ventas.FirstOrDefaultAsync(v => v.VentaID == id, ct);
            if (venta == null) return NotFound();

            venta.Estado = "Enviado";
            await _context.SaveChangesAsync(ct);

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { ok = true, estado = venta.Estado });

            TempData["MensajeExito"] = "Venta marcada como enviada.";
            return RedirectToAction(nameof(Reportes));
        }

        // ------------------------------- Reportar / Revertir venta -------------------------------

        /// <summary>
        /// Reversa una venta: repone stocks, registra devoluciones y marca la venta como cancelada.
        /// Motivos esperados: "deposito_falso", "devolucion", "otro".
        /// </summary>
        // POST /Panel/ReportarVenta
        [HttpPost("ReportarVenta")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReportarVenta(int ventaId, string motivo, string? nota, CancellationToken ct)
        {
            if (ventaId <= 0 || string.IsNullOrWhiteSpace(motivo))
            {
                return AjaxOrRedirectError("Datos inválidos.", nameof(Reportes));
            }

            var venta = await _context.Ventas
                .Include(v => v.DetalleVentas).ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(v => v.VentaID == ventaId, ct);

            if (venta == null)
                return AjaxOrRedirectError("Venta no encontrada.", nameof(Reportes));

            // Evitar doble reversión
            var estado = (venta.Estado ?? string.Empty).ToLowerInvariant();
            if (estado.Contains("cancel"))
                return AjaxOrRedirectError("La venta ya se encuentra cancelada.", nameof(Reportes));

            try
            {
                await using var tx = await _context.Database.BeginTransactionAsync(ct);

                // 1) Reponer stock de todos los detalles
                foreach (var d in venta.DetalleVentas)
                {
                    if (d.Producto != null)
                        d.Producto.Stock += d.Cantidad;
                }

                // 2) Registrar devoluciones (una por cada detalle)
                var motivoCanon = motivo.Trim().ToLowerInvariant();
                var label = motivoCanon switch
                {
                    "devolucion" => "devolucion",
                    "deposito_falso" => "deposito_falso",
                    _ => "otro"
                };

                foreach (var d in venta.DetalleVentas)
                {
                    _context.Devoluciones.Add(new Devoluciones
                    {
                        DetalleVentaID = d.DetalleVentaID,
                        FechaDevolucion = DateTime.UtcNow,
                        Motivo = string.IsNullOrWhiteSpace(nota) ? label : $"{label}: {nota}",
                        CantidadDevuelta = d.Cantidad,
                        Aprobada = true
                    });
                }

                // 3) Marcar venta cancelada/revertida
                venta.Estado = "Cancelada (revertida)";

                await _context.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                _logger.LogInformation("Venta {VentaID} revertida. Motivo: {Motivo}", ventaId, motivo);

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { ok = true });

                TempData["MensajeExito"] = "La venta fue revertida y el stock repuesto.";
                return RedirectToAction(nameof(Reportes));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al revertir la venta {VentaID}", ventaId);

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { ok = false, error = "No se pudo revertir la venta." });

                TempData["MensajeError"] = "No se pudo revertir la venta.";
                return RedirectToAction(nameof(Reportes));
            }
        }

        // ------------------------------- Utils -------------------------------

        private IActionResult AjaxOrRedirectError(string message, string redirectAction)
        {
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { ok = false, error = message });

            TempData["MensajeError"] = message;
            return RedirectToAction(redirectAction);
        }
    }
}
