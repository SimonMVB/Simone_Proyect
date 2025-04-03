using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Simone.Models
{
    public class Reseñas
    {
        [Key]
        public int ReseñaID { get; set; }

        public int ProductoID { get; set; }
        public int ClienteID { get; set; }
        public int? Calificacion { get; set; }
        public string? Comentario { get; set; }
        public DateTime? FechaReseña { get; set; }

        [ForeignKey("ProductoID")]
        public Producto Producto { get; set; }  // ✅ Asegurar que está bien definido

        [ForeignKey("ClienteID")]
        public Cliente Cliente { get; set; }  // ✅ Asegurar que no es un "object"
    }
}
