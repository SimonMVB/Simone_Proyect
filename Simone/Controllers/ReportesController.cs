using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Simone.Data;
using Simone.Models;              // Ventas, DetalleVentas, Producto, Usuario, Devoluciones
using Simone.ViewModels.Reportes; // VMs de reportes
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Simone.Controllers
{
    [Authorize(Roles = "Administrador")]
    [Route("Panel")]
    [AutoValidateAntiforgeryToken]
    public class ReportesController : Controller
    {
        private readonly TiendaDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<ReportesController> _logger;

        // Comprobantes permitidos
        private static readonly HashSet<string> _allowExt = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".webp", ".pdf" };

        public ReportesController(
            TiendaDbContext context,
            IWebHostEnvironment env,
            ILogger<ReportesController> logger)
        {
            _context = context;
            _env = env;
            _logger = logger;
        }

        // ----------------------- Helpers (comprobante / metadata) -----------------------

        private string WebRootAbs()
            => _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

        private string UploadsFolderAbs()
            => Path.Combine(WebRootAbs(), "uploads", "comprobantes");

        /// <summary>
        /// Normaliza una ruta o URL del comprobante hacia algo servible por el navegador.
        /// - http/https: se devuelve tal cual.
        /// - ~/ o / : se devuelve vía Url.Content (si aplica) o tal cual.
        /// - relativa (p.ej. "images/.."): se antepone "/".
        /// </summary>
        private string? NormalizarCompUrl(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var v = raw.Trim();

            // Absolutas
            if (v.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                v.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return v;

            // ~/ o / (dejar que StaticFiles lo sirva)
            if (v.StartsWith("~/")) return Url.Content(v);
            if (v.StartsWith("/")) return v;

            // Relativa a wwwroot
            var rel = "/" + v.TrimStart('/');
            return rel;
        }

        /// <summary>
        /// Busca en el disco un archivo "venta-{id}.*" dentro de /uploads/comprobantes y devuelve su URL relativa.
        /// Toma el archivo más reciente.
        /// </summary>
        private string? BuscarComprobanteUrl(int ventaId)
        {
            var folder = UploadsFolderAbs();
            if (!Directory.Exists(folder)) return null;

            var files = Directory.EnumerateFiles(folder, $"venta-{ventaId}.*", SearchOption.TopDirectoryOnly)
                                 .Where(f => _allowExt.Contains(Path.GetExtension(f)))
                                 .OrderByDescending(f => System.IO.File.GetLastWriteTimeUtc(f))
                                 .ToList();
            if (files.Count == 0) return null;

            var webroot = WebRootAbs();
            var rel = Path.GetRelativePath(webroot, files[0]).Replace("\\", "/");
            var url = NormalizarCompUrl("/" + rel.TrimStart('/'));
            _logger.LogInformation("Comprobante para venta {VentaID}: {Url}", ventaId, url ?? "(ninguno)");
            return url;
        }

        // ---- utilidades de parseo JSON (case-insensitive y tolerantes) ----

        /// <summary>Lee el primer string cuyo nombre coincida (CI) en el elemento dado.</summary>
        private static string? ReadStrCI(JsonElement el, params string[] names)
        {
            foreach (var p in el.EnumerateObject())
                foreach (var n in names)
                    if (string.Equals(p.Name, n, StringComparison.OrdinalIgnoreCase))
                        return p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : p.Value.ToString();
            return null;
        }

        /// <summary>Intenta extraer el "banco" desde múltiples formas posibles.</summary>
        private static string? TryExtractBanco(JsonElement root)
        {
            // 1) "banco":"pichincha"  o  "Banco":"..."
            var bank = ReadStrCI(root, "banco");
            if (!string.IsNullOrWhiteSpace(bank)) return bank;

            // 2) "bancoSeleccion": "pichincha"
            if (root.TryGetProperty("bancoSeleccion", out var sel))
            {
                if (sel.ValueKind == JsonValueKind.String)
                    return sel.GetString();

                if (sel.ValueKind == JsonValueKind.Object)
                {
                    // 2.a) { "bancoSeleccion": { "codigo"/"nombre"/"valor": "..." } }
                    bank = ReadStrCI(sel, "codigo", "nombre", "valor");
                    if (!string.IsNullOrWhiteSpace(bank)) return bank;

                    // 2.b) { "bancoSeleccion": { "banco": { "codigo"/"nombre"/"valor": "..." }, ... } }
                    if (sel.TryGetProperty("banco", out var bancoObj) && bancoObj.ValueKind == JsonValueKind.Object)
                    {
                        bank = ReadStrCI(bancoObj, "codigo", "nombre", "valor");
                        if (!string.IsNullOrWhiteSpace(bank)) return bank;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Lee metadatos (depositante, banco) en:
        ///   JSON: venta-{id}.meta.json  -> { "depositante": "...", "banco": "..." }
        ///         o cualquier variante con "bancoSeleccion" (string / objeto / anidado .banco)
        ///   TXT:  venta-{id}.txt        -> (solo depositante)
        /// Prioriza JSON; si no hay, intenta TXT.
        /// </summary>
        private (string? depositante, string? banco) BuscarMetaDeposito(int ventaId)
        {
            var folder = UploadsFolderAbs();
            if (!Directory.Exists(folder)) return (null, null);

            var jsonMeta = Path.Combine(folder, $"venta-{ventaId}.meta.json");
            if (System.IO.File.Exists(jsonMeta))
            {
                try
                {
                    using var doc = JsonDocument.Parse(System.IO.File.ReadAllText(jsonMeta));
                    var root = doc.RootElement;

                    // depositante admite "depositante"/"Depositante"
                    var dep = ReadStrCI(root, "depositante");

                    // banco en sus variantes
                    var bank = TryExtractBanco(root);

                    dep = string.IsNullOrWhiteSpace(dep) ? null : dep.Trim();
                    bank = string.IsNullOrWhiteSpace(bank) ? null : bank.Trim();

                    if (dep == null && bank == null)
                        _logger.LogWarning("Meta JSON sin datos útiles para venta {VentaID}. Archivo: {Path}", ventaId, jsonMeta);

                    return (dep, bank);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudo parsear el meta JSON de venta {VentaID}. Path: {Path}", ventaId, jsonMeta);
                    // Caerá a TXT
                }
            }

            var txtMeta = Path.Combine(folder, $"venta-{ventaId}.txt");
            if (System.IO.File.Exists(txtMeta))
            {
                var val = System.IO.File.ReadAllText(txtMeta).Trim();
                if (!string.IsNullOrWhiteSpace(val)) return (val, null);
            }

            _logger.LogWarning("Sin metadatos de depósito para venta {VentaID}. Buscado en {Folder}", ventaId, folder);
            return (null, null);
        }

        /// <summary>
        /// Persiste metadatos de depósito (si los necesitas guardar desde alguna acción).
        /// </summary>
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
            _logger.LogInformation("Meta de depósito guardado para venta {VentaID} en {Path}", ventaId, metaPath);
        }

        // ------------------------------- Reportes (listado y detalle) -------------------------------

        // GET /Panel/Reportes
        [HttpGet("Reportes")]
        public async Task<IActionResult> Reportes(CancellationToken ct)
        {
            // Métricas (null-safe)
            ViewBag.TotalVentas = await _context.Ventas.AsNoTracking().CountAsync(ct);
            ViewBag.TotalIngresos = await _context.Ventas.AsNoTracking().SumAsync(v => (decimal?)v.Total, ct) ?? 0m;
            ViewBag.ProductosVendidos = await _context.DetalleVentas.AsNoTracking().SumAsync(dv => (int?)dv.Cantidad, ct) ?? 0;

            var desde = DateTime.UtcNow.AddMonths(-1);
            ViewBag.ClientesNuevos = await _context.Users
                .OfType<Usuario>()
                .AsNoTracking()
                .CountAsync(u => u.FechaRegistro >= desde, ct);

            // Top 50 ventas recientes (proyección directa)
            var vm = await _context.Ventas
                .AsNoTracking()
                .OrderByDescending(v => v.FechaVenta)
                .Take(50)
                .Select(v => new Simone.ViewModels.Reportes.CompradorResumenVM
                {
                    VentaID = v.VentaID,
                    UsuarioId = v.UsuarioId!,
                    Nombre = v.Usuario != null && !string.IsNullOrWhiteSpace(v.Usuario.NombreCompleto)
                        ? v.Usuario.NombreCompleto
                        : "(sin usuario)",
                    Email = v.Usuario != null ? v.Usuario.Email : null,
                    Telefono = v.Usuario != null ? (v.Usuario.Telefono ?? v.Usuario.PhoneNumber) : null,
                    Fecha = v.FechaVenta,
                    Estado = v.Estado ?? string.Empty,
                    MetodoPago = v.MetodoPago ?? string.Empty,
                    Total = v.Total,
                    FotoPerfil = v.Usuario != null ? v.Usuario.FotoPerfil : null
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
#if NET5_0_OR_GREATER
                .AsSplitQuery()
#endif
                .FirstOrDefaultAsync(x => x.VentaID == id, ct);

            if (v == null) return NotFound();

            // Comprobante: primero ruta guardada en Usuario, luego /uploads/comprobantes
            var compUrl =
                NormalizarCompUrl(v.Usuario?.FotoComprobanteDeposito) ??
                BuscarComprobanteUrl(v.VentaID);

            // Meta por venta (preferido). Si no hay, usa Usuario.NombreDepositante (si existe).
            var (depMeta, bancoMeta) = BuscarMetaDeposito(v.VentaID);
            var depositante = !string.IsNullOrWhiteSpace(depMeta)
                                ? depMeta!.Trim()
                                : (string.IsNullOrWhiteSpace(v.Usuario?.NombreDepositante)
                                    ? null
                                    : v.Usuario!.NombreDepositante!.Trim());

            // Direcciones (perfil como fuente si no hay historial)
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
                // Pago / depósito
                Banco = bancoMeta,
                Depositante = depositante,
                ComprobanteUrl = compUrl,

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
                Cedula = v.Usuario?.Cedula,
                Direccion = v.Usuario?.Direccion,
                FotoPerfil = v.Usuario?.FotoPerfil,

                // Envío (perfil como fallback)
                PerfilCiudad = v.Usuario?.Ciudad,
                PerfilProvincia = v.Usuario?.Provincia,
                PerfilReferencia = v.Usuario?.Referencia,
                Direcciones = direcciones,

                // Detalle
                Detalles = (v.DetalleVentas ?? new List<DetalleVentas>())
                    .OrderBy(d => d.DetalleVentaID)
                    .Select(d => new DetalleVentaVM
                    {
                        Producto = d.Producto?.Nombre
                                   ?? (d.ProductoID != 0 ? $"#{d.ProductoID}" : "(producto)"),
                        Cantidad = d.Cantidad,
                        Subtotal = d.Subtotal ?? (d.PrecioUnitario * d.Cantidad)
                    })
                    .ToList()
            };

            return View("~/Views/Reportes/VentaDetalle.cshtml", vm);
        }

        // ------------------------------- Acciones rápidas -------------------------------

        // POST /Panel/MarcarEnviada
        [HttpPost("MarcarEnviada")]
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
        [HttpPost("ReportarVenta")]
        public async Task<IActionResult> ReportarVenta(int ventaId, string motivo, string? nota, CancellationToken ct)
        {
            if (ventaId <= 0 || string.IsNullOrWhiteSpace(motivo))
                return AjaxOrRedirectError("Datos inválidos.", nameof(Reportes));

            var venta = await _context.Ventas
                .Include(v => v.DetalleVentas).ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(v => v.VentaID == ventaId, ct);

            if (venta == null)
                return AjaxOrRedirectError("Venta no encontrada.", nameof(Reportes));

            var estado = (venta.Estado ?? string.Empty).ToLowerInvariant();
            if (estado.Contains("cancel"))
                return AjaxOrRedirectError("La venta ya se encuentra cancelada.", nameof(Reportes));

            try
            {
                await using var tx = await _context.Database.BeginTransactionAsync(ct);

                var detalles = venta.DetalleVentas ?? new List<DetalleVentas>();

                // 1) Reponer stock
                foreach (var d in detalles)
                {
                    if (d.Producto != null)
                        d.Producto.Stock += d.Cantidad;
                }

                // 2) Registrar devoluciones
                var motivoCanon = motivo.Trim().ToLowerInvariant();
                var label = motivoCanon switch
                {
                    "devolucion" => "devolucion",
                    "deposito_falso" => "deposito_falso",
                    _ => "otro"
                };
                var textoMotivo = string.IsNullOrWhiteSpace(nota) ? label : $"{label}: {nota.Trim()}";

                foreach (var d in detalles)
                {
                    _context.Devoluciones.Add(new Devoluciones
                    {
                        DetalleVentaID = d.DetalleVentaID,
                        FechaDevolucion = DateTime.UtcNow,
                        Motivo = textoMotivo,
                        CantidadDevuelta = d.Cantidad,
                        Aprobada = true
                    });
                }

                // 3) Marcar venta cancelada
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
