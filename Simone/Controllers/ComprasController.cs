using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Simone.Data;
using Simone.Extensions;
using Simone.Models;
using Simone.Services;
using System.Security.Claims;
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
                TotalProducts = totalProducts,
                ProductoIDsFavoritos = new List<int>()

            };
            if (User.Identity.IsAuthenticated)
            {
                var userId = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
                model.ProductoIDsFavoritos = await _context.Favoritos
                    .Where(f => f.UsuarioId == userId)
                    .Select(f => f.ProductoId)
                    .ToListAsync();
            }
            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcesarCompra(CompraViewModel model)
        {
            // Validación del modelo (por si usas campos adicionales)
            if (!ModelState.IsValid)
            {
                // Vuelve a cargar los datos necesarios para la vista Resumen
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    TempData["MensajeError"] = "Debes iniciar sesión para confirmar la compra.";
                    return RedirectToAction("Login", "Cuenta");
                }

                var carrito = await _carrito.GetByClienteIdAsync(user.Id);
                var carritoDetalles = await _carrito.LoadCartDetails(carrito.CarritoID);
                decimal totalCompra = carritoDetalles.Sum(cd => cd.Precio * cd.Cantidad);

                ViewBag.CarritoDetalles = carritoDetalles;
                ViewBag.TotalCompra = totalCompra;
                ViewBag.HasAddress = !string.IsNullOrEmpty(user.Direccion);

                // Devuelve la vista resumen con los datos cargados y el modelo actual
                return View("Resumen", user);
            }

            // Usuario autenticado
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                TempData["MensajeError"] = "Debes iniciar sesión para confirmar la compra.";
                return RedirectToAction("Login", "Cuenta");
            }

            // Verifica el carrito
            var carritoUser = await _carrito.GetByClienteIdAsync(currentUser.Id);
            var carritoDetallesUser = await _carrito.LoadCartDetails(carritoUser.CarritoID);
            if (!carritoDetallesUser.Any())
            {
                TempData["MensajeError"] = "Tu carrito está vacío.";
                return RedirectToAction("CompraError", "Compras");
            }

            // Verifica que tenga dirección
            if (string.IsNullOrEmpty(currentUser.Direccion))
            {
                TempData["MensajeError"] = "Debes registrar una dirección de envío antes de finalizar la compra.";
                return RedirectToAction("Resumen", "Compras");
            }

            try
            {
                // Procesa el carrito y registra la compra (ajusta según tu lógica)
                var result = await _carrito.ProcessCartDetails(carritoUser.CarritoID, currentUser);
                if (result)
                {
                    await _carrito.AddAsync(currentUser); // Crea un nuevo carrito vacío después de la compra
                }

                TempData["MensajeExito"] = "Compra realizada con éxito.";
                return RedirectToAction("CompraExito", "Compras");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error al procesar el pedido: {ex.Message}");
                TempData["MensajeError"] = "Hubo un error al procesar tu pedido.";
                return RedirectToAction("CompraError", "Compras");
            }
        }





        public IActionResult CompraError()
        {
            return View();
        }

        // Acción que muestra la página de éxito tras finalizar la compra
        public IActionResult CompraExito()
        {
            return View();
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
        /// Añadir un producto al carrito (soporta AJAX y POST normal).
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AnadirAlCarrito([Bind("ProductoID,Cantidad")] CatalogoViewModel model, CancellationToken ct = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                if (EsAjax()) return Json(new { ok = false, error = "Debes iniciar sesión." });
                TempData["MensajeError"] = "Debes iniciar sesión para añadir productos al carrito.";
                return RedirectToAction("Login", "Cuenta");
            }

            // Normaliza cantidad
            if (model.Cantidad <= 0) model.Cantidad = 1;

            var producto = await _productos.GetByIdAsync(model.ProductoID);
            if (producto == null)
            {
                if (EsAjax()) return Json(new { ok = false, error = "Producto no encontrado." });
                TempData["MensajeError"] = "Producto no encontrado.";
                return RedirectToReferrerOr("Catalogo");
            }

            // Asegura carrito
            var carrito = await _carrito.GetByClienteIdAsync(user.Id);
            if (carrito == null)
            {
                await _carrito.AddAsync(user);
                carrito = await _carrito.GetByClienteIdAsync(user.Id);
            }

            // Carga detalles actuales
            var detalles = await _carrito.LoadCartDetails(carrito.CarritoID);
            var cantidadEnCarrito = detalles.Where(c => c.ProductoID == model.ProductoID).Sum(c => c.Cantidad);

            // Valida stock total solicitado (lo que ya hay + lo que piden)
            if (cantidadEnCarrito + model.Cantidad > producto.Stock)
            {
                var msg = "La cantidad solicitada supera el stock disponible.";
                if (EsAjax()) return Json(new { ok = false, error = msg, stock = producto.Stock, enCarrito = cantidadEnCarrito });
                TempData["MensajeError"] = msg;
                return RedirectToReferrerOr("Catalogo");
            }

            // Añadir
            var ok = await _carrito.AnadirProducto(producto, user, model.Cantidad);
            if (!ok)
            {
                if (EsAjax()) return Json(new { ok = false, error = "No se pudo añadir al carrito." });
                TempData["MensajeError"] = "No se pudo añadir el producto al carrito.";
                return RedirectToReferrerOr("Catalogo");
            }

            // Nuevo contador de ítems
            var count = await _context.CarritoDetalle
                .Where(cd => cd.CarritoID == carrito.CarritoID)
                .SumAsync(cd => cd.Cantidad, ct);

            if (EsAjax()) return Json(new { ok = true, count });

            TempData["MensajeExito"] = "Producto añadido al carrito con éxito.";
            return RedirectToReferrerOr("Catalogo");
        }

        // Helpers en el mismo controlador
        private bool EsAjax() =>
            string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

        private IActionResult RedirectToReferrerOr(string action, string controller = "Compras")
        {
            var referer = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrWhiteSpace(referer)) return Redirect(referer);
            return RedirectToAction(action, controller);
        }

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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmarCompra()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                TempData["MensajeError"] = "Debes iniciar sesión para confirmar la compra.";
                return RedirectToAction("Login", "Cuenta");
            }

            var carrito = await _carrito.GetByClienteIdAsync(user.Id);
            if (carrito == null)
            {
                TempData["MensajeError"] = "Tu carrito está vacío.";
                return RedirectToAction("Resumen");
            }

            var carritoDetalles = await _carrito.LoadCartDetails(carrito.CarritoID);
            if (carritoDetalles == null || !carritoDetalles.Any())
            {
                TempData["MensajeError"] = "Tu carrito está vacío.";
                return RedirectToAction("Resumen");
            }

            // Si no tiene dirección, permanece en Resumen con datos cargados
            if (string.IsNullOrWhiteSpace(user.Direccion))
            {
                ViewBag.HasAddress = false;
                ViewBag.CarritoDetalles = carritoDetalles;
                ViewBag.TotalCompra = carritoDetalles.Sum(cd => cd.Precio * cd.Cantidad);
                TempData["MensajeError"] = "Agrega una dirección para continuar con la compra.";
                return View("Resumen", user);
            }

            try
            {
                var ok = await _carrito.ProcessCartDetails(carrito.CarritoID, user);

                if (ok)
                {
                    // crea un carrito nuevo y limpio para siguientes compras
                    await _carrito.AddAsync(user);
                    TempData["MensajeExito"] = "Compra realizada con éxito.";
                    return RedirectToAction("CompraExito", "Compras");
                }

                // Si el servicio devolvió false, regresamos a Resumen (no a Exito)
                TempData["MensajeError"] = "No se pudo completar la compra. Revisa tu carrito e intenta nuevamente.";
                return RedirectToAction("Resumen");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al procesar el pedido");
                TempData["MensajeError"] = "Hubo un error al procesar tu pedido.";
                return RedirectToAction("CompraError", "Compras");
            }
        }

    }
}
