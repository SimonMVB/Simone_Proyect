using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Simone.Models;
using Simone.Data;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Simone.Controllers
{
    [Authorize(Roles = "Administrador")]
    public class AdminController : Controller
    {
        private readonly UserManager<Usuario> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ILogger<AdminController> _logger;
        private readonly TiendaDbContext _context;

        public AdminController(UserManager<Usuario> userManager, RoleManager<IdentityRole> roleManager, ILogger<AdminController> logger, TiendaDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;
            _context = context;
        }

        public IActionResult Panel()
        {
            return View();
        }

        public IActionResult Usuarios()
        {
            var usuarios = _userManager.Users.ToList();
            return View(usuarios);
        }

        [HttpGet]
        public async Task<IActionResult> EditarRol(string id)
        {
            var usuario = await _userManager.FindByIdAsync(id);
            if (usuario == null)
                return NotFound();

            var roles = await _userManager.GetRolesAsync(usuario);
            ViewBag.RolesDisponibles = _roleManager.Roles.Select(r => r.Name).ToList();
            ViewBag.RolesAsignados = roles;
            return View(usuario);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarRol(string id, string nuevoRol)
        {
            var usuario = await _userManager.FindByIdAsync(id);
            if (usuario == null)
                return NotFound();

            var rolesActuales = await _userManager.GetRolesAsync(usuario);
            var eliminarRoles = await _userManager.RemoveFromRolesAsync(usuario, rolesActuales);
            if (!eliminarRoles.Succeeded)
            {
                ModelState.AddModelError("", "No se pudieron eliminar los roles anteriores.");
                return View(usuario);
            }

            var resultado = await _userManager.AddToRoleAsync(usuario, nuevoRol);
            if (!resultado.Succeeded)
            {
                ModelState.AddModelError("", "No se pudo asignar el nuevo rol.");
                return View(usuario);
            }

            _logger.LogInformation("Administrador cambió el rol del usuario {Email} a {Rol}.", usuario.Email, nuevoRol);
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
