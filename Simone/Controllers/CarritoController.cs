using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Simone.Data;
using Simone.Extensions;
using Simone.Models;
using Simone.Services; // ICarritoService, EnviosCarritoService
using System.Collections.Generic;

namespace Simone.Controllers
{
    public class CarritoController : Controller
    {
        private readonly TiendaDbContext _context;
        private readonly UserManager<Usuario> _userManager;
        private readonly ICarritoService _carritoService;        // ← BD
        private readonly EnviosCarritoService _enviosCarrito;    // ← envíos

        public CarritoController(
            TiendaDbContext context,
            UserManager<Usuario> user,
            ICarritoService carritoService,
            EnviosCarritoService enviosCarrito)
        {
            _context = context;
            _userManager = user;
            _carritoService = carritoService;
            _enviosCarrito = enviosCarrito;
        }

        // -------- helpers --------
        private bool EsAjax() =>
            string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

        private (int count, decimal total) Resumen(List<CarritoDetalle> detalles)
            => (detalles.Sum(c => c.Cantidad), detalles.Sum(c => c.Precio * c.Cantidad));

        private Promocion? ObtenerCupon()
            => HttpContext.Session.GetObjectFromJson<Promocion>("Cupon");

        private void GuardarCupon(Promocion cupon)
            => HttpContext.Session.SetObjectAsJson("Cupon", cupon);

        // ======================= VISTA PRINCIPAL =======================
        [HttpGet]
        public async Task<IActionResult> VerCarrito()
        {
            var usuario = await _userManager.GetUserAsync(User);
            var detalles = new List<CarritoDetalle>();

            if (usuario != null)
            {
                var carrito = await _carritoService.GetByUsuarioIdAsync(usuario.Id);
                if (carrito != null)
                    detalles = await _carritoService.LoadCartDetails(carrito.CarritoID);
            }

            // Cupón
            var cupon = ObtenerCupon();
            ViewBag.Descuento = cupon?.Descuento ?? 0m;
            ViewBag.CodigoCupon = cupon?.CodigoCupon;

            // ===== Envíos (si hay destino y vendedores en el carrito) =====
            var prov = usuario?.Provincia;
            var city = usuario?.Ciudad;
            ViewBag.DestinoProvincia = prov;
            ViewBag.DestinoCiudad = city;

            var vendorIds = detalles
                .Select(c => c.Producto?.VendedorID)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Cast<string>()
                .Distinct()
                .ToList();

            var vendorNames = await _userManager.Users
                .Where(u => vendorIds.Contains(u.Id))
                .Select(u => new { u.Id, u.NombreCompleto, u.Email })
                .ToDictionaryAsync(
                    k => k.Id,
                    v => string.IsNullOrWhiteSpace(v.NombreCompleto) ? (v.Email ?? v.Id) : v.NombreCompleto
                );
            ViewBag.VendedorNombres = vendorNames;

            decimal envioTotal = 0m;
            Dictionary<string, decimal> envioPorVend = new();
            List<string> envioMsgs = new();

            if (!string.IsNullOrWhiteSpace(prov) && vendorIds.Count > 0)
            {
                var res = await _enviosCarrito.CalcularAsync(vendorIds, prov!, city);
                envioTotal = res.TotalEnvio;
                envioPorVend = res.PorVendedor;
                envioMsgs = res.Mensajes;
            }

            ViewBag.EnvioTotal = envioTotal;
            ViewBag.EnvioPorVendedor = envioPorVend;
            ViewBag.EnvioMensajes = envioMsgs;

            return View(detalles);
        }

        // Mini resumen para navbar/widget
        [HttpGet]
        [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> CartInfo()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
                return Json(new { count = 0, subtotal = 0m });

            var carrito = await _carritoService.GetByUsuarioIdAsync(usuario.Id);
            if (carrito == null)
                return Json(new { count = 0, subtotal = 0m });

            var detalles = await _carritoService.LoadCartDetails(carrito.CarritoID);
            var r = Resumen(detalles);
            return Json(new { count = r.count, subtotal = r.total });
        }

        // ======================= MUTACIONES (AJAX) =======================

        // Agregar (opcional, si tu flujo agrega desde otro controlador puedes ignorarlo)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AgregarAlCarrito(int productoId, int cantidad = 1, int? productoVarianteId = null)
        {
            if (cantidad < 1) cantidad = 1;

            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
                return EsAjax() ? Json(new { ok = false, error = "Debes iniciar sesión." }) : RedirectToAction(nameof(VerCarrito));

            var producto = await _context.Productos.FirstOrDefaultAsync(p => p.ProductoID == productoId);
            if (producto == null)
                return EsAjax() ? Json(new { ok = false, error = "Producto no encontrado." }) : RedirectToAction(nameof(VerCarrito));

            try
            {
                var okAdd = await _carritoService.AnadirProducto(producto, usuario, cantidad, productoVarianteId);
                if (!okAdd)
                    return EsAjax() ? Json(new { ok = false, error = "No se pudo agregar al carrito." }) : RedirectToAction(nameof(VerCarrito));

                var carrito = await _carritoService.GetByUsuarioIdAsync(usuario.Id);
                var detalles = carrito != null ? await _carritoService.LoadCartDetails(carrito.CarritoID) : new List<CarritoDetalle>();
                var r = Resumen(detalles);

                if (EsAjax()) return Json(new { ok = true, count = r.count, total = r.total });

                TempData["MensajeExito"] = "Producto agregado al carrito correctamente.";
                return RedirectToAction(nameof(VerCarrito));
            }
            catch (Exception ex)
            {
                if (EsAjax()) return Json(new { ok = false, error = ex.Message });
                TempData["MensajeError"] = ex.Message;
                return RedirectToAction(nameof(VerCarrito));
            }
        }

