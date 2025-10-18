using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Simone.Data;
using Simone.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Simone.Controllers
{
    [Authorize]
    [AutoValidateAntiforgeryToken]
    public class MisComprasController : Controller
    {
        private readonly TiendaDbContext _context;
        private readonly UserManager<Usuario> _userManager;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<MisComprasController> _logger;

        private static readonly HashSet<string> _extPermitidas =
            new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp", ".pdf" };

        public MisComprasController(
            TiendaDbContext context,
            UserManager<Usuario> userManager,
            IWebHostEnvironment env,
            ILogger<MisComprasController> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _env = env ?? throw new ArgumentNullException(nameof(env));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // ========================= Helpers (rutas / meta / comprobantes) =========================


        /// <summary>
        /// Lee "envio.total" desde venta-{id}.meta.json si existe.
        /// </summary>
        private decimal? BuscarEnvioTotal(int ventaId)
        {
            try
            {
                var metaPath = Path.Combine(UploadsFolderAbs(), $"venta-{ventaId}.meta.json");
                if (!System.IO.File.Exists(metaPath)) return null;

                var json = System.IO.File.ReadAllText(metaPath);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("envio", out var envio) &&
                    envio.ValueKind == JsonValueKind.Object &&
                    envio.TryGetProperty("total", out var totalElem) &&
                    totalElem.ValueKind == JsonValueKind.Number &&
                    totalElem.TryGetDecimal(out var total))
                {
                    return total;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo leer envio.total del meta de la venta {VentaID}", ventaId);
            }
            return null;
        }


        private string WebRootAbs()
            => _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

        private string UploadsFolderAbs()
            => Path.Combine(WebRootAbs(), "uploads", "comprobantes");

        /// <summary>
        /// Devuelve una URL servible para el navegador a partir de:
        ///  - http/https: igual
        ///  - ~/ : vía Url.Content
        ///  - /relativa a wwwroot: se devuelve tal cual
        ///  - relativa sin slash: se antepone "/"
        /// </summary>
        private string? NormalizarCompUrl(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var v = raw.Trim();

            if (v.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                v.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return v;

            if (v.StartsWith("~/")) return Url.Content(v);
            if (v.StartsWith("/")) return v;

            return "/" + v.TrimStart('/');
        }

        /// <summary>
        /// Busca "venta-{id}.*" dentro de /wwwroot/uploads/comprobantes y devuelve una URL relativa
        /// con cache-buster (?v=) para evitar imágenes antiguas del navegador.
        /// </summary>
        private string? BuscarComprobanteUrl(int ventaId)
        {
            var folder = UploadsFolderAbs();
            if (!Directory.Exists(folder)) return null;

            var files = Directory.EnumerateFiles(folder, $"venta-{ventaId}.*", SearchOption.TopDirectoryOnly)
                                 .Where(f => _extPermitidas.Contains(Path.GetExtension(f)))
                                 .OrderByDescending(f => System.IO.File.GetLastWriteTimeUtc(f))
                                 .ToList();
            if (files.Count == 0) return null;

            var file = files[0];
            var rel = Path.GetRelativePath(WebRootAbs(), file).Replace("\\", "/");
            var ver = System.IO.File.GetLastWriteTimeUtc(file).Ticks.ToString();

            return NormalizarCompUrl("/" + rel.TrimStart('/')) + "?v=" + ver;
        }

        /// <summary>
        /// Lee metadatos del depósito desde:
        ///  - venta-{id}.meta.json (formato de ConfirmarCompra)
        ///  - venta-{id}.txt (solo depositante, legado)
        /// Devuelve (depositante, bancoPlain). Para banco contempla:
        ///  - "banco": "pichincha"
        ///  - "bancoSeleccion": "admin:pichincha" | "tienda:{vid}:{codigo}"
        ///  - "bancoSeleccion": { banco:{ nombre/codigo/valor } }
        /// </summary>
        private (string? depositante, string? banco) BuscarMetaDeposito(int ventaId)
        {
            var folder = UploadsFolderAbs();
            if (!Directory.Exists(folder)) return (null, null);

            var metaPath = Path.Combine(folder, $"venta-{ventaId}.meta.json");
            if (System.IO.File.Exists(metaPath))
            {
                try
                {
                    var json = System.IO.File.ReadAllText(metaPath);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    string? dep = null;
                    string? banco = null;

                    if (root.TryGetProperty("depositante", out var pDep))
                        dep = pDep.ValueKind == JsonValueKind.String ? pDep.GetString() : pDep.ToString();

                    // bancoSeleccion puede ser string o objeto
                    if (root.TryGetProperty("bancoSeleccion", out var pSel))
                    {
                        if (pSel.ValueKind == JsonValueKind.String)
                        {
                            banco = pSel.GetString();
                        }
                        else if (pSel.ValueKind == JsonValueKind.Object)
                        {
                            // puede venir como { banco:{ nombre/codigo/valor } } o directamente { nombre,codigo,valor }
                            if (pSel.TryGetProperty("banco", out var pBancoObj) && pBancoObj.ValueKind == JsonValueKind.Object)
                            {
                                banco = LeerNombreBancoDeObjeto(pBancoObj);
                            }
                            else
                            {
                                banco = LeerNombreBancoDeObjeto(pSel);
                            }
                        }
                    }
                    else if (root.TryGetProperty("banco", out var pBanco))
                    {
                        banco = pBanco.ValueKind == JsonValueKind.String ? pBanco.GetString() : pBanco.ToString();
                    }

                    dep = string.IsNullOrWhiteSpace(dep) ? null : dep!.Trim();
                    banco = string.IsNullOrWhiteSpace(banco) ? null : banco!.Trim();
                    return (dep, banco);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudo leer meta JSON de la venta {VentaID}", ventaId);
                }
            }

            var txt = Path.Combine(folder, $"venta-{ventaId}.txt");
            if (System.IO.File.Exists(txt))
            {
                var v = System.IO.File.ReadAllText(txt).Trim();
                if (!string.IsNullOrWhiteSpace(v)) return (v, null);
            }

            return (null, null);

            static string? LeerNombreBancoDeObjeto(JsonElement obj)
            {
                if (obj.TryGetProperty("nombre", out var pn) && pn.ValueKind == JsonValueKind.String)
                    return pn.GetString();
                if (obj.TryGetProperty("codigo", out var pc) && pc.ValueKind == JsonValueKind.String)
                    return pc.GetString();
                if (obj.TryGetProperty("valor", out var pv) && pv.ValueKind == JsonValueKind.String)
                    return pv.GetString();
                return null;
            }
        }

        // =================================== Listado ===================================

        /// <summary>Historial de compras del usuario autenticado (paginado).</summary>
        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Index(int page = 1, int pageSize = 15, CancellationToken ct = default)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
            {
                TempData["MensajeError"] = "Debes iniciar sesión para ver tus compras.";
                return RedirectToAction("Login", "Cuenta");
            }

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 5, 50);

            var baseQuery = _context.Ventas
                .AsNoTracking()
                .Where(v => v.UsuarioId == userId)
                .OrderByDescending(v => v.FechaVenta);

            var total = await baseQuery.CountAsync(ct);

            var ventas = await baseQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.Total = total;

            return View(ventas);
        }

        // =================================== Detalle ===================================

        /// <summary>Detalle de una compra del usuario autenticado.</summary>
        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Detalle(int id, CancellationToken ct = default)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
            {
                TempData["MensajeError"] = "Debes iniciar sesión para ver el detalle.";
                return RedirectToAction("Login", "Cuenta");
            }

            var venta = await _context.Ventas
                .AsNoTracking()
                .Where(v => v.VentaID == id && v.UsuarioId == userId) // seguridad: solo compras propias
                .Include(v => v.Usuario)
                .Include(v => v.DetalleVentas).ThenInclude(dv => dv.Producto)
#if NET5_0_OR_GREATER
                .AsSplitQuery()
#endif
                .FirstOrDefaultAsync(ct);

            if (venta == null)
            {
                TempData["MensajeError"] = "No se encontró la compra solicitada.";
                return RedirectToAction(nameof(Index));
            }

            // ---- Enriquecer con comprobante + meta ----

            // 1) Comprobante: PRIORIDAD a /uploads/comprobantes (último + cache-buster); fallback: campo de perfil
            var compUrl = BuscarComprobanteUrl(venta.VentaID)
                          ?? NormalizarCompUrl(venta.Usuario?.FotoComprobanteDeposito);

            // 2) Metadatos por venta (depositante + banco)
            var (depMeta, bancoMeta) = BuscarMetaDeposito(venta.VentaID);
            var depositante = !string.IsNullOrWhiteSpace(depMeta)
                                ? depMeta!.Trim()
                                : (string.IsNullOrWhiteSpace(venta.Usuario?.NombreDepositante) ? null
                                   : venta.Usuario!.NombreDepositante!.Trim());

            // 3) Subtotales y envío
            var subtotal = venta.DetalleVentas?.Sum(d => d.Subtotal) ?? 0m;

            // Intentar leer envío del meta. Si no hay, usar diferencia Total - Subtotal (no negativa).
            var envioMeta = BuscarEnvioTotal(venta.VentaID);
            var envioTotal = envioMeta ?? Math.Max(0m, (venta.Total is decimal t ? t : 0m) - subtotal);


            // ---- Exponer a la vista ----
            ViewBag.ComprobanteUrl = compUrl;
            ViewBag.Depositante = depositante;
            ViewBag.Banco = bancoMeta;
            ViewBag.Subtotal = subtotal;
            ViewBag.EnvioTotal = envioTotal;

            return View(venta);
        }

    }
}
