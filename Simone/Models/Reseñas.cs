using System;

namespace Simone.Models
{
    public class Reseñas
    {
        public int ReseñaID { get; set; }  // Clave primaria
        public int ProductoID { get; set; }  // Clave foránea con Productos
        public int ClienteID { get; set; }  // Clave foránea con Clientes
        public int? Calificacion { get; set; }  // Calificación del producto (puede ser nulo)
        public string? Comentario { get; set; }  // Puede ser nulo
        public DateTime? FechaReseña { get; set; }  // Puede ser nulo

        // Relaciones
        public Productos Producto { get; set; }
        public Cliente Clientes { get; set; }
    }
}
