using Microsoft.AspNetCore.Mvc;
using Simone.Models;
using Simone.Data;
using Microsoft.EntityFrameworkCore;
using Simone.Extensions;

namespace Simone.Controllers
{
    public class CarritoController : Controller
    {
        private readonly TiendaDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CarritoController(TiendaDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        // Obtener carrito de la sesión
        private List<CarritoDetalle> ObtenerCarrito()
        {
            return _httpContextAccessor.HttpContext.Session.GetObjectFromJson<List<CarritoDetalle>>("Carrito") ?? new List<CarritoDetalle>();
        }

        // Guardar carrito en la sesión
        private void GuardarCarrito(List<CarritoDetalle> carrito)
        {
            _httpContextAccessor.HttpContext.Session.SetObjectAsJson("Carrito", carrito);
        }

        // Obtener cupón actual de la sesión
        private Promocion? ObtenerCupon()
        {
            return _httpContextAccessor.HttpContext.Session.GetObjectFromJson<Promocion>("Cupon");
        }

        // Guardar cupón en la sesión
        private void GuardarCupon(Promocion cupon)
        {
            _httpContextAccessor.HttpContext.Session.SetObjectAsJson("Cupon", cupon);
        }

        // Eliminar cupón de la sesión
        [HttpPost]
        public IActionResult QuitarCupon()
        {
            _httpContextAccessor.HttpContext.Session.Remove("Cupon");
            TempData["MensajeExito"] = "Cupón eliminado correctamente.";
            return RedirectToAction("VerCarrito");
        }

        // Agregar producto al carrito
        public IActionResult AgregarAlCarrito(int productoId, int cantidad)
        {
            var carrito = ObtenerCarrito();
            var producto = _context.Productos.FirstOrDefault(p => p.ProductoID == productoId);

            if (producto != null)
            {
                var item = carrito.FirstOrDefault(c => c.ProductoID == productoId);
                if (item == null)
                {
                    carrito.Add(new CarritoDetalle
                    {
                        ProductoID = productoId,
                        Producto = producto,
                        Cantidad = cantidad,
                        Precio = producto.PrecioVenta
                    });
                }
                else
                {
                    item.Cantidad += cantidad;
                }

                GuardarCarrito(carrito);
                TempData["MensajeExito"] = "Producto agregado al carrito correctamente.";
            }

            return RedirectToAction("VerCarrito");
        }

        // Ver carrito
        public IActionResult VerCarrito()
        {
            var carrito = ObtenerCarrito();
            var cupon = ObtenerCupon();

            ViewBag.Descuento = cupon?.Descuento ?? 0;
            ViewBag.CodigoCupon = cupon?.CodigoCupon;

            return View(carrito);
        }

        // Eliminar producto del carrito
        public IActionResult EliminarDelCarrito(int productoId)
        {
            var carrito = ObtenerCarrito();
            var item = carrito.FirstOrDefault(c => c.ProductoID == productoId);

            if (item != null)
            {
                carrito.Remove(item);
                GuardarCarrito(carrito);
                TempData["MensajeExito"] = "Producto eliminado del carrito.";
            }

            return RedirectToAction("VerCarrito");
        }

        // Actualizar cantidad
        [HttpPost]
        public IActionResult ActualizarCarrito(int productoId, int cantidad)
        {
            if (cantidad < 1)
            {
                TempData["MensajeError"] = "La cantidad debe ser al menos 1.";
                return RedirectToAction("VerCarrito");
            }

            var carrito = ObtenerCarrito();
            var item = carrito.FirstOrDefault(c => c.ProductoID == productoId);

            if (item != null)
            {
                item.Cantidad = cantidad;
                GuardarCarrito(carrito);
                TempData["MensajeExito"] = "Cantidad actualizada.";
            }

            return RedirectToAction("VerCarrito");
        }

        // Aplicar cupón
        [HttpPost]
        public IActionResult AplicarCupon(string codigoCupon)
        {
            var cupon = _context.Promociones
                .FirstOrDefault(p => p.CodigoCupon == codigoCupon &&
                                     (p.FechaInicio == null || p.FechaInicio <= DateTime.Now) &&
                                     (p.FechaFin == null || p.FechaFin >= DateTime.Now));

            if (cupon != null)
            {
                GuardarCupon(cupon);
                TempData["CuponAplicado"] = $"Cupón '{cupon.CodigoCupon}' aplicado correctamente: {cupon.Descuento:C} de descuento.";
            }
            else
            {
                _httpContextAccessor.HttpContext.Session.Remove("Cupon");
                TempData["CuponError"] = "El cupón ingresado no es válido o ha expirado.";
            }

            return RedirectToAction("VerCarrito");
        }

        // Confirmar compra
        [HttpPost]
        public async Task<IActionResult> ConfirmarCompra()
        {
            var carrito = ObtenerCarrito();
            if (carrito == null || !carrito.Any())
            {
                TempData["MensajeError"] = "Tu carrito está vacío.";
                return RedirectToAction("VerCarrito");
            }

            var userId = User.Identity?.Name;
            var cliente = await _context.Clientes.FirstOrDefaultAsync(c => c.Email == userId);

            if (cliente == null)
            {
                TempData["MensajeError"] = "Debes iniciar sesión para confirmar tu compra.";
                return RedirectToAction("VerCarrito");
            }

            var cupon = ObtenerCupon();
            decimal total = carrito.Sum(c => c.Total);
            decimal descuento = cupon?.Descuento ?? 0;
            decimal totalFinal = total - descuento;

            var venta = new Ventas
            {
                ClienteID = cliente.ClienteID,
                FechaVenta = DateTime.Now,
                Total = totalFinal
            };

            _context.Ventas.Add(venta);
            await _context.SaveChangesAsync();

            foreach (var item in carrito)
            {
                _context.DetalleVentas.Add(new DetalleVentas
                {
                    VentaID = venta.VentaID,
                    ProductoID = item.ProductoID,
                    Cantidad = item.Cantidad,
                    PrecioUnitario = item.Precio,
                    Subtotal = item.Total
                });
            }

            if (cupon != null)
            {
                _context.CuponesUsados.Add(new CuponesUsados
                {
                    ClienteID = cliente.ClienteID,
                    PromocionID = cupon.PromocionID,
                    FechaUso = DateTime.Now
                });
            }

            await _context.SaveChangesAsync();

            _httpContextAccessor.HttpContext.Session.Remove("Carrito");
            _httpContextAccessor.HttpContext.Session.Remove("Cupon");

            TempData["MensajeExito"] = "¡Gracias por tu compra!";
            return RedirectToAction("ConfirmacionCompra", new { id = venta.VentaID });
        }

        // Página de confirmación
        public IActionResult ConfirmacionCompra(int id)
        {
            return View(id);
        }
    }
}
