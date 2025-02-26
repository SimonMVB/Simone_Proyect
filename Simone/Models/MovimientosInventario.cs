using System;
using System.ComponentModel.DataAnnotations;

namespace Simone.Models
{
    public class MovimientosInventario
    {
        [Key]  // ✅ Definir clave primaria
        public int MovimientoID { get; set; }

        public int ProductoID { get; set; }  // Clave foránea con Productos
        public int Cantidad { get; set; }  // Cantidad del movimiento
        public string TipoMovimiento { get; set; }  // Tipo de movimiento (Entrada/Salida)
        public DateTime? FechaMovimiento { get; set; }  // Puede ser nulo
        public string? Descripcion { get; set; }  // Puede ser nulo

        // Relación con Productos
        public Productos Producto { get; set; }
    }
}
