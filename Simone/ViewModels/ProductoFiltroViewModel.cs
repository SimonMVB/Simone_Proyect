using Simone.Models;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Simone.ViewModels
{
    /// <summary>
    /// ViewModel para la vista de productos con filtros y paginación
    /// Versión mejorada con soporte completo para filtros avanzados
    /// </summary>
    public class ProductoFiltroViewModel
    {
        #region Productos

        /// <summary>
        /// Lista de productos que coinciden con los filtros aplicados
        /// </summary>
        public List<Producto> Productos { get; set; } = new List<Producto>();

        #endregion

        #region Filtros Aplicados

        /// <summary>
        /// Precio mínimo aplicado como filtro
        /// </summary>
        [Display(Name = "Precio mínimo")]
        [Range(0, 1000000, ErrorMessage = "El precio mínimo debe ser mayor o igual a 0")]
        public decimal? PrecioMin { get; set; }

        /// <summary>
        /// Precio máximo aplicado como filtro
        /// </summary>
        [Display(Name = "Precio máximo")]
        [Range(0, 1000000, ErrorMessage = "El precio máximo debe ser mayor a 0")]
        public decimal PrecioMax { get; set; } = 10000;

        /// <summary>
        /// Filtrar solo productos con stock disponible
        /// </summary>
        [Display(Name = "Solo productos disponibles")]
        public bool SoloDisponibles { get; set; }

        /// <summary>
        /// Lista de marcas seleccionadas para filtrar
        /// </summary>
        public List<string> MarcasSeleccionadas { get; set; } = new List<string>();

        /// <summary>
        /// Lista de colores seleccionados para filtrar
        /// </summary>
        public List<string> ColoresSeleccionados { get; set; } = new List<string>();

        /// <summary>
        /// Lista de tallas seleccionadas para filtrar
        /// </summary>
        public List<string> TallasSeleccionadas { get; set; } = new List<string>();

        /// <summary>
        /// Término de búsqueda libre
        /// </summary>
        [Display(Name = "Búsqueda")]
        [StringLength(100, ErrorMessage = "La búsqueda no puede exceder 100 caracteres")]
        public string? Busqueda { get; set; }

        /// <summary>
        /// Criterio de ordenamiento seleccionado
        /// </summary>
        [Display(Name = "Ordenar por")]
        public string? OrdenarPor { get; set; }

        #endregion

        #region Opciones de Filtros Disponibles

        /// <summary>
        /// Lista de todas las marcas disponibles para filtrar
        /// </summary>
        public List<string> MarcasDisponibles { get; set; } = new List<string>();

        /// <summary>
        /// Lista de todos los colores disponibles para filtrar
        /// </summary>
        public List<string> ColoresDisponibles { get; set; } = new List<string>();

        /// <summary>
        /// Lista de todas las tallas disponibles para filtrar
        /// </summary>
        public List<string> TallasDisponibles { get; set; } = new List<string>();

        #endregion

        #region Información de Paginación

        /// <summary>
        /// Número de página actual (base 1)
        /// </summary>
        [Display(Name = "Página")]
        [Range(1, int.MaxValue, ErrorMessage = "La página debe ser mayor a 0")]
        public int PaginaActual { get; set; } = 1;

        /// <summary>
        /// Total de páginas disponibles
        /// </summary>
        public int TotalPaginas { get; set; }

        /// <summary>
        /// Total de productos que coinciden con los filtros
        /// </summary>
        public int TotalProductos { get; set; }

        /// <summary>
        /// Número de productos por página
        /// </summary>
        public int ProductosPorPagina { get; set; } = 12;

        /// <summary>
        /// Indica si existe página anterior
        /// </summary>
        public bool TienePaginaAnterior => PaginaActual > 1;

        /// <summary>
        /// Indica si existe página siguiente
        /// </summary>
        public bool TienePaginaSiguiente => PaginaActual < TotalPaginas;

        /// <summary>
        /// Número de la página anterior
        /// </summary>
        public int PaginaAnterior => PaginaActual - 1;

        /// <summary>
        /// Número de la página siguiente
        /// </summary>
        public int PaginaSiguiente => PaginaActual + 1;

        /// <summary>
        /// Rango de productos mostrados (ej: "1-12")
        /// </summary>
        public string RangoProductos
        {
            get
            {
                if (TotalProductos == 0)
                    return "0";

                var inicio = ((PaginaActual - 1) * ProductosPorPagina) + 1;
                var fin = Math.Min(PaginaActual * ProductosPorPagina, TotalProductos);
                return $"{inicio}-{fin}";
            }
        }

        #endregion

        #region Métodos Helper

        /// <summary>
        /// Verifica si hay algún filtro aplicado
        /// </summary>
        public bool TieneFiltrosAplicados()
        {
            return MarcasSeleccionadas.Any() ||
                   ColoresSeleccionados.Any() ||
                   TallasSeleccionadas.Any() ||
                   PrecioMin.HasValue ||
                   PrecioMax < 10000 ||
                   SoloDisponibles ||
                   !string.IsNullOrWhiteSpace(Busqueda);
        }

        /// <summary>
        /// Obtiene el conteo total de filtros activos
        /// </summary>
        public int ConteoFiltrosActivos()
        {
            int conteo = 0;

            if (MarcasSeleccionadas.Any()) conteo++;
            if (ColoresSeleccionados.Any()) conteo++;
            if (TallasSeleccionadas.Any()) conteo++;
            if (PrecioMin.HasValue) conteo++;
            if (PrecioMax < 10000) conteo++;
            if (SoloDisponibles) conteo++;
            if (!string.IsNullOrWhiteSpace(Busqueda)) conteo++;

            return conteo;
        }

        /// <summary>
        /// Genera lista de números de página para mostrar en navegación
        /// </summary>
        /// <param name="ventana">Cantidad de páginas a mostrar a cada lado</param>
        public List<int> ObtenerPaginasVisibles(int ventana = 2)
        {
            var paginas = new List<int>();

            var inicio = Math.Max(1, PaginaActual - ventana);
            var fin = Math.Min(TotalPaginas, PaginaActual + ventana);

            for (int i = inicio; i <= fin; i++)
            {
                paginas.Add(i);
            }

            return paginas;
        }

        #endregion
    }
}