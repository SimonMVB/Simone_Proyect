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
    /// <summary>
    /// Controlador para manejo de categorías de productos con filtros
    /// Versión compatible con el modelo de datos actual
    /// </summary>
    public class CategoriasController : Controller
    {
        private readonly TiendaDbContext _context;
        private readonly ILogger<CategoriasController> _logger;

        // Configuración de paginación
        private const int PRODUCTOS_POR_PAGINA = 12;
        private const int PRECIO_MAXIMO_DEFAULT = 10000;

        public CategoriasController(TiendaDbContext context, ILogger<CategoriasController> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region Acciones Públicas por Categoría

        /// <summary>
        /// Vista general con todos los productos y filtros
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Ver_Todo(
            List<string>? MarcasSeleccionadas = null,
            List<string>? ColoresSeleccionados = null,
            List<string>? TallasSeleccionadas = null,
            decimal? PrecioMin = null,
            decimal? PrecioMax = null,
            string? Busqueda = null,
            string? Ordenar = null,
            bool SoloDisponibles = false,
            int pagina = 1)
        {
            _logger.LogInformation(
                "Ver_Todo solicitado. Pagina={Pagina}, SoloDisponibles={SoloDisponibles}",
                pagina,
                SoloDisponibles);

            return await ObtenerVistaFiltrada(
                categoriaNombre: null,
                marcasSeleccionadas: MarcasSeleccionadas,
                coloresSeleccionados: ColoresSeleccionados,
                tallasSeleccionadas: TallasSeleccionadas,
                precioMin: PrecioMin,
                precioMax: PrecioMax,
                busqueda: Busqueda,
                ordenar: Ordenar,
                soloDisponibles: SoloDisponibles,
                pagina: pagina);
        }

        /// <summary>
        /// Categoría: Blusas
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Blusas(
            List<string>? MarcasSeleccionadas = null,
            List<string>? ColoresSeleccionados = null,
            List<string>? TallasSeleccionadas = null,
            decimal? PrecioMin = null,
            decimal? PrecioMax = null,
            string? Ordenar = null,
            bool SoloDisponibles = false,
            int pagina = 1)
        {
            ViewBag.NombreCategoria = "Blusas";

            return await ObtenerVistaFiltrada(
                categoriaNombre: "Blusas",
                marcasSeleccionadas: MarcasSeleccionadas,
                coloresSeleccionados: ColoresSeleccionados,
                tallasSeleccionadas: TallasSeleccionadas,
                precioMin: PrecioMin,
                precioMax: PrecioMax,
                busqueda: null,
                ordenar: Ordenar,
                soloDisponibles: SoloDisponibles,
                pagina: pagina);
        }

        /// <summary>
        /// Categoría: Body's
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Bodys(
            List<string>? MarcasSeleccionadas = null,
            List<string>? ColoresSeleccionados = null,
            List<string>? TallasSeleccionadas = null,
            decimal? PrecioMin = null,
            decimal? PrecioMax = null,
            string? Ordenar = null,
            bool SoloDisponibles = false,
            int pagina = 1)
        {
            ViewBag.NombreCategoria = "Body's";

            return await ObtenerVistaFiltrada(
                categoriaNombre: "Body's",
                marcasSeleccionadas: MarcasSeleccionadas,
                coloresSeleccionados: ColoresSeleccionados,
                tallasSeleccionadas: TallasSeleccionadas,
                precioMin: PrecioMin,
                precioMax: PrecioMax,
                busqueda: null,
                ordenar: Ordenar,
                soloDisponibles: SoloDisponibles,
                pagina: pagina);
        }

        /// <summary>
        /// Categoría: Bolsas
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Bolsas(
            List<string>? MarcasSeleccionadas = null,
            List<string>? ColoresSeleccionados = null,
            List<string>? TallasSeleccionadas = null,
            decimal? PrecioMin = null,
            decimal? PrecioMax = null,
            string? Ordenar = null,
            bool SoloDisponibles = false,
            int pagina = 1)
        {
            ViewBag.NombreCategoria = "Bolsas";

            return await ObtenerVistaFiltrada(
                categoriaNombre: "Bolsas",
                marcasSeleccionadas: MarcasSeleccionadas,
                coloresSeleccionados: ColoresSeleccionados,
                tallasSeleccionadas: TallasSeleccionadas,
                precioMin: PrecioMin,
                precioMax: PrecioMax,
                busqueda: null,
                ordenar: Ordenar,
                soloDisponibles: SoloDisponibles,
                pagina: pagina);
        }

        /// <summary>
        /// Categoría: Conjuntos
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Conjuntos(
            List<string>? MarcasSeleccionadas = null,
            List<string>? ColoresSeleccionados = null,
            List<string>? TallasSeleccionadas = null,
            decimal? PrecioMin = null,
            decimal? PrecioMax = null,
            string? Ordenar = null,
            bool SoloDisponibles = false,
            int pagina = 1)
        {
            ViewBag.NombreCategoria = "Conjuntos";

            return await ObtenerVistaFiltrada(
                categoriaNombre: "Conjuntos",
                marcasSeleccionadas: MarcasSeleccionadas,
                coloresSeleccionados: ColoresSeleccionados,
                tallasSeleccionadas: TallasSeleccionadas,
                precioMin: PrecioMin,
                precioMax: PrecioMax,
                busqueda: null,
                ordenar: Ordenar,
                soloDisponibles: SoloDisponibles,
                pagina: pagina);
        }

        /// <summary>
        /// Categoría: Faldas
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Faldas(
            List<string>? MarcasSeleccionadas = null,
            List<string>? ColoresSeleccionados = null,
            List<string>? TallasSeleccionadas = null,
            decimal? PrecioMin = null,
            decimal? PrecioMax = null,
            string? Ordenar = null,
            bool SoloDisponibles = false,
            int pagina = 1)
        {
            ViewBag.NombreCategoria = "Faldas";

            return await ObtenerVistaFiltrada(
                categoriaNombre: "Faldas",
                marcasSeleccionadas: MarcasSeleccionadas,
                coloresSeleccionados: ColoresSeleccionados,
                tallasSeleccionadas: TallasSeleccionadas,
                precioMin: PrecioMin,
                precioMax: PrecioMax,
                busqueda: null,
                ordenar: Ordenar,
                soloDisponibles: SoloDisponibles,
                pagina: pagina);
        }

        /// <summary>
        /// Categoría: Jeans
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Jeans(
            List<string>? MarcasSeleccionadas = null,
            List<string>? ColoresSeleccionados = null,
            List<string>? TallasSeleccionadas = null,
            decimal? PrecioMin = null,
            decimal? PrecioMax = null,
            string? Ordenar = null,
            bool SoloDisponibles = false,
            int pagina = 1)
        {
            ViewBag.NombreCategoria = "Jeans";

            return await ObtenerVistaFiltrada(
                categoriaNombre: "Jeans",
                marcasSeleccionadas: MarcasSeleccionadas,
                coloresSeleccionados: ColoresSeleccionados,
                tallasSeleccionadas: TallasSeleccionadas,
                precioMin: PrecioMin,
                precioMax: PrecioMax,
                busqueda: null,
                ordenar: Ordenar,
                soloDisponibles: SoloDisponibles,
                pagina: pagina);
        }

        /// <summary>
        /// Categoría: Pantalones
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Pantalones(
            List<string>? MarcasSeleccionadas = null,
            List<string>? ColoresSeleccionados = null,
            List<string>? TallasSeleccionadas = null,
            decimal? PrecioMin = null,
            decimal? PrecioMax = null,
            string? Ordenar = null,
            bool SoloDisponibles = false,
            int pagina = 1)
        {
            ViewBag.NombreCategoria = "Pantalones";

            return await ObtenerVistaFiltrada(
                categoriaNombre: "Pantalones",
                marcasSeleccionadas: MarcasSeleccionadas,
                coloresSeleccionados: ColoresSeleccionados,
                tallasSeleccionadas: TallasSeleccionadas,
                precioMin: PrecioMin,
                precioMax: PrecioMax,
                busqueda: null,
                ordenar: Ordenar,
                soloDisponibles: SoloDisponibles,
                pagina: pagina);
        }

        /// <summary>
        /// Categoría: Tops
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Tops(
            List<string>? MarcasSeleccionadas = null,
            List<string>? ColoresSeleccionados = null,
            List<string>? TallasSeleccionadas = null,
            decimal? PrecioMin = null,
            decimal? PrecioMax = null,
            string? Ordenar = null,
            bool SoloDisponibles = false,
            int pagina = 1)
        {
            ViewBag.NombreCategoria = "Tops";

            return await ObtenerVistaFiltrada(
                categoriaNombre: "Tops",
                marcasSeleccionadas: MarcasSeleccionadas,
                coloresSeleccionados: ColoresSeleccionados,
                tallasSeleccionadas: TallasSeleccionadas,
                precioMin: PrecioMin,
                precioMax: PrecioMax,
                busqueda: null,
                ordenar: Ordenar,
                soloDisponibles: SoloDisponibles,
                pagina: pagina);
        }

        /// <summary>
        /// Categoría: Trajes de Baño
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> TrajeDeBano(
            List<string>? MarcasSeleccionadas = null,
            List<string>? ColoresSeleccionados = null,
            List<string>? TallasSeleccionadas = null,
            decimal? PrecioMin = null,
            decimal? PrecioMax = null,
            string? Ordenar = null,
            bool SoloDisponibles = false,
            int pagina = 1)
        {
            ViewBag.NombreCategoria = "Trajes de Baño";

            return await ObtenerVistaFiltrada(
                categoriaNombre: "Traje de Baño",
                marcasSeleccionadas: MarcasSeleccionadas,
                coloresSeleccionados: ColoresSeleccionados,
                tallasSeleccionadas: TallasSeleccionadas,
                precioMin: PrecioMin,
                precioMax: PrecioMax,
                busqueda: null,
                ordenar: Ordenar,
                soloDisponibles: SoloDisponibles,
                pagina: pagina);
        }

        /// <summary>
        /// Categoría: Vestidos
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Vestidos(
            List<string>? MarcasSeleccionadas = null,
            List<string>? ColoresSeleccionados = null,
            List<string>? TallasSeleccionadas = null,
            decimal? PrecioMin = null,
            decimal? PrecioMax = null,
            string? Ordenar = null,
            bool SoloDisponibles = false,
            int pagina = 1)
        {
            ViewBag.NombreCategoria = "Vestidos";

            return await ObtenerVistaFiltrada(
                categoriaNombre: "Vestidos",
                marcasSeleccionadas: MarcasSeleccionadas,
                coloresSeleccionados: ColoresSeleccionados,
                tallasSeleccionadas: TallasSeleccionadas,
                precioMin: PrecioMin,
                precioMax: PrecioMax,
                busqueda: null,
                ordenar: Ordenar,
                soloDisponibles: SoloDisponibles,
                pagina: pagina);
        }

        #endregion

        #region Métodos Privados de Lógica

        /// <summary>
        /// Obtiene productos filtrados con paginación
        /// Compatible con el modelo de datos actual
        /// </summary>
        private async Task<IActionResult> ObtenerVistaFiltrada(
            string? categoriaNombre,
            List<string>? marcasSeleccionadas,
            List<string>? coloresSeleccionados,
            List<string>? tallasSeleccionadas,
            decimal? precioMin,
            decimal? precioMax,
            string? busqueda,
            string? ordenar,
            bool soloDisponibles,
            int pagina)
        {
            try
            {
                // Inicializar listas si son null
                marcasSeleccionadas ??= new List<string>();
                coloresSeleccionados ??= new List<string>();
                tallasSeleccionadas ??= new List<string>();

                // Validar página
                if (pagina < 1) pagina = 1;

                // ========== QUERY BASE DE PRODUCTOS ==========
                var productosQuery = _context.Productos
                    .Include(p => p.Categoria)
                    .Include(p => p.Subcategoria)
                    .Include(p => p.ImagenesProductos)
                    .AsQueryable();

                // ========== FILTROS ==========

                // Filtro por categoría (usando nombre como en el código original)
                if (!string.IsNullOrWhiteSpace(categoriaNombre))
                {
                    productosQuery = productosQuery.Where(p =>
                        p.Categoria != null &&
                        p.Categoria.Nombre == categoriaNombre);
                }

                // Filtro por búsqueda
                if (!string.IsNullOrWhiteSpace(busqueda))
                {
                    var busquedaLower = busqueda.ToLower();
                    productosQuery = productosQuery.Where(p =>
                        p.Nombre.ToLower().Contains(busquedaLower) ||
                        (p.Descripcion != null && p.Descripcion.ToLower().Contains(busquedaLower)));
                }

                // Filtro por rango de precios
                if (precioMin.HasValue)
                {
                    productosQuery = productosQuery.Where(p => p.PrecioVenta >= precioMin.Value);
                }

                if (precioMax.HasValue)
                {
                    productosQuery = productosQuery.Where(p => p.PrecioVenta <= precioMax.Value);
                }
                else
                {
                    productosQuery = productosQuery.Where(p => p.PrecioVenta <= PRECIO_MAXIMO_DEFAULT);
                }

                // Filtro por stock disponible
                if (soloDisponibles)
                {
                    productosQuery = productosQuery.Where(p => p.Stock > 0);
                }

                // Filtros por marca, color, talla (si existen estos campos en tu modelo)
                // NOTA: Ajusta estos filtros según los campos reales de tu modelo Producto
                // Si estos campos están en ProductoVariante, necesitarás ajustar las queries

                if (marcasSeleccionadas.Any())
                {
                    // Descomenta si tienes campo Marca en Producto:
                    // productosQuery = productosQuery.Where(p => 
                    //     p.Marca != null && marcasSeleccionadas.Contains(p.Marca));
                }

                if (coloresSeleccionados.Any())
                {
                    // Descomenta si tienes campo Color en Producto:
                    // productosQuery = productosQuery.Where(p => 
                    //     p.Color != null && coloresSeleccionados.Contains(p.Color));
                }

                if (tallasSeleccionadas.Any())
                {
                    // Descomenta si tienes campo Talla en Producto:
                    // productosQuery = productosQuery.Where(p => 
                    //     p.Talla != null && tallasSeleccionadas.Contains(p.Talla));
                }

                // ========== ORDENAMIENTO ==========
                productosQuery = ordenar switch
                {
                    "precio_asc" => productosQuery.OrderBy(p => p.PrecioVenta),
                    "precio_desc" => productosQuery.OrderByDescending(p => p.PrecioVenta),
                    "nombre_asc" => productosQuery.OrderBy(p => p.Nombre),
                    "nombre_desc" => productosQuery.OrderByDescending(p => p.Nombre),
                    "nuevos" => productosQuery.OrderByDescending(p => p.ProductoID),
                    _ => productosQuery.OrderBy(p => p.Nombre)
                };

                // ========== CONTAR TOTAL ANTES DE PAGINAR ==========
                var totalProductos = await productosQuery.CountAsync();

                // ========== PAGINACIÓN ==========
                var productosPaginados = await productosQuery
                    .Skip((pagina - 1) * PRODUCTOS_POR_PAGINA)
                    .Take(PRODUCTOS_POR_PAGINA)
                    .AsNoTracking()
                    .ToListAsync();

                // ========== OBTENER FILTROS DISPONIBLES ==========
                var filtrosDisponibles = await ObtenerFiltrosDisponibles(categoriaNombre);

                // ========== CALCULAR INFORMACIÓN DE PAGINACIÓN ==========
                var totalPaginas = (int)Math.Ceiling(totalProductos / (double)PRODUCTOS_POR_PAGINA);

                // ========== PREPARAR VIEWMODEL ==========
                var viewModel = new ProductoFiltroViewModel
                {
                    Productos = productosPaginados,

                    // Filtros aplicados
                    PrecioMin = precioMin,
                    PrecioMax = precioMax ?? PRECIO_MAXIMO_DEFAULT,
                    SoloDisponibles = soloDisponibles,
                    MarcasSeleccionadas = marcasSeleccionadas,
                    ColoresSeleccionados = coloresSeleccionados,
                    TallasSeleccionadas = tallasSeleccionadas,
                    Busqueda = busqueda,
                    OrdenarPor = ordenar,

                    // Opciones de filtros
                    MarcasDisponibles = filtrosDisponibles.Marcas,
                    ColoresDisponibles = filtrosDisponibles.Colores,
                    TallasDisponibles = filtrosDisponibles.Tallas,

                    // Información de paginación
                    PaginaActual = pagina,
                    TotalPaginas = totalPaginas,
                    TotalProductos = totalProductos,
                    ProductosPorPagina = PRODUCTOS_POR_PAGINA
                };

                // ========== VIEWBAG PARA VISTA ==========
                ViewBag.PrecioMaximoGlobal = PRECIO_MAXIMO_DEFAULT;
                ViewBag.OrdenamientoActual = ordenar ?? "nombre_asc";

                _logger.LogInformation(
                    "Vista filtrada generada. Productos: {TotalProductos}, " +
                    "Página: {Pagina}/{TotalPaginas}, Categoría: {Categoria}",
                    totalProductos,
                    pagina,
                    totalPaginas,
                    categoriaNombre ?? "Todas");

                return View("Ver_Todo", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error al cargar productos filtrados. Categoría: {Categoria}, Pagina: {Pagina}",
                    categoriaNombre,
                    pagina);

                TempData["Error"] = "Ocurrió un error al cargar los productos. Por favor, intenta nuevamente.";

                return View("Ver_Todo", new ProductoFiltroViewModel
                {
                    Productos = new List<Producto>(),
                    MarcasDisponibles = new List<string>(),
                    ColoresDisponibles = new List<string>(),
                    TallasDisponibles = new List<string>(),
                    PrecioMax = PRECIO_MAXIMO_DEFAULT
                });
            }
        }

        /// <summary>
        /// Obtiene los filtros disponibles (marcas, colores, tallas)
        /// </summary>
        private async Task<(List<string> Marcas, List<string> Colores, List<string> Tallas)>
            ObtenerFiltrosDisponibles(string? categoriaNombre)
        {
            try
            {
                // Query base de productos
                var productosQuery = _context.Productos.AsQueryable();

                // Filtrar por categoría si es necesario
                if (!string.IsNullOrWhiteSpace(categoriaNombre))
                {
                    productosQuery = productosQuery
                        .Where(p => p.Categoria != null && p.Categoria.Nombre == categoriaNombre);
                }

                // Obtener listas únicas
                // NOTA: Descomenta y ajusta según los campos reales de tu modelo

                var marcas = new List<string>();
                // Si tienes campo Marca:
                // var marcas = await productosQuery
                //     .Where(p => !string.IsNullOrWhiteSpace(p.Marca))
                //     .Select(p => p.Marca)
                //     .Distinct()
                //     .OrderBy(m => m)
                //     .AsNoTracking()
                //     .ToListAsync();

                var colores = new List<string>();
                // Si tienes campo Color:
                // var colores = await productosQuery
                //     .Where(p => !string.IsNullOrWhiteSpace(p.Color))
                //     .Select(p => p.Color)
                //     .Distinct()
                //     .OrderBy(c => c)
                //     .AsNoTracking()
                //     .ToListAsync();

                var tallas = new List<string>();
                // Si tienes campo Talla:
                // var tallas = await productosQuery
                //     .Where(p => !string.IsNullOrWhiteSpace(p.Talla))
                //     .Select(p => p.Talla)
                //     .Distinct()
                //     .OrderBy(t => t)
                //     .AsNoTracking()
                //     .ToListAsync();

                return (marcas, colores, tallas);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener filtros disponibles");
                return (new List<string>(), new List<string>(), new List<string>());
            }
        }

        #endregion
    }
}