using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Simone.Models;
using Microsoft.AspNetCore.Authorization;
using Simone.Data;


namespace Simone.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly TiendaDbContext _context;

        public HomeController(ILogger<HomeController> logger, TiendaDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        // ==============================
        // 🔹 Página de Inicio con Productos Destacados
        // ==============================
        public IActionResult Index()
        {
            _logger.LogInformation("Cargando la página de inicio...");

            // Obtener productos destacados (por ejemplo, los primeros 4 productos)
            var productosDestacados = _context.Productos
                .OrderByDescending(p => p.FechaAgregado)
                .Take(4)
                .ToList();

            // Enviar datos a la vista
            ViewData["Titulo"] = "Bienvenido a Simone";
            ViewBag.Productos = productosDestacados;

            return View();
        }

        // ==============================
        // 🔹 Página de Privacidad
        // ==============================
        public IActionResult Privacy()
        {
            _logger.LogInformation("Se accedió a la página de privacidad.");
            return View();
        }

        // ==============================
        // 🔹 Manejo de Errores Avanzado
        // ==============================
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            var errorId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
            _logger.LogError($"Error detectado. ID: {errorId}");

            return View(new ErrorViewModel { RequestId = errorId });
        }
    }
}
