using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Simone.Data;
using Simone.Extensions;
using Simone.Models;

namespace Simone.Controllers
{
    public class CarritoController : Controller
    {
        private readonly TiendaDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly UserManager<Usuario> _userManager;

        public CarritoController(
            TiendaDbContext context,
            IHttpContextAccessor httpContextAccessor,
            UserManager<Usuario> user)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _userManager = user;
        }

        // -------- helpers --------
        private bool EsAjax() =>
            string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

        private List<CarritoDetalle> ObtenerCarrito()
        {
            return _httpContextAccessor.HttpContext!.Session
                .GetObjectFromJson<List<CarritoDetalle>>("Carrito")
                ?? new List<CarritoDetalle>();
        }

        private void GuardarCarrito(List<CarritoDetalle> carrito)
        {
            // Nunca persistimos navegación Producto en sesión
            foreach (var it in carrito) it.Producto = null;
            _httpContextAccessor.HttpContext!.Session.SetObjectAsJson("Carrito", carrito);
        }

        private (int count, decimal total) Resumen(List<CarritoDetalle> carrito)
            => (carrito.Sum(c => c.Cantidad), carrito.Sum(c => c.Precio * c.Cantidad));

        private Promocion? ObtenerCupon()
            => _httpContextAccessor.HttpContext!.Session.GetObjectFromJson<Promocion>("Cupon");

        private void GuardarCupon(Promocion cupon)
            => _httpContextAccessor.HttpContext!.Session.SetObjectAsJson("Cupon", cupon);

        // -------- acciones --------

        // Ver carrito (rehidrata Producto SOLO para la vista)
        [HttpGet]
        public async Task<IActionResult> VerCarrito()
        {
            var carrito = ObtenerCarrito();

            // Rehidratar Producto SOLO para la vista (no lo guardamos en sesión)
            var ids = carrito.Select(c => c.ProductoID).Distinct().ToList();

            var productosDict = await _context.Productos
                .AsNoTracking()
                .Where(p => ids.Contains(p.ProductoID))
                .ToDictionaryAsync(p => p.ProductoID, p => p);

            foreach (var it in carrito)
            {
                if (productosDict.TryGetValue(it.ProductoID, out var prod))
                    it.Producto = prod; // ← asignamos después de sacar a 'prod'
            }

            var cupon = ObtenerCupon();
            ViewBag.Descuento = cupon?.Descuento ?? 0m;
            ViewBag.CodigoCupon = cupon?.CodigoCupon;

            return View(carrito);
        }


        // Mini resumen para navbar/widget
        [HttpGet]
        [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult CartInfo()
        {
            var r = Resumen(ObtenerCarrito());
            return Json(new { count = r.count, subtotal = r.total });
        }

        // Agregar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AgregarAlCarrito(int productoId, int cantidad = 1)
        {
            if (cantidad < 1) cantidad = 1;

            var carrito = ObtenerCarrito();
            var producto = await _context.Productos
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.ProductoID == productoId);

            if (producto == null)
                return EsAjax()
                    ? Json(new { ok = false, error = "Producto no encontrado." })
                    : RedirectToAction(nameof(VerCarrito));

            var item = carrito.FirstOrDefault(c => c.ProductoID == productoId);
            if (item == null)
                carrito.Add(new CarritoDetalle { ProductoID = productoId, Cantidad = cantidad, Precio = producto.PrecioVenta });
            else
                item.Cantidad += cantidad;

            GuardarCarrito(carrito);

            var r = Resumen(carrito);
            if (EsAjax()) return Json(new { ok = true, count = r.count, total = r.total });

            TempData["MensajeExito"] = "Producto agregado al carrito correctamente.";
            return RedirectToAction(nameof(VerCarrito));
        }

