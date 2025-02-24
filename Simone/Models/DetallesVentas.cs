using System;
using Simone.Models;


namespace Simone.Models
{
    public class DetalleVentas
    {
        public int DetalleVentaID { get; set; }  // Clave primaria
        public int VentaID { get; set; }  // Clave foránea con Ventas
        public int ProductoID { get; set; }  // Clave foránea con Productos
        public int Cantidad { get; set; }  // Cantidad vendida
        public decimal PrecioUnitario { get; set; }  // Precio por unidad
        public decimal? Descuento { get; set; }  // Puede ser nulo
        public decimal? Subtotal { get; set; }  // Puede ser nulo
        public DateTime FechaCreacion { get; set; }  // Fecha de creación

        // Relación con Ventas
        public Ventas Venta { get; set; }

        // Relación con Productos
        public Productos Producto { get; set; }
    }
}
