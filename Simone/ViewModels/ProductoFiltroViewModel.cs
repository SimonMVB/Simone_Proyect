using Simone.Models;
using System.Collections.Generic;

namespace Simone.ViewModels
{
    /// <summary>
    /// ViewModel para filtrar y mostrar productos en la vista.
    /// Contiene tanto los parámetros seleccionados por el usuario como las opciones disponibles para cada filtro.
    /// </summary>
    public class ProductoFiltroViewModel
    {
        /// <summary>
        /// Lista de productos filtrados que se mostrarán en la vista.
        /// </summary>
        public List<Productos> Productos { get; set; } = new List<Productos>();

        /// <summary>
        /// Lista de marcas seleccionadas por el usuario para filtrar los productos.
        /// </summary>
        public List<string> MarcasSeleccionadas { get; set; } = new List<string>();

        /// <summary>
        /// Lista de colores seleccionados por el usuario para filtrar los productos.
        /// </summary>
        public List<string> ColoresSeleccionados { get; set; } = new List<string>();

        /// <summary>
        /// Lista de tallas seleccionadas por el usuario para filtrar los productos.
        /// </summary>
        public List<string> TallasSeleccionadas { get; set; } = new List<string>();

        /// <summary>
        /// Valor máximo de precio para filtrar los productos. Valor por defecto: 500.
        /// </summary>
        public int PrecioMax { get; set; } = 500;

        /// <summary>
        /// Indica si se deben mostrar solo productos disponibles en stock.
        /// </summary>
        public bool SoloDisponibles { get; set; } = false;

        /// <summary>
        /// Lista de marcas disponibles que se pueden seleccionar para filtrar.
        /// </summary>
        public List<string> MarcasDisponibles { get; set; } = new List<string>();

        /// <summary>
        /// Lista de colores disponibles que se pueden seleccionar para filtrar.
        /// </summary>
        public List<string> ColoresDisponibles { get; set; } = new List<string>();

        /// <summary>
        /// Lista de tallas disponibles que se pueden seleccionar para filtrar.
        /// </summary>
        public List<string> TallasDisponibles { get; set; } = new List<string>();
    }
}
