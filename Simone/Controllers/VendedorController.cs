using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Simone.Models;
using Simone.Services;
using System.Text.RegularExpressions;
using Cfg = Simone.Configuration;

// ===== NUEVOS using =====
using Microsoft.EntityFrameworkCore;
using Simone.Data;
using Simone.ViewModels.Reportes;
using Microsoft.AspNetCore.Hosting;
using System.Text.Json;
using System.IO;
using System.Threading;
using System.Linq;

namespace Simone.Controllers
{
    // Admin también puede ingresar para ayudar a un vendedor (pasando ?vId=)
    [Authorize(Roles = "Administrador,Vendedor")]
    [Route("Vendedor")]
    public class VendedorController : Controller
    {
        private readonly UserManager<Usuario> _userManager;
        private readonly ILogger<VendedorController> _logger;
        private readonly IBancosConfigService _bancos;

        // ===== NUEVOS campos =====
        private readonly TiendaDbContext _context;
        private readonly IWebHostEnvironment _env;

        public VendedorController(
            UserManager<Usuario> userManager,
            ILogger<VendedorController> logger,
            IBancosConfigService bancos,
            TiendaDbContext context,                 // inyección DbContext
            IWebHostEnvironment env)                 // inyección WebRoot
        {
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _bancos = bancos ?? throw new ArgumentNullException(nameof(bancos));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _env = env ?? throw new ArgumentNullException(nameof(env));
        }

        // -------------------- Helpers --------------------
        private string CurrentVendorId(string? vIdFromQuery = null)
        {
            var myId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(myId))
                throw new InvalidOperationException("No se pudo determinar el usuario actual.");

            // Solo Admin puede operar sobre otro vendedor
            if (User.IsInRole("Administrador") && !string.IsNullOrWhiteSpace(vIdFromQuery))
                return vIdFromQuery;

            return myId;
        }

        private static string K(string? s) => (s ?? string.Empty).Trim().ToLowerInvariant();
        private static string T(string? s) => (s ?? string.Empty).Trim();
        private static string? TN(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        private static readonly Regex CodigoRegex = new(@"^[a-z0-9_-]{2,50}$", RegexOptions.Compiled);
        private static readonly Regex NumeroCuentaRegex = new(@"^[0-9]{6,20}$", RegexOptions.Compiled);
        private static readonly Regex Texto40Regex = new(@"^.{1,40}$", RegexOptions.Compiled);
        private static readonly Regex Texto120Regex = new(@"^.{1,120}$", RegexOptions.Compiled);
        private static readonly Regex RucRegex = new(@"^\d{10}(\d{3})?$", RegexOptions.Compiled);
        private static readonly Regex LogoPathRegex = new(@"^[A-Za-z0-9_\-/\.]{1,200}$", RegexOptions.Compiled);

        // ===== Helpers nuevos: comprobantes/metadata =====
        private string UploadsFolderAbs()
            => Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"),
                            "uploads", "comprobantes");

        private string? NormalizarCompUrl(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var v = raw.Trim();
            if (v.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || v.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return v;
            if (v.StartsWith("~/")) return Url.Content(v);
            if (v.StartsWith("/")) return v;
            return "/" + v.TrimStart('/');
        }

        private string? BuscarComprobanteUrl(int ventaId)
        {
            var folder = UploadsFolderAbs();
            if (!Directory.Exists(folder)) return null;

            var allow = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".jpg", ".jpeg", ".png", ".webp", ".pdf" };

            var files = Directory.EnumerateFiles(folder, $"venta-{ventaId}.*", SearchOption.TopDirectoryOnly)
                                 .Where(f => allow.Contains(Path.GetExtension(f)))
                                 .OrderByDescending(f => System.IO.File.GetLastWriteTimeUtc(f))
                                 .ToList();
            if (files.Count == 0) return null;

            var webroot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var rel = Path.GetRelativePath(webroot, files[0]).Replace("\\", "/");
            return NormalizarCompUrl("/" + rel.TrimStart('/'));
        }