        // Actualizar cantidad (por CarritoDetalleID)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarCarrito(int carritoDetalleId, int cantidad, int? productoId = null)
        {
            var (ok, lineSubtotal, error) = await _carritoService.ActualizarCantidadAsync(carritoDetalleId, cantidad);
            if (!ok)
                return Json(new { ok = false, error });

            var usuario = await _userManager.GetUserAsync(User);
            var carrito = usuario != null ? await _carritoService.GetByUsuarioIdAsync(usuario.Id) : null;
            var detalles = carrito != null ? await _carritoService.LoadCartDetails(carrito.CarritoID) : new List<CarritoDetalle>();
            var r = Resumen(detalles);

            return Json(new { ok = true, count = r.count, total = r.total, lineSubtotal });
        }

        // Eliminar ítem (por CarritoDetalleID)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarDelCarrito(int carritoDetalleId, int? productoId = null)
        {
            var ok = await _carritoService.BorrarProductoCarrito(carritoDetalleId);

            var usuario = await _userManager.GetUserAsync(User);
            var carrito = usuario != null ? await _carritoService.GetByUsuarioIdAsync(usuario.Id) : null;
            var detalles = carrito != null ? await _carritoService.LoadCartDetails(carrito.CarritoID) : new List<CarritoDetalle>();
            var r = Resumen(detalles);

            if (EsAjax()) return Json(new { ok, count = r.count, total = r.total });
            TempData["MensajeExito"] = ok ? "Producto eliminado del carrito." : "No se pudo eliminar el producto.";
            return RedirectToAction(nameof(VerCarrito));
        }

        // ======================= CUPONES =======================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AplicarCupon(string codigoCupon)
        {
            var ahora = DateTime.UtcNow;
            var cupon = _context.Promociones
                .AsNoTracking()
                .FirstOrDefault(p => p.CodigoCupon == codigoCupon &&
                                     (p.FechaInicio == null || p.FechaInicio <= ahora) &&
                                     (p.FechaFin == null || p.FechaFin >= ahora));

            if (cupon != null)
            {
                GuardarCupon(cupon);
                if (EsAjax()) return Json(new { ok = true, descuento = cupon.Descuento, codigo = cupon.CodigoCupon });

                TempData["CuponAplicado"] = $"Cupón '{cupon.CodigoCupon}' aplicado: {cupon.Descuento:C}.";
            }
            else
            {
                HttpContext.Session.Remove("Cupon");
                if (EsAjax()) return Json(new { ok = false, error = "Cupón inválido o expirado." });

                TempData["CuponError"] = "El cupón ingresado no es válido o ha expirado.";
            }

            return RedirectToAction(nameof(VerCarrito));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult QuitarCupon()
        {
            HttpContext.Session.Remove("Cupon");
            if (EsAjax()) return Json(new { ok = true });

            TempData["MensajeExito"] = "Cupón eliminado correctamente.";
            return RedirectToAction(nameof(VerCarrito));
        }

        // ======================= CHECKOUT =======================
        // Nota: el total de la venta se calcula en el servicio con los precios de línea.
        // El cupón actualmente NO se descuenta en la venta (solo UI). Si quieres integrarlo en DB, lo hacemos en el siguiente paso.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmarCompra(string? EnvioProvincia, string? EnvioCiudad, decimal? EnvioPrecio)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                TempData["MensajeError"] = "Debes iniciar sesión para confirmar tu compra.";
                return RedirectToAction(nameof(VerCarrito));
            }

            var carrito = await _carritoService.GetByUsuarioIdAsync(usuario.Id);
            if (carrito == null)
            {
                TempData["MensajeError"] = "Tu carrito está vacío.";
                return RedirectToAction(nameof(VerCarrito));
            }

            var detalles = await _carritoService.LoadCartDetails(carrito.CarritoID);
            if (!detalles.Any())
            {
                TempData["MensajeError"] = "Tu carrito está vacío.";
                return RedirectToAction(nameof(VerCarrito));
            }

            try
            {
                var ok = await _carritoService.ProcessCartDetails(carrito.CarritoID, usuario);
                if (!ok)
                {
                    TempData["MensajeError"] = "No se pudo procesar la compra.";
                    return RedirectToAction(nameof(VerCarrito));
                }

                // Borra cupón de la sesión (UI) y redirige a la última venta del usuario
                HttpContext.Session.Remove("Cupon");

                var ventaId = await _context.Ventas
                    .Where(v => v.UsuarioId == usuario.Id)
                    .OrderByDescending(v => v.FechaVenta)
                    .Select(v => v.VentaID)
                    .FirstOrDefaultAsync();

                TempData["MensajeExito"] = "¡Gracias por tu compra!";
                return RedirectToAction(nameof(ConfirmacionCompra), new { id = ventaId });
            }
            catch (Exception ex)
            {
                TempData["MensajeError"] = ex.Message;
                return RedirectToAction(nameof(VerCarrito));
            }
        }

        [HttpGet]
        public IActionResult ConfirmacionCompra(int id) => View(id);
    }
}
