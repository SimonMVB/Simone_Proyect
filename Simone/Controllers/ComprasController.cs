using Microsoft.AspNetCore.Mvc;
using Simone.Models;
using Simone.Data;
using Microsoft.EntityFrameworkCore;
using Simone.Extensions;
using Simone.Services;
using Microsoft.AspNetCore.Identity;

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
        private readonly ILogger<AdminController> _logger;


        public ComprasController(
            UserManager<Usuario> user,
         TiendaDbContext context,
          ProductosService productos,
           CategoriasService categorias,
            SubcategoriasService subcategorias,
        CarritoService carrito,
        ILogger<AdminController> logger
            )
        {
            _context = context;
            _productos = productos;
            _categorias = categorias;
            _subcategorias = subcategorias;
            _userManager = user;
            _logger = logger;
            _carrito = carrito;
        }

        [HttpGet]
        public async Task<IActionResult> Catalogo(int? categoriaID, int[] subcategoriaIDs, int pageNumber = 1, int pageSize = 20)
        {
            // Load categories for filter display
            var categorias = await _categorias.GetAllAsync();

            // Load subcategories for the selected category (or empty if none)
            var subcategorias = categoriaID.HasValue
                ? await _subcategorias.GetByCategoriaIdAsync(categoriaID.Value)
                : new List<Subcategorias>();

            // Prepare queryable filtered products
            IQueryable<Producto> query = _context.Productos;

            if (categoriaID.HasValue)
            {
                query = query.Where(p => p.CategoriaID == categoriaID.Value);
            }

            if (subcategoriaIDs != null && subcategoriaIDs.Length > 0)
            {
                query = query.Where(p => subcategoriaIDs.Contains(p.SubcategoriaID));
            }

            var totalProducts = await query.CountAsync();

            var productos = await query
                .OrderBy(p => p.Nombre) // or any ordering
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
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
                return View("Invalido");
            }
        }

        [HttpPost]
        public async Task<IActionResult> AnadirAlCarrito(CatalogoViewModel model)
        {
            // Retrieve the logged-in user
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Cuenta");
            }

            // Retrieve the product by its ID
            var producto = await _productos.GetByIdAsync(model.ProductoID);
            if (producto == null)
            {
                // Return to "Invalido" if the product is not found
                return View("Invalido", new { Message = "Producto no encontrado" });
            }

            // Add product to cart
            var success = await _carrito.AnadirProducto(producto, user, model.Cantidad);
            if (success)
            {
                // Get the referrer URL (previous page)
                var refererUrl = Request.Headers["Referer"].ToString();

                // If the referer URL is valid, redirect back to that page; otherwise, redirect to a default page
                if (!string.IsNullOrEmpty(refererUrl))
                {
                    return Redirect(refererUrl);
                }
                else
                {
                    // Default fallback if there's no referer (e.g., user directly accessed the page)
                    return RedirectToAction("Catalogo", "Compras");
                }
            }
            else
            {
                // Handle failure to add product to cart
                return View("Invalido", new { Message = "No se pudo añadir el producto al carrito" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> EliminarArticulo(int carritoDetalleID)
        {
            await _carrito.BorrarProductoCarrito(carritoDetalleID);
            return RedirectToAction("Catalogo", "Compras");
        }

        private async Task<bool> isLoggedIn()
        {
            var user = await _userManager.GetUserAsync(User);
            return user != null;
        }


        [HttpGet]
        public async Task<IActionResult> Resumen()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Cuenta");
            }

            // Get the user's cart details (CarritoDetalle)
            var carrito = await _carrito.GetByClienteIdAsync(user.Id);
            var carritoDetalles = await _carrito.LoadCartDetails(carrito.CarritoID);
            decimal totalCompra = carritoDetalles.Sum(cd => cd.Precio * cd.Cantidad); // Calculate the total price of the cart

            // Check if the user has an address
            var address = user.Direccion; // Assuming `Direccion` is the address field in the `Usuario` model

            // Pass the data to the view
            ViewBag.CarritoDetalles = carritoDetalles;
            ViewBag.TotalCompra = totalCompra;
            ViewBag.HasAddress = !string.IsNullOrEmpty(address); // Check if address is available

            return View(user); // Pass the user object to the view for address check
        }

        [HttpPost]
        public async Task<IActionResult> ConfirmarCompra()
        {
            // Step 1: Ensure the user is logged in
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Cuenta"); // Redirect to login page if not logged in
            }

            // Step 2: Get the user's active cart details
            var carrito = await _carrito.GetByClienteIdAsync(user.Id);
            var carritoDetalles = await _carrito.LoadCartDetails(carrito.CarritoID);

            if (carritoDetalles == null || !carritoDetalles.Any())
            {
                // If no items are in the cart, redirect to the error page
                return RedirectToAction("CompraError", "Compras");
            }

            // Step 3: Check if the user has a valid address
            if (string.IsNullOrEmpty(user.Direccion))
            {
                // If no address is provided, prompt the user to add one
                ViewBag.HasAddress = false;
                return View("Resumen");
            }

            // Step 4: Optionally check for payment method here, for now assume payment info is already provided
            // For example, you could validate the payment details here (if needed).
            // If payment information is incomplete, redirect to a payment info page

            // Step 5: Simulate completing the purchase (e.g., update cart state, process order)
            try
            {
                // Example: Update cart state (changing the state from "Active" to "Completed")

                if (carrito != null)
                {
                    var result = await _carrito.ProcessCartDetails(carrito.CarritoID, user);
                    if (result)
                    {
                        await _carrito.AddAsync(user); // Create a new cart
                    }

                }

                // Step 6: Redirect to the success page after confirming the purchase
                return RedirectToAction("CompraExito", "Compras"); // Redirect to success page

            }
            catch (Exception ex)
            {
                // Handle any error that occurs during the order processing
                _logger.LogError($"Error processing order: {ex.Message}");
                return RedirectToAction("CompraError", "Compras"); // Redirect to error page
            }
        }
    }
}