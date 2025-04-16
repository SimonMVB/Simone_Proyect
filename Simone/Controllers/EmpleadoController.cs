using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Simone.Data;
using Simone.Models;
using System.Linq;

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

        // Panel de inicio del empleado
        public IActionResult Dashboard()
        {
            return View();
        }

        // Ver listado de pedidos (solo lectura)
        public IActionResult Pedidos()
        {
            var pedidos = _context.Pedidos.ToList();
            return View(pedidos);
        }

        // Ver inventario de productos (solo lectura)
        public IActionResult Inventario()
        {
            var productos = _context.Productos.ToList();
            return View(productos);
        }

        // Ver historial de ventas realizadas
        public IActionResult Ventas()
        {
            var ventas = _context.Ventas.ToList();
            return View(ventas);
        }
    }
}
