using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Simone.Data;
using Simone.Models;
using Simone.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Simone.Controllers
{
    /// <summary>
    /// Controller para administración de atributos de categorías
    /// ACTUALIZADO: Ya no usa CategoriaEnterpriseService (eliminado)
    /// </summary>
    [Authorize(Roles = "Administrador")]
    public class AtributosController : Controller
    {
        private readonly CategoriaAtributoService _atributoService;
        private readonly TiendaDbContext _context;
        private readonly ILogger<AtributosController> _logger;

        public AtributosController(
            CategoriaAtributoService atributoService,
            TiendaDbContext context,
            ILogger<AtributosController> logger)
        {
            _atributoService = atributoService;
            _context = context;
            _logger = logger;
        }

        // ==================== VISTAS ====================

        /// <summary>
        /// GET: /Atributos?categoriaId=5
        /// Lista de atributos de una categoría
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index(int? categoriaId)
        {
            try
            {
                if (!categoriaId.HasValue)
                {
                    TempData["Error"] = "Debe especificar una categoría";
                    return RedirectToAction("Categorias", "Panel");
                }

                var categoria = await _context.Categorias
                    .FirstOrDefaultAsync(c => c.CategoriaID == categoriaId.Value);

                if (categoria == null)
                {
                    TempData["Error"] = "Categoría no encontrada";
                    return RedirectToAction("Categorias", "Panel");
                }

                var atributos = await _atributoService.ObtenerPorCategoriaAsync(categoriaId.Value);

                ViewBag.Categoria = categoria;
                ViewBag.Breadcrumbs = categoria.Breadcrumbs;

                return View(atributos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar atributos de categoría {ID}", categoriaId);
                TempData["Error"] = "Error al cargar los atributos";
                return RedirectToAction("Categorias", "Panel");
            }
        }

        /// <summary>
        /// GET: /Atributos/Create?categoriaId=5
        /// Formulario crear atributo
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Create(int? categoriaId)
        {
            try
            {
                if (!categoriaId.HasValue)
                {
                    TempData["Error"] = "Debe especificar una categoría";
                    return RedirectToAction("Categorias", "Panel");
                }

                var categoria = await _context.Categorias
                    .FirstOrDefaultAsync(c => c.CategoriaID == categoriaId.Value);

                if (categoria == null)
                {
                    TempData["Error"] = "Categoría no encontrada";
                    return RedirectToAction("Categorias", "Panel");
                }

                ViewBag.Categoria = categoria;

                return View(new CategoriaAtributo
                {
                    CategoriaID = categoriaId.Value,
                    Activo = true,
                    MostrarEnFicha = true,
                    Filtrable = true,
                    Orden = 0
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar formulario de creación");
                TempData["Error"] = "Error al cargar el formulario";
                return RedirectToAction("Index", new { categoriaId });
            }
        }

        /// <summary>
        /// POST: /Atributos/Create
        /// Crear atributo
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CategoriaAtributo atributo, string? opciones)
        {
            try
            {
                // Procesar opciones si se proporcionaron
                if (!string.IsNullOrWhiteSpace(opciones))
                {
                    var listaOpciones = opciones
                        .Split('\n')
                        .Select(o => o.Trim())
                        .Where(o => !string.IsNullOrWhiteSpace(o))
                        .ToList();

                    atributo.OpcionesJson = _atributoService.SerializarOpciones(listaOpciones);
                }

                if (!ModelState.IsValid)
                {
                    var categoria = await _context.Categorias
                        .FirstOrDefaultAsync(c => c.CategoriaID == atributo.CategoriaID);
                    ViewBag.Categoria = categoria;
                    return View(atributo);
                }

                var (exito, mensaje, nuevo) = await _atributoService.CrearAsync(atributo);

                if (exito)
                {
                    TempData["Success"] = mensaje;
                    return RedirectToAction(nameof(Index), new { categoriaId = atributo.CategoriaID });
                }

                ModelState.AddModelError("", mensaje);
                var cat = await _context.Categorias
                    .FirstOrDefaultAsync(c => c.CategoriaID == atributo.CategoriaID);
                ViewBag.Categoria = cat;
                return View(atributo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear atributo");
                ModelState.AddModelError("", "Error al crear el atributo");
                var categoria = await _context.Categorias
                    .FirstOrDefaultAsync(c => c.CategoriaID == atributo.CategoriaID);
                ViewBag.Categoria = categoria;
                return View(atributo);
            }
        }

        /// <summary>
        /// GET: /Atributos/Edit/5
        /// Formulario editar atributo
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var atributo = await _atributoService.ObtenerPorIdAsync(id);
                if (atributo == null)
                {
                    TempData["Error"] = "Atributo no encontrado";
                    return RedirectToAction("Categorias", "Panel");
                }

                var categoria = await _context.Categorias
                    .FirstOrDefaultAsync(c => c.CategoriaID == atributo.CategoriaID);
                ViewBag.Categoria = categoria;

                // Convertir opciones JSON a texto para el textarea
                var opciones = _atributoService.ParsearOpciones(atributo.OpcionesJson);
                ViewBag.OpcionesTexto = string.Join("\n", opciones);

                return View(atributo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar edición ID: {ID}", id);
                TempData["Error"] = "Error al cargar el atributo";
                return RedirectToAction("Categorias", "Panel");
            }
        }

        /// <summary>
        /// POST: /Atributos/Edit/5
        /// Actualizar atributo
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CategoriaAtributo atributo, string? opciones)
        {
            if (id != atributo.AtributoID)
                return BadRequest();

            try
            {
                // Procesar opciones si se proporcionaron
                if (!string.IsNullOrWhiteSpace(opciones))
                {
                    var listaOpciones = opciones
                        .Split('\n')
                        .Select(o => o.Trim())
                        .Where(o => !string.IsNullOrWhiteSpace(o))
                        .ToList();

                    atributo.OpcionesJson = _atributoService.SerializarOpciones(listaOpciones);
                }

                if (!ModelState.IsValid)
                {
                    var cat = await _context.Categorias
                        .FirstOrDefaultAsync(c => c.CategoriaID == atributo.CategoriaID);
                    ViewBag.Categoria = cat;
                    ViewBag.OpcionesTexto = opciones;
                    return View(atributo);
                }

                var (exito, mensaje) = await _atributoService.ActualizarAsync(atributo);

                if (exito)
                {
                    TempData["Success"] = mensaje;
                    return RedirectToAction(nameof(Index), new { categoriaId = atributo.CategoriaID });
                }

                ModelState.AddModelError("", mensaje);
                var categoria = await _context.Categorias
                    .FirstOrDefaultAsync(c => c.CategoriaID == atributo.CategoriaID);
                ViewBag.Categoria = categoria;
                ViewBag.OpcionesTexto = opciones;
                return View(atributo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar atributo ID: {ID}", id);
                ModelState.AddModelError("", "Error al actualizar el atributo");
                return View(atributo);
            }
        }

        /// <summary>
        /// GET: /Atributos/Delete/5
        /// Confirmación eliminación
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var atributo = await _atributoService.ObtenerPorIdAsync(id);
                if (atributo == null)
                {
                    TempData["Error"] = "Atributo no encontrado";
                    return RedirectToAction("Categorias", "Panel");
                }

                var categoria = await _context.Categorias
                    .FirstOrDefaultAsync(c => c.CategoriaID == atributo.CategoriaID);
                ViewBag.Categoria = categoria;

                return View(atributo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar eliminación ID: {ID}", id);
                TempData["Error"] = "Error al cargar el atributo";
                return RedirectToAction("Categorias", "Panel");
            }
        }

        /// <summary>
        /// POST: /Atributos/Delete/5
        /// Eliminar atributo
        /// </summary>
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var atributo = await _atributoService.ObtenerPorIdAsync(id);
                if (atributo == null)
                {
                    TempData["Error"] = "Atributo no encontrado";
                    return RedirectToAction("Categorias", "Panel");
                }

                var categoriaId = atributo.CategoriaID;
                var (exito, mensaje) = await _atributoService.EliminarAsync(id);

                if (exito)
                    TempData["Success"] = mensaje;
                else
                    TempData["Error"] = mensaje;

                return RedirectToAction(nameof(Index), new { categoriaId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar atributo ID: {ID}", id);
                TempData["Error"] = "Error al eliminar el atributo";
                return RedirectToAction("Categorias", "Panel");
            }
        }

        // ==================== API / AJAX ====================

        /// <summary>
        /// GET: /Atributos/GenerarNombreTecnico
        /// Generar nombre técnico desde nombre (AJAX)
        /// </summary>
        [HttpGet]
        public IActionResult GenerarNombreTecnico(string nombre)
        {
            try
            {
                var nombreTecnico = _atributoService.GenerarNombreTecnico(nombre);
                return Json(new { success = true, nombreTecnico });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar nombre técnico");
                return Json(new { success = false, error = "Error al generar nombre técnico" });
            }
        }

        /// <summary>
        /// POST: /Atributos/CambiarOrden
        /// Cambiar orden de atributo (AJAX)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CambiarOrden(int id, int orden)
        {
            try
            {
                var (exito, mensaje) = await _atributoService.CambiarOrdenAsync(id, orden);
                return Json(new { success = exito, message = mensaje });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cambiar orden");
                return Json(new { success = false, error = "Error al cambiar el orden" });
            }
        }

        /// <summary>
        /// POST: /Atributos/Duplicar/5
        /// Duplicar atributo (AJAX)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Duplicar(int id)
        {
            try
            {
                var (exito, mensaje, duplicado) = await _atributoService.DuplicarAsync(id);

                if (exito)
                {
                    return Json(new
                    {
                        success = true,
                        message = mensaje,
                        atributoId = duplicado!.AtributoID
                    });
                }

                return Json(new { success = false, error = mensaje });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al duplicar atributo");
                return Json(new { success = false, error = "Error al duplicar el atributo" });
            }
        }

        /// <summary>
        /// POST: /Atributos/ToggleActivo/5
        /// Activar/Desactivar atributo (AJAX)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ToggleActivo(int id)
        {
            try
            {
                var (exito, mensaje, nuevoEstado) = await _atributoService.ToggleActivoAsync(id);

                if (exito)
                    return Json(new { success = true, activo = nuevoEstado, message = mensaje });

                return Json(new { success = false, error = mensaje });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cambiar estado");
                return Json(new { success = false, error = "Error al cambiar el estado" });
            }
        }

        /// <summary>
        /// GET: /Atributos/PorCategoria/5
        /// Obtener atributos de una categoría (AJAX)
        /// </summary>
        [HttpGet]
        [AllowAnonymous] // Permitir para formulario de productos
        public async Task<IActionResult> PorCategoria(int id)
        {
            try
            {
                var atributos = await _atributoService.ObtenerActivosPorCategoriaAsync(id);

                var resultado = atributos.Select(a => new
                {
                    atributoId = a.AtributoID,
                    nombre = a.Nombre,
                    nombreTecnico = a.NombreTecnico,
                    descripcion = a.Descripcion,
                    tipoCampo = a.TipoCampo,
                    opciones = a.OpcionesLista,
                    unidad = a.Unidad,
                    iconoClass = a.IconoClass,
                    grupo = a.Grupo,
                    orden = a.Orden,
                    obligatorio = a.Obligatorio,
                    valorMinimo = a.ValorMinimo,
                    valorMaximo = a.ValorMaximo
                }).ToList();

                return Json(new { success = true, atributos = resultado });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener atributos de categoría {ID}", id);
                return Json(new { success = false, error = "Error al cargar atributos" });
            }
        }

        /// <summary>
        /// POST: /Atributos/CopiarACategoria
        /// Copiar atributos de una categoría a otra (AJAX)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CopiarACategoria(int categoriaOrigenId, int categoriaDestinoId)
        {
            try
            {
                var (exito, mensaje, copiados) = await _atributoService.CopiarAtributosAsync(
                    categoriaOrigenId, categoriaDestinoId);

                return Json(new { success = exito, message = mensaje, count = copiados });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al copiar atributos");
                return Json(new { success = false, error = "Error al copiar los atributos" });
            }
        }

        /// <summary>
        /// GET: /Atributos/Estadisticas/5
        /// Estadísticas de atributos de una categoría (AJAX)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Estadisticas(int id)
        {
            try
            {
                var (total, activos, obligatorios, filtrables) =
                    await _atributoService.ObtenerEstadisticasAsync(id);

                return Json(new
                {
                    success = true,
                    total,
                    activos,
                    obligatorios,
                    filtrables
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener estadísticas de categoría {ID}", id);
                return Json(new { success = false, error = "Error al cargar estadísticas" });
            }
        }
    }
}
