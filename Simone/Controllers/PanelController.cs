using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Simone.Models;
using Simone.Data;
using Simone.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Simone.Controllers
{
    [Authorize(Roles = "Administrador" + "," + "Vendedor")]
    /// <summary>
    /// Controlador principal de la aplicación. Maneja las páginas de inicio,
    /// privacidad, ofertas, nosotros y el manejo de errores.
    /// </summary>
    public class PanelController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly TiendaDbContext _context;
        private readonly UserManager<Usuario> _userManager;
        private readonly RoleManager<Roles> _roleManager;
        private readonly CategoriasService _categoriasManager;
        private readonly SubcategoriasService _subcategoriasManager;
        private readonly ProductosService _productosManager;
        private readonly ProveedorService _proveedoresManager;

        /// <summary>
        /// Constructor que recibe el logger y el contexto de la base de datos.
        /// </summary>
        /// <param name="logger">Instancia de ILogger para registro de eventos.</param>
        /// <param name="context">Contexto de la base de datos para consultas.</param>
        public PanelController(
            ILogger<HomeController> logger,
            TiendaDbContext context,
            UserManager<Usuario> user,
            RoleManager<Roles> rol,
            CategoriasService categorias,
            SubcategoriasService subcategorias,
            ProductosService productos,
            ProveedorService proveedores)
        {
            _logger = logger;
            _context = context;
            _userManager = user;
            _roleManager = rol;
            _categoriasManager = categorias;
            _subcategoriasManager = subcategorias;
            _productosManager = productos;
            _proveedoresManager = proveedores;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Usuarios()
        {
            // Query para obtener usuarios y sus roles
            var usuariosConRoles = from user in _userManager.Users
                                   join userRole in _context.UserRoles on user.Id equals userRole.UserId
                                   join role in _context.Roles on userRole.RoleId equals role.Id
                                   select new
                                   {
                                       user.Id,
                                       user.Email,
                                       user.Telefono,
                                       user.Direccion,
                                       user.NombreCompleto,
                                       roleId = role.Id,
                                       roleName = role.Name
                                   };

            // Ejecutar el query y convertirlo en lista
            var usuarios = usuariosConRoles.ToList();

            // Convertirlo en Usuario object
            var usuariosList = usuarios.Select(u => new Usuario
            {
                Id = u.Id,
                Email = u.Email,
                Telefono = u.Telefono,
                Direccion = u.Direccion,
                NombreCompleto = u.NombreCompleto,
                RolID = u.roleId,  // Rol ID
            }).ToList();

            // Enviar la data a la vista via ViewBag
            ViewBag.Usuarios = usuariosList;
            ViewBag.Roles = _roleManager.Roles.ToList(); // Obtener todos los roles para rellenar el dropdown

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarUsuario(string id)
        {
            var usuario = await _userManager.FindByIdAsync(id);
            if (usuario == null)
                return NotFound();

            var resultado = await _userManager.DeleteAsync(usuario);
            if (resultado.Succeeded)
            {
                _logger.LogInformation("Administrador eliminó al usuario {Email}.", usuario.Email);
                return RedirectToAction("Usuarios");
            }
            else
            {
                ModelState.AddModelError("", "Error al eliminar usuario.");
                return View("Usuarios", _userManager.Users.ToList());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarRol(string usuarioID, string nuevoRolID)
        {
            // Validacion de los parametros de la funcion
            if (string.IsNullOrEmpty(nuevoRolID) || string.IsNullOrEmpty(usuarioID))
            {
                ModelState.AddModelError("", "El rol seleccionado no es válido.");
                return RedirectToAction("Usuarios"); // Redireccion en caso de error
            }

            // Obtener el usuario por su ID
            var usuario = await _userManager.FindByIdAsync(usuarioID);
            if (usuario == null)
                return NotFound();

            // Obtener el nuevo rol por su ID
            var nuevoRol = await _roleManager.FindByIdAsync(nuevoRolID);
            if (nuevoRol == null)
            {
                ModelState.AddModelError("", "El rol seleccionado no existe.");
                return RedirectToAction("Usuarios"); // Redireccion en caso de error
            }

            // Obtener el rol actual del usuario y eliminarlo
            var rolesActuales = await _userManager.GetRolesAsync(usuario);
            var resultadoEliminar = await _userManager.RemoveFromRolesAsync(usuario, rolesActuales);
            if (!resultadoEliminar.Succeeded)
            {
                ModelState.AddModelError("", "No se pudieron eliminar los roles anteriores.");
                return RedirectToAction("Usuarios");
            }

            // Asignar el nuevo rol por medio del nombre. (Se podria crear un metodo para recibir ID en lugar de nombre)
            var resultadoAsignar = await _userManager.AddToRoleAsync(usuario, nuevoRol.Name);
            if (!resultadoAsignar.Succeeded)
            {
                ModelState.AddModelError("", "No se pudo asignar el nuevo rol.");
                return RedirectToAction("Usuarios");
            }

            // Log
            _logger.LogInformation("Administrador cambió el rol del usuario {Email} a {Rol}.", usuario.Email, nuevoRol.Name);

            // Actualizar la pagina
            TempData["MensajeExito"] = $"El rol del usuario {usuario.Email} fue actualizado a {nuevoRol.Name}.";
            return RedirectToAction("Usuarios");
        }

        [HttpGet]
        public async Task<IActionResult> Categorias()
        {
            var categorias = await _categoriasManager.GetAllAsync();
            ViewBag.Categorias = categorias;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AnadirCategoria(string nombreCategoria)
        {
            Categorias categoria = new Categorias
            {
                Nombre = nombreCategoria,
            };
            bool success = await _categoriasManager.AddAsync(categoria);
            if (success)
            {
                return RedirectToAction("Categorias");
            }
            return RedirectToAction("Categorias");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarCategoria(int categoriaID, string nombreCategoria)
        {
            if (ModelState.IsValid)
            {
                var categoria = await _categoriasManager.GetByIdAsync(categoriaID);
                if (categoria == null)
                {
                    return NotFound();
                }

                categoria.Nombre = nombreCategoria;
                await _categoriasManager.UpdateAsync(categoria);

                return RedirectToAction("Categorias");
            }

            return RedirectToAction("Categorias");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarCategoria(int categoriaID)
        {
            bool success = await _categoriasManager.DeleteAsync(categoriaID);
            return RedirectToAction("Categorias");
        }

        [HttpGet]
        public async Task<IActionResult> Subcategorias()
        {
            // Load Subcategorias with the related Categoria name
            var subcategorias = await _subcategoriasManager.GetAllSubcategoriasWithCategoriaAsync();

            // Send Subcategorias to ViewBag
            ViewBag.Subcategorias = subcategorias;
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> AnadirSubcategoria()
        {
            var categorias = await _categoriasManager.GetAllAsync();
            ViewBag.Categorias = categorias;
            return View("SubcategoriaForm");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AnadirSubcategoria(int categoriaID, string nombresubCategoria)
        {
            Subcategorias subcategoria = new Subcategorias
            {
                CategoriaID = categoriaID,
                NombreSubcategoria = nombresubCategoria
            };
            bool success = await _subcategoriasManager.AddAsync(subcategoria);
            if (success)
            {
                return RedirectToAction("Subcategorias");
            }
            return RedirectToAction("Subcategorias");
        }

        [HttpGet]
        public async Task<IActionResult> EditarSubcategoria(int subcategoriaID)
        {
            var subcategoria = await _subcategoriasManager.GetByIdAsync(subcategoriaID);
            ViewBag.Subcategoria = subcategoria;
            var categorias = await _categoriasManager.GetAllAsync();
            ViewBag.Categorias = categorias;
            return View("SubcategoriaForm");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarSubcategoria(int subcategoriaID, int categoriaID, string nombresubCategoria)
        {
            if (ModelState.IsValid)
            {
                var subcategoria = await _subcategoriasManager.GetByIdAsync(subcategoriaID);
                if (subcategoria == null)
                {
                    return NotFound();
                }

                subcategoria.NombreSubcategoria = nombresubCategoria;
                subcategoria.CategoriaID = categoriaID;

                await _subcategoriasManager.UpdateAsync(subcategoria);

                return RedirectToAction("Subcategorias");
            }

            return RedirectToAction("Subcategorias");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarSubcategoria(int categoriaID)
        {
            bool success = await _subcategoriasManager.DeleteAsync(categoriaID);
            return RedirectToAction("Subcategorias");
        }

        [HttpGet]
        public async Task<IActionResult> Proveedores()
        {
            var proveedores = await _proveedoresManager.GetAllAsync();
            ViewBag.Proveedores = proveedores;
            return View();
        }

        [HttpGet]
        public IActionResult AnadirProveedor()
        {
            return View("ProveedorForm");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AnadirProveedor(
            string nombreProveedor,
            string contacto,
            string telefono,
            string email,
            string direccion
            )
        {
            Proveedores proveedor = new Proveedores
            {
                NombreProveedor = nombreProveedor,
                Contacto = contacto,
                Telefono = telefono,
                Email = email,
                Direccion = direccion,
            };
            bool success = await _proveedoresManager.AddAsync(proveedor);
            if (success)
            {
                return RedirectToAction("Proveedores");
            }
            return RedirectToAction("Proveedores");
        }

        [HttpGet]
        public async Task<IActionResult> EditarProveedor(int proveedorID)
        {
            var proveedores = await _proveedoresManager.GetByIdAsync(proveedorID);
            ViewBag.Proveedor = proveedores;
            return View("ProveedorForm");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarProveedor(
            int proveedorID,
            string nombreProveedor,
            string contacto,
            string telefono,
            string email,
            string direccion
            )
        {
            if (ModelState.IsValid)
            {
                var proveedor = await _proveedoresManager.GetByIdAsync(proveedorID);
                if (proveedor == null)
                {
                    return NotFound();
                }

                proveedor.NombreProveedor = nombreProveedor;
                proveedor.Contacto = contacto;
                proveedor.Telefono = telefono;
                proveedor.Email = email;
                proveedor.Direccion = direccion;

                await _proveedoresManager.UpdateAsync(proveedor);

                return RedirectToAction("Proveedores");
            }

            return RedirectToAction("Proveedores");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarProveedor(int categoriaID)
        {
            bool success = await _proveedoresManager.DeleteAsync(categoriaID);
            return RedirectToAction("Categorias");
        }

        [HttpGet]
        public async Task<IActionResult> Productos()
        {
            var productos = await _productosManager.GetAllAsync();
            ViewBag.Productos = productos;
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> AnadirProducto()
        {
            var categorias = await _categoriasManager.GetAllAsync();
            ViewBag.Categorias = categorias;
            var subcategorias = await _subcategoriasManager.GetAllAsync();
            ViewBag.subcategorias = subcategorias;
            var proveedores = await _proveedoresManager.GetAllAsync();
            ViewBag.Proveedores = proveedores;
            return View("ProductoForm");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AnadirProducto(
            String nombreProducto,
            String descripcion,
            String talla,
            String color,
            String marca,
            decimal precioCompra,
            decimal precioVenta,
            int proveedorID,
            int categoriaID,
            int subcategoriaID,
            int stock,
            IFormFile imagen
            )
        {

            var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(imagen.FileName);
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/Productos", uniqueFileName);

            // Save the image to the server
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await imagen.CopyToAsync(stream);
            }

            var producto = new Producto
            {
                Nombre = nombreProducto,
                FechaAgregado = DateTime.Now,
                Descripcion = descripcion,
                Talla = talla,
                Color = color,
                Marca = marca,
                PrecioCompra = precioCompra,
                PrecioVenta = precioVenta,
                Stock = stock,
                ProveedorID = proveedorID,
                CategoriaID = categoriaID,
                SubcategoriaID = subcategoriaID,
                ImagenPath = "/images/Productos/" + uniqueFileName
            };
            await _productosManager.AddAsync(producto);

            return RedirectToAction("Productos");
        }

        [HttpGet]
        public async Task<IActionResult> EditarProducto(int productoID)
        {
            var producto = await _productosManager.GetByIdAsync(productoID);
            var categorias = await _categoriasManager.GetAllAsync();
            ViewBag.Categorias = categorias;
            var proveedores = await _proveedoresManager.GetAllAsync();
            ViewBag.Proveedores = proveedores;
            var subcategorias = await _subcategoriasManager.GetAllAsync();
            ViewBag.Subcategorias = subcategorias;
            ViewBag.Producto = producto;
            return View("ProductoForm");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarProducto(
            int productoID,
            String nombreProducto,
            String descripcion,
            String talla,
            String color,
            String marca,
            String existingImagenPath,
            decimal precioCompra,
            decimal precioVenta,
            int proveedorID,
            int categoriaID,
            int subcategoriaID,
            int stock,
            IFormFile imagen
            )
        {
                var producto = await _productosManager.GetByIdAsync(productoID);
                if (producto == null)
                {
                    return NotFound();
                }

                producto.Nombre = nombreProducto;
                producto.Descripcion = descripcion;
                producto.Talla = talla;
                producto.Color = color;
                producto.Marca = marca;
                producto.PrecioCompra = precioCompra;
                producto.PrecioVenta = precioVenta;
                producto.ProveedorID = proveedorID;
                producto.CategoriaID = categoriaID;
                producto.SubcategoriaID = subcategoriaID;
                producto.Stock = stock;

                if (imagen != null)
                {
                    var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(imagen.FileName);
                    var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/Productos", uniqueFileName);

                    // Save the image to the server
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await imagen.CopyToAsync(stream);
                    }

                    producto.ImagenPath = "/images/Productos/" + uniqueFileName;
                }
                else
                {
                    producto.ImagenPath = existingImagenPath;
                }

                await _productosManager.UpdateAsync(producto);

                return RedirectToAction("Productos");

            return RedirectToAction("Productos");
        }

        public IActionResult Ofertas()
        {
            var productosEnOferta = _context.Productos
                .Where(p => p.PrecioVenta < p.PrecioCompra) // o cualquier lógica de oferta
                .Include(p => p.ImagenesProductos)
                .ToList();

            return View(productosEnOferta);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarProducto(int productoID)
        {
            bool success = await _productosManager.DeleteAsync(productoID);
            return RedirectToAction("Productos");
        }

    }
}