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
    public class BancoCuenta : Controller
    {
        private readonly UserManager<Usuario> _userManager;
        private readonly RoleManager<Roles> _roleManager;
        private readonly ILogger<BancoCuenta> _logger;
        private readonly TiendaDbContext _context;

        public BancoCuenta(UserManager<Usuario> userManager, RoleManager<Roles> roleManager, ILogger<BancoCuenta> logger, TiendaDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;
            _context = context;
        }


    }
}
