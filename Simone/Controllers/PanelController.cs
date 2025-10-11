using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Simone.Data;
using Simone.Models;
using Simone.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Simone.Controllers
{
    [Authorize(Roles = "Administrador,Vendedor")]
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
        private readonly IWebHostEnvironment _env;

        public PanelController(
            ILogger<PanelController> logger,
            TiendaDbContext context,
            UserManager<Usuario> user,
            RoleManager<Roles> rol,
            CategoriasService categorias,
            SubcategoriasService subcategorias,
            ProductosService productos,
            ProveedorService proveedores,
            IWebHostEnvironment env)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _userManager = user ?? throw new ArgumentNullException(nameof(user));
            _roleManager = rol ?? throw new ArgumentNullException(nameof(rol));
            _categoriasManager = categorias ?? throw new ArgumentNullException(nameof(categorias));
            _subcategoriasManager = subcategorias ?? throw new ArgumentNullException(nameof(subcategorias));
            _productosManager = productos ?? throw new ArgumentNullException(nameof(productos));
            _proveedoresManager = proveedores ?? throw new ArgumentNullException(nameof(proveedores));
            _env = env ?? throw new ArgumentNullException(nameof(env));
        }

        // ===== Helpers =====

        private string CurrentUserId() => _userManager.GetUserId(User)!;
        private bool IsAdmin() => User.IsInRole("Administrador");

        private string GalleryRootAbs() =>
            Path.Combine(_env.WebRootPath, "images", "Productos");

        private string ProductFolderAbs(int productId) =>
            Path.Combine(GalleryRootAbs(), productId.ToString());

        private static readonly JsonSerializerOptions _jsonOpts = new() { WriteIndented = true };

        private static readonly HashSet<string> _allowedImgExt = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".webp", ".gif" };

        private static readonly HashSet<string> _allowedMime = new(StringComparer.OrdinalIgnoreCase)
        { "image/jpeg", "image/png", "image/webp", "image/gif" };

        private const int _maxFiles = 8;
        private const long _maxImgBytes = 8 * 1024 * 1024;
        private const long _maxFormBytes = 64L * 1024 * 1024;

        // ====================== INICIO ======================
        [HttpGet]
        public IActionResult Index() => View();

        // ====================== USUARIOS (ADMIN) ======================
        [Authorize(Roles = "Administrador")]
        [HttpGet]
        public async Task<IActionResult> Usuarios(string? tiendaId = null)
        {
            var tiendasList = await _context.Vendedores
                .AsNoTracking()
                .OrderBy(v => v.Nombre)
                .Select(v => new SelectListItem { Value = v.VendedorId.ToString(), Text = v.Nombre })
                .ToListAsync();

            var usuariosConRol =
                from u in _userManager.Users.AsNoTracking()
                join ur in _context.UserRoles.AsNoTracking() on u.Id equals ur.UserId
                join r in _context.Roles.AsNoTracking() on ur.RoleId equals r.Id
                select new { u, rolId = r.Id, rolName = r.Name };

            if (!string.IsNullOrWhiteSpace(tiendaId) && int.TryParse(tiendaId, out var vid))
                usuariosConRol = usuariosConRol.Where(x => x.u.VendedorId == vid);

            var lista = await usuariosConRol
                .Select(x => new Usuario
                {
                    Id = x.u.Id,
                    Email = x.u.Email,
                    Telefono = x.u.Telefono,
                    Direccion = x.u.Direccion,
                    NombreCompleto = x.u.NombreCompleto,
                    Activo = x.u.Activo,
                    RolID = x.rolId,
                    VendedorId = x.u.VendedorId
                })
                .ToListAsync();

            ViewBag.Usuarios = lista;
            ViewBag.Roles = _roleManager.Roles
                .AsNoTracking()
                .Select(r => new SelectListItem { Value = r.Id, Text = r.Name })
                .ToList();

            ViewBag.Tiendas = tiendasList;
            ViewBag.FiltroTiendaId = tiendaId ?? "";

            return View();
        }

        [Authorize(Roles = "Administrador")]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AsignarTiendaUsuario(string usuarioID, int tiendaID, string? returnTiendaId = null)
        {
            if (string.IsNullOrWhiteSpace(usuarioID))
            {
                TempData["MensajeError"] = "Usuario inválido.";
                return RedirectToAction("Usuarios", new { tiendaId = returnTiendaId });
            }

            var user = await _userManager.FindByIdAsync(usuarioID);
            if (user == null)
            {
                TempData["MensajeError"] = "Usuario no encontrado.";
                return RedirectToAction("Usuarios", new { tiendaId = returnTiendaId });
            }

            var vendedor = await _context.Vendedores.FindAsync(tiendaID);
            if (vendedor == null)
            {
                TempData["MensajeError"] = "Tienda no válida.";
                return RedirectToAction("Usuarios", new { tiendaId = returnTiendaId });
            }

            user.VendedorId = tiendaID;
            var res = await _userManager.UpdateAsync(user);
            TempData[res.Succeeded ? "MensajeExito" : "MensajeError"] =
                res.Succeeded ? "Tienda asignada correctamente." : "No se pudo asignar la tienda.";

            return RedirectToAction("Usuarios", new { tiendaId = returnTiendaId });
        }

        [Authorize(Roles = "Administrador")]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> QuitarTiendaUsuario(string usuarioID, string? returnTiendaId = null)
        {
            if (string.IsNullOrWhiteSpace(usuarioID))
            {
                TempData["MensajeError"] = "Usuario inválido.";
                return RedirectToAction("Usuarios", new { tiendaId = returnTiendaId });
            }

            var user = await _userManager.FindByIdAsync(usuarioID);
            if (user == null)
            {
                TempData["MensajeError"] = "Usuario no encontrado.";
                return RedirectToAction("Usuarios", new { tiendaId = returnTiendaId });
            }

            user.VendedorId = null;
            var res = await _userManager.UpdateAsync(user);
            TempData[res.Succeeded ? "MensajeExito" : "MensajeError"] =
                res.Succeeded ? "Tienda quitada correctamente." : "No se pudo quitar la tienda.";

            return RedirectToAction("Usuarios", new { tiendaId = returnTiendaId });
        }

        [Authorize(Roles = "Administrador")]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearTiendaSimple(string nombre)
        {
            if (string.IsNullOrWhiteSpace(nombre))
                return Json(new { ok = false, msg = "Nombre requerido." });

            var v = new Vendedor { Nombre = nombre.Trim() };
            _context.Vendedores.Add(v);
            await _context.SaveChangesAsync();

            var tiendas = await _context.Vendedores
                .AsNoTracking()
                .OrderBy(x => x.Nombre)
                .Select(x => new { value = x.VendedorId.ToString(), text = x.Nombre })
                .ToListAsync();

            return Json(new { ok = true, newId = v.VendedorId.ToString(), newText = v.Nombre, tiendas });
        }

        [Authorize(Roles = "Administrador")]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarUsuario(string id)
        {
            var usuario = await _userManager.FindByIdAsync(id);
            if (usuario == null)
            {
                TempData["MensajeError"] = "Usuario no encontrado.";
                return RedirectToAction("Usuarios");
            }

            var resultado = await _userManager.DeleteAsync(usuario);
            if (resultado.Succeeded)
            {
                _logger.LogInformation("Administrador eliminó al usuario {Email}.", usuario.Email);
                TempData["MensajeExito"] = "Usuario eliminado correctamente.";
                return RedirectToAction("Usuarios");
            }

            TempData["MensajeError"] = "Error al eliminar usuario.";
            return RedirectToAction("Usuarios");
        }

        [Authorize(Roles = "Administrador")]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarRol(string usuarioID, string nuevoRolID)
        {
            if (string.IsNullOrEmpty(nuevoRolID) || string.IsNullOrEmpty(usuarioID))
            {
                TempData["MensajeError"] = "El rol seleccionado no es válido.";
                return RedirectToAction("Usuarios");
            }

            var usuario = await _userManager.FindByIdAsync(usuarioID);
            if (usuario == null)
            {
                TempData["MensajeError"] = "Usuario no encontrado.";
                return RedirectToAction("Usuarios");
            }

            var nuevoRol = await _roleManager.FindByIdAsync(nuevoRolID);
            if (nuevoRol == null)
            {
                TempData["MensajeError"] = "El rol seleccionado no existe.";
                return RedirectToAction("Usuarios");
            }

            var rolesActuales = await _userManager.GetRolesAsync(usuario);
            var resultadoEliminar = await _userManager.RemoveFromRolesAsync(usuario, rolesActuales);
            if (!resultadoEliminar.Succeeded)
            {
                TempData["MensajeError"] = "No se pudieron eliminar los roles anteriores.";
                return RedirectToAction("Usuarios");
            }

            var resultadoAsignar = await _userManager.AddToRoleAsync(usuario, nuevoRol.Name);
            if (!resultadoAsignar.Succeeded)
            {
                TempData["MensajeError"] = "No se pudo asignar el nuevo rol.";
                return RedirectToAction("Usuarios");
            }

            _logger.LogInformation("Administrador cambió el rol del usuario {Email} a {Rol}.", usuario.Email, nuevoRol.Name);
            TempData["MensajeExito"] = $"El rol del usuario {usuario.Email} fue actualizado a {nuevoRol.Name}.";
            return RedirectToAction("Usuarios");
        }

        // ====================== CATEGORÍAS ======================
        [Authorize(Roles = "Administrador")]
        [HttpGet]
        public async Task<IActionResult> Categorias()
        {
            ViewBag.Categorias = await _categoriasManager.GetAllAsync();
            return View();
        }

        [Authorize(Roles = "Administrador")]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AnadirCategoria(string nombreCategoria)
        {
            var categoria = new Categorias { Nombre = (nombreCategoria ?? string.Empty).Trim() };
            await _categoriasManager.AddAsync(categoria);
            return RedirectToAction("Categorias");
        }

        [Authorize(Roles = "Administrador")]
        [HttpPost, ValidateAntiForgeryToken]
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
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarCategoria(int categoriaID)
        {
            await _categoriasManager.DeleteAsync(categoriaID);
            return RedirectToAction("Categorias");
        }

        // ====================== SUBCATEGORÍAS ======================
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

        [HttpPost, ValidateAntiForgeryToken]
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

        [HttpPost, ValidateAntiForgeryToken]
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

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarSubcategoria(int subcategoriaID)
        {
            var sub = await _subcategoriasManager.GetByIdAsync(subcategoriaID);
            if (sub == null) return NotFound();

            if (!IsAdmin() && sub.VendedorID != CurrentUserId())
                return Forbid();

            try
            {
                await _subcategoriasManager.DeleteAsync(subcategoriaID);
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
        [HttpPost, ValidateAntiForgeryToken]
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
        [HttpPost, ValidateAntiForgeryToken]
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
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarProveedor(int proveedorID)
        {
            await _proveedoresManager.DeleteAsync(proveedorID);
            return RedirectToAction("Proveedores");
        }

        // ====================== PRODUCTOS ======================
        [HttpGet]
        public async Task<IActionResult> Productos()
        {
            IQueryable<Producto> query = _context.Productos
                .AsNoTracking()
                .Include(p => p.Categoria)
                .Include(p => p.Variantes);

            if (!IsAdmin())
                query = query.Where(p => p.VendedorID == CurrentUserId());

            var productos = await query.OrderBy(p => p.Nombre).ToListAsync();

            ViewBag.Categorias = await _categoriasManager.GetAllAsync();
            return View(productos);
        }

        [HttpGet]
        public async Task<IActionResult> AnadirProducto()
        {
            await FillProductoFormBags();
            return View("ProductoForm");
        }

        [HttpPost, ValidateAntiForgeryToken, RequestFormLimits(MultipartBodyLengthLimit = _maxFormBytes)]
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
            IFormFile? imagen,
            IFormFile[]? Imagenes,
            int? ImagenPrincipalIndex,
            string? ImagenesIgnore,
            [FromForm] string[]? VarColor,
            [FromForm] string[]? VarTalla,
            [FromForm] string[]? VarPrecio,
            [FromForm] int[]? VarStock)
        {
            try
            {
                // Validación de subcategoría
                var sub = await _context.Subcategorias
                    .Include(s => s.Categoria)
                    .FirstOrDefaultAsync(s => s.SubcategoriaID == subcategoriaID && s.CategoriaID == categoriaID);

                if (sub == null)
                {
                    TempData["Err"] = "Subcategoría no encontrada.";
                    await FillProductoFormBags();
                    return View("ProductoForm");
                }

                if (!IsAdmin() && sub.VendedorID != CurrentUserId())
                {
                    TempData["Err"] = "No tienes permisos para usar esta subcategoría.";
                    await FillProductoFormBags();
                    return View("ProductoForm");
                }

                // Fallback: si no llegaron Var* pero sí Variantes[i].*, leer del form
                if (VarColor == null || VarTalla == null || VarPrecio == null || VarStock == null)
                {
                    var v = ReadVariantesFromForm();
                    VarColor ??= v.Colores;
                    VarTalla ??= v.Tallas;
                    VarPrecio ??= v.Precios;
                    VarStock ??= v.Stocks;
                }

                // Variantes
                var (hasVariants, normVariants, variantError) = await NormalizeAndValidateVariants(
                    VarColor, VarTalla, VarPrecio, VarStock, precioCompra);

                if (!string.IsNullOrEmpty(variantError))
                {
                    TempData["Err"] = variantError;
                    await FillProductoFormBags();
                    ViewBag.Producto = PresetProducto(nombreProducto, descripcion, talla, color, marca,
                        precioCompra, precioVenta, stock, proveedorID, categoriaID, subcategoriaID);
                    return View("ProductoForm");
                }

                // Crear producto base
                var producto = new Producto
                {
                    Nombre = (nombreProducto ?? string.Empty).Trim(),
                    FechaAgregado = DateTime.UtcNow,
                    Descripcion = descripcion?.Trim(),
                    Marca = marca?.Trim(),
                    PrecioCompra = Math.Round(precioCompra, 2),
                    ProveedorID = proveedorID,
                    CategoriaID = categoriaID,
                    SubcategoriaID = subcategoriaID,
                    VendedorID = CurrentUserId()
                };

                if (hasVariants)
                {
                    producto.Talla = null;
                    producto.Color = null;
                    producto.Stock = normVariants.Sum(v => v.Stock);
                    producto.PrecioVenta = Math.Round(normVariants.Min(v => v.Precio), 2);
                }
                else
                {
                    producto.Talla = talla?.Trim();
                    producto.Color = color?.Trim();
                    producto.Stock = Math.Max(0, stock);

                    var pvFinal = Math.Round(precioVenta, 2);
                    if (pvFinal <= precioCompra)
                    {
                        TempData["Err"] = $"El precio de venta (${pvFinal}) debe ser mayor al precio de compra (${precioCompra}).";
                        await FillProductoFormBags();
                        ViewBag.Producto = PresetProducto(nombreProducto, descripcion, talla, color, marca,
                            precioCompra, precioVenta, stock, proveedorID, categoriaID, subcategoriaID);
                        return View("ProductoForm");
                    }
                    producto.PrecioVenta = pvFinal;
                }

                await using var trx = await _context.Database.BeginTransactionAsync();
                try
                {
                    _context.Productos.Add(producto);
                    await _context.SaveChangesAsync();

                    if (hasVariants)
                    {
                        var variants = normVariants.Select(v => new ProductoVariante
                        {
                            ProductoID = producto.ProductoID,
                            Color = v.Color,
                            Talla = v.Talla,
                            PrecioVenta = v.Precio,
                            Stock = v.Stock,
                            SKU = GenerateSKU(producto, v.Color, v.Talla)
                        }).ToList();

                        await _context.ProductoVariantes.AddRangeAsync(variants);
                        await _context.SaveChangesAsync();
                    }

                    var saveResult = await SaveGalleryAsync(
                        producto,
                        Imagenes,
                        imagen,
                        ImagenPrincipalIndex,
                        existingImagenPath: null,
                        imagenesIgnore: ImagenesIgnore);

                    if (!saveResult.Success)
                    {
                        await trx.RollbackAsync();
                        TempData["Err"] = saveResult.ErrorMessage;
                        await FillProductoFormBags();
                        ViewBag.Producto = producto;
                        return View("ProductoForm");
                    }

                    await trx.CommitAsync();

                    _logger.LogInformation("Producto {ProductoID} creado con {VarianteCount} variantes",
                        producto.ProductoID, hasVariants ? normVariants.Count : 0);

                    TempData["Ok"] = hasVariants
                        ? $"Producto con {normVariants.Count} variantes añadido correctamente."
                        : "Producto añadido correctamente.";

                    return RedirectToAction("Productos");
                }
                catch (Exception ex)
                {
                    await trx.RollbackAsync();
                    _logger.LogError(ex, "Error al añadir producto {Nombre}", nombreProducto);
                    TempData["Err"] = "Ocurrió un error al guardar el producto.";
                    await FillProductoFormBags();
                    ViewBag.Producto = producto;
                    return View("ProductoForm");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error general en AnadirProducto");
                TempData["Err"] = "Ocurrió un error inesperado al procesar el producto.";
                await FillProductoFormBags();
                return View("ProductoForm");
            }
        }

        [HttpGet]
        public async Task<IActionResult> EditarProducto(int productoID)
        {
            var producto = await _context.Productos
                .Include(p => p.Variantes)
                .Include(p => p.Subcategoria)
                .FirstOrDefaultAsync(p => p.ProductoID == productoID);

            if (producto == null)
            {
                TempData["Err"] = "Producto no encontrado.";
                return RedirectToAction("Productos");
            }

            if (!IsAdmin() && producto.VendedorID != CurrentUserId())
            {
                TempData["Err"] = "No tienes permisos para editar este producto.";
                return RedirectToAction("Productos");
            }

            var variantes = producto.Variantes.OrderBy(v => v.Color).ThenBy(v => v.Talla).ToList();
            ViewBag.Variantes = variantes;

            await LoadGalleryData(producto);

            await FillProductoFormBags();
            ViewBag.Producto = producto;
            return View("ProductoForm");
        }

        [HttpPost, ValidateAntiForgeryToken, RequestFormLimits(MultipartBodyLengthLimit = _maxFormBytes)]
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
            IFormFile? imagen,
            IFormFile[]? Imagenes,
            int? ImagenPrincipalIndex,
            string? ImagenesIgnore,
            [FromForm] string[]? VarColor,
            [FromForm] string[]? VarTalla,
            [FromForm] string[]? VarPrecio,
            [FromForm] int[]? VarStock)
        {
            try
            {
                var producto = await _context.Productos
                    .Include(p => p.Variantes)
                    .FirstOrDefaultAsync(p => p.ProductoID == productoID);

                if (producto == null)
                {
                    TempData["Err"] = "Producto no encontrado.";
                    return RedirectToAction("Productos");
                }

                if (!IsAdmin() && producto.VendedorID != CurrentUserId())
                {
                    TempData["Err"] = "No tienes permisos para editar este producto.";
                    return RedirectToAction("Productos");
                }

                var sub = await _context.Subcategorias
                    .FirstOrDefaultAsync(s => s.SubcategoriaID == subcategoriaID && s.CategoriaID == categoriaID);

                if (sub == null || (!IsAdmin() && sub.VendedorID != CurrentUserId()))
                {
                    TempData["Err"] = "Subcategoría inválida o no pertenece a tu tienda.";
                    await FillProductoFormBags();
                    ViewBag.Producto = producto;
                    return View("ProductoForm");
                }

                // Fallback: si no llegaron Var* pero sí Variantes[i].*, leer del form
                if (VarColor == null || VarTalla == null || VarPrecio == null || VarStock == null)
                {
                    var v = ReadVariantesFromForm();
                    VarColor ??= v.Colores;
                    VarTalla ??= v.Tallas;
                    VarPrecio ??= v.Precios;
                    VarStock ??= v.Stocks;
                }

                var (hasVariants, normVariants, variantError) = await NormalizeAndValidateVariants(
                    VarColor, VarTalla, VarPrecio, VarStock, precioCompra);

                if (!string.IsNullOrEmpty(variantError))
                {
                    TempData["Err"] = variantError;
                    await FillProductoFormBags();
                    ViewBag.Producto = producto;
                    return View("ProductoForm");
                }

                producto.Nombre = (nombreProducto ?? string.Empty).Trim();
                producto.Descripcion = descripcion?.Trim();
                producto.Marca = marca?.Trim();
                producto.PrecioCompra = Math.Round(precioCompra, 2);
                producto.ProveedorID = proveedorID;
                producto.CategoriaID = categoriaID;
                producto.SubcategoriaID = subcategoriaID;

                if (hasVariants)
                {
                    producto.Talla = null;
                    producto.Color = null;
                    producto.Stock = normVariants.Sum(v => v.Stock);
                    producto.PrecioVenta = Math.Round(normVariants.Min(v => v.Precio), 2);
                }
                else
                {
                    producto.Talla = talla?.Trim();
                    producto.Color = color?.Trim();
                    producto.Stock = Math.Max(0, stock);

                    var pvFinal = Math.Round(precioVenta, 2);
                    if (pvFinal <= precioCompra)
                    {
                        TempData["Err"] = $"El precio de venta (${pvFinal}) debe ser mayor al precio de compra (${precioCompra}).";
                        await FillProductoFormBags();
                        ViewBag.Producto = producto;
                        return View("ProductoForm");
                    }
                    producto.PrecioVenta = pvFinal;
                }

                await using var trx = await _context.Database.BeginTransactionAsync();
                try
                {
                    var currentVariants = await _context.ProductoVariantes
                        .Where(v => v.ProductoID == producto.ProductoID).ToListAsync();

                    if (currentVariants.Any())
                        _context.ProductoVariantes.RemoveRange(currentVariants);

                    if (hasVariants)
                    {
                        var variants = normVariants.Select(v => new ProductoVariante
                        {
                            ProductoID = producto.ProductoID,
                            Color = v.Color,
                            Talla = v.Talla,
                            PrecioVenta = v.Precio,
                            Stock = v.Stock,
                            SKU = GenerateSKU(producto, v.Color, v.Talla)
                        }).ToList();

                        await _context.ProductoVariantes.AddRangeAsync(variants);
                    }

                    var saveResult = await SaveGalleryAsync(
                        producto,
                        Imagenes,
                        imagen,
                        ImagenPrincipalIndex,
                        existingImagenPath,
                        ImagenesIgnore);

                    if (!saveResult.Success)
                    {
                        await trx.RollbackAsync();
                        TempData["Err"] = saveResult.ErrorMessage;
                        await FillProductoFormBags();
                        ViewBag.Producto = producto;
                        return View("ProductoForm");
                    }

                    await _context.SaveChangesAsync();
                    await trx.CommitAsync();

                    _logger.LogInformation("Producto {ProductoID} actualizado con {VarianteCount} variantes",
                        producto.ProductoID, hasVariants ? normVariants.Count : 0);

                    TempData["Ok"] = hasVariants
                        ? $"Producto actualizado con {normVariants.Count} variantes."
                        : "Producto actualizado correctamente.";

                    return RedirectToAction("Productos");
                }
                catch (Exception ex)
                {
                    await trx.RollbackAsync();
                    _logger.LogError(ex, "Error al editar producto {ProductoID}", productoID);
                    TempData["Err"] = "Ocurrió un error al actualizar el producto.";
                    await FillProductoFormBags();
                    ViewBag.Producto = producto;
                    return View("ProductoForm");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error general en EditarProducto");
                TempData["Err"] = "Ocurrió un error inesperado al procesar el producto.";
                return RedirectToAction("Productos");
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarProducto(int productoID)
        {
            var producto = await _productosManager.GetByIdAsync(productoID);
            if (producto == null) return NotFound();
            if (!IsAdmin() && producto.VendedorID != CurrentUserId())
                return Forbid();

            try
            {
                var folder = ProductFolderAbs(productoID);
                if (Directory.Exists(folder))
                    Directory.Delete(folder, true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo eliminar la carpeta de galería del producto {pid}", productoID);
            }

            await _productosManager.DeleteAsync(productoID);
            return RedirectToAction("Productos");
        }

        // ======= Variantes JSON (opcional) =======
        [HttpGet]
        public async Task<IActionResult> VariantesJson(int productoId)
        {
            var producto = await _context.Productos
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.ProductoID == productoId);

            if (producto == null) return NotFound();

            if (!IsAdmin() && producto.VendedorID != CurrentUserId())
                return Forbid();

            var data = await _context.ProductoVariantes
                .AsNoTracking()
                .Where(v => v.ProductoID == productoId)
                .OrderBy(v => v.Color).ThenBy(v => v.Talla)
                .Select(v => new
                {
                    v.Color,
                    v.Talla,
                    v.PrecioVenta,
                    v.Stock,
                    v.SKU
                })
                .ToListAsync();

            return Json(data);
        }

        // ====================== MÉTODOS AUXILIARES ======================

        private async Task FillProductoFormBags()
        {
            ViewBag.Categorias = await _categoriasManager.GetAllAsync();
            ViewBag.Proveedores = await _proveedoresManager.GetAllAsync();
            ViewBag.Subcategorias = IsAdmin()
                ? await _subcategoriasManager.GetAllAsync()
                : await _subcategoriasManager.GetAllByVendedorAsync(CurrentUserId());
        }

        private Producto PresetProducto(string nombre, string desc, string talla, string color, string marca,
                                        decimal pc, decimal pv, int stock, int provId, int catId, int subId)
            => new()
            {
                Nombre = nombre,
                Descripcion = desc,
                Talla = talla,
                Color = color,
                Marca = marca,
                PrecioCompra = pc,
                PrecioVenta = pv,
                Stock = stock,
                ProveedorID = provId,
                CategoriaID = catId,
                SubcategoriaID = subId
            };

        private async Task<(bool hasVariants, List<(string Color, string Talla, decimal Precio, int Stock)> variants, string error)>
            NormalizeAndValidateVariants(string[]? VarColor, string[]? VarTalla, string[]? VarPrecio, int[]? VarStock, decimal precioCompra)
        {
            if (VarColor == null || VarTalla == null || VarPrecio == null || VarStock == null)
                return (false, new(), "");

            var len = new[] { VarColor.Length, VarTalla.Length, VarPrecio.Length, VarStock.Length }.Min();
            if (len <= 0) return (false, new(), "");

            var prices = ParseDecimalArray(VarPrecio.Take(len).ToArray());
            var variants = new List<(string Color, string Talla, decimal Precio, int Stock)>();
            var errors = new List<string>();

            for (int i = 0; i < len; i++)
            {
                var color = (VarColor[i] ?? "").Trim();
                var talla = (VarTalla[i] ?? "").Trim();
                var precio = prices[i];
                var stock = Math.Max(0, VarStock[i]);

                if (string.IsNullOrWhiteSpace(color))
                {
                    errors.Add($"La variante {i + 1} no tiene color especificado.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(talla))
                {
                    errors.Add($"La variante {i + 1} no tiene talla especificada.");
                    continue;
                }

                if (precio <= 0)
                {
                    errors.Add($"La variante {color} - {talla} tiene precio inválido.");
                    continue;
                }

                if (precio <= precioCompra)
                {
                    errors.Add($"La variante {color} - {talla} tiene precio menor o igual al precio de compra.");
                    continue;
                }

                if (stock < 0)
                {
                    errors.Add($"La variante {color} - {talla} tiene stock negativo.");
                    continue;
                }

                variants.Add((color, talla, precio, stock));
            }

            if (errors.Any())
                return (false, new(), string.Join(" ", errors));

            // Detectar duplicados (NO fusionar ni promediar)
            var dup = variants
                .GroupBy(v => (v.Color.Trim().ToLowerInvariant(), v.Talla.Trim().ToLowerInvariant()))
                .FirstOrDefault(g => g.Count() > 1);

            if (dup != null)
            {
                var (c, t) = dup.Key;
                return (false, new(), $"Hay combinaciones repetidas (Color/Talla): {c} - {t}. Elimina o corrige las repetidas.");
            }

            var cleaned = variants
                .Select(v => (v.Color, v.Talla, Precio: Math.Round(v.Precio, 2), v.Stock))
                .ToList();

            return (cleaned.Count > 0, cleaned, "");
        }

        // Acepta "4,00" y "4.00"
        private decimal[] ParseDecimalArray(string[] raw)
        {
            var list = new List<decimal>(raw.Length);
            var es = CultureInfo.GetCultureInfo("es-EC");

            foreach (var s in raw)
            {
                var txt = (s ?? "").Trim();
                decimal val;

                if (!decimal.TryParse(txt, NumberStyles.Any, es, out val))
                {
                    var norm = txt.Replace(',', '.');
                    if (!decimal.TryParse(norm, NumberStyles.Any, CultureInfo.InvariantCulture, out val))
                        val = 0m;
                }

                list.Add(Math.Round(val, 2));
            }
            return list.ToArray();
        }

        // Lee variantes cuando llegan como Variantes[i].*
        private (string[]? Colores, string[]? Tallas, string[]? Precios, int[]? Stocks) ReadVariantesFromForm()
        {
            var form = Request?.Form;
            if (form == null) return (null, null, null, null);

            var indices = new HashSet<int>();
            foreach (var key in form.Keys)
            {
                var m = Regex.Match(key, @"^Variantes\[(\d+)\]\.(Color|Talla|Precio|Stock)$");
                if (m.Success && int.TryParse(m.Groups[1].Value, out var i)) indices.Add(i);
            }
            if (indices.Count == 0) return (null, null, null, null);

            var max = indices.Max();
            var col = new string[max + 1];
            var tal = new string[max + 1];
            var pre = new string[max + 1];
            var stk = new int[max + 1];

            for (int i = 0; i <= max; i++)
            {
                col[i] = form[$"Variantes[{i}].Color"];
                tal[i] = form[$"Variantes[{i}].Talla"];
                pre[i] = form[$"Variantes[{i}].Precio"];
                int.TryParse(form[$"Variantes[{i}].Stock"], out stk[i]);
            }

            return (col, tal, pre, stk);
        }

        private string GenerateSKU(Producto producto, string color, string talla)
        {
            string marca = (producto.Marca ?? "PRD").Trim();
            string base3 = (marca.Length >= 3 ? marca.Substring(0, 3) : marca).ToUpperInvariant();

            string c = (color ?? "NA").Trim();
            string color3 = (c.Length >= 3 ? c.Substring(0, 3) : c).ToUpperInvariant();

            string t = (talla ?? "UNQ").Replace(" ", "").ToUpperInvariant();

            return $"{base3}-{producto.ProductoID}-{color3}-{t}";
        }

        private async Task LoadGalleryData(Producto producto)
        {
            var folder = ProductFolderAbs(producto.ProductoID);
            var metaPath = Path.Combine(folder, $"product-{producto.ProductoID}.gallery.json");
            var gallery = new List<string>();
            string? portada = producto.ImagenPath;

            try
            {
                if (System.IO.File.Exists(metaPath))
                {
                    var json = await System.IO.File.ReadAllTextAsync(metaPath);
                    using var doc = JsonDocument.Parse(json);

                    if (doc.RootElement.TryGetProperty("images", out var arr) && arr.ValueKind == JsonValueKind.Array)
                    {
                        gallery = arr.EnumerateArray()
                            .Select(e => e.GetString()!)
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .ToList();
                    }

                    if (doc.RootElement.TryGetProperty("portada", out var p) && p.ValueKind == JsonValueKind.String)
                        portada = p.GetString();
                }
                else
                {
                    // Fallback: escanear carpeta (por si se perdió el JSON)
                    if (Directory.Exists(folder))
                    {
                        var files = Directory.GetFiles(folder)
                            .Where(f => _allowedImgExt.Contains(Path.GetExtension(f)))
                            .OrderBy(f => f)
                            .Select(f => $"/images/Productos/{producto.ProductoID}/{Path.GetFileName(f)}".Replace("\\", "/"))
                            .ToList();

                        gallery = files;
                        portada ??= files.FirstOrDefault();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al cargar galería del producto {ProductoID}", producto.ProductoID);
            }

            ViewBag.Galeria = gallery;
            ViewBag.Portada = portada;
        }

        private async Task<(bool Success, string ErrorMessage)> SaveGalleryAsync(
            Producto producto,
            IFormFile[]? nuevasImagenes,
            IFormFile? imagenLegacy,
            int? imagenPrincipalIndex,
            string? existingImagenPath = null,
            string? imagenesIgnore = null)
        {
            try
            {
                var folder = ProductFolderAbs(producto.ProductoID);
                Directory.CreateDirectory(folder);

                // Seguridad de path
                if (!Path.GetFullPath(folder).StartsWith(Path.GetFullPath(GalleryRootAbs()), StringComparison.OrdinalIgnoreCase))
                    return (false, "Ruta de galería inválida.");

                // Cargar existentes desde JSON (si no hay, escanea)
                var metaPath = Path.Combine(folder, $"product-{producto.ProductoID}.gallery.json");
                var imagenesExistentes = await LoadExistingImages(metaPath);
                if (imagenesExistentes.Count == 0 && Directory.Exists(folder))
                {
                    imagenesExistentes = Directory.GetFiles(folder)
                        .Where(f => _allowedImgExt.Contains(Path.GetExtension(f)))
                        .Select(f => $"/images/Productos/{producto.ProductoID}/{Path.GetFileName(f)}".Replace("\\", "/"))
                        .ToList();
                }

                // --- Procesar "ignore" ---
                var indicesNuevasAIgnorar = new HashSet<int>();
                var urlsExistentesAEliminar = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                if (!string.IsNullOrWhiteSpace(imagenesIgnore))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(imagenesIgnore);
                        if (doc.RootElement.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var el in doc.RootElement.EnumerateArray())
                            {
                                if (el.ValueKind == JsonValueKind.String)
                                {
                                    var u = el.GetString();
                                    if (!string.IsNullOrWhiteSpace(u))
                                        urlsExistentesAEliminar.Add(u!);
                                }
                                else if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var idx) && idx >= 0)
                                {
                                    indicesNuevasAIgnorar.Add(idx);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "JSON de ImagenesIgnore inválido");
                    }
                }

                // Quitar existentes marcadas para eliminar (por URL exacta)
                if (urlsExistentesAEliminar.Count > 0 && imagenesExistentes.Count > 0)
                {
                    var toKeep = new List<string>();
                    foreach (var url in imagenesExistentes)
                    {
                        if (!urlsExistentesAEliminar.Contains(url))
                        {
                            toKeep.Add(url);
                        }
                        else
                        {
                            // borrar archivo físico
                            try
                            {
                                var nombre = url.Split('/', '\\').Last();
                                var ruta = Path.Combine(folder, nombre);
                                if (System.IO.File.Exists(ruta)) System.IO.File.Delete(ruta);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "No se pudo borrar imagen existente {Url}", url);
                            }
                        }
                    }
                    imagenesExistentes = toKeep;
                }

                // Recopilar nuevas imágenes (filtrando ignoradas por índice)
                var archivos = new List<IFormFile>();
                if (nuevasImagenes != null && nuevasImagenes.Length > 0)
                {
                    for (int i = 0; i < nuevasImagenes.Length; i++)
                    {
                        if (!indicesNuevasAIgnorar.Contains(i))
                            archivos.Add(nuevasImagenes[i]);
                    }
                }
                else if (imagenLegacy != null)
                {
                    archivos.Add(imagenLegacy);
                }

                // Validar/guardar nuevas (hasta completar el máximo)
                var nuevasUrls = new List<string>();
                foreach (var archivo in archivos)
                {
                    if (nuevasUrls.Count + imagenesExistentes.Count >= _maxFiles) break;

                    var resultado = await GuardarImagenSegura(archivo, folder);
                    if (!resultado.Success)
                        return (false, resultado.ErrorMessage);

                    nuevasUrls.Add(resultado.Url!);
                }

                // Combinar y deduplicar (máx 8)
                var todasLasImagenes = imagenesExistentes
                    .Concat(nuevasUrls)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(_maxFiles)
                    .ToList();

                // Portada
                string? imagenPrincipal = null;

                if (imagenPrincipalIndex.HasValue && imagenPrincipalIndex.Value >= 0
                    && imagenPrincipalIndex.Value < nuevasUrls.Count)
                {
                    imagenPrincipal = nuevasUrls[imagenPrincipalIndex.Value];
                }
                else if (!string.IsNullOrWhiteSpace(existingImagenPath)
                         && todasLasImagenes.Contains(existingImagenPath!))
                {
                    imagenPrincipal = existingImagenPath!;
                }
                else if (!string.IsNullOrWhiteSpace(producto.ImagenPath)
                         && todasLasImagenes.Contains(producto.ImagenPath))
                {
                    // Preservar portada previa si sigue existiendo
                    imagenPrincipal = producto.ImagenPath;
                }
                else if (todasLasImagenes.Any())
                {
                    imagenPrincipal = todasLasImagenes[0];
                }

                await GuardarMetadata(metaPath, todasLasImagenes, imagenPrincipal);

                producto.ImagenPath = imagenPrincipal;

                await LimpiarImagenesNoUsadas(folder, todasLasImagenes);

                return (true, "");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en SaveGalleryAsync para producto {ProductoID}", producto.ProductoID);
                return (false, "Error al guardar la galería de imágenes.");
            }
        }

        private async Task<List<string>> LoadExistingImages(string metaPath)
        {
            var imagenes = new List<string>();
            try
            {
                if (System.IO.File.Exists(metaPath))
                {
                    using var doc = JsonDocument.Parse(await System.IO.File.ReadAllTextAsync(metaPath));
                    if (doc.RootElement.TryGetProperty("images", out var arr) && arr.ValueKind == JsonValueKind.Array)
                    {
                        imagenes = arr.EnumerateArray()
                            .Select(e => e.GetString()!)
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .ToList();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al cargar imágenes existentes");
            }
            return imagenes;
        }

        private async Task<(bool Success, string? Url, string ErrorMessage)> GuardarImagenSegura(IFormFile archivo, string folder)
        {
            try
            {
                if (archivo.Length <= 0 || archivo.Length > _maxImgBytes)
                    return (false, null, "Archivo de imagen inválido o demasiado grande.");

                var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
                if (!_allowedImgExt.Contains(extension))
                    return (false, null, "Formato de imagen no permitido.");

                await using var stream = archivo.OpenReadStream();
                if (!LooksLikeImage(stream, extension, out var mime) || (mime != null && !_allowedMime.Contains(mime)))
                    return (false, null, "Contenido de imagen no válido.");

                var nombreArchivo = $"{Guid.NewGuid():N}{extension}";
                var rutaCompleta = Path.Combine(folder, nombreArchivo);

                await using var fileStream = new FileStream(rutaCompleta, FileMode.Create);
                await archivo.CopyToAsync(fileStream);

                var productId = Path.GetFileName(folder);
                var url = $"/images/Productos/{productId}/{nombreArchivo}".Replace("\\", "/");
                return (true, url, "");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar imagen");
                return (false, null, "Error al guardar la imagen.");
            }
        }

        private async Task GuardarMetadata(string metaPath, List<string> imagenes, string? imagenPrincipal)
        {
            var metadata = new { portada = imagenPrincipal, images = imagenes };
            var json = JsonSerializer.Serialize(metadata, _jsonOpts);
            await System.IO.File.WriteAllTextAsync(metaPath, json);
        }

        private async Task LimpiarImagenesNoUsadas(string folder, List<string> imagenesUsadas)
        {
            try
            {
                var archivosEnCarpeta = Directory.GetFiles(folder)
                    .Where(f => !f.EndsWith(".gallery.json", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var archivo in archivosEnCarpeta)
                {
                    var nombreArchivo = Path.GetFileName(archivo);
                    var enUso = imagenesUsadas.Any(url => url.EndsWith("/" + nombreArchivo, StringComparison.OrdinalIgnoreCase));

                    if (!enUso)
                    {
                        try { System.IO.File.Delete(archivo); }
                        catch (Exception ex) { _logger.LogWarning(ex, "No se pudo eliminar archivo {Archivo}", archivo); }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al limpiar imágenes no usadas");
            }
        }

        // —— Sniffing rápido de archivos (cabeceras) ——
        private static bool LooksLikeImage(Stream s, string extLower, out string? detected)
        {
            detected = null;
            try
            {
                s.Seek(0, SeekOrigin.Begin);
                Span<byte> head = stackalloc byte[16];
                int read = s.Read(head);
                s.Seek(0, SeekOrigin.Begin);
                if (read < 4) return false;

                // JPEG FF D8
                if (head[0] == 0xFF && head[1] == 0xD8) { detected = "image/jpeg"; return true; }
                // PNG 89 50 4E 47
                if (head[0] == 0x89 && head[1] == 0x50 && head[2] == 0x4E && head[3] == 0x47) { detected = "image/png"; return true; }
                // GIF 'G','I','F'
                if (head[0] == 0x47 && head[1] == 0x49 && head[2] == 0x46) { detected = "image/gif"; return true; }
                // WEBP: 'RIFF' .... 'WEBP'
                if (read >= 12 &&
                    head[0] == (byte)'R' && head[1] == (byte)'I' && head[2] == (byte)'F' && head[3] == (byte)'F' &&
                    head[8] == (byte)'W' && head[9] == (byte)'E' && head[10] == (byte)'B' && head[11] == (byte)'P')
                { detected = "image/webp"; return true; }
            }
            catch { }
            return false;
        }

        private sealed class TupleIgnoreCaseComparer : IEqualityComparer<(string c, string t)>
        {
            public bool Equals((string c, string t) x, (string c, string t) y)
                => string.Equals(x.c, y.c, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.t, y.t, StringComparison.OrdinalIgnoreCase);

            public int GetHashCode((string c, string t) k)
            {
                unchecked
                {
                    int h1 = StringComparer.OrdinalIgnoreCase.GetHashCode(k.c ?? string.Empty);
                    int h2 = StringComparer.OrdinalIgnoreCase.GetHashCode(k.t ?? string.Empty);
                    return (h1 * 397) ^ h2;
                }
            }
        }
    }
}
