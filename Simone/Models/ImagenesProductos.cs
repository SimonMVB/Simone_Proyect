using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Simone.Models
{
    public class ImagenesProductos
    {
        [Key]  // ✅ Definir clave primaria
        public int ImagenID { get; set; }

        public int ProductoID { get; set; }  // Clave foránea con Productos

        [Required]
        public string RutaImagen { get; set; }  // Ruta de la imagen

        // Relación con Productos
        [ForeignKey("ProductoID")]
        public Productos Producto { get; set; }
    }
}
