using System;
using System.ComponentModel.DataAnnotations.Schema;
using Simone.Models;
using System.ComponentModel.DataAnnotations;


namespace Simone.Models
{
    public class DetalleVentas
    {
        [Key] // 🔹 Asegura que DetalleVentaID sea la clave primaria
        public int DetalleVentaID { get; set; }

        public int VentaID { get; set; }  // Clave foránea con Ventas
        public int ProductoID { get; set; }  // Clave foránea con Productos
        public int Cantidad { get; set; }  // Cantidad vendida
        public decimal PrecioUnitario { get; set; }  // Precio por unidad
        public decimal? Descuento { get; set; }  // Puede ser nulo
        public decimal? Subtotal { get; set; }  // Puede ser nulo
        public DateTime FechaCreacion { get; set; }  // Fecha de creación

        // Relación con Ventas
        // Relaciones
        [ForeignKey("VentaID")]
        public Ventas Venta { get; set; }

        // Relación con Productos
        [ForeignKey("ProductoID")]
        public Productos Producto { get; set; }
    }
}