        // Actualizar cantidad
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ActualizarCarrito(int productoId, int cantidad)
        {
            var carrito = ObtenerCarrito();
            var item = carrito.FirstOrDefault(c => c.ProductoID == productoId);

            if (item == null)
                return EsAjax()
                    ? Json(new { ok = false, error = "El producto no está en el carrito." })
                    : RedirectToAction(nameof(VerCarrito));

            if (cantidad < 1) cantidad = 1;
            item.Cantidad = cantidad;
            GuardarCarrito(carrito);

            var r = Resumen(carrito);
            if (EsAjax()) return Json(new { ok = true, count = r.count, total = r.total, subtotal = item.Precio * item.Cantidad });

            TempData["MensajeExito"] = "Cantidad actualizada.";
            return RedirectToAction(nameof(VerCarrito));
        }

        // Eliminar producto
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EliminarDelCarrito(int productoId)
        {
            var carrito = ObtenerCarrito();
            var item = carrito.FirstOrDefault(c => c.ProductoID == productoId);
            if (item != null)
            {
                carrito.Remove(item);
                GuardarCarrito(carrito);
            }

            var r = Resumen(carrito);
            if (EsAjax()) return Json(new { ok = true, count = r.count, total = r.total });

            TempData["MensajeExito"] = "Producto eliminado del carrito.";
            return RedirectToAction(nameof(VerCarrito));
        }

        // Cupón
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

                TempData["CuponAplicado"] = $"Cupón '{cupon.CodigoCupon}' aplicado correctamente: {cupon.Descuento:C} de descuento.";
            }
            else
            {
                _httpContextAccessor.HttpContext!.Session.Remove("Cupon");
                if (EsAjax()) return Json(new { ok = false, error = "Cupón inválido o expirado." });

                TempData["CuponError"] = "El cupón ingresado no es válido o ha expirado.";
            }

            return RedirectToAction(nameof(VerCarrito));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult QuitarCupon()
        {
            _httpContextAccessor.HttpContext!.Session.Remove("Cupon");
            if (EsAjax()) return Json(new { ok = true });

            TempData["MensajeExito"] = "Cupón eliminado correctamente.";
            return RedirectToAction(nameof(VerCarrito));
        }

        // Confirmar compra (sin cambios de negocio)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmarCompra(int carritoID)
        {
            var carrito = ObtenerCarrito();
            if (carrito == null || !carrito.Any())
            {
                TempData["MensajeError"] = "Tu carrito está vacío.";
                return RedirectToAction(nameof(VerCarrito));
            }

            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                TempData["MensajeError"] = "Debes iniciar sesión para confirmar tu compra.";
                return RedirectToAction(nameof(VerCarrito));
            }

            var cupon = ObtenerCupon();
            decimal total = carrito.Sum(c => c.Total);
            decimal descuento = cupon?.Descuento ?? 0m;
            decimal totalFinal = Math.Max(0m, total - descuento);

            var venta = new Ventas
            {
                UsuarioId = usuario.Id,
                FechaVenta = DateTime.UtcNow,
                Estado = "Completada",
                MetodoPago = "Transferencia",
                Total = totalFinal
            };

            _context.Ventas.Add(venta);
            await _context.SaveChangesAsync();

            var detalles = carrito.Select(item => new DetalleVentas
            {
                VentaID = venta.VentaID,
                ProductoID = item.ProductoID,
                Cantidad = item.Cantidad,
                PrecioUnitario = item.Precio,
                Descuento = 0,
                Subtotal = item.Total,
                FechaCreacion = DateTime.UtcNow
            });
            await _context.DetalleVentas.AddRangeAsync(detalles);
            await _context.SaveChangesAsync();

            _httpContextAccessor.HttpContext!.Session.Remove("Carrito");
            _httpContextAccessor.HttpContext!.Session.Remove("Cupon");

            TempData["MensajeExito"] = "¡Gracias por tu compra!";
            return RedirectToAction("ConfirmacionCompra", new { id = venta.VentaID });
        }

        public IActionResult ConfirmacionCompra(int id) => View(id);
    }
}
