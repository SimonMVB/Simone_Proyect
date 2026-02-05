using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Simone.Data;
using Simone.Models;
using Simone.Services;

namespace Simone.Controllers
{
    /// <summary>
    /// Panel de administración y gestión de vendedor
    /// Gestiona usuarios, categorías, subcategorías, proveedores y productos
    /// Versión optimizada con mejores prácticas empresariales
    /// </summary>
    [Authorize(Roles = "Administrador,Vendedor")]
    public class PanelController : Controller
    {
        #region Dependencias

        private readonly ILogger<PanelController> _logger;
        private readonly TiendaDbContext _context;
        private readonly UserManager<Usuario> _userManager;
        private readonly RoleManager<Roles> _roleManager;
        private readonly CategoriasService _categoriasManager;
        private readonly SubcategoriasService _subcategoriasManager;
        private readonly ProductosService _productosManager;
        private readonly ProveedorService _proveedoresManager;
        private readonly IWebHostEnvironment _env;
        private readonly IMemoryCache _cache;
        private readonly CategoriaAtributoService _categoriaAtributoService;
        private readonly ProductoAtributoService _productoAtributoService;

        #endregion

        #region Constantes

        // Roles
        private const string ROL_ADMINISTRADOR = "Administrador";
        private const string ROL_VENDEDOR = "Vendedor";

        // Rutas
        private const string FOLDER_IMAGES = "images";
        private const string FOLDER_PRODUCTOS = "Productos";

        // Límites de archivos
        private const int MAX_IMAGENES_GALERIA = 8;
        private const long MAX_IMAGEN_BYTES = 8 * 1024 * 1024;
        private const long MAX_FORM_BYTES = 64L * 1024 * 1024;

        // Extensiones permitidas
        private static readonly HashSet<string> EXTENSIONES_PERMITIDAS_IMAGEN = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".gif"
        };

