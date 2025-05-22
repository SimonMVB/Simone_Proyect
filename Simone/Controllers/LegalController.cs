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
    public class LegalController : Controller
    {
        public IActionResult PoliticaPrivacidad()
        {
            return View();
        }

        public IActionResult TerminosCondiciones()
        {
            return View();
        }

        public IActionResult PoliticaCookies()
        {
            return View();
        }

        public IActionResult AvisoLegal()
        {
            return View();
        }
    }
}