        private (string? depositante, string? banco) BuscarMetaDeposito(int ventaId)
        {
            var folder = UploadsFolderAbs();
            if (!Directory.Exists(folder)) return (null, null);

            var metaJson = Path.Combine(folder, $"venta-{ventaId}.meta.json");
            if (System.IO.File.Exists(metaJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(System.IO.File.ReadAllText(metaJson));
                    var root = doc.RootElement;

                    string? dep = null;
                    string? bank = null;

                    if (root.TryGetProperty("depositante", out var pDep))
                        dep = pDep.ValueKind == JsonValueKind.String ? pDep.GetString() : pDep.ToString();

                    if (root.TryGetProperty("banco", out var pBanco))
                        bank = pBanco.ValueKind == JsonValueKind.String ? pBanco.GetString() : pBanco.ToString();

                    return (dep, bank);
                }
                catch { /* swallow */ }
            }

            var metaTxt = Path.Combine(folder, $"venta-{ventaId}.txt");
            if (System.IO.File.Exists(metaTxt))
            {
                try
                {
                    var dep = System.IO.File.ReadAllText(metaTxt)?.Trim();
                    return (string.IsNullOrWhiteSpace(dep) ? null : dep, null);
                }
                catch { /* swallow */ }
            }

            return (null, null);
        }

        // -------------------- Acciones existentes --------------------
        [HttpGet("Productos")] public IActionResult Productos() => View();
        [HttpGet("AnadirProducto")] public IActionResult AnadirProducto() => View();

        // -------------------- Bancos (vendedor) --------------------
        public sealed class UpsertVm
        {
            public string? OriginalCodigo { get; set; }
            public string Codigo { get; set; } = string.Empty;      // clave estable (p.ej. pacifico)
            public string Nombre { get; set; } = string.Empty;      // Banco del Pacífico
            public string Numero { get; set; } = string.Empty;      // ########
            public string Tipo { get; set; } = "Cuenta de Ahorros";
            public string? Titular { get; set; }
            public string? Ruc { get; set; }
            public string? LogoPath { get; set; }
            public bool Activo { get; set; } = true;
        }

        private static (bool ok, string? err) Validate(UpsertVm vm)
        {
            if (string.IsNullOrWhiteSpace(vm.Codigo) || !CodigoRegex.IsMatch(vm.Codigo))
                return (false, "Código inválido. Usa minúsculas, números, '-' o '_' (2-50).");
            if (string.IsNullOrWhiteSpace(vm.Nombre) || !Texto120Regex.IsMatch(vm.Nombre))
                return (false, "Nombre de banco inválido (máximo 120).");
            if (string.IsNullOrWhiteSpace(vm.Numero) || !NumeroCuentaRegex.IsMatch(vm.Numero))
                return (false, "Número de cuenta inválido (solo dígitos, 6-20).");
            if (string.IsNullOrWhiteSpace(vm.Tipo) || !Texto40Regex.IsMatch(vm.Tipo))
                return (false, "Tipo de cuenta inválido.");
            if (!string.IsNullOrWhiteSpace(vm.Titular) && !Texto120Regex.IsMatch(vm.Titular))
                return (false, "Titular inválido (máximo 120).");
            if (!string.IsNullOrWhiteSpace(vm.Ruc) && !RucRegex.IsMatch(vm.Ruc))
                return (false, "RUC/Cédula inválido (10 o 13 dígitos).");
            if (!string.IsNullOrWhiteSpace(vm.LogoPath) &&
                (!LogoPathRegex.IsMatch(vm.LogoPath) || vm.LogoPath.Contains("..") || vm.LogoPath.Contains("://")))
                return (false, "Ruta de logo inválida (debe ser relativa).");
            return (true, null);
        }

