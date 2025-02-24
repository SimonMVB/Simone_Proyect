using System;

namespace Simone.Models
{
    public class HistorialPrecios
    {
        public int HistorialPrecioID { get; set; }  // Clave primaria
        public int ProductoID { get; set; }  // Clave foránea con Productos
        public decimal PrecioAnterior { get; set; }  // Precio antes del cambio
        public decimal PrecioNuevo { get; set; }  // Precio después del cambio
        public DateTime FechaCambio { get; set; }  // Fecha en que se realizó el cambio
        public string UsuarioModifico { get; set; }  // Usuario que hizo el cambio

        // Relación con Productos
        public Productos Producto { get; set; }
    }
}