        private static readonly HashSet<string> MIME_TYPES_PERMITIDOS = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg", "image/png", "image/webp", "image/gif"
        };

        // Patrones de archivos
        private const string PATTERN_GALLERY_JSON = "product-{0}.gallery.json";
        private const string PATTERN_GALLERY_EXTENSION = ".gallery.json";

        // JSON Properties
        private const string JSON_PROP_PORTADA = "portada";
        private const string JSON_PROP_IMAGES = "images";

        // SKU
        private const string SKU_DEFAULT_MARCA = "PRD";
        private const string SKU_DEFAULT_COLOR = "NA";
        private const string SKU_DEFAULT_TALLA = "UNQ";
        private const int SKU_LENGTH_CODIGO = 3;

        // Cache
        private const string CACHE_KEY_CATEGORIAS = "Categorias_All";
        private const string CACHE_KEY_PROVEEDORES = "Proveedores_All";
        private const string CACHE_KEY_SUBCATEGORIAS_PREFIX = "Subcategorias_Vendor_";
        private const string CACHE_KEY_TIENDAS = "Tiendas_All";

        private static readonly TimeSpan CACHE_DURATION_CATEGORIAS = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan CACHE_DURATION_PROVEEDORES = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan CACHE_DURATION_SUBCATEGORIAS = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan CACHE_DURATION_TIENDAS = TimeSpan.FromMinutes(30);

        // JSON Serializer
        private static readonly JsonSerializerOptions JSON_OPTIONS = new() { WriteIndented = true };

        // Headers AJAX
        private const string HEADER_AJAX = "X-Requested-With";
        private const string HEADER_AJAX_VALUE = "XMLHttpRequest";



        #endregion

        #region Constructor

        public PanelController(
    ILogger<PanelController> logger,
    TiendaDbContext context,
    UserManager<Usuario> user,
    RoleManager<Roles> rol,
    CategoriasService categorias,
    SubcategoriasService subcategorias,
    ProductosService productos,
    ProveedorService proveedores,
    IWebHostEnvironment env,
    IMemoryCache cache,
    CategoriaAtributoService categoriaAtributoService,      // ✅ NUEVO
    ProductoAtributoService productoAtributoService)         // ✅ NUEVO
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
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _categoriaAtributoService = categoriaAtributoService ?? throw new ArgumentNullException(nameof(categoriaAtributoService));     // ✅ NUEVO
            _productoAtributoService = productoAtributoService ?? throw new ArgumentNullException(nameof(productoAtributoService));         // ✅ NUEVO
        }

        #endregion

        #region Helpers - General

        private string CurrentUserId() => _userManager.GetUserId(User)!;
        private bool IsAdmin() => User.IsInRole(ROL_ADMINISTRADOR);
        private string GalleryRootAbs() => Path.Combine(_env.WebRootPath, FOLDER_IMAGES, FOLDER_PRODUCTOS);
        private string ProductFolderAbs(int productId) => Path.Combine(GalleryRootAbs(), productId.ToString());

        #endregion

        #region Helpers - Cache

        private async Task<List<SelectListItem>> ObtenerTiendasConCacheAsync(CancellationToken ct = default)
        {
            return await _cache.GetOrCreateAsync(CACHE_KEY_TIENDAS, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CACHE_DURATION_TIENDAS;
                _logger.LogDebug("Cargando tiendas desde BD (cache miss)");
                return await _context.Vendedores.AsNoTracking().OrderBy(v => v.Nombre)
                    .Select(v => new SelectListItem { Value = v.VendedorId.ToString(), Text = v.Nombre })
                    .ToListAsync(ct);
            }) ?? new List<SelectListItem>();
        }

        private async Task<List<Categorias>> ObtenerCategoriasConCacheAsync()
        {
            return await _cache.GetOrCreateAsync(CACHE_KEY_CATEGORIAS, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CACHE_DURATION_CATEGORIAS;
                _logger.LogDebug("Cargando categorías desde BD (cache miss)");
                return await _categoriasManager.GetAllAsync();
            }) ?? new List<Categorias>();
        }

        private async Task<List<Proveedores>> ObtenerProveedoresConCacheAsync()
        {
            return await _cache.GetOrCreateAsync(CACHE_KEY_PROVEEDORES, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CACHE_DURATION_PROVEEDORES;
                _logger.LogDebug("Cargando proveedores desde BD (cache miss)");
                return await _proveedoresManager.GetAllAsync();
            }) ?? new List<Proveedores>();
        }

        private async Task<List<Subcategorias>> ObtenerSubcategoriasVendedorConCacheAsync(string vendedorId)
        {
            var cacheKey = $"{CACHE_KEY_SUBCATEGORIAS_PREFIX}{vendedorId}";
            return await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CACHE_DURATION_SUBCATEGORIAS;
                _logger.LogDebug("Cargando subcategorías de vendedor desde BD (cache miss). VendedorId: {VendedorId}", vendedorId);
                return await _subcategoriasManager.GetAllByVendedorAsync(vendedorId);
            }) ?? new List<Subcategorias>();
        }

        private void InvalidarCacheTiendas()
        {
            _cache.Remove(CACHE_KEY_TIENDAS);
            _logger.LogDebug("Cache de tiendas invalidado");
        }

        private void InvalidarCacheCategorias()
        {
            _cache.Remove(CACHE_KEY_CATEGORIAS);
            _logger.LogDebug("Cache de categorías invalidado");
        }

        private void InvalidarCacheProveedores()
        {
            _cache.Remove(CACHE_KEY_PROVEEDORES);
            _logger.LogDebug("Cache de proveedores invalidado");
        }

        private void InvalidarCacheSubcategoriasVendedor(string vendedorId)
        {
            var cacheKey = $"{CACHE_KEY_SUBCATEGORIAS_PREFIX}{vendedorId}";
            _cache.Remove(cacheKey);
            _logger.LogDebug("Cache de subcategorías invalidado. VendedorId: {VendedorId}", vendedorId);
        }

        #endregion

        #region Index

        [HttpGet]
        public IActionResult Index()
        {
            try
            {
                _logger.LogInformation("Panel accedido. Usuario: {User}, Rol: {Role}",
                    User?.Identity?.Name, IsAdmin() ? ROL_ADMINISTRADOR : ROL_VENDEDOR);
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar panel");
                return View();
            }
        }

        #endregion

        #region Usuarios (Admin)

        [Authorize(Roles = "Administrador")]
        [HttpGet]
        public async Task<IActionResult> Usuarios(string? tiendaId = null, CancellationToken ct = default)
        {
            try
            {
                _logger.LogInformation("Listado de usuarios solicitado. Filtro TiendaId: {TiendaId}", tiendaId ?? "Ninguno");

                var tiendasList = await ObtenerTiendasConCacheAsync(ct);
                var usuariosConRol = from u in _userManager.Users.AsNoTracking()
                                     join ur in _context.UserRoles.AsNoTracking() on u.Id equals ur.UserId
                                     join r in _context.Roles.AsNoTracking() on ur.RoleId equals r.Id
                                     select new { u, rolId = r.Id, rolName = r.Name };

                if (!string.IsNullOrWhiteSpace(tiendaId) && int.TryParse(tiendaId, out var vid))
                {
                    usuariosConRol = usuariosConRol.Where(x => x.u.VendedorId == vid);
                    _logger.LogDebug("Filtrando usuarios por TiendaId: {TiendaId}", vid);
                }

                var lista = await usuariosConRol.Select(x => new Usuario
                {
                    Id = x.u.Id,
                    Email = x.u.Email,
                    Telefono = x.u.Telefono,
                    Direccion = x.u.Direccion,
                    NombreCompleto = x.u.NombreCompleto,
                    Activo = x.u.Activo,
                    RolID = x.rolId,
                    VendedorId = x.u.VendedorId
                }).ToListAsync(ct);

                _logger.LogDebug("Usuarios cargados: {Count}", lista.Count);

                ViewBag.Usuarios = lista;
                ViewBag.Roles = _roleManager.Roles.AsNoTracking()
                    .Select(r => new SelectListItem { Value = r.Id, Text = r.Name }).ToList();
                ViewBag.Tiendas = tiendasList;
                ViewBag.FiltroTiendaId = tiendaId ?? "";
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar usuarios");
                TempData["MensajeError"] = "Error al cargar la lista de usuarios.";
                return View();
            }
        }

        [Authorize(Roles = "Administrador")]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AsignarTiendaUsuario(string usuarioID, int tiendaID, string? returnTiendaId = null, CancellationToken ct = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(usuarioID))
                {
                    _logger.LogWarning("Intento de asignar tienda con usuario inválido");
                    TempData["MensajeError"] = "Usuario inválido.";
                    return RedirectToAction(nameof(Usuarios), new { tiendaId = returnTiendaId });
                }

                var user = await _userManager.FindByIdAsync(usuarioID);
                if (user == null)
                {
                    _logger.LogWarning("Intento de asignar tienda a usuario inexistente. UsuarioId: {UsuarioId}", usuarioID);
                    TempData["MensajeError"] = "Usuario no encontrado.";
                    return RedirectToAction(nameof(Usuarios), new { tiendaId = returnTiendaId });
                }

                var vendedor = await _context.Vendedores.FindAsync(new object[] { tiendaID }, ct);
                if (vendedor == null)
                {
                    _logger.LogWarning("Intento de asignar tienda inexistente. TiendaId: {TiendaId}", tiendaID);
                    TempData["MensajeError"] = "Tienda no válida.";
                    return RedirectToAction(nameof(Usuarios), new { tiendaId = returnTiendaId });
                }

                user.VendedorId = tiendaID;
                var res = await _userManager.UpdateAsync(user);

                if (res.Succeeded)
                {
                    _logger.LogInformation("Tienda asignada. UsuarioId: {UsuarioId}, Email: {Email}, TiendaId: {TiendaId}, Tienda: {TiendaNombre}",
                        user.Id, user.Email, tiendaID, vendedor.Nombre);
                    TempData["MensajeExito"] = "Tienda asignada correctamente.";
                }
                else
                {
                    _logger.LogWarning("Error al asignar tienda. UsuarioId: {UsuarioId}, Errores: {Errores}",
                        user.Id, string.Join(", ", res.Errors.Select(e => e.Description)));
                    TempData["MensajeError"] = "No se pudo asignar la tienda.";
                }

                return RedirectToAction(nameof(Usuarios), new { tiendaId = returnTiendaId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al asignar tienda. UsuarioId: {UsuarioId}", usuarioID);
                TempData["MensajeError"] = "Error inesperado al asignar la tienda.";
                return RedirectToAction(nameof(Usuarios), new { tiendaId = returnTiendaId });
            }
        }

        [Authorize(Roles = "Administrador")]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> QuitarTiendaUsuario(string usuarioID, string? returnTiendaId = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(usuarioID))
                {
                    _logger.LogWarning("Intento de quitar tienda con usuario inválido");
                    TempData["MensajeError"] = "Usuario inválido.";
                    return RedirectToAction(nameof(Usuarios), new { tiendaId = returnTiendaId });
                }

                var user = await _userManager.FindByIdAsync(usuarioID);
                if (user == null)
                {
                    _logger.LogWarning("Intento de quitar tienda a usuario inexistente. UsuarioId: {UsuarioId}", usuarioID);
                    TempData["MensajeError"] = "Usuario no encontrado.";
                    return RedirectToAction(nameof(Usuarios), new { tiendaId = returnTiendaId });
                }

                var tiendaAnterior = user.VendedorId;
                user.VendedorId = null;
                var res = await _userManager.UpdateAsync(user);

                if (res.Succeeded)
                {
                    _logger.LogInformation("Tienda quitada. UsuarioId: {UsuarioId}, Email: {Email}, TiendaAnterior: {TiendaId}",
                        user.Id, user.Email, tiendaAnterior);
                    TempData["MensajeExito"] = "Tienda quitada correctamente.";
                }
                else
                {
                    _logger.LogWarning("Error al quitar tienda. UsuarioId: {UsuarioId}", user.Id);
                    TempData["MensajeError"] = "No se pudo quitar la tienda.";
                }

                return RedirectToAction(nameof(Usuarios), new { tiendaId = returnTiendaId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al quitar tienda. UsuarioId: {UsuarioId}", usuarioID);
                TempData["MensajeError"] = "Error inesperado al quitar la tienda.";
                return RedirectToAction(nameof(Usuarios), new { tiendaId = returnTiendaId });
            }
        }

        [Authorize(Roles = "Administrador")]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearTiendaSimple(string nombre, CancellationToken ct = default)
        {
            // SEGURIDAD: Este endpoint solo debe ser llamado por AJAX
            if (!EsAjax())
            {
                _logger.LogWarning("Intento de acceder a CrearTiendaSimple sin AJAX");
                TempData["MensajeError"] = "Este endpoint solo acepta solicitudes AJAX";
                return RedirectToAction(nameof(Usuarios));
            }

            try
            {
                if (string.IsNullOrWhiteSpace(nombre))
                {
                    _logger.LogWarning("Intento de crear tienda sin nombre");
                    return Json(new { ok = false, msg = "Nombre requerido." });
                }

                var vendedor = new Vendedor { Nombre = nombre.Trim() };
                _context.Vendedores.Add(vendedor);
                await _context.SaveChangesAsync(ct);

                _logger.LogInformation("Tienda creada. TiendaId: {TiendaId}, Nombre: {Nombre}", vendedor.VendedorId, vendedor.Nombre);

                InvalidarCacheTiendas();

                var tiendas = await ObtenerTiendasConCacheAsync(ct);
                return Json(new
                {
                    ok = true,
                    newId = vendedor.VendedorId.ToString(),
                    newText = vendedor.Nombre,
                    tiendas = tiendas.Select(t => new { value = t.Value, text = t.Text })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear tienda. Nombre: {Nombre}", nombre);
                return Json(new { ok = false, msg = "Error al crear la tienda." });
            }
        }

        [Authorize(Roles = "Administrador")]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarUsuario(string id)
        {
            try
            {
                var usuario = await _userManager.FindByIdAsync(id);
                if (usuario == null)
                {
                    _logger.LogWarning("Intento de eliminar usuario inexistente. UsuarioId: {UsuarioId}", id);
                    TempData["MensajeError"] = "Usuario no encontrado.";
                    return RedirectToAction(nameof(Usuarios));
                }

                var resultado = await _userManager.DeleteAsync(usuario);
                if (resultado.Succeeded)
                {
                    _logger.LogWarning("Usuario eliminado. UsuarioId: {UsuarioId}, Email: {Email}", usuario.Id, usuario.Email);
                    TempData["MensajeExito"] = "Usuario eliminado correctamente.";
                }
                else
                {
                    _logger.LogError("Error al eliminar usuario. UsuarioId: {UsuarioId}, Errores: {Errores}",
                        usuario.Id, string.Join(", ", resultado.Errors.Select(e => e.Description)));
                    TempData["MensajeError"] = "Error al eliminar usuario.";
                }

                return RedirectToAction(nameof(Usuarios));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar usuario. UsuarioId: {UsuarioId}", id);
                TempData["MensajeError"] = "Error inesperado al eliminar el usuario.";
                return RedirectToAction(nameof(Usuarios));
            }
        }

        [Authorize(Roles = "Administrador")]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarRol(string usuarioID, string nuevoRolID)
        {
            try
            {
                if (string.IsNullOrEmpty(nuevoRolID) || string.IsNullOrEmpty(usuarioID))
                {
                    _logger.LogWarning("Intento de cambiar rol con datos inválidos");
                    TempData["MensajeError"] = "El rol seleccionado no es válido.";
                    return RedirectToAction(nameof(Usuarios));
                }

                var usuario = await _userManager.FindByIdAsync(usuarioID);
                if (usuario == null)
                {
                    _logger.LogWarning("Intento de cambiar rol a usuario inexistente. UsuarioId: {UsuarioId}", usuarioID);
                    TempData["MensajeError"] = "Usuario no encontrado.";
                    return RedirectToAction(nameof(Usuarios));
                }

                var nuevoRol = await _roleManager.FindByIdAsync(nuevoRolID);
                if (nuevoRol == null)
                {
                    _logger.LogWarning("Intento de asignar rol inexistente. RolId: {RolId}", nuevoRolID);
                    TempData["MensajeError"] = "El rol seleccionado no existe.";
                    return RedirectToAction(nameof(Usuarios));
                }

                var rolesActuales = await _userManager.GetRolesAsync(usuario);
                var resultadoEliminar = await _userManager.RemoveFromRolesAsync(usuario, rolesActuales);

                if (!resultadoEliminar.Succeeded)
                {
                    _logger.LogWarning("Error al eliminar roles anteriores. UsuarioId: {UsuarioId}", usuarioID);
                    TempData["MensajeError"] = "No se pudieron eliminar los roles anteriores.";
                    return RedirectToAction(nameof(Usuarios));
                }

                var resultadoAsignar = await _userManager.AddToRoleAsync(usuario, nuevoRol.Name!);
                if (!resultadoAsignar.Succeeded)
                {
                    _logger.LogWarning("Error al asignar nuevo rol. UsuarioId: {UsuarioId}, Rol: {Rol}", usuarioID, nuevoRol.Name);
                    TempData["MensajeError"] = "No se pudo asignar el nuevo rol.";
                    return RedirectToAction(nameof(Usuarios));
                }

                _logger.LogInformation("Rol cambiado. UsuarioId: {UsuarioId}, Email: {Email}, NuevoRol: {Rol}",
                    usuario.Id, usuario.Email, nuevoRol.Name);
                TempData["MensajeExito"] = $"El rol del usuario {usuario.Email} fue actualizado a {nuevoRol.Name}.";
                return RedirectToAction(nameof(Usuarios));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cambiar rol. UsuarioId: {UsuarioId}", usuarioID);
                TempData["MensajeError"] = "Error inesperado al cambiar el rol.";
                return RedirectToAction(nameof(Usuarios));
            }
        }

        #endregion

        #region Categorías (Admin)

        [Authorize(Roles = "Administrador")]
        [HttpGet]
        public async Task<IActionResult> Categorias()
        {
            try
            {
                _logger.LogInformation("Listado de categorías solicitado");
                var categorias = await ObtenerCategoriasConCacheAsync();
                _logger.LogDebug("Categorías cargadas: {Count}", categorias.Count);
                ViewBag.Categorias = categorias;
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar categorías");
                TempData["MensajeError"] = "Error al cargar las categorías.";
                return View();
            }
        }

        [Authorize(Roles = "Administrador")]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AnadirCategoria(string nombreCategoria)
        {
            try
            {
                var categoria = new Categorias { Nombre = (nombreCategoria ?? string.Empty).Trim() };
                await _categoriasManager.AddAsync(categoria);
                _logger.LogInformation("Categoría creada. CategoriaId: {CategoriaId}, Nombre: {Nombre}",
                    categoria.CategoriaID, categoria.Nombre);
                InvalidarCacheCategorias();
                return RedirectToAction(nameof(Categorias));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear categoría. Nombre: {Nombre}", nombreCategoria);
                TempData["MensajeError"] = "Error al crear la categoría.";
                return RedirectToAction(nameof(Categorias));
            }
        }

        [Authorize(Roles = "Administrador")]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarCategoria(int categoriaID, string nombreCategoria)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("ModelState inválido al editar categoría");
                    return RedirectToAction(nameof(Categorias));
                }

                var categoria = await _categoriasManager.GetByIdAsync(categoriaID);
                if (categoria == null)
                {
                    _logger.LogWarning("Intento de editar categoría inexistente. CategoriaId: {CategoriaId}", categoriaID);
                    return NotFound();
                }

                categoria.Nombre = (nombreCategoria ?? string.Empty).Trim();
                await _categoriasManager.UpdateAsync(categoria);
                _logger.LogInformation("Categoría actualizada. CategoriaId: {CategoriaId}, Nombre: {Nombre}",
                    categoria.CategoriaID, categoria.Nombre);
                InvalidarCacheCategorias();
                return RedirectToAction(nameof(Categorias));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al editar categoría. CategoriaId: {CategoriaId}", categoriaID);
                TempData["MensajeError"] = "Error al actualizar la categoría.";
                return RedirectToAction(nameof(Categorias));
            }
        }

        [Authorize(Roles = "Administrador")]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarCategoria(int categoriaID)
        {
            try
            {
                await _categoriasManager.DeleteAsync(categoriaID);
                _logger.LogInformation("Categoría eliminada. CategoriaId: {CategoriaId}", categoriaID);
                InvalidarCacheCategorias();
                return RedirectToAction(nameof(Categorias));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar categoría. CategoriaId: {CategoriaId}", categoriaID);
                TempData["MensajeError"] = "Error al eliminar la categoría.";
                return RedirectToAction(nameof(Categorias));
            }
        }

        #endregion

        #region Subcategorías

        [HttpGet]
        public async Task<IActionResult> Subcategorias(string? vendorId, CancellationToken ct = default)
        {
            try
            {
                var vid = (IsAdmin() && !string.IsNullOrWhiteSpace(vendorId)) ? vendorId! : CurrentUserId();
                _logger.LogInformation("Listado de subcategorías solicitado. VendorId: {VendorId}", vid);

                var subcategorias = await _context.Subcategorias.AsNoTracking().Include(s => s.Categoria)
                    .Where(s => s.VendedorID == vid).OrderBy(s => s.CategoriaID).ThenBy(s => s.NombreSubcategoria)
                    .ToListAsync(ct);

                _logger.LogDebug("Subcategorías cargadas: {Count}", subcategorias.Count);
                ViewBag.Subcategorias = subcategorias;
                ViewBag.TargetVendorId = (IsAdmin() && !string.IsNullOrWhiteSpace(vendorId)) ? vendorId : null;
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar subcategorías");
                TempData["MensajeError"] = "Error al cargar las subcategorías.";
                return View();
            }
        }

        [HttpGet]
        public async Task<IActionResult> AnadirSubcategoria()
        {
            try
            {
                ViewBag.Categorias = await ObtenerCategoriasConCacheAsync();
                return View("SubcategoriaForm");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar formulario de subcategoría");
                return RedirectToAction(nameof(Subcategorias));
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AnadirSubcategoria(int categoriaID, string nombresubCategoria)
        {
            try
            {
                var subcategoria = new Subcategorias
                {
                    CategoriaID = categoriaID,
                    NombreSubcategoria = (nombresubCategoria ?? string.Empty).Trim(),
                    VendedorID = CurrentUserId()
                };

                var ok = await _subcategoriasManager.AddAsync(subcategoria);
                if (ok)
                {
                    _logger.LogInformation("Subcategoría creada. SubcategoriaId: {SubcategoriaId}, Nombre: {Nombre}, VendedorId: {VendedorId}",
                        subcategoria.SubcategoriaID, subcategoria.NombreSubcategoria, subcategoria.VendedorID);
                    InvalidarCacheSubcategoriasVendedor(CurrentUserId());
                    TempData["Ok"] = "Subcategoría creada.";
                    return RedirectToAction(nameof(Subcategorias));
                }

                _logger.LogWarning("No se pudo crear la subcategoría");
                TempData["Err"] = "No se pudo crear la subcategoría.";
            }
            catch (DbUpdateException ex) when (
                   ex.InnerException?.Message.Contains("IX_Subcategorias_VendedorID_CategoriaID_NombreSubcategoria") == true
                || ex.InnerException?.Message.Contains("2601") == true
                || ex.InnerException?.Message.Contains("2627") == true)
            {
                _logger.LogWarning(ex, "Subcategoría duplicada. CategoriaId: {CategoriaId}, Nombre: {Nombre}",
                    categoriaID, nombresubCategoria);
                TempData["Err"] = "Ya existe una subcategoría con ese nombre en esa categoría.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear subcategoría");
                TempData["Err"] = "Error al guardar la subcategoría.";
            }

            ViewBag.Categorias = await ObtenerCategoriasConCacheAsync();
            return View("SubcategoriaForm");
        }

        [HttpGet]
        public async Task<IActionResult> EditarSubcategoria(int subcategoriaID)
        {
            try
            {
                var subcategoria = await _subcategoriasManager.GetByIdAsync(subcategoriaID);
                if (subcategoria == null)
                {
                    _logger.LogWarning("Subcategoría no encontrada. SubcategoriaId: {SubcategoriaId}", subcategoriaID);
                    return NotFound();
                }

                if (!IsAdmin() && subcategoria.VendedorID != CurrentUserId())
                {
                    _logger.LogWarning("Acceso denegado a subcategoría. SubcategoriaId: {SubcategoriaId}, VendedorId: {VendedorId}",
                        subcategoriaID, CurrentUserId());
                    return Forbid();
                }

                ViewBag.Subcategoria = subcategoria;
                ViewBag.Categorias = await ObtenerCategoriasConCacheAsync();
                return View("SubcategoriaForm");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar subcategoría. SubcategoriaId: {SubcategoriaId}", subcategoriaID);
                return RedirectToAction(nameof(Subcategorias));
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarSubcategoria(int subcategoriaID, int categoriaID, string nombresubCategoria)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("ModelState inválido al editar subcategoría");
                    return RedirectToAction(nameof(Subcategorias));
                }

                var subcategoria = await _subcategoriasManager.GetByIdAsync(subcategoriaID);
                if (subcategoria == null)
                {
                    _logger.LogWarning("Subcategoría no encontrada al editar. SubcategoriaId: {SubcategoriaId}", subcategoriaID);
                    return NotFound();
                }

                if (!IsAdmin() && subcategoria.VendedorID != CurrentUserId())
                {
                    _logger.LogWarning("Acceso denegado al editar subcategoría. SubcategoriaId: {SubcategoriaId}", subcategoriaID);
                    return Forbid();
                }

                var vendedorId = subcategoria.VendedorID;
                subcategoria.NombreSubcategoria = (nombresubCategoria ?? string.Empty).Trim();
                subcategoria.CategoriaID = categoriaID;

                await _subcategoriasManager.UpdateAsync(subcategoria);
                _logger.LogInformation("Subcategoría actualizada. SubcategoriaId: {SubcategoriaId}, Nombre: {Nombre}",
                    subcategoria.SubcategoriaID, subcategoria.NombreSubcategoria);

                if (!string.IsNullOrEmpty(vendedorId))
                    InvalidarCacheSubcategoriasVendedor(vendedorId);

                TempData["Ok"] = "Subcategoría actualizada.";
                return RedirectToAction(nameof(Subcategorias));
            }
            catch (DbUpdateException ex) when (
                   ex.InnerException?.Message.Contains("IX_Subcategorias_VendedorID_CategoriaID_NombreSubcategoria") == true
                || ex.InnerException?.Message.Contains("2601") == true
                || ex.InnerException?.Message.Contains("2627") == true)
            {
                _logger.LogWarning(ex, "Subcategoría duplicada al editar. SubcategoriaId: {SubcategoriaId}", subcategoriaID);
                TempData["Err"] = "Ya existe una subcategoría con ese nombre en esa categoría.";
                var subcategoria = await _subcategoriasManager.GetByIdAsync(subcategoriaID);
                ViewBag.Subcategoria = subcategoria;
                ViewBag.Categorias = await ObtenerCategoriasConCacheAsync();
                return View("SubcategoriaForm");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al editar subcategoría. SubcategoriaId: {SubcategoriaId}", subcategoriaID);
                TempData["Err"] = "Error al actualizar la subcategoría.";
                var subcategoria = await _subcategoriasManager.GetByIdAsync(subcategoriaID);
                ViewBag.Subcategoria = subcategoria;
                ViewBag.Categorias = await ObtenerCategoriasConCacheAsync();
                return View("SubcategoriaForm");
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarSubcategoria(int subcategoriaID)
        {
            try
            {
                var sub = await _subcategoriasManager.GetByIdAsync(subcategoriaID);
                if (sub == null)
                {
                    _logger.LogWarning("Subcategoría no encontrada al eliminar. SubcategoriaId: {SubcategoriaId}", subcategoriaID);
                    return NotFound();
                }

                if (!IsAdmin() && sub.VendedorID != CurrentUserId())
                {
                    _logger.LogWarning("Acceso denegado al eliminar subcategoría. SubcategoriaId: {SubcategoriaId}", subcategoriaID);
                    return Forbid();
                }

                var vendedorId = sub.VendedorID;
                await _subcategoriasManager.DeleteAsync(subcategoriaID);
                _logger.LogInformation("Subcategoría eliminada. SubcategoriaId: {SubcategoriaId}", subcategoriaID);

                if (!string.IsNullOrEmpty(vendedorId))
                    InvalidarCacheSubcategoriasVendedor(vendedorId);

                TempData["Ok"] = "Subcategoría eliminada.";
            }
            catch (DbUpdateException ex)
            {
                _logger.LogWarning(ex, "No se puede eliminar subcategoría con productos. SubcategoriaId: {SubcategoriaId}", subcategoriaID);
                TempData["Err"] = "No se puede eliminar: hay productos asociados a esta subcategoría.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar subcategoría. SubcategoriaId: {SubcategoriaId}", subcategoriaID);
                TempData["Err"] = "Error al eliminar la subcategoría.";
            }

            return RedirectToAction(nameof(Subcategorias));
        }

        #endregion

        #region Proveedores (Admin)

        [Authorize(Roles = "Administrador")]
        [HttpGet]
        public async Task<IActionResult> Proveedores()
        {
            try
            {
                _logger.LogInformation("Listado de proveedores solicitado");
                var proveedores = await ObtenerProveedoresConCacheAsync();
                _logger.LogDebug("Proveedores cargados: {Count}", proveedores.Count);
                ViewBag.Proveedores = proveedores;
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar proveedores");
                TempData["MensajeError"] = "Error al cargar los proveedores.";
                return View();
            }
        }

        [Authorize(Roles = "Administrador")]
        [HttpGet]
        public IActionResult AnadirProveedor() => View("ProveedorForm");

        [Authorize(Roles = "Administrador")]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AnadirProveedor(string nombreProveedor, string contacto, string telefono, string email, string direccion)
        {
            try
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
                _logger.LogInformation("Proveedor creado. ProveedorId: {ProveedorId}, Nombre: {Nombre}",
                    proveedor.ProveedorID, proveedor.NombreProveedor);
                InvalidarCacheProveedores();
                return RedirectToAction(nameof(Proveedores));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear proveedor. Nombre: {Nombre}", nombreProveedor);
                TempData["MensajeError"] = "Error al crear el proveedor.";
                return View("ProveedorForm");
            }
        }

        [Authorize(Roles = "Administrador")]
        [HttpGet]
        public async Task<IActionResult> EditarProveedor(int proveedorID)
        {
            try
            {
                var proveedor = await _proveedoresManager.GetByIdAsync(proveedorID);
                if (proveedor == null)
                {
                    _logger.LogWarning("Proveedor no encontrado. ProveedorId: {ProveedorId}", proveedorID);
                    return NotFound();
                }

                ViewBag.Proveedor = proveedor;
                return View("ProveedorForm");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar proveedor. ProveedorId: {ProveedorId}", proveedorID);
                return RedirectToAction(nameof(Proveedores));
            }
        }

        [Authorize(Roles = "Administrador")]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarProveedor(int proveedorID, string nombreProveedor, string contacto, string telefono, string email, string direccion)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("ModelState inválido al editar proveedor");
                    return RedirectToAction(nameof(Proveedores));
                }

                var proveedor = await _proveedoresManager.GetByIdAsync(proveedorID);
                if (proveedor == null)
                {
                    _logger.LogWarning("Proveedor no encontrado al editar. ProveedorId: {ProveedorId}", proveedorID);
                    return NotFound();
                }

                proveedor.NombreProveedor = (nombreProveedor ?? string.Empty).Trim();
                proveedor.Contacto = contacto;
                proveedor.Telefono = telefono;
                proveedor.Email = email;
                proveedor.Direccion = direccion;

                await _proveedoresManager.UpdateAsync(proveedor);
                _logger.LogInformation("Proveedor actualizado. ProveedorId: {ProveedorId}, Nombre: {Nombre}",
                    proveedor.ProveedorID, proveedor.NombreProveedor);
                InvalidarCacheProveedores();
                return RedirectToAction(nameof(Proveedores));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al editar proveedor. ProveedorId: {ProveedorId}", proveedorID);
                TempData["MensajeError"] = "Error al actualizar el proveedor.";
                return RedirectToAction(nameof(Proveedores));
            }
        }

        [Authorize(Roles = "Administrador")]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarProveedor(int proveedorID)
        {
            try
            {
                await _proveedoresManager.DeleteAsync(proveedorID);
                _logger.LogInformation("Proveedor eliminado. ProveedorId: {ProveedorId}", proveedorID);
                InvalidarCacheProveedores();
                return RedirectToAction(nameof(Proveedores));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar proveedor. ProveedorId: {ProveedorId}", proveedorID);
                TempData["MensajeError"] = "Error al eliminar el proveedor.";
                return RedirectToAction(nameof(Proveedores));
            }
        }

        #endregion

        #region Productos

        [HttpGet]
        public async Task<IActionResult> Productos(CancellationToken ct = default)
        {
            try
            {
                _logger.LogInformation("Listado de productos solicitado. Usuario: {UserId}, EsAdmin: {EsAdmin}",
                    CurrentUserId(), IsAdmin());

                IQueryable<Producto> query = _context.Productos.AsNoTracking()
                    .Include(p => p.Categoria).Include(p => p.Variantes);

                //if (!IsAdmin())
                //    query = query.Where(p => p.VendedorID == CurrentUserId());

                var productos = await query.OrderBy(p => p.Nombre).ToListAsync(ct);
                _logger.LogDebug("Productos cargados: {Count}", productos.Count);

                ViewBag.Categorias = await ObtenerCategoriasConCacheAsync();
                return View(productos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar productos");
                TempData["MensajeError"] = "Error al cargar los productos.";
                return View(new List<Producto>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> AnadirProducto()
        {
            try
            {
                await FillProductoFormBags();
                return View("ProductoForm");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar formulario de producto");
                return RedirectToAction(nameof(Productos));
            }
        }


        #region GESTIÓN DE ATRIBUTOS DE CATEGORÍA

        /// <summary>
        /// GET: /Panel/Atributos - Vista principal
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Atributos(int? categoriaId = null)
        {
            try
            {
                // Cargar todas las categorías
                var categorias = await _context.Categorias
    .OrderBy(c => c.Nombre)
    .ToListAsync();

                ViewBag.Categorias = categorias;
                ViewBag.CategoriaSeleccionada = categoriaId ?? 0;

                if (categoriaId.HasValue && categoriaId.Value > 0)
                {
                    var categoria = categorias.FirstOrDefault(c => c.CategoriaID == categoriaId.Value);
                    ViewBag.CategoriaNombre = categoria?.Nombre ?? "";

                    var atributos = await _categoriaAtributoService.ObtenerPorCategoriaAsync(categoriaId.Value);
                    ViewBag.Atributos = atributos;
                }
                else
                {
                    ViewBag.CategoriaNombre = "";
                    ViewBag.Atributos = new List<CategoriaAtributo>();
                }

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar vista de atributos");
                TempData["Err"] = "Error al cargar los atributos.";
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// POST: /Panel/CrearAtributo
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearAtributo(
            int categoriaId,
            string nombre,
            string? nombreTecnico,
            string tipoCampo,
            string? opcionesJson,
            string? unidad,
            string? descripcion,
            string? grupo,
            string? iconoClass,
            int orden = 0,
            bool obligatorio = false,
            bool filtrable = true,
            bool mostrarEnFicha = true,
            bool activo = true)
        {
            try
            {
                var atributo = new CategoriaAtributo
                {
                    CategoriaID = categoriaId,
                    Nombre = nombre?.Trim() ?? "",
                    NombreTecnico = nombreTecnico?.Trim() ?? "",
                    TipoCampo = tipoCampo ?? "text",
                    OpcionesJson = opcionesJson,
                    Unidad = unidad?.Trim(),
                    Descripcion = descripcion?.Trim(),
                    Grupo = grupo?.Trim(),
                    IconoClass = iconoClass?.Trim(),
                    Orden = orden,
                    Obligatorio = obligatorio,
                    Filtrable = filtrable,
                    MostrarEnFicha = mostrarEnFicha,
                    Activo = activo
                };

                var (exito, mensaje, _) = await _categoriaAtributoService.CrearAsync(atributo);
                TempData[exito ? "Ok" : "Err"] = mensaje;

                return RedirectToAction(nameof(Atributos), new { categoriaId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear atributo");
                TempData["Err"] = "Error al crear el atributo.";
                return RedirectToAction(nameof(Atributos), new { categoriaId });
            }
        }

        /// <summary>
        /// POST: /Panel/EditarAtributo
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarAtributo(
            int atributoId,
            int categoriaId,
            string nombre,
            string? nombreTecnico,
            string tipoCampo,
            string? opcionesJson,
            string? unidad,
            string? descripcion,
            string? grupo,
            string? iconoClass,
            int orden = 0,
            bool obligatorio = false,
            bool filtrable = true,
            bool mostrarEnFicha = true,
            bool activo = true)
        {
            try
            {
                var atributo = new CategoriaAtributo
                {
                    AtributoID = atributoId,
                    CategoriaID = categoriaId,
                    Nombre = nombre?.Trim() ?? "",
                    NombreTecnico = nombreTecnico?.Trim() ?? "",
                    TipoCampo = tipoCampo ?? "text",
                    OpcionesJson = opcionesJson,
                    Unidad = unidad?.Trim(),
                    Descripcion = descripcion?.Trim(),
                    Grupo = grupo?.Trim(),
                    IconoClass = iconoClass?.Trim(),
                    Orden = orden,
                    Obligatorio = obligatorio,
                    Filtrable = filtrable,
                    MostrarEnFicha = mostrarEnFicha,
                    Activo = activo
                };

                var (exito, mensaje) = await _categoriaAtributoService.ActualizarAsync(atributo);
                TempData[exito ? "Ok" : "Err"] = mensaje;

                return RedirectToAction(nameof(Atributos), new { categoriaId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al editar atributo ID: {Id}", atributoId);
                TempData["Err"] = "Error al actualizar el atributo.";
                return RedirectToAction(nameof(Atributos), new { categoriaId });
            }
        }

        /// <summary>
        /// GET: /Panel/EliminarAtributo
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> EliminarAtributo(int id, int categoriaId)
        {
            try
            {
                var (exito, mensaje) = await _categoriaAtributoService.EliminarAsync(id);
                TempData[exito ? "Ok" : "Err"] = mensaje;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar atributo ID: {Id}", id);
                TempData["Err"] = "Error al eliminar el atributo.";
            }

            return RedirectToAction(nameof(Atributos), new { categoriaId });
        }

        /// <summary>
        /// GET: /Panel/DuplicarAtributo
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> DuplicarAtributo(int id, int categoriaId)
        {
            try
            {
                var (exito, mensaje, _) = await _categoriaAtributoService.DuplicarAsync(id);
                TempData[exito ? "Ok" : "Err"] = mensaje;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al duplicar atributo ID: {Id}", id);
                TempData["Err"] = "Error al duplicar el atributo.";
            }

            return RedirectToAction(nameof(Atributos), new { categoriaId });
        }

        /// <summary>
        /// POST: /Panel/ToggleAtributoActivo (AJAX)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ToggleAtributoActivo([FromBody] ToggleAtributoRequest request)
        {
            try
            {
                var atributo = await _categoriaAtributoService.ObtenerPorIdAsync(request.Id);
                if (atributo == null)
                    return Json(new { success = false, message = "Atributo no encontrado" });

                atributo.Activo = request.Activo;
                var (exito, mensaje) = await _categoriaAtributoService.ActualizarAsync(atributo);

                return Json(new { success = exito, message = mensaje });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cambiar estado de atributo ID: {Id}", request.Id);
                return Json(new { success = false, message = "Error al cambiar estado" });
            }
        }

        // DTO para AJAX
        public class ToggleAtributoRequest
        {
            public int Id { get; set; }
            public bool Activo { get; set; }
        }

        #endregion


        [HttpPost, ValidateAntiForgeryToken, RequestFormLimits(MultipartBodyLengthLimit = 64L * 1024 * 1024)]
        public async Task<IActionResult> AnadirProducto(
            string nombreProducto, string descripcion, string talla, string color, string marca,
            decimal precioCompra, decimal precioVenta, int? proveedorID, int categoriaID, int subcategoriaID, int stock,
            IFormFile? imagen, IFormFile[]? Imagenes, int? ImagenPrincipalIndex, string? ImagenesIgnore,
            [FromForm] string[]? VarColor, [FromForm] string[]? VarTalla, [FromForm] string[]? VarPrecio, [FromForm] int[]? VarStock)
        {
            try
            {
                _logger.LogInformation("Creando producto. Nombre: {Nombre}, VendedorId: {VendedorId}",
                    nombreProducto, CurrentUserId());

                var sub = await _context.Subcategorias.Include(s => s.Categoria)
                    .FirstOrDefaultAsync(s => s.SubcategoriaID == subcategoriaID && s.CategoriaID == categoriaID);

                if (sub == null)
                {
                    _logger.LogWarning("Subcategoría no encontrada. SubcategoriaId: {SubcategoriaId}", subcategoriaID);
                    TempData["Err"] = "Subcategoría no encontrada.";
                    await FillProductoFormBags();
                    return View("ProductoForm");
                }

                if (!IsAdmin() && sub.VendedorID != CurrentUserId())
                {
                    _logger.LogWarning("Acceso denegado a subcategoría. SubcategoriaId: {SubcategoriaId}, VendedorId: {VendedorId}",
                        subcategoriaID, CurrentUserId());
                    TempData["Err"] = "No tienes permisos para usar esta subcategoría.";
                    await FillProductoFormBags();
                    return View("ProductoForm");
                }

                var (hasVariants, normVariants, variantError) = await NormalizeAndValidateVariants(VarColor, VarTalla, VarPrecio, VarStock, precioCompra);

                if (!string.IsNullOrEmpty(variantError))
                {
                    _logger.LogWarning("Error en variantes. Error: {Error}", variantError);
                    TempData["Err"] = variantError;
                    await FillProductoFormBags();
                    ViewBag.Producto = PresetProducto(nombreProducto, descripcion, talla, color, marca,
                        precioCompra, precioVenta, stock, proveedorID, categoriaID, subcategoriaID);
                    return View("ProductoForm");
                }

                var producto = new Producto
                {
                    Nombre = (nombreProducto ?? string.Empty).Trim(),
                    FechaAgregado = DateTime.UtcNow,
                    Descripcion = descripcion?.Trim(),
                    Marca = marca?.Trim(),
                    PrecioCompra = Math.Round(precioCompra, 2),
                    ProveedorID = proveedorID > 0 ? proveedorID : null,  // ✅ Nullable: solo asigna si > 0
                    CategoriaID = categoriaID,
                    SubcategoriaID = subcategoriaID,
                    VendedorID = CurrentUserId()
                };

                if (hasVariants)
                {
                    producto.Talla = null;
                    producto.Color = null;
                    producto.Stock = normVariants.Sum(v => v.Stock);
                    producto.PrecioVenta = normVariants.Min(v => v.Precio);
                }
                else
                {
                    producto.Talla = talla?.Trim();
                    producto.Color = color?.Trim();
                    producto.Stock = Math.Max(0, stock);

                    var pvFinal = Math.Round(precioVenta, 2);
                    if (pvFinal <= precioCompra)
                    {
                        _logger.LogWarning("Precio de venta menor o igual al de compra. PV: {PV}, PC: {PC}", pvFinal, precioCompra);
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

                    var saveResult = await SaveGalleryAsync(producto, Imagenes, imagen, ImagenPrincipalIndex, null, ImagenesIgnore);

                    if (!saveResult.Success)
                    {
                        await trx.RollbackAsync();
                        _logger.LogWarning("Error al guardar galería. ProductoId: {ProductoId}, Error: {Error}",
                            producto.ProductoID, saveResult.ErrorMessage);
                        TempData["Err"] = saveResult.ErrorMessage;
                        await FillProductoFormBags();
                        ViewBag.Producto = producto;
                        return View("ProductoForm");
                    }

                    await trx.CommitAsync();

                    await ProcesarAtributosDinamicos(producto.ProductoID, Request.Form);

                    _logger.LogInformation("Producto creado. ProductoId: {ProductoId}, Variantes: {VarianteCount}",
                        producto.ProductoID, hasVariants ? normVariants.Count : 0);

                    TempData["Ok"] = hasVariants
                        ? $"Producto con {normVariants.Count} variantes añadido correctamente."
                        : "Producto añadido correctamente.";

                    return RedirectToAction(nameof(Productos));
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
        public async Task<IActionResult> EditarProducto(int productoID, CancellationToken ct = default)
        {
            try
            {
                var producto = await _context.Productos.Include(p => p.Variantes).Include(p => p.Subcategoria)
                    .FirstOrDefaultAsync(p => p.ProductoID == productoID, ct);

                if (producto == null)
                {
                    _logger.LogWarning("Producto no encontrado. ProductoId: {ProductoId}", productoID);
                    TempData["Err"] = "Producto no encontrado.";
                    return RedirectToAction(nameof(Productos));
                }

                if (!IsAdmin() && producto.VendedorID != CurrentUserId())
                {
                    _logger.LogWarning("Acceso denegado a producto. ProductoId: {ProductoId}, VendedorId: {VendedorId}",
                        productoID, CurrentUserId());
                    TempData["Err"] = "No tienes permisos para editar este producto.";
                    return RedirectToAction(nameof(Productos));
                }

                var variantes = producto.Variantes.OrderBy(v => v.Color).ThenBy(v => v.Talla).ToList();
                ViewBag.Variantes = variantes;

                await LoadGalleryData(producto);
                await FillProductoFormBags();
                ViewBag.Producto = producto;
                ViewBag.ProductoID = producto.ProductoID;  
                return View("ProductoForm");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar producto. ProductoId: {ProductoId}", productoID);
                TempData["Err"] = "Error al cargar el producto.";
                return RedirectToAction(nameof(Productos));
            }
        }

        [HttpPost, ValidateAntiForgeryToken, RequestFormLimits(MultipartBodyLengthLimit = 64L * 1024 * 1024)]
        public async Task<IActionResult> EditarProducto(
            int productoID, string nombreProducto, string descripcion, string talla, string color, string marca,
            string existingImagenPath, decimal precioCompra, decimal precioVenta, int? proveedorID, int categoriaID,
            int subcategoriaID, int stock, IFormFile? imagen, IFormFile[]? Imagenes, int? ImagenPrincipalIndex,
            string? ImagenesIgnore, [FromForm] string[]? VarColor, [FromForm] string[]? VarTalla,
            [FromForm] string[]? VarPrecio, [FromForm] int[]? VarStock, CancellationToken ct = default)
        {
            try
            {
                var producto = await _context.Productos.Include(p => p.Variantes)
                    .FirstOrDefaultAsync(p => p.ProductoID == productoID, ct);

                if (producto == null)
                {
                    _logger.LogWarning("Producto no encontrado al editar. ProductoId: {ProductoId}", productoID);
                    TempData["Err"] = "Producto no encontrado.";
                    return RedirectToAction(nameof(Productos));
                }

                if (!IsAdmin() && producto.VendedorID != CurrentUserId())
                {
                    _logger.LogWarning("Acceso denegado al editar producto. ProductoId: {ProductoId}", productoID);
                    TempData["Err"] = "No tienes permisos para editar este producto.";
                    return RedirectToAction(nameof(Productos));
                }

                var sub = await _context.Subcategorias.FirstOrDefaultAsync(s => s.SubcategoriaID == subcategoriaID && s.CategoriaID == categoriaID, ct);

                if (sub == null || (!IsAdmin() && sub.VendedorID != CurrentUserId()))
                {
                    _logger.LogWarning("Subcategoría inválida. SubcategoriaId: {SubcategoriaId}", subcategoriaID);
                    TempData["Err"] = "Subcategoría inválida o no pertenece a tu tienda.";
                    await FillProductoFormBags();
                    ViewBag.Producto = producto;
                    return View("ProductoForm");
                }

                var (hasVariants, normVariants, variantError) = await NormalizeAndValidateVariants(VarColor, VarTalla, VarPrecio, VarStock, precioCompra);

                if (!string.IsNullOrEmpty(variantError))
                {
                    _logger.LogWarning("Error en variantes al editar. Error: {Error}", variantError);
                    TempData["Err"] = variantError;
                    await FillProductoFormBags();
                    ViewBag.Producto = producto;
                    return View("ProductoForm");
                }

                producto.Nombre = (nombreProducto ?? string.Empty).Trim();
                producto.Descripcion = descripcion?.Trim();
                producto.Marca = marca?.Trim();
                producto.PrecioCompra = Math.Round(precioCompra, 2);
                producto.ProveedorID = proveedorID > 0 ? proveedorID : null;  // ✅ Nullable: solo asigna si > 0
                producto.CategoriaID = categoriaID;
                producto.SubcategoriaID = subcategoriaID;

                if (hasVariants)
                {
                    producto.Talla = null;
                    producto.Color = null;
                    producto.Stock = normVariants.Sum(v => v.Stock);
                    producto.PrecioVenta = normVariants.Min(v => v.Precio);
                }
                else
                {
                    producto.Talla = talla?.Trim();
                    producto.Color = color?.Trim();
                    producto.Stock = Math.Max(0, stock);

                    var pvFinal = Math.Round(precioVenta, 2);
                    if (pvFinal <= precioCompra)
                    {
                        _logger.LogWarning("Precio de venta menor o igual al de compra al editar. PV: {PV}, PC: {PC}", pvFinal, precioCompra);
                        TempData["Err"] = $"El precio de venta (${pvFinal}) debe ser mayor al precio de compra (${precioCompra}).";
                        await FillProductoFormBags();
                        ViewBag.Producto = producto;
                        return View("ProductoForm");
                    }
                    producto.PrecioVenta = pvFinal;
                }

                await using var trx = await _context.Database.BeginTransactionAsync(ct);
                try
                {
                    var currentVariants = await _context.ProductoVariantes.Where(v => v.ProductoID == producto.ProductoID).ToListAsync(ct);

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

                        await _context.ProductoVariantes.AddRangeAsync(variants, ct);
                    }

                    var saveResult = await SaveGalleryAsync(producto, Imagenes, imagen, ImagenPrincipalIndex, existingImagenPath, ImagenesIgnore);

                    if (!saveResult.Success)
                    {
                        await trx.RollbackAsync(ct);
                        _logger.LogWarning("Error al guardar galería al editar. ProductoId: {ProductoId}, Error: {Error}",
                            producto.ProductoID, saveResult.ErrorMessage);
                        TempData["Err"] = saveResult.ErrorMessage;
                        await FillProductoFormBags();
                        ViewBag.Producto = producto;
                        return View("ProductoForm");
                    }

                    await _context.SaveChangesAsync(ct);
                    await ProcesarAtributosDinamicos(producto.ProductoID, Request.Form);
                    await trx.CommitAsync(ct);

                    _logger.LogInformation("Producto actualizado. ProductoId: {ProductoId}, Variantes: {VarianteCount}",
                        producto.ProductoID, hasVariants ? normVariants.Count : 0);

                    TempData["Ok"] = hasVariants
                        ? $"Producto actualizado con {normVariants.Count} variantes."
                        : "Producto actualizado correctamente.";

                    return RedirectToAction(nameof(Productos));
                }
                catch (Exception ex)
                {
                    await trx.RollbackAsync(ct);
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
                return RedirectToAction(nameof(Productos));
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarProducto(int productoID)
        {
            try
            {
                var producto = await _productosManager.GetByIdAsync(productoID);
                if (producto == null)
                {
                    _logger.LogWarning("Producto no encontrado al eliminar. ProductoId: {ProductoId}", productoID);
                    return NotFound();
                }

                if (!IsAdmin() && producto.VendedorID != CurrentUserId())
                {
                    _logger.LogWarning("Acceso denegado al eliminar producto. ProductoId: {ProductoId}", productoID);
                    return Forbid();
                }

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
                _logger.LogInformation("Producto eliminado. ProductoId: {ProductoId}", productoID);
                return RedirectToAction(nameof(Productos));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar producto. ProductoId: {ProductoId}", productoID);
                TempData["MensajeError"] = "Error al eliminar el producto.";
                return RedirectToAction(nameof(Productos));
            }
        }

        [HttpGet]
        public async Task<IActionResult> VariantesJson(int productoId, CancellationToken ct = default)
        {
            try
            {
                var producto = await _context.Productos.AsNoTracking().FirstOrDefaultAsync(p => p.ProductoID == productoId, ct);

                if (producto == null)
                {
                    _logger.LogWarning("Producto no encontrado para variantes JSON. ProductoId: {ProductoId}", productoId);
                    return NotFound();
                }

                if (!IsAdmin() && producto.VendedorID != CurrentUserId())
                {
                    _logger.LogWarning("Acceso denegado a variantes. ProductoId: {ProductoId}", productoId);
                    return Forbid();
                }

                var data = await _context.ProductoVariantes.AsNoTracking().Where(v => v.ProductoID == productoId)
                    .OrderBy(v => v.Color).ThenBy(v => v.Talla)
                    .Select(v => new { v.Color, v.Talla, v.PrecioVenta, v.Stock, v.SKU })
                    .ToListAsync(ct);

                _logger.LogDebug("Variantes JSON solicitadas. ProductoId: {ProductoId}, Count: {Count}", productoId, data.Count);
                return Json(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener variantes JSON. ProductoId: {ProductoId}", productoId);
                return StatusCode(500);
            }
        }

        #endregion

        #region Helpers - Productos

        private async Task FillProductoFormBags()
        {
            ViewBag.Categorias = await ObtenerCategoriasConCacheAsync();
            ViewBag.Proveedores = await ObtenerProveedoresConCacheAsync();
            ViewBag.Subcategorias = IsAdmin()
                ? await _subcategoriasManager.GetAllAsync()
                : await ObtenerSubcategoriasVendedorConCacheAsync(CurrentUserId());
        }

        private Producto PresetProducto(string nombre, string desc, string talla, string color, string marca,
                                        decimal pc, decimal pv, int stock, int? provId, int catId, int subId)
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

            var grouped = variants
                .GroupBy(v => (v.Color.Trim().ToLowerInvariant(), v.Talla.Trim().ToLowerInvariant()))
                .Select(g => (
                    g.First().Color,
                    g.First().Talla,
                    Precio: Math.Round(g.Average(v => v.Precio), 2),
                    Stock: g.Sum(v => v.Stock)
                ))
                .ToList();

            return (grouped.Count > 0, grouped, "");
        }

        private decimal[] ParseDecimalArray(string[] raw)
        {
            var list = new List<decimal>(raw.Length);
            foreach (var s in raw)
            {
                var txt = (s ?? "").Trim().Replace(',', '.');
                if (!decimal.TryParse(txt, NumberStyles.Any, CultureInfo.InvariantCulture, out var val))
                    val = 0m;
                list.Add(Math.Round(val, 2));
            }
            return list.ToArray();
        }

        private string GenerateSKU(Producto producto, string color, string talla)
        {
            string marca = (producto.Marca ?? SKU_DEFAULT_MARCA).Trim();
            string base3 = (marca.Length >= SKU_LENGTH_CODIGO ? marca.Substring(0, SKU_LENGTH_CODIGO) : marca).ToUpperInvariant();

            string c = (color ?? SKU_DEFAULT_COLOR).Trim();
            string color3 = (c.Length >= SKU_LENGTH_CODIGO ? c.Substring(0, SKU_LENGTH_CODIGO) : c).ToUpperInvariant();

            string t = (talla ?? SKU_DEFAULT_TALLA).Replace(" ", "").ToUpperInvariant();

            return $"{base3}-{producto.ProductoID}-{color3}-{t}";
        }

        #endregion

        #region Helpers - Galería

        private async Task LoadGalleryData(Producto producto)
        {
            var folder = ProductFolderAbs(producto.ProductoID);
            var metaPath = Path.Combine(folder, string.Format(PATTERN_GALLERY_JSON, producto.ProductoID));
            var gallery = new List<string>();
            string? portada = producto.ImagenPath;

            try
            {
                if (System.IO.File.Exists(metaPath))
                {
                    var json = await System.IO.File.ReadAllTextAsync(metaPath);
                    using var doc = JsonDocument.Parse(json);

                    if (doc.RootElement.TryGetProperty(JSON_PROP_IMAGES, out var arr) && arr.ValueKind == JsonValueKind.Array)
                    {
                        gallery = arr.EnumerateArray()
                            .Select(e => e.GetString()!)
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .ToList();
                    }

                    if (doc.RootElement.TryGetProperty(JSON_PROP_PORTADA, out var p) && p.ValueKind == JsonValueKind.String)
                    {
                        portada = p.GetString();
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
            Producto producto, IFormFile[]? nuevasImagenes, IFormFile? imagenLegacy,
            int? imagenPrincipalIndex, string? existingImagenPath = null, string? imagenesIgnore = null)
        {
            try
            {
                var folder = ProductFolderAbs(producto.ProductoID);
                Directory.CreateDirectory(folder);

                if (!Path.GetFullPath(folder).StartsWith(Path.GetFullPath(GalleryRootAbs()), StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Ruta de galería inválida. ProductoId: {ProductoId}", producto.ProductoID);
                    return (false, "Ruta de galería inválida.");
                }

                var metaPath = Path.Combine(folder, string.Format(PATTERN_GALLERY_JSON, producto.ProductoID));
                var imagenesExistentes = await LoadExistingImages(metaPath);

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
                            try
                            {
                                var nombre = Path.GetFileName(new Uri("http://dummy" + url).AbsolutePath);
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

                var nuevasUrls = new List<string>();
                foreach (var archivo in archivos)
                {
                    if (nuevasUrls.Count + imagenesExistentes.Count >= MAX_IMAGENES_GALERIA) break;

                    var resultado = await GuardarImagenSegura(archivo, folder);
                    if (!resultado.Success)
                    {
                        _logger.LogWarning("Error al guardar imagen. ProductoId: {ProductoId}, Error: {Error}",
                            producto.ProductoID, resultado.ErrorMessage);
                        return (false, resultado.ErrorMessage);
                    }

                    nuevasUrls.Add(resultado.Url!);
                }

                var todasLasImagenes = imagenesExistentes.Concat(nuevasUrls).Take(MAX_IMAGENES_GALERIA).ToList();

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
                else if (todasLasImagenes.Any())
                {
                    imagenPrincipal = todasLasImagenes[0];
                }

                await GuardarMetadata(metaPath, todasLasImagenes, imagenPrincipal);

                producto.ImagenPath = imagenPrincipal;

                await LimpiarImagenesNoUsadas(folder, todasLasImagenes);

                _logger.LogDebug("Galería guardada. ProductoId: {ProductoId}, Imágenes: {Count}",
                    producto.ProductoID, todasLasImagenes.Count);

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
                    if (doc.RootElement.TryGetProperty(JSON_PROP_IMAGES, out var arr) && arr.ValueKind == JsonValueKind.Array)
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
                if (archivo.Length <= 0 || archivo.Length > MAX_IMAGEN_BYTES)
                {
                    _logger.LogWarning("Archivo de imagen inválido o demasiado grande. Tamaño: {Size}", archivo.Length);
                    return (false, null, "Archivo de imagen inválido o demasiado grande.");
                }

                var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
                if (!EXTENSIONES_PERMITIDAS_IMAGEN.Contains(extension))
                {
                    _logger.LogWarning("Formato de imagen no permitido. Extensión: {Extension}", extension);
                    return (false, null, "Formato de imagen no permitido.");
                }

                await using var stream = archivo.OpenReadStream();
                if (!LooksLikeImage(stream, extension, out var mime) || (mime != null && !MIME_TYPES_PERMITIDOS.Contains(mime)))
                {
                    _logger.LogWarning("Contenido de imagen no válido. MIME: {Mime}", mime ?? "desconocido");
                    return (false, null, "Contenido de imagen no válido.");
                }

                var nombreArchivo = $"{Guid.NewGuid():N}{extension}";
                var rutaCompleta = Path.Combine(folder, nombreArchivo);

                await using var fileStream = new FileStream(rutaCompleta, FileMode.Create);
                await archivo.CopyToAsync(fileStream);

                var url = $"/{FOLDER_IMAGES}/{FOLDER_PRODUCTOS}/{Path.GetFileName(folder)}/{nombreArchivo}".Replace("\\", "/");

                _logger.LogDebug("Imagen guardada. Url: {Url}", url);
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
            var json = JsonSerializer.Serialize(metadata, JSON_OPTIONS);
            await System.IO.File.WriteAllTextAsync(metaPath, json);
        }

        private async Task LimpiarImagenesNoUsadas(string folder, List<string> imagenesUsadas)
        {
            try
            {
                var archivosEnCarpeta = Directory.GetFiles(folder)
                    .Where(f => !f.EndsWith(PATTERN_GALLERY_EXTENSION, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var archivo in archivosEnCarpeta)
                {
                    var nombreArchivo = Path.GetFileName(archivo);
                    var enUso = imagenesUsadas.Any(url => url.EndsWith("/" + nombreArchivo, StringComparison.OrdinalIgnoreCase));

                    if (!enUso)
                    {
                        try
                        {
                            System.IO.File.Delete(archivo);
                            _logger.LogDebug("Imagen no usada eliminada: {Archivo}", nombreArchivo);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "No se pudo eliminar archivo {Archivo}", archivo);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al limpiar imágenes no usadas");
            }
        }

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

                if (head[0] == 0xFF && head[1] == 0xD8) { detected = "image/jpeg"; return true; }
                if (head[0] == 0x89 && head[1] == 0x50 && head[2] == 0x4E && head[3] == 0x47) { detected = "image/png"; return true; }
                if (head[0] == 0x47 && head[1] == 0x49 && head[2] == 0x46) { detected = "image/gif"; return true; }
                if (read >= 12 &&
                    head[0] == (byte)'R' && head[1] == (byte)'I' && head[2] == (byte)'F' && head[3] == (byte)'F' &&
                    head[8] == (byte)'W' && head[9] == (byte)'E' && head[10] == (byte)'B' && head[11] == (byte)'P')
                { detected = "image/webp"; return true; }
            }
            catch { }
            return false;
        }


        /// <summary>
        /// ✅ NUEVO: Obtener atributos de una categoría vía AJAX
        /// GET: /Panel/ObtenerAtributosPorCategoria?categoriaId=8
        /// Usado por JavaScript para cargar campos dinámicamente
        /// </summary>
        [HttpGet]
        public async Task<JsonResult> ObtenerAtributosPorCategoria(int categoriaId)
        {
            try
            {
                _logger.LogDebug("Obteniendo atributos para categoría {CategoriaId}", categoriaId);

                var atributos = await _categoriaAtributoService.ObtenerActivosPorCategoriaAsync(categoriaId);

                // Transformar a formato JSON simple para JavaScript
                var resultado = atributos.Select(a => new
                {
                    atributoId = a.AtributoID,
                    nombre = a.Nombre,
                    nombreTecnico = a.NombreTecnico,
                    descripcion = a.Descripcion,
                    tipoCampo = a.TipoCampo,
                    opciones = a.OpcionesLista,
                    unidad = a.Unidad,
                    iconoClass = a.IconoClass,
                    grupo = a.Grupo,
                    orden = a.Orden,
                    obligatorio = a.Obligatorio,
                    valorMinimo = a.ValorMinimo,
                    valorMaximo = a.ValorMaximo
                }).OrderBy(a => a.orden).ToList();

                _logger.LogDebug("Retornando {Count} atributos para categoría {CategoriaId}", resultado.Count, categoriaId);
                return Json(new { success = true, atributos = resultado });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener atributos de categoría {CategoriaId}", categoriaId);
                return Json(new { success = false, mensaje = "Error al cargar atributos" });
            }
        }

        /// <summary>
        /// ✅ NUEVO: Obtener valores de atributos de un producto vía AJAX
        /// GET: /Panel/ObtenerValoresAtributos?productoId=5
        /// Usado en modo edición para pre-llenar valores
        /// </summary>
        [HttpGet]
        public async Task<JsonResult> ObtenerValoresAtributos(int productoId)
        {
            try
            {
                _logger.LogDebug("Obteniendo valores de atributos para producto {ProductoId}", productoId);

                var valores = await _productoAtributoService.ObtenerValoresPorProductoAsync(productoId);

                // Transformar a diccionario simple: {atributoId: valor}
                var resultado = valores.ToDictionary(
                    v => v.AtributoID,
                    v => v.Valor
                );

                _logger.LogDebug("Retornando {Count} valores para producto {ProductoId}", resultado.Count, productoId);
                return Json(new { success = true, valores = resultado });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener valores de producto {ProductoId}", productoId);
                return Json(new { success = false, mensaje = "Error al cargar valores" });
            }
        }

        #endregion

        #region Atributos Dinámicos - Helper

        /// <summary>
        /// ✅ NUEVO: Procesar atributos dinámicos del formulario
        /// Extrae los campos "atributo_X" del formulario y los guarda en BD
        /// </summary>
        private async Task ProcesarAtributosDinamicos(int productoId, IFormCollection form)
        {
            try
            {
                var valores = new Dictionary<int, string>();

                // Extraer todos los campos que empiezan con "atributo_"
                foreach (var key in form.Keys)
                {
                    if (key.StartsWith("atributo_"))
                    {
                        var atributoIdStr = key.Replace("atributo_", "").Replace("[]", "");

                        if (int.TryParse(atributoIdStr, out var atributoId))
                        {
                            var valor = form[key].ToString();

                            if (!string.IsNullOrWhiteSpace(valor))
                            {
                                valores[atributoId] = valor;
                            }
                        }
                    }
                }

                // Guardar valores usando el servicio
                if (valores.Any())
                {
                    var (exito, mensaje, errores) = await _productoAtributoService
                        .GuardarValoresAsync(productoId, valores);

                    if (exito)
                    {
                        _logger.LogInformation("Atributos guardados para producto {ProductoId}. Total: {Count}",
                            productoId, valores.Count);
                    }
                    else
                    {
                        _logger.LogWarning("Errores al guardar atributos del producto {ProductoId}: {Errores}",
                            productoId, string.Join(", ", errores));
                    }
                }
                else
                {
                    _logger.LogDebug("No se encontraron atributos para guardar en producto {ProductoId}", productoId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al procesar atributos dinámicos para producto {ProductoId}", productoId);
                // No lanzamos excepción para no bloquear el guardado del producto
            }
        }


        /// <summary>
        /// Verifica si la solicitud actual es AJAX
        /// </summary>
        private bool EsAjax()
        {
            return Request.Headers.TryGetValue(HEADER_AJAX, out var headerValue) &&
                   string.Equals(headerValue, HEADER_AJAX_VALUE, StringComparison.OrdinalIgnoreCase);
        }

        #endregion
    }
}