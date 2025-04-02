using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Simone.Data;
using Simone.Models;
using Simone.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Simone.Controllers
{
    public class CategoriasController : Controller
    {
        private readonly TiendaDbContext _context;
        private readonly ILogger<CategoriasController> _logger;

        public CategoriasController(TiendaDbContext context, ILogger<CategoriasController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Vista general con filtros para mostrar todos los productos.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Ver_Todo(
            List<string> MarcasSeleccionadas,
            List<string> ColoresSeleccionados,
            List<string> TallasSeleccionadas,
            int PrecioMax = 500,
            bool SoloDisponibles = false)
        {
            return await ObtenerVistaFiltrada(null, MarcasSeleccionadas, ColoresSeleccionados, TallasSeleccionadas, PrecioMax, SoloDisponibles);
        }

        /// <summary>
        /// Muestra los productos filtrados por categoría específica.
        /// </summary>
        private async Task<IActionResult> ObtenerVistaFiltrada(string? categoria,
            List<string>? MarcasSeleccionadas,
            List<string>? ColoresSeleccionados,
            List<string>? TallasSeleccionadas,
            int PrecioMax,
            bool SoloDisponibles)
        {
            try
            {
                MarcasSeleccionadas ??= new List<string>();
                ColoresSeleccionados ??= new List<string>();
                TallasSeleccionadas ??= new List<string>();

                var productosQuery = _context.Productos
                    .Include(p => p.ImagenesProductos)
                    .AsQueryable();

                if (!string.IsNullOrEmpty(categoria))
                    productosQuery = productosQuery.Where(p => p.Categoria.NombreCategoria.ToLower() == categoria.ToLower());

                if (MarcasSeleccionadas.Any())
                    productosQuery = productosQuery.Where(p => MarcasSeleccionadas.Contains(p.Marca));

                if (ColoresSeleccionados.Any())
                    productosQuery = productosQuery.Where(p => ColoresSeleccionados.Contains(p.Color));

                if (TallasSeleccionadas.Any())
                    productosQuery = productosQuery.Where(p => TallasSeleccionadas.Contains(p.Talla));

                if (SoloDisponibles)
                    productosQuery = productosQuery.Where(p => p.Stock > 0);

                productosQuery = productosQuery.Where(p => p.PrecioVenta <= PrecioMax)
                                               .OrderBy(p => p.NombreProducto);

                var productos = await productosQuery.ToListAsync();

                var marcasDisponibles = await _context.Productos
                    .Where(p => p.Marca != null)
                    .Select(p => p.Marca)
                    .Distinct()
                    .OrderBy(m => m)
                    .ToListAsync();

                var coloresDisponibles = await _context.Productos
                    .Where(p => p.Color != null)
                    .Select(p => p.Color)
                    .Distinct()
                    .OrderBy(c => c)
                    .ToListAsync();

                var tallasDisponibles = await _context.Productos
                    .Where(p => p.Talla != null)
                    .Select(p => p.Talla)
                    .Distinct()
                    .OrderBy(t => t)
                    .ToListAsync();

                var vm = new ProductoFiltroViewModel
                {
                    Productos = productos,
                    PrecioMax = PrecioMax,
                    SoloDisponibles = SoloDisponibles,
                    MarcasSeleccionadas = MarcasSeleccionadas,
                    ColoresSeleccionados = ColoresSeleccionados,
                    TallasSeleccionadas = TallasSeleccionadas,
                    MarcasDisponibles = marcasDisponibles,
                    ColoresDisponibles = coloresDisponibles,
                    TallasDisponibles = tallasDisponibles
                };

                return View("Ver_Todo", vm); // Usa la misma vista para todo
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar productos{Categoria}", categoria != null ? $" de la categoría {categoria}" : "");
                return StatusCode(500, "Ocurrió un error al cargar los productos.");
            }
        }

        // ✅ Métodos por categoría (pueden usar vistas específicas si las prefieres)
        public async Task<IActionResult> Blusas() => await ObtenerVistaFiltrada("Blusas", null, null, null, 500, false);
        public async Task<IActionResult> Tops() => await ObtenerVistaFiltrada("Tops", null, null, null, 500, false);
        public async Task<IActionResult> Bodys() => await ObtenerVistaFiltrada("Bodys", null, null, null, 500, false);
        public async Task<IActionResult> TrajeDeBaño() => await ObtenerVistaFiltrada("Traje de Baño", null, null, null, 500, false);
        public async Task<IActionResult> Conjuntos() => await ObtenerVistaFiltrada("Conjuntos", null, null, null, 500, false);
        public async Task<IActionResult> Vestidos() => await ObtenerVistaFiltrada("Vestidos", null, null, null, 500, false);
        public async Task<IActionResult> Faldas() => await ObtenerVistaFiltrada("Faldas", null, null, null, 500, false);
        public async Task<IActionResult> Pantalones() => await ObtenerVistaFiltrada("Pantalones", null, null, null, 500, false);
        public async Task<IActionResult> Jeans() => await ObtenerVistaFiltrada("Jeans", null, null, null, 500, false);
        public async Task<IActionResult> Bolsas() => await ObtenerVistaFiltrada("Bolsas", null, null, null, 500, false);
    }
}
