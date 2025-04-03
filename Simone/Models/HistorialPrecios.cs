using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Simone.Models
{
    public class HistorialPrecios
    {
        [Key]  // ✅ Definir clave primaria
        public int HistorialPrecioID { get; set; }

        public int ProductoID { get; set; }  // Clave foránea con Productos
        public decimal PrecioAnterior { get; set; }  // Precio antes del cambio
        public decimal PrecioNuevo { get; set; }  // Precio después del cambio
        public DateTime FechaCambio { get; set; }  // Fecha en que se realizó el cambio
        public string UsuarioModifico { get; set; }  // Usuario que hizo el cambio

        // Relación con Productos
        [ForeignKey("ProductoID")]
        public Producto Producto { get; set; }
    }
}
