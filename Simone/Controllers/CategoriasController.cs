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
        /// <param name="MarcasSeleccionadas">Lista de marcas seleccionadas</param>
        /// <param name="ColoresSeleccionados">Lista de colores seleccionados</param>
        /// <param name="TallasSeleccionadas">Lista de tallas seleccionadas</param>
        /// <param name="PrecioMax">Precio máximo permitido</param>
        /// <param name="SoloDisponibles">Indica si se muestran solo productos disponibles</param>
        /// <returns>Vista con el ViewModel de productos y filtros</returns>
        [HttpGet]
        public async Task<IActionResult> Ver_Todo(
            List<string> MarcasSeleccionadas,
            List<string> ColoresSeleccionados,
            List<string> TallasSeleccionadas,
            int PrecioMax = 500,
            bool SoloDisponibles = false)
        {
            try
            {
                // Asegurarse de que las listas de filtros no sean null.
                MarcasSeleccionadas ??= new List<string>();
                ColoresSeleccionados ??= new List<string>();
                TallasSeleccionadas ??= new List<string>();

                // Construir la consulta base de productos, incluyendo las imágenes.
                var productosQuery = _context.Productos
                    .Include(p => p.ImagenesProductos)
                    .AsQueryable();

                // Aplicar filtros dinámicos.
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

                // Obtener valores únicos para los filtros.
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

                // Preparar el ViewModel con los productos filtrados y los datos de los filtros.
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

                return View(vm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en Ver_Todo");
                return StatusCode(500, "Ocurrió un error interno. Por favor, intenta nuevamente más tarde.");
            }
        }
    }
}
