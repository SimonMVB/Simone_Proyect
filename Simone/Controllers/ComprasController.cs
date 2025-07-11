using Microsoft.AspNetCore.Mvc;
using Simone.Models;
using Simone.Data;
using Microsoft.EntityFrameworkCore;
using Simone.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading.Tasks;

namespace Simone.Controllers
{
    public class ComprasController : Controller
    {
        private readonly TiendaDbContext _context;
        private readonly ProductosService _productos;
        private readonly CategoriasService _categorias;
        private readonly SubcategoriasService _subcategorias;
        private readonly CarritoService _carrito;
        private readonly UserManager<Usuario> _userManager;
        private readonly ILogger<ComprasController> _logger;

        public ComprasController(
            UserManager<Usuario> user,
            TiendaDbContext context,
            ProductosService productos,
            CategoriasService categorias,
            SubcategoriasService subcategorias,
            CarritoService carrito,
            ILogger<ComprasController> logger)
        {
            _context = context;
            _productos = productos;
            _categorias = categorias;
            _subcategorias = subcategorias;
            _userManager = user;
            _logger = logger;
            _carrito = carrito;
        }

        /// <summary>
        /// Acción para mostrar el catálogo de productos con opciones de paginación y filtrado por categorías y subcategorías.
        /// </summary>
        /// <param name="categoriaID">ID de la categoría seleccionada (opcional)</param>
        /// <param name="subcategoriaIDs">Array de IDs de subcategorías seleccionadas (opcional)</param>
        /// <param name="pageNumber">Número de página para la paginación</param>
        /// <param name="pageSize">Cantidad de productos por página</param>
        /// <returns>Vista del catálogo con productos filtrados y paginados</returns>
        [HttpGet]
        public async Task<IActionResult> Catalogo(int? categoriaID, int[] subcategoriaIDs, int pageNumber = 1, int pageSize = 20)
        {
            var categorias = await _categorias.GetAllAsync();
            var subcategorias = categoriaID.HasValue
                ? await _subcategorias.GetByCategoriaIdAsync(categoriaID.Value)
                : new List<Subcategorias>();

            IQueryable<Producto> query = _context.Productos;

            if (categoriaID.HasValue)
                query = query.Where(p => p.CategoriaID == categoriaID.Value);

            if (subcategoriaIDs != null && subcategoriaIDs.Length > 0)
                query = query.Where(p => subcategoriaIDs.Contains(p.SubcategoriaID));

            var totalProducts = await query.CountAsync();

            var productos = await query
                .OrderBy(p => p.Nombre) // Ordena por nombre del producto
                .Skip((pageNumber - 1) * pageSize) // Salta los productos de las páginas anteriores
                .Take(pageSize) // Toma solo los productos de la página actual
                .ToListAsync();

            var model = new CatalogoViewModel
            {
                Categorias = categorias,
                SelectedCategoriaID = categoriaID,
                Subcategorias = subcategorias,
                SelectedSubcategoriaIDs = subcategoriaIDs?.ToList() ?? new List<int>(),
                Productos = productos,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalProducts = totalProducts
            };

            return View(model);
        }

        /// <summary>
        /// Acción para mostrar los detalles de un producto específico.
        /// </summary>
        /// <param name="productoID">ID del producto a mostrar</param>
        /// <returns>Vista con los detalles del producto</returns>
        [HttpGet]
        public async Task<IActionResult> VerProducto(int productoID)
        {
            var producto = await _productos.GetByIdAsync(productoID);
            if (producto != null)
            {
                ViewBag.Producto = producto;
                return View();
            }
            else
            {
                // Mejorar la respuesta cuando el producto no existe
                TempData["MensajeError"] = "El producto solicitado no está disponible.";
                return RedirectToAction("Catalogo", "Compras"); // Redirigir al catálogo si el producto no existe
            }
        }

        /// <summary>
        /// Acción para añadir un producto al carrito del usuario.
        /// </summary>
        /// <param name="model">Modelo que contiene los detalles del producto y la cantidad a añadir</param>
        /// <returns>Redirige a la última página o al catálogo si el producto se añade correctamente</returns>
        [HttpPost]
        public async Task<IActionResult> AnadirAlCarrito(CatalogoViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                TempData["MensajeError"] = "Debes iniciar sesión para añadir productos al carrito.";
                return RedirectToAction("Login", "Cuenta"); // Redirige al login si el usuario no está autenticado
            }

            var producto = await _productos.GetByIdAsync(model.ProductoID);
            if (producto == null)
            {
                TempData["MensajeError"] = "Producto no encontrado";
                return View("Invalido"); // Si el producto no se encuentra
            }

            // Obtiene la cantidad existente de este producto en el carrito del usuario
            var carrito = await _carrito.GetByClienteIdAsync(user.Id);
            var carritoDetalles = await _carrito.LoadCartDetails(carrito.CarritoID);

