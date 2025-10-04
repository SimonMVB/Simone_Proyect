using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Simone.Data;
using Simone.Models;
using Simone.Services;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Simone.Controllers
{
    [Authorize(Roles = "Administrador,Vendedor")]
    /// <summary>
    /// Controlador principal de la aplicación. Maneja el Panel (usuarios, categorías,
    /// subcategorías, proveedores, productos).
    /// </summary>
    public class PanelController : Controller
    {
        private readonly ILogger<PanelController> _logger;
        private readonly TiendaDbContext _context;
        private readonly UserManager<Usuario> _userManager;
        private readonly RoleManager<Roles> _roleManager;
        private readonly CategoriasService _categoriasManager;
        private readonly SubcategoriasService _subcategoriasManager;
        private readonly ProductosService _productosManager;
        private readonly ProveedorService _proveedoresManager;

        public PanelController(
            ILogger<PanelController> logger,
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

        // ===== Helpers =====
        private string CurrentUserId() => _userManager.GetUserId(User)!;
        private bool IsAdmin() => User.IsInRole("Administrador");

        // ====================== INICIO ======================
        [HttpGet]
        public IActionResult Index() => View();

        // ====================== USUARIOS (ADMIN) ======================
        [Authorize(Roles = "Administrador")]
        [HttpGet]
        public IActionResult Usuarios()
        {
            var usuariosConRoles =
                from user in _userManager.Users
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

            var usuariosList = usuariosConRoles
                .AsEnumerable()
                .Select(u => new Usuario
                {
                    Id = u.Id,
                    Email = u.Email,
                    Telefono = u.Telefono,
                    Direccion = u.Direccion,
                    NombreCompleto = u.NombreCompleto,
                    RolID = u.roleId,
                })
                .ToList();

            ViewBag.Usuarios = usuariosList;
            ViewBag.Roles = _roleManager.Roles.ToList();
            return View();
        }

        [Authorize(Roles = "Administrador")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarUsuario(string id)
        {
            var usuario = await _userManager.FindByIdAsync(id);
            if (usuario == null) return NotFound();

            var resultado = await _userManager.DeleteAsync(usuario);
            if (resultado.Succeeded)
            {
                _logger.LogInformation("Administrador eliminó al usuario {Email}.", usuario.Email);
                return RedirectToAction("Usuarios");
            }

            ModelState.AddModelError("", "Error al eliminar usuario.");
            return View("Usuarios", _userManager.Users.ToList());
        }

        [Authorize(Roles = "Administrador")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarRol(string usuarioID, string nuevoRolID)
        {
            if (string.IsNullOrEmpty(nuevoRolID) || string.IsNullOrEmpty(usuarioID))
            {
                ModelState.AddModelError("", "El rol seleccionado no es válido.");
                return RedirectToAction("Usuarios");
            }

            var usuario = await _userManager.FindByIdAsync(usuarioID);
            if (usuario == null) return NotFound();

            var nuevoRol = await _roleManager.FindByIdAsync(nuevoRolID);
            if (nuevoRol == null)
            {
                ModelState.AddModelError("", "El rol seleccionado no existe.");
                return RedirectToAction("Usuarios");
            }

            var rolesActuales = await _userManager.GetRolesAsync(usuario);
            var resultadoEliminar = await _userManager.RemoveFromRolesAsync(usuario, rolesActuales);
            if (!resultadoEliminar.Succeeded)
            {
                ModelState.AddModelError("", "No se pudieron eliminar los roles anteriores.");
                return RedirectToAction("Usuarios");
            }

            var resultadoAsignar = await _userManager.AddToRoleAsync(usuario, nuevoRol.Name);
            if (!resultadoAsignar.Succeeded)
            {
                ModelState.AddModelError("", "No se pudo asignar el nuevo rol.");
                return RedirectToAction("Usuarios");
            }

            _logger.LogInformation("Administrador cambió el rol del usuario {Email} a {Rol}.", usuario.Email, nuevoRol.Name);
            TempData["MensajeExito"] = $"El rol del usuario {usuario.Email} fue actualizado a {nuevoRol.Name}.";
            return RedirectToAction("Usuarios");
        }

        // ====================== CATEGORÍAS (ADMIN) ======================
        [Authorize(Roles = "Administrador")]
        [HttpGet]
        public async Task<IActionResult> Categorias()
        {
            ViewBag.Categorias = await _categoriasManager.GetAllAsync();
            return View();
        }

        [Authorize(Roles = "Administrador")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AnadirCategoria(string nombreCategoria)
        {
            var categoria = new Categorias { Nombre = (nombreCategoria ?? string.Empty).Trim() };
            await _categoriasManager.AddAsync(categoria);
            return RedirectToAction("Categorias");
        }

        [Authorize(Roles = "Administrador")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarCategoria(int categoriaID, string nombreCategoria)
        {
            if (!ModelState.IsValid) return RedirectToAction("Categorias");

            var categoria = await _categoriasManager.GetByIdAsync(categoriaID);
            if (categoria == null) return NotFound();

            categoria.Nombre = (nombreCategoria ?? string.Empty).Trim();
            await _categoriasManager.UpdateAsync(categoria);
            return RedirectToAction("Categorias");
        }

        [Authorize(Roles = "Administrador")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarCategoria(int categoriaID)
        {
            await _categoriasManager.DeleteAsync(categoriaID);
            return RedirectToAction("Categorias");
        }

        // ====================== SUBCATEGORÍAS (ADMIN/VENDEDOR) ======================
        [HttpGet]
        public async Task<IActionResult> Subcategorias(string? vendorId)
        {
            var vid = (IsAdmin() && !string.IsNullOrWhiteSpace(vendorId)) ? vendorId! : CurrentUserId();

            var subcategorias = await _context.Subcategorias
                                              .AsNoTracking()
                                              .Include(s => s.Categoria)
                                              .Where(s => s.VendedorID == vid)
                                              .OrderBy(s => s.CategoriaID)
                                              .ThenBy(s => s.NombreSubcategoria)
                                              .ToListAsync();

            ViewBag.Subcategorias = subcategorias;
            ViewBag.TargetVendorId = (IsAdmin() && !string.IsNullOrWhiteSpace(vendorId)) ? vendorId : null;
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> AnadirSubcategoria()
        {
            ViewBag.Categorias = await _categoriasManager.GetAllAsync();
            return View("SubcategoriaForm");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AnadirSubcategoria(int categoriaID, string nombresubCategoria)
        {
            var subcategoria = new Subcategorias
            {
                CategoriaID = categoriaID,
                NombreSubcategoria = (nombresubCategoria ?? string.Empty).Trim(),
                VendedorID = CurrentUserId()
            };

            try
            {
                var ok = await _subcategoriasManager.AddAsync(subcategoria);
                if (ok)
                {
                    TempData["Ok"] = "Subcategoría creada.";
                    return RedirectToAction("Subcategorias");
                }
                TempData["Err"] = "No se pudo crear la subcategoría.";
            }
            catch (DbUpdateException ex) when (
                   ex.InnerException?.Message.Contains("IX_Subcategorias_VendedorID_CategoriaID_NombreSubcategoria") == true
                || ex.InnerException?.Message.Contains("2601") == true
                || ex.InnerException?.Message.Contains("2627") == true)
            {
                TempData["Err"] = "Ya existe una subcategoría con ese nombre en esa categoría.";
            }
            catch
            {
                TempData["Err"] = "Error al guardar la subcategoría.";
            }

            ViewBag.Categorias = await _categoriasManager.GetAllAsync();
            return View("SubcategoriaForm");
        }

        [HttpGet]
        public async Task<IActionResult> EditarSubcategoria(int subcategoriaID)
        {
            var subcategoria = await _subcategoriasManager.GetByIdAsync(subcategoriaID);
            if (subcategoria == null) return NotFound();

            if (!IsAdmin() && subcategoria.VendedorID != CurrentUserId())
                return Forbid();

            ViewBag.Subcategoria = subcategoria;
            ViewBag.Categorias = await _categoriasManager.GetAllAsync();
            return View("SubcategoriaForm");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarSubcategoria(int subcategoriaID, int categoriaID, string nombresubCategoria)
        {
            if (!ModelState.IsValid) return RedirectToAction("Subcategorias");

            var subcategoria = await _subcategoriasManager.GetByIdAsync(subcategoriaID);
            if (subcategoria == null) return NotFound();

            if (!IsAdmin() && subcategoria.VendedorID != CurrentUserId())
                return Forbid();

            subcategoria.NombreSubcategoria = (nombresubCategoria ?? string.Empty).Trim();
            subcategoria.CategoriaID = categoriaID;

            try
            {
                await _subcategoriasManager.UpdateAsync(subcategoria);
                TempData["Ok"] = "Subcategoría actualizada.";
            }
            catch (DbUpdateException ex) when (
                   ex.InnerException?.Message.Contains("IX_Subcategorias_VendedorID_CategoriaID_NombreSubcategoria") == true
                || ex.InnerException?.Message.Contains("2601") == true
                || ex.InnerException?.Message.Contains("2627") == true)
            {
                TempData["Err"] = "Ya existe una subcategoría con ese nombre en esa categoría.";
                ViewBag.Subcategoria = subcategoria;
                ViewBag.Categorias = await _categoriasManager.GetAllAsync();
                return View("SubcategoriaForm");
            }
            catch
            {
                TempData["Err"] = "Error al actualizar la subcategoría.";
                ViewBag.Subcategoria = subcategoria;
                ViewBag.Categorias = await _categoriasManager.GetAllAsync();
                return View("SubcategoriaForm");
            }

            return RedirectToAction("Subcategorias");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarSubcategoria(int categoriaID)
        {
            var sub = await _subcategoriasManager.GetByIdAsync(categoriaID); // categoriaID == SubcategoriaID (legacy)
            if (sub == null) return NotFound();

            if (!IsAdmin() && sub.VendedorID != CurrentUserId())
                return Forbid();

            try
            {
                await _subcategoriasManager.DeleteAsync(categoriaID);
                TempData["Ok"] = "Subcategoría eliminada.";
            }
            catch (DbUpdateException)
            {
                TempData["Err"] = "No se puede eliminar: hay productos asociados a esta subcategoría.";
            }
            catch
            {
                TempData["Err"] = "Error al eliminar la subcategoría.";
            }

            return RedirectToAction("Subcategorias");
        }

        // ====================== PROVEEDORES (ADMIN) ======================
        [Authorize(Roles = "Administrador")]
        [HttpGet]
        public async Task<IActionResult> Proveedores()
        {
            ViewBag.Proveedores = await _proveedoresManager.GetAllAsync();
            return View();
        }

        [Authorize(Roles = "Administrador")]
        [HttpGet]
        public IActionResult AnadirProveedor() => View("ProveedorForm");

        [Authorize(Roles = "Administrador")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AnadirProveedor(
            string nombreProveedor,
            string contacto,
            string telefono,
            string email,
            string direccion)
        {
            var proveedor = new Proveedores
            {
                NombreProveedor = (nombreProveedor ?? string.Empty).Trim(),
                Contacto = contacto,
                Telefono = telefono,
                Email = email,
                Direccion = direccion,
            };
            await _proveedoresManager.AddAsync(proveedor);
            return RedirectToAction("Proveedores");
        }

        [Authorize(Roles = "Administrador")]
        [HttpGet]
        public async Task<IActionResult> EditarProveedor(int proveedorID)
        {
            ViewBag.Proveedor = await _proveedoresManager.GetByIdAsync(proveedorID);
            return View("ProveedorForm");
        }

        [Authorize(Roles = "Administrador")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarProveedor(
            int proveedorID,
            string nombreProveedor,
            string contacto,
            string telefono,
            string email,
            string direccion)
        {
            if (!ModelState.IsValid) return RedirectToAction("Proveedores");

            var proveedor = await _proveedoresManager.GetByIdAsync(proveedorID);
            if (proveedor == null) return NotFound();

            proveedor.NombreProveedor = (nombreProveedor ?? string.Empty).Trim();
            proveedor.Contacto = contacto;
            proveedor.Telefono = telefono;
            proveedor.Email = email;
            proveedor.Direccion = direccion;

            await _proveedoresManager.UpdateAsync(proveedor);
            return RedirectToAction("Proveedores");
        }

        [Authorize(Roles = "Administrador")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarProveedor(int proveedorID)
        {
            await _proveedoresManager.DeleteAsync(proveedorID);
            return RedirectToAction("Proveedores");
        }

        // ====================== PRODUCTOS (ADMIN/VENDEDOR) ======================
        [HttpGet]
        public async Task<IActionResult> Productos()
        {
            if (IsAdmin())
            {
                ViewBag.Productos = await _productosManager.GetAllAsync();
            }
            else
            {
                ViewBag.Productos = await _productosManager.GetByVendedorID(CurrentUserId());
            }
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> AnadirProducto()
        {
            ViewBag.Categorias = await _categoriasManager.GetAllAsync();
            ViewBag.Subcategorias = IsAdmin()
                ? await _subcategoriasManager.GetAllAsync()
                : await _subcategoriasManager.GetAllByVendedorAsync(CurrentUserId());
            ViewBag.Proveedores = await _proveedoresManager.GetAllAsync();
            return View("ProductoForm");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AnadirProducto(
            string nombreProducto,
            string descripcion,
            string talla,
            string color,
            string marca,
            decimal precioCompra,
            decimal precioVenta,
            int proveedorID,
            int categoriaID,
            int subcategoriaID,
            int stock,
            IFormFile imagen)
        {
            // Validar subcategoría: existe, pertenece a la categoría y (si no es admin) al vendedor actual
            var sub = await _subcategoriasManager.GetByIdAsync(subcategoriaID);
            if (sub == null || sub.CategoriaID != categoriaID || (!IsAdmin() && sub.VendedorID != CurrentUserId()))
            {
                TempData["Err"] = "Subcategoría inválida para la categoría seleccionada o no pertenece a tu tienda.";
                // Reponer combos y datos para la vista
                ViewBag.Categorias = await _categoriasManager.GetAllAsync();
                ViewBag.Subcategorias = IsAdmin()
                    ? await _subcategoriasManager.GetAllAsync()
                    : await _subcategoriasManager.GetAllByVendedorAsync(CurrentUserId());
                ViewBag.Proveedores = await _proveedoresManager.GetAllAsync();

                ViewBag.Producto = new Producto
                {
                    Nombre = nombreProducto,
                    Descripcion = descripcion,
                    Talla = talla,
                    Color = color,
                    Marca = marca,
                    PrecioCompra = precioCompra,
                    PrecioVenta = precioVenta,
                    Stock = stock,
                    ProveedorID = proveedorID,
                    CategoriaID = categoriaID,
                    SubcategoriaID = subcategoriaID
                };
                return View("ProductoForm");
            }

            var producto = new Producto
            {
                Nombre = (nombreProducto ?? string.Empty).Trim(),
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
                VendedorID = CurrentUserId(),
            };

            if (imagen != null)
            {
                var dir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "Productos");
                Directory.CreateDirectory(dir);
                var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(imagen.FileName);
                var filePath = Path.Combine(dir, uniqueFileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await imagen.CopyToAsync(stream);
                }
                producto.ImagenPath = "/images/Productos/" + uniqueFileName;
            }

            await _productosManager.AddAsync(producto);
            return RedirectToAction("Productos");
        }

        [HttpGet]
        public async Task<IActionResult> EditarProducto(int productoID)
        {
            var producto = await _productosManager.GetByIdAsync(productoID);
            if (producto == null) return NotFound();
            if (!IsAdmin() && producto.VendedorID != CurrentUserId())
                return Forbid();

            ViewBag.Categorias = await _categoriasManager.GetAllAsync();
            ViewBag.Proveedores = await _proveedoresManager.GetAllAsync();
            ViewBag.Subcategorias = IsAdmin()
                ? await _subcategoriasManager.GetAllAsync()
                : await _subcategoriasManager.GetAllByVendedorAsync(CurrentUserId());
            ViewBag.Producto = producto;
            return View("ProductoForm");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarProducto(
            int productoID,
            string nombreProducto,
            string descripcion,
            string talla,
            string color,
            string marca,
            string existingImagenPath,
            decimal precioCompra,
            decimal precioVenta,
            int proveedorID,
            int categoriaID,
            int subcategoriaID,
            int stock,
            IFormFile imagen)
        {
            var producto = await _productosManager.GetByIdAsync(productoID);
            if (producto == null) return NotFound();
            if (!IsAdmin() && producto.VendedorID != CurrentUserId())
                return Forbid();

            // Validación de subcategoría coherente
            var sub = await _subcategoriasManager.GetByIdAsync(subcategoriaID);
            if (sub == null || sub.CategoriaID != categoriaID || (!IsAdmin() && sub.VendedorID != CurrentUserId()))
            {
                TempData["Err"] = "Subcategoría inválida para la categoría seleccionada o no pertenece a tu tienda.";

                ViewBag.Categorias = await _categoriasManager.GetAllAsync();
                ViewBag.Proveedores = await _proveedoresManager.GetAllAsync();
                ViewBag.Subcategorias = IsAdmin()
                    ? await _subcategoriasManager.GetAllAsync()
                    : await _subcategoriasManager.GetAllByVendedorAsync(CurrentUserId());
                ViewBag.Producto = producto;
                return View("ProductoForm");
            }

            producto.Nombre = (nombreProducto ?? string.Empty).Trim();
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
                var dir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "Productos");
                Directory.CreateDirectory(dir);
                var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(imagen.FileName);
                var filePath = Path.Combine(dir, uniqueFileName);
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
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarProducto(int productoID)
        {
            var producto = await _productosManager.GetByIdAsync(productoID);
            if (producto == null) return NotFound();
            if (!IsAdmin() && producto.VendedorID != CurrentUserId())
                return Forbid();

            await _productosManager.DeleteAsync(productoID);
            return RedirectToAction("Productos");
        }
    }
}
