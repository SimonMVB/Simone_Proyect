using Microsoft.AspNetCore.Http;
using Simone.Models;
using System.Collections.Generic;

namespace Simone.ViewModels
{
    public class ProductoFormVM
    {
        public Producto Producto { get; set; } = new();

        // Nuevas imágenes subidas desde el form
        public IFormFile[] NuevasImagenes { get; set; } = System.Array.Empty<IFormFile>();

        // Id de imagen marcada como principal (radio en la vista)
        public int? ImagenPrincipalId { get; set; }

        // Reordenamiento (id -> orden)
        public Dictionary<int, int> OrdenPorId { get; set; } = new();

        // Imágenes existentes a eliminar
        public int[] EliminarImagenIds { get; set; } = System.Array.Empty<int>();

        // Variantes en el form
        public List<ProductoVariante> Variantes { get; set; } = new();
    }
}