            // Calcula la cantidad total del producto en el carrito
            var cantidadEnCarrito = carritoDetalles
                .Where(c => c.ProductoID == model.ProductoID)
                .Sum(c => c.Cantidad);

            // Valida si la cantidad solicitada excede el stock disponible
            if (cantidadEnCarrito + model.Cantidad > producto.Stock)
            {
                TempData["MensajeError"] = "La cantidad de producto requerido supera el stock disponible.";
                return RedirectToAction("Catalogo", "Compras"); // Redirige al catálogo si excede el stock
            }

            // Si el producto se añade correctamente al carrito
            var success = await _carrito.AnadirProducto(producto, user, model.Cantidad);
            if (success)
            {
                TempData["MensajeExito"] = "Producto añadido al carrito con éxito.";
                return RedirectToAction("Catalogo", "Compras"); // Redirige al catálogo si el producto se añade correctamente
            }
            else
            {
                TempData["MensajeError"] = "No se pudo añadir el producto al carrito";
                return View("Invalido"); // Maneja el fallo
            }
        }

        /// <summary>
        /// Acción para eliminar un artículo del carrito.
        /// </summary>
        /// <param name="carritoDetalleID">ID del artículo en el carrito a eliminar</param>
        /// <returns>Redirige a la vista del catálogo después de la eliminación</returns>
        [HttpPost]
        public async Task<IActionResult> EliminarArticulo(int carritoDetalleID)
        {
            await _carrito.BorrarProductoCarrito(carritoDetalleID); // Elimina el artículo del carrito
            TempData["MensajeExito"] = "Producto eliminado del carrito con éxito.";
            return RedirectToAction("Catalogo", "Compras"); // Redirige a la vista del catálogo
        }

        /// <summary>
        /// Acción para mostrar el resumen del carrito.
        /// </summary>
        /// <returns>Vista con los detalles del carrito y el total</returns>
        [HttpGet]
        public async Task<IActionResult> Resumen()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                TempData["MensajeError"] = "Debes iniciar sesión para ver el resumen de tu carrito.";
                return RedirectToAction("Login", "Cuenta"); // Redirige a login si el usuario no está autenticado
            }

            var carrito = await _carrito.GetByClienteIdAsync(user.Id);
            var carritoDetalles = await _carrito.LoadCartDetails(carrito.CarritoID);
            decimal totalCompra = carritoDetalles.Sum(cd => cd.Precio * cd.Cantidad); // Calcula el total de la compra

            var address = user.Direccion; // Obtiene la dirección del usuario

            // Establece datos para ser mostrados en la vista
            ViewBag.CarritoDetalles = carritoDetalles;
            ViewBag.TotalCompra = totalCompra;
            ViewBag.HasAddress = !string.IsNullOrEmpty(address); // Verifica si el usuario tiene dirección

            return View(user); // Devuelve la vista con los datos del usuario
        }

        /// <summary>
        /// Acción para confirmar la compra y finalizar el pedido.
        /// </summary>
        /// <returns>Redirige a una página de éxito si la compra es exitosa</returns>
        [HttpPost]
        public async Task<IActionResult> ConfirmarCompra()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                TempData["MensajeError"] = "Debes iniciar sesión para confirmar la compra.";
                return RedirectToAction("Login", "Cuenta"); // Redirige a login si el usuario no está autenticado
            }

            var carrito = await _carrito.GetByClienteIdAsync(user.Id);
            var carritoDetalles = await _carrito.LoadCartDetails(carrito.CarritoID);

            // Verifica si hay artículos en el carrito
            if (!carritoDetalles.Any())
            {
                TempData["MensajeError"] = "Tu carrito está vacío.";
                return RedirectToAction("CompraError", "Compras"); // Si no hay artículos, redirige a la página de error
            }

            // Verifica si el usuario tiene dirección
            if (string.IsNullOrEmpty(user.Direccion))
            {
                ViewBag.HasAddress = false;
                return View("Resumen"); // Si no tiene dirección, se queda en la página de resumen
            }

            try
            {
                var result = await _carrito.ProcessCartDetails(carrito.CarritoID, user); // Procesa los detalles del carrito
                if (result)
                {
                    await _carrito.AddAsync(user); // Crea un nuevo carrito para el usuario
                }

                TempData["MensajeExito"] = "Compra realizada con éxito.";
                return RedirectToAction("CompraExito", "Compras"); // Redirige a la página de éxito
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error al procesar el pedido: {ex.Message}"); // Registra cualquier error
                TempData["MensajeError"] = "Hubo un error al procesar tu pedido.";
                return RedirectToAction("CompraError", "Compras"); // Redirige a la página de error en caso de fallo
            }
        }
    }
}
