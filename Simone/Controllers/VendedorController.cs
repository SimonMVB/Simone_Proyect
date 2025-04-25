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
    [Authorize(Roles = "Administrador" + "," + "Vendedor")]
    public class VendedorController : Controller
    {

        private readonly UserManager<Usuario> _userManager;
        private readonly RoleManager<Roles> _roleManager;
        private readonly ILogger<AdminController> _logger;
        private readonly TiendaDbContext _context;

        public VendedorController(UserManager<Usuario> userManager, RoleManager<Roles> roleManager, ILogger<AdminController> logger, TiendaDbContext context)
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

    }
}