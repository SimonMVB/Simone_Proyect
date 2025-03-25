using Microsoft.AspNetCore.Mvc;
using Simone.Models;
using Simone.Data;
using Microsoft.EntityFrameworkCore;

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
            var carrito = _httpContextAccessor.HttpContext.Session.GetObjectFromJson<List<CarritoDetalle>>("Carrito");
            if (carrito == null)
            {
                carrito = new List<CarritoDetalle>();
            }
            return carrito;
        }

        // Guardar carrito en la sesión
        private void GuardarCarrito(List<CarritoDetalle> carrito)
        {
            _httpContextAccessor.HttpContext.Session.SetObjectAsJson("Carrito", carrito);
        }

        // Agregar producto al carrito
        public IActionResult AgregarAlCarrito(int productoId, int cantidad)
        {
            var carrito = ObtenerCarrito();
            var producto = _context.Productos.FirstOrDefault(p => p.ProductoID == productoId);

            if (producto != null)
            {
                var carritoItem = carrito.FirstOrDefault(c => c.ProductoID == productoId);
                if (carritoItem == null)
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
                    carritoItem.Cantidad += cantidad;  // Si ya existe, solo aumentar la cantidad
                }
            }

            GuardarCarrito(carrito);
            return RedirectToAction("VerCarrito");
        }

        // Ver carrito
        public IActionResult VerCarrito()
        {
            var carrito = ObtenerCarrito();
            return View(carrito);
        }

        // Eliminar un producto del carrito
        public IActionResult EliminarDelCarrito(int productoId)
        {
            var carrito = ObtenerCarrito();
            var carritoItem = carrito.FirstOrDefault(c => c.ProductoID == productoId);

            if (carritoItem != null)
            {
                carrito.Remove(carritoItem);
            }

            GuardarCarrito(carrito);
            return RedirectToAction("VerCarrito");
        }

        // Actualizar cantidad del carrito
        [HttpPost]
        public IActionResult ActualizarCarrito(int productoId, int cantidad)
        {
            var carrito = ObtenerCarrito();
            var carritoItem = carrito.FirstOrDefault(c => c.ProductoID == productoId);

            if (carritoItem != null)
            {
                carritoItem.Cantidad = cantidad;
            }

            GuardarCarrito(carrito);
            return RedirectToAction("VerCarrito");
        }
    }
}
