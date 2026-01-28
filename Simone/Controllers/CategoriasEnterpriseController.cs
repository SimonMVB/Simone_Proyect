using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Simone.Models;
using Simone.Services;
using System;
using System.Threading.Tasks;

namespace Simone.Controllers
{
    /// <summary>
    /// Controller para administración de categorías enterprise
    /// </summary>
    [Authorize] // Cambiar por [Authorize(Roles = "Admin")] si tienes roles
    public class CategoriasEnterpriseController : Controller
    {
        private readonly CategoriaEnterpriseService _service;
        private readonly ILogger<CategoriasEnterpriseController> _logger;

        public CategoriasEnterpriseController(
            CategoriaEnterpriseService service,
            ILogger<CategoriasEnterpriseController> logger)
        {
            _service = service;
            _logger = logger;
        }

        // ==================== VISTAS ====================

        /// <summary>
        /// GET: /CategoriasEnterprise
        /// Lista de categorías en árbol
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            try
            {
                var categorias = await _service.ObtenerArbolAsync();
                return View(categorias);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar lista de categorías");
                TempData["Error"] = "Error al cargar las categorías";
                return View();
            }
        }

        /// <summary>
        /// GET: /CategoriasEnterprise/Create
        /// Formulario crear categoría
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Create(int? padreId)
        {
            try
            {
                ViewBag.Categorias = await _service.ObtenerActivasAsync();

                if (padreId.HasValue)
                {
                    var padre = await _service.ObtenerPorIdAsync(padreId.Value);
                    ViewBag.CategoriaPadre = padre;
                }

                return View(new Categoria
                {
                    CategoriaPadreID = padreId,
                    Activa = true,
                    MostrarEnMenu = true,
                    Orden = 0
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar formulario de creación");
                TempData["Error"] = "Error al cargar el formulario";
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// POST: /CategoriasEnterprise/Create
        /// Crear categoría
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Categoria categoria)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    ViewBag.Categorias = await _service.ObtenerActivasAsync();
                    return View(categoria);
                }

                var (exito, mensaje, nueva) = await _service.CrearAsync(categoria);

                if (exito)
                {
                    TempData["Success"] = mensaje;
                    return RedirectToAction(nameof(Index));
                }

                ModelState.AddModelError("", mensaje);
                ViewBag.Categorias = await _service.ObtenerActivasAsync();
                return View(categoria);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear categoría");
                ModelState.AddModelError("", "Error al crear la categoría");
                ViewBag.Categorias = await _service.ObtenerActivasAsync();
                return View(categoria);
            }
        }

        /// <summary>
        /// GET: /CategoriasEnterprise/Edit/5
        /// Formulario editar categoría
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var categoria = await _service.ObtenerPorIdAsync(id);
                if (categoria == null)
                {
                    TempData["Error"] = "Categoría no encontrada";
                    return RedirectToAction(nameof(Index));
                }

                // Excluir la categoría actual y sus descendientes
                var todasCategorias = await _service.ObtenerActivasAsync();
                var descendientes = await _service.ObtenerDescendientesIdsAsync(id);
                descendientes.Add(id);

                ViewBag.Categorias = todasCategorias
                    .Where(c => !descendientes.Contains(c.CategoriaID))
                    .ToList();

                return View(categoria);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar edición ID: {ID}", id);
                TempData["Error"] = "Error al cargar la categoría";
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// POST: /CategoriasEnterprise/Edit/5
        /// Actualizar categoría
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Categoria categoria)
        {
            if (id != categoria.CategoriaID)
                return BadRequest();

            try
            {
                if (!ModelState.IsValid)
                {
                    var todasCategorias = await _service.ObtenerActivasAsync();
                    var descendientes = await _service.ObtenerDescendientesIdsAsync(id);
                    descendientes.Add(id);

                    ViewBag.Categorias = todasCategorias
                        .Where(c => !descendientes.Contains(c.CategoriaID))
                        .ToList();

                    return View(categoria);
                }

                var (exito, mensaje) = await _service.ActualizarAsync(categoria);

                if (exito)
                {
                    TempData["Success"] = mensaje;
                    return RedirectToAction(nameof(Index));
                }

                ModelState.AddModelError("", mensaje);

                var todas = await _service.ObtenerActivasAsync();
                var desc = await _service.ObtenerDescendientesIdsAsync(id);
                desc.Add(id);

                ViewBag.Categorias = todas.Where(c => !desc.Contains(c.CategoriaID)).ToList();
                return View(categoria);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar categoría ID: {ID}", id);
                ModelState.AddModelError("", "Error al actualizar la categoría");
                return View(categoria);
            }
        }

        /// <summary>
        /// GET: /CategoriasEnterprise/Delete/5
        /// Confirmación de eliminación
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var categoria = await _service.ObtenerPorIdAsync(id);
                if (categoria == null)
                {
                    TempData["Error"] = "Categoría no encontrada";
                    return RedirectToAction(nameof(Index));
                }

                return View(categoria);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar eliminación ID: {ID}", id);
                TempData["Error"] = "Error al cargar la categoría";
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// POST: /CategoriasEnterprise/Delete/5
        /// Eliminar categoría
        /// </summary>
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var (exito, mensaje) = await _service.EliminarAsync(id);

                if (exito)
                    TempData["Success"] = mensaje;
                else
                    TempData["Error"] = mensaje;

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar categoría ID: {ID}", id);
                TempData["Error"] = "Error al eliminar la categoría";
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// GET: /CategoriasEnterprise/Details/5
        /// Ver detalles
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var categoria = await _service.ObtenerPorIdAsync(id);
                if (categoria == null)
                {
                    TempData["Error"] = "Categoría no encontrada";
                    return RedirectToAction(nameof(Index));
                }

                ViewBag.Breadcrumbs = await _service.ObtenerBreadcrumbsAsync(id);
                return View(categoria);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar detalles ID: {ID}", id);
                TempData["Error"] = "Error al cargar la categoría";
                return RedirectToAction(nameof(Index));
            }
        }

        // ==================== API / AJAX ====================

        /// <summary>
        /// GET: /CategoriasEnterprise/GenerarSlug
        /// Generar slug desde nombre (AJAX)
        /// </summary>
        [HttpGet]
        public IActionResult GenerarSlug(string nombre)
        {
            try
            {
                var slug = _service.GenerarSlug(nombre);
                return Json(new { success = true, slug });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar slug");
                return Json(new { success = false, error = "Error al generar slug" });
            }
        }

        /// <summary>
        /// GET: /CategoriasEnterprise/GetHijas/5
        /// Obtener hijas (AJAX)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetHijas(int id)
        {
            try
            {
                var hijas = await _service.ObtenerHijasAsync(id);
                return Json(new { success = true, data = hijas });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener hijas ID: {ID}", id);
                return Json(new { success = false, error = "Error al cargar subcategorías" });
            }
        }

        /// <summary>
        /// GET: /CategoriasEnterprise/Buscar
        /// Buscar categorías (AJAX)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Buscar(string q)
        {
            try
            {
                var resultados = await _service.BuscarAsync(q);
                return Json(new
                {
                    success = true,
                    data = resultados.Select(c => new
                    {
                        id = c.CategoriaID,
                        nombre = c.Nombre,
                        path = c.Path
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al buscar");
                return Json(new { success = false, error = "Error en la búsqueda" });
            }
        }
    }
}
