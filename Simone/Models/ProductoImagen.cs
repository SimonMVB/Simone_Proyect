using System;
using System.ComponentModel.DataAnnotations;

namespace Simone.Models
{
    public class ProductoImagen
    {
        public int ProductoImagenID { get; set; }

        [Required]
        public int ProductoID { get; set; }
        public Producto Producto { get; set; } = null!;

        [Required, MaxLength(300)]
        public string Path { get; set; } = null!; // /uploads/productos/{id}/{archivo}

        public bool Principal { get; set; } = false; // imagen destacada
        public int Orden { get; set; } = 0;          // para ordenar la galería

        public DateTime CreadoUtc { get; set; } = DateTime.UtcNow;
    }
}
