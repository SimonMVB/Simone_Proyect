using System;
using Simone.Models;

namespace Simone.Models
{
    public class DetallesCompra
    {
        public int DetalleID { get; set; }  // Clave primaria
        public int CompraID { get; set; }  // Clave foránea con Compras
        public int ProductoID { get; set; }  // Clave foránea con Productos
        public int Cantidad { get; set; }  // Cantidad comprada
        public decimal PrecioUnitario { get; set; }  // Precio por unidad
        public decimal? Subtotal { get; set; }  // Puede ser nulo

        // Relación con Compras
        public Compras Compra { get; set; }

        // Relación con Productos
        public Productos Producto { get; set; }
    }
}
