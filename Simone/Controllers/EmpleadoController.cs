using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Simone.Data;
using Simone.Models;
using System.Linq;
using System.Threading.Tasks;

namespace Simone.Controllers
{
    [Authorize(Roles = "Empleado")]
    public class EmpleadoController : Controller
    {
        private readonly TiendaDbContext _context;
        private readonly ILogger<EmpleadoController> _logger;

        public EmpleadoController(TiendaDbContext context, ILogger<EmpleadoController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public IActionResult Dashboard()
        {
            return View();
        }

        // Ejemplo: gestión de pedidos
        public IActionResult Pedidos()
        {
            var pedidos = _context.Pedidos.ToList();
            return View(pedidos);
        }

        // Ejemplo: gestión de inventario
        public IActionResult Inventario()
        {
            var productos = _context.Productos.ToList();
            return View(productos);
        }

        // Ejemplo: revisión de ventas
        public IActionResult Ventas()
        {
            var ventas = _context.Ventas.ToList();
            return View(ventas);
        }
    }
}
