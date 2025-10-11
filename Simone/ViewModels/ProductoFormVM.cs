using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Simone.ViewModels
{
    public sealed class ProductoFormVM
    {
        // ----- Producto base (solo lo editable) -----
        public int? ProductoID { get; set; }

        [Required, StringLength(200)]
        public string Nombre { get; set; } = "";

        [StringLength(2000)]
        public string? Descripcion { get; set; }

        [StringLength(120)]
        public string? Marca { get; set; }

        [Range(0.01, double.MaxValue)]
        public decimal PrecioCompra { get; set; }

        // Si hay variantes, puedes dejarla null y calcular en servidor
        [Range(0.01, double.MaxValue)]
        public decimal? PrecioVenta { get; set; }

        [Range(0, int.MaxValue)]
        public int Stock { get; set; }

        public string? Talla { get; set; }
        public string? Color { get; set; }

        [Required] public int ProveedorID { get; set; }
        [Required] public int CategoriaID { get; set; }
        [Required] public int SubcategoriaID { get; set; }

        // ----- Imágenes -----
        public IFormFile[] NuevasImagenes { get; set; } = System.Array.Empty<IFormFile>();

        // Portada: soportar existente o nueva
        public int? PortadaExistenteId { get; set; }   // id de imagen ya guardada
        public int? PortadaNuevaIndex { get; set; }    // índice dentro de NuevasImagenes

        // Reordenamiento (id -> orden) y eliminaciones
        public Dictionary<int, int> OrdenPorId { get; set; } = new();
        public int[] EliminarImagenIds { get; set; } = System.Array.Empty<int>();

        // ----- Variantes -----
        public List<VarianteVM> Variantes { get; set; } = new();
    }

    public sealed class VarianteVM
    {
        public int? ProductoVarianteID { get; set; } // para edición

        [Required, StringLength(50)]
        public string Color { get; set; } = "";

        [Required, StringLength(20)]
        public string Talla { get; set; } = "";

        [Range(0.01, double.MaxValue)]
        public decimal PrecioVenta { get; set; }

        [Range(0, int.MaxValue)]
        public int Stock { get; set; }
    }
}
