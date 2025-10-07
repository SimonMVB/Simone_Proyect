using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Simone.Controllers
{
    /// <summary>
    /// Vista ligera para visualizar el estimado de envío consumiendo /api/envios/estimar.
    /// No modifica nada del flujo de compra: solo muestra el cálculo actual.
    /// </summary>
    [Authorize(Roles = "Administrador,Vendedor,Cliente")]
    public class EnviosUiController : Controller
    {
        // GET /Envios/Widget
        [HttpGet]
        [Route("Envios/Widget")]
        public IActionResult Widget()
        {
            return View();
        }
    }
}
