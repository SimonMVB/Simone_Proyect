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

        /// <summary>
        /// Constructor que recibe el logger y el contexto de la base de datos.
        /// </summary>
        /// <param name="logger">Instancia de ILogger para registro de eventos.</param>
        /// <param name="context">Contexto de la base de datos para consultas.</param>
        public PanelController(ILogger<HomeController> logger, TiendaDbContext context, UserManager<Usuario> user, RoleManager<Roles> rol, CategoriasService categorias)
        {
            _logger = logger;
            _context = context;
            _userManager = user;
            _roleManager = rol;
            _categoriasManager = categorias;
        }

        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> Categorias()
        {
            var categorias = await _categoriasManager.GetAllCategoriasAsync();
            ViewBag.Categorias = categorias;
            return View();
        }

        public async Task<IActionResult> AnadirCategoria(string nombreCategoria)
        {
            Categorias categoria = new Categorias
            {
                Nombre = nombreCategoria,
            };
            bool success = await _categoriasManager.AddCategoriaAsync(categoria);
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
                var categoria = await _context.Categorias.FindAsync(categoriaID);
                if (categoria == null)
                {
                    return NotFound();
                }

                categoria.Nombre = nombreCategoria;
                _context.Categorias.Update(categoria);
                await _context.SaveChangesAsync();

                return RedirectToAction("Categorias");
            }

            return RedirectToAction("Categorias");
        }

        public async Task<IActionResult> EliminarCategoria(int categoriaID)
        {
            bool success = await _categoriasManager.DeleteCategoriaAsync(categoriaID);
            return RedirectToAction("Categorias");
        }

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
    }
}