        // GET /Vendedor/Bancos?vId=<opcional para Admin>
        [HttpGet("Bancos")]
        public async Task<IActionResult> Bancos([FromQuery] string? vId = null)
        {
            try
            {
                var vendorId = CurrentVendorId(vId);

                var cuentas = (await _bancos.GetByProveedorAsync(vendorId))?.ToList()
                              ?? new List<Cfg.CuentaBancaria>();

                ViewBag.Cuentas = cuentas.OrderBy(x => x.Nombre).ThenBy(x => x.Codigo).ToList();
                ViewBag.TargetVendorId = vendorId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cargando cuentas bancarias del vendedor.");
                ViewBag.Cuentas = new List<Cfg.CuentaBancaria>();
                TempData["MensajeError"] = "No se pudieron cargar las cuentas bancarias.";
            }

            ViewBag.MensajeExito = TempData["MensajeExito"];
            ViewBag.MensajeError = TempData["MensajeError"];
            ViewBag.ModelErrors = TempData["ModelErrors"];

            return View(); // Views/Vendedor/Bancos.cshtml
        }

        // POST /Vendedor/Bancos/Save?vId=<opcional para Admin>
        [HttpPost("Bancos/Save")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save([FromForm] UpsertVm vm, [FromQuery] string? vId = null)
        {
            var vendorId = CurrentVendorId(vId);

            vm.OriginalCodigo = TN(vm.OriginalCodigo);
            vm.Codigo = T(vm.Codigo).ToLowerInvariant();
            vm.Nombre = T(vm.Nombre);
            vm.Numero = T(vm.Numero);
            vm.Tipo = T(vm.Tipo);
            vm.Titular = TN(vm.Titular);
            vm.Ruc = TN(vm.Ruc);
            vm.LogoPath = TN(vm.LogoPath);

            var (ok, err) = Validate(vm);
            if (!ok)
            {
                TempData["MensajeError"] = err;
                TempData["ModelErrors"] = err;
                return RedirectToAction(nameof(Bancos), new { vId = (User.IsInRole("Administrador") ? vendorId : null) });
            }

            try
            {
                var cuentas = (await _bancos.GetByProveedorAsync(vendorId))?.ToList()
                              ?? new List<Cfg.CuentaBancaria>();

                bool IsDup(string code, Cfg.CuentaBancaria? exclude = null) =>
                    cuentas.Any(c => !ReferenceEquals(c, exclude) && K(c.Codigo) == K(code));

                if (string.IsNullOrEmpty(vm.OriginalCodigo))
                {
                    if (IsDup(vm.Codigo))
                    {
                        TempData["MensajeError"] = "Ya existe una cuenta con ese código.";
                        return RedirectToAction(nameof(Bancos), new { vId = (User.IsInRole("Administrador") ? vendorId : null) });
                    }

                    cuentas.Add(new Cfg.CuentaBancaria
                    {
                        Codigo = vm.Codigo,
                        Nombre = vm.Nombre,
                        Numero = vm.Numero,
                        Tipo = vm.Tipo,
                        Titular = vm.Titular,
                        Ruc = vm.Ruc,
                        LogoPath = vm.LogoPath,
                        Activo = vm.Activo
                    });
                }
                else
                {
                    var actual = cuentas.FirstOrDefault(c => K(c.Codigo) == K(vm.OriginalCodigo));
                    if (actual == null)
                    {
                        TempData["MensajeError"] = "No se encontró la cuenta original para editar.";
                        return RedirectToAction(nameof(Bancos), new { vId = (User.IsInRole("Administrador") ? vendorId : null) });
                    }

                    if (K(vm.Codigo) != K(vm.OriginalCodigo) && IsDup(vm.Codigo, actual))
                    {
                        TempData["MensajeError"] = "El nuevo código ya pertenece a otra cuenta.";
                        return RedirectToAction(nameof(Bancos), new { vId = (User.IsInRole("Administrador") ? vendorId : null) });
                    }

                    actual.Codigo = vm.Codigo;
                    actual.Nombre = vm.Nombre;
                    actual.Numero = vm.Numero;
                    actual.Tipo = vm.Tipo;
                    actual.Titular = vm.Titular;
                    actual.Ruc = vm.Ruc;
                    actual.LogoPath = vm.LogoPath;
                    actual.Activo = vm.Activo;
                }

                await _bancos.SetByProveedorAsync(vendorId, cuentas);
                TempData["MensajeExito"] = "Cuenta guardada correctamente.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar cuenta bancaria del vendedor {VendorId}", vendorId);
                TempData["MensajeError"] = "Ocurrió un error al guardar la cuenta bancaria.";
            }

            return RedirectToAction(nameof(Bancos), new { vId = (User.IsInRole("Administrador") ? vendorId : null) });
        }

        // POST /Vendedor/Bancos/Delete?vId=<opcional para Admin>
        [HttpPost("Bancos/Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete([FromForm] string codigo, [FromQuery] string? vId = null)
        {
            var vendorId = CurrentVendorId(vId);

            if (string.IsNullOrWhiteSpace(codigo))
            {
                TempData["MensajeError"] = "Código inválido.";
                return RedirectToAction(nameof(Bancos), new { vId = (User.IsInRole("Administrador") ? vendorId : null) });
            }

            try
            {
                var cuentas = (await _bancos.GetByProveedorAsync(vendorId))?.ToList()
                              ?? new List<Cfg.CuentaBancaria>();

                var item = cuentas.FirstOrDefault(c => K(c.Codigo) == K(codigo));
                if (item != null)
                {
                    cuentas.Remove(item);
                    await _bancos.SetByProveedorAsync(vendorId, cuentas);
                    TempData["MensajeExito"] = "Cuenta eliminada correctamente.";
                }
                else
                {
                    TempData["MensajeError"] = "No se encontró la cuenta especificada.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar cuenta bancaria (VendorId: {VendorId}, Codigo: {Codigo})", vendorId, codigo);
                TempData["MensajeError"] = "Ocurrió un error al eliminar la cuenta bancaria.";
            }

            return RedirectToAction(nameof(Bancos), new { vId = (User.IsInRole("Administrador") ? vendorId : null) });
        }

        // POST /Vendedor/Bancos/Toggle?vId=<opcional para Admin>
        [HttpPost("Bancos/Toggle")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Toggle([FromForm] string codigo, [FromQuery] string? vId = null)
        {
            var vendorId = CurrentVendorId(vId);

            try
            {
                var cuentas = (await _bancos.GetByProveedorAsync(vendorId))?.ToList()
                              ?? new List<Cfg.CuentaBancaria>();

                var cta = cuentas.FirstOrDefault(c => K(c.Codigo) == K(codigo));
                if (cta != null)
                {
                    cta.Activo = !cta.Activo;
                    await _bancos.SetByProveedorAsync(vendorId, cuentas);
                    TempData["MensajeExito"] = "Estado actualizado.";
                }
                else
                {
                    TempData["MensajeError"] = "No se encontró la cuenta especificada.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error alternando estado de cuenta (VendorId: {VendorId}, Codigo: {Codigo})", vendorId, codigo);
                TempData["MensajeError"] = "No se pudo actualizar el estado.";
            }

            return RedirectToAction(nameof(Bancos), new { vId = (User.IsInRole("Administrador") ? vendorId : null) });
        }

        // ======================== NUEVO: Reportes Vendedor ========================

        // GET /Vendedor/Reportes
        [HttpGet("Reportes")]
        public async Task<IActionResult> Reportes([FromQuery] string? vId, CancellationToken ct)
        {
            var vendorId = CurrentVendorId(vId);

            // Total por venta SOLO considerando ítems del vendedor
            var agregados = await _context.DetalleVentas
                .AsNoTracking()
                .Where(dv => dv.Producto != null && dv.Producto.VendedorID == vendorId)
                .GroupBy(dv => dv.VentaID)
                .Select(g => new
                {
                    VentaID = g.Key,
                    TotalVendedor = g.Sum(dv => (dv.Subtotal ?? ((dv.PrecioUnitario * dv.Cantidad) - (dv.Descuento ?? 0m))))
                })
                .ToListAsync(ct);

            var ventaIds = agregados.Select(a => a.VentaID).ToList();
            if (ventaIds.Count == 0)
            {
                ViewData["Title"] = "Mis ventas";
                return View("~/Views/Reportes/Reportes.cshtml", Enumerable.Empty<CompradorResumenVM>());
            }

            var vm = await _context.Ventas
                .AsNoTracking()
                .Where(v => ventaIds.Contains(v.VentaID))
                .Include(v => v.Usuario)
                .OrderByDescending(v => v.FechaVenta)
                .Select(v => new CompradorResumenVM
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
                    Total = 0m, // se actualiza con el lookup
                    FotoPerfil = v.Usuario != null ? v.Usuario.FotoPerfil : null
                })
                .ToListAsync(ct);

            var lookup = agregados.ToDictionary(a => a.VentaID, a => a.TotalVendedor);
            foreach (var r in vm)
                if (lookup.TryGetValue(r.VentaID, out var t)) r.Total = t;

            ViewData["Title"] = "Mis ventas";
            return View("~/Views/Reportes/Reportes.cshtml", vm);
        }

        // GET /Vendedor/Reportes/Detalle/{id}
        [HttpGet("Reportes/Detalle/{id:int}", Name = "Vendedor_VentaDetalle")]
        public async Task<IActionResult> VentaDetalle(int id, [FromQuery] string? vId, CancellationToken ct)
        {
            var vendorId = CurrentVendorId(vId);

            var v = await _context.Ventas
                .AsNoTracking()
                .Include(x => x.Usuario)
                .Include(x => x.DetalleVentas).ThenInclude(d => d.Producto)
#if NET5_0_OR_GREATER
                .AsSplitQuery()
#endif
                .FirstOrDefaultAsync(x => x.VentaID == id, ct);

            if (v == null) return NotFound();

            var misDetalles = (v.DetalleVentas ?? new List<DetalleVentas>())
                .Where(d => d.Producto != null && d.Producto.VendedorID == vendorId)
                .ToList();

            if (misDetalles.Count == 0)
                return Forbid(); // la venta no tiene productos de este vendedor

            var totalVend = misDetalles.Sum(d => (d.Subtotal ?? ((d.PrecioUnitario * d.Cantidad) - (d.Descuento ?? 0m))));

            // Comprobante: primero el del perfil (si guardas ahí), luego en /uploads/comprobantes
            var compUrl =
                NormalizarCompUrl(v.Usuario?.FotoComprobanteDeposito) ??
                BuscarComprobanteUrl(v.VentaID);

            var (depositante, bancoMeta) = BuscarMetaDeposito(v.VentaID);

            var direcciones = new List<DireccionVM>();
            if (v.Usuario != null)
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
                Total = totalVend,

                // Persona
                UsuarioId = v.UsuarioId ?? v.Usuario?.Id ?? string.Empty,
                Nombre = v.Usuario?.NombreCompleto ?? "(sin usuario)",
                Email = v.Usuario?.Email,
                Telefono = v.Usuario?.Telefono ?? v.Usuario?.PhoneNumber,
                Cedula = v.Usuario?.Cedula,
                Direccion = v.Usuario?.Direccion,
                FotoPerfil = v.Usuario?.FotoPerfil,

                // Envío (perfil fallback)
                PerfilCiudad = v.Usuario?.Ciudad,
                PerfilProvincia = v.Usuario?.Provincia,
                PerfilReferencia = v.Usuario?.Referencia,

                Direcciones = direcciones,

                // Ítems SOLO del vendedor
                Detalles = misDetalles.Select(d => new DetalleVentaVM
                {
                    Producto = d.Producto?.Nombre ?? $"Producto #{d.ProductoID}",
                    Cantidad = d.Cantidad,
                    Subtotal = d.Subtotal ?? ((d.PrecioUnitario * d.Cantidad) - (d.Descuento ?? 0m))
                }).ToList()
            };

            // Reutilizamos la vista de admin para detalle (está estilada y ya usa el VM)
            return View("~/Views/Reportes/VentaDetalle.cshtml", vm);
        }
    }
}
