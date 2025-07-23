using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Simone.Models;
using Simone.Data;

namespace Simone.Controllers



{
    /// <summary>
    /// Controlador principal de la aplicación. Maneja las páginas de inicio,
    /// privacidad, ofertas, nosotros y el manejo de errores.
    /// </summary>
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly TiendaDbContext _context;

        /// <summary>
        /// Constructor que recibe el logger y el contexto de la base de datos.
        /// </summary>
        /// <param name="logger">Instancia de ILogger para registro de eventos.</param>
        /// <param name="context">Contexto de la base de datos para consultas.</param>
        public HomeController(ILogger<HomeController> logger, TiendaDbContext context)
        {
            _logger = logger;
            _context = context;
        }
        public IActionResult Proximamente()
        {
            return View();
        }

        #region Index
        /// <summary>
        /// Página de inicio con productos destacados o información principal.
        /// </summary>
        /// <returns>Retorna la vista principal (Index).</returns>
        [HttpGet]
        public IActionResult Index()
        {
            var destacados = _context.Productos
                .OrderBy(r => Guid.NewGuid())
                .Take(4)
                .ToList();
            return View(destacados);
        }

        #endregion

        #region Privacy
        /// <summary>
        /// Página de privacidad (términos y condiciones o políticas de uso).
        /// </summary>
        /// <returns>Retorna la vista de privacidad.</returns>
        [HttpGet]
        public IActionResult Privacy()
        {
            _logger.LogInformation("Accediendo a la página de privacidad.");
            return View();
        }
        #endregion

        #region Ofertas
        /// <summary>
        /// Página que muestra productos en oferta o promociones especiales.
        /// </summary>
        /// <returns>Retorna la vista de Ofertas con la información correspondiente.</returns>
        [HttpGet]
        public IActionResult Ofertas()
        {
            _logger.LogInformation("Accediendo a la página de Ofertas.");

            // Ejemplo de obtención de productos en oferta:
            // var productosEnOferta = _context.Productos
            //     .Where(p => p.EnOferta == true)
            //     .ToList();
            // return View(productosEnOferta);

            // Por ahora, devolvemos la vista sin datos (o con datos mock).
           return View("~/Views/Panel/Ofertas.cshtml");
        }
        #endregion

        #region Nosotros
        /// <summary>
        /// Página que muestra información sobre la empresa, su misión, visión, etc.
        /// </summary>
        /// <returns>Retorna la vista de Nosotros.</returns>
        [HttpGet]
        public IActionResult Nosotros()
        {
            _logger.LogInformation("Accediendo a la página de Nosotros.");
            return View();
        }
        #endregion

        #region Error
        /// <summary>
        /// Manejo avanzado de errores. Retorna la vista de Error con información detallada.
        /// </summary>
        /// <returns>Vista de Error con el RequestId y otros datos para diagnóstico.</returns>
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            var errorId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
            _logger.LogError($"Error detectado. ID: {errorId}");
            return View(new ErrorViewModel { RequestId = errorId });
        }
        #endregion
    }
}
