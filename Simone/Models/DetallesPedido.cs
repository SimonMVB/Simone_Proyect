using System;
using Simone.Models;

namespace Simone.Models
{
    public class DetallesPedido
    {
        public int DetalleID { get; set; }  // Clave primaria
        public int PedidoID { get; set; }  // Clave foránea con Pedidos
        public int ProductoID { get; set; }  // Clave foránea con Productos
        public int Cantidad { get; set; }  // Cantidad de productos en el pedido
        public decimal PrecioUnitario { get; set; }  // Precio de cada unidad
        public decimal? Subtotal { get; set; }  // Puede ser nulo

        // Relación con Pedidos
        public Pedidos Pedido { get; set; }

        // Relación con Productos
        public Productos Producto { get; set; }
    }
}
