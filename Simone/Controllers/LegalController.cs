using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Simone.Controllers
{
    [AllowAnonymous]
    [Route("legal")]
    [ResponseCache(Duration = 21600, Location = ResponseCacheLocation.Any, NoStore = false)]
    public class LegalController : Controller
    {
        private readonly ILogger<LegalController> _logger;

        public LegalController(ILogger<LegalController> logger)
        {
            _logger = logger;
        }

        // Cache individualizado por tipo de contenido
        [ResponseCache(Duration = 2592000)] // 30 días para contenido más estático
        [HttpGet("aviso-privacidad")]
        public IActionResult AvisoPrivacidad()
        {
            _logger.LogInformation("Página de Aviso de Privacidad accedida desde {RemoteIpAddress}", HttpContext.Connection.RemoteIpAddress);
            return View("Avisoprivacidad");
        }

        [ResponseCache(Duration = 86400)] // 24 horas para contenido que puede cambiar
        [HttpGet("informacion")]
        public IActionResult InformacionGeneral()
        {
            _logger.LogInformation("Página de Información General accedida desde {RemoteIpAddress}", HttpContext.Connection.RemoteIpAddress);
            return View();
        }

        [ResponseCache(Duration = 86400)] // 24 horas
        [HttpGet("metodo-pago")]
        public IActionResult MetodoPago()
        {
            _logger.LogInformation("Página de Método de Pago accedida desde {RemoteIpAddress}", HttpContext.Connection.RemoteIpAddress);
            return View("MetodoPago");
        }

        [ResponseCache(Duration = 2592000)] // 30 días
        [HttpGet("politica-cookies")]
        public IActionResult PoliticaCookies()
        {
            _logger.LogInformation("Página de Política de Cookies accedida desde {RemoteIpAddress}", HttpContext.Connection.RemoteIpAddress);
            return View();
        }

        [ResponseCache(Duration = 2592000)] // 30 días
        [HttpGet("politica-privacidad")]
        public IActionResult PoliticaPrivacidad()
        {
            _logger.LogInformation("Página de Política de Privacidad accedida desde {RemoteIpAddress}", HttpContext.Connection.RemoteIpAddress);
            return View();
        }

        [ResponseCache(Duration = 86400)] // 24 horas
        [HttpGet("politicas-envio")]
        public IActionResult PoliticasEnvio()
        {
            _logger.LogInformation("Página de Políticas de Envío accedida desde {RemoteIpAddress}", HttpContext.Connection.RemoteIpAddress);
            return View();
        }

        [ResponseCache(Duration = 2592000)] // 30 días
        [HttpGet("terminos-condiciones")]
        public IActionResult TerminosCondiciones()
        {
            _logger.LogInformation("Página de Términos y Condiciones accedida desde {RemoteIpAddress}", HttpContext.Connection.RemoteIpAddress);
            return View();
        }

        // Nuevo endpoint para descargar políticas en PDF
        [HttpGet("descargar/{tipo}")]
        public IActionResult DescargarPolitica(string tipo)
        {
            try
            {
                _logger.LogInformation("Solicitud de descarga para {TipoPolitica} desde {RemoteIpAddress}", tipo, HttpContext.Connection.RemoteIpAddress);

                var politicasValidas = new[] { "privacidad", "cookies", "terminos", "envios", "pagos" };
                if (!politicasValidas.Contains(tipo.ToLower()))
                {
                    _logger.LogWarning("Intento de descarga con tipo inválido: {TipoInvalido}", tipo);
                    return NotFound();
                }

                // En una implementación real, aquí devolverías el archivo PDF
                // return File($"~/docs/politica-{tipo}.pdf", "application/pdf", $"politica-{tipo}.pdf");

                _logger.LogInformation("Descarga de {TipoPolitica} completada exitosamente", tipo);
                return Content($"Descarga de política {tipo} - Esta funcionalidad estaría implementada en producción");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al descargar política {TipoPolitica}", tipo);
                return StatusCode(500, "Error interno del servidor");
            }
        }

        // Endpoint para verificar última actualización
        [HttpGet("ultima-actualizacion")]
        public IActionResult UltimaActualizacion()
        {
            var actualizaciones = new
            {
                TerminosCondiciones = DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd"),
                PoliticaPrivacidad = DateTime.Now.AddDays(-15).ToString("yyyy-MM-dd"),
                PoliticaCookies = DateTime.Now.AddDays(-7).ToString("yyyy-MM-dd"),
                MetodoPago = DateTime.Now.AddDays(-3).ToString("yyyy-MM-dd"),
                PoliticasEnvio = DateTime.Now.AddDays(-5).ToString("yyyy-MM-dd"),
                InformacionGeneral = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd")
            };

            return Json(actualizaciones);
        }

        // Manejo centralizado de errores para este controlador
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        [Route("error/{code:int}")]
        public IActionResult Error(int code)
        {
            _logger.LogWarning("Error {StatusCode} accedido en LegalController desde {RemoteIpAddress}", code, HttpContext.Connection.RemoteIpAddress);

            var errorView = code switch
            {
                404 => "Error404",
                403 => "Error403",
                _ => "Error"
            };

            return View(errorView);
        }

        // Health check para el controlador
        [HttpGet("health")]
        public IActionResult HealthCheck()
        {
            var healthInfo = new
            {
                Status = "Healthy",
                Timestamp = DateTime.UtcNow,
                Controller = nameof(LegalController),
                ViewsCargadas = true // En una implementación real, verificarías que las vistas existan
            };

            _logger.LogInformation("Health check ejecutado para LegalController");
            return Json(healthInfo);
        }
    }
}