using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Simone.Models
{
    public class DetallesPedido
    {
        [Key] // ✅ Definir clave primaria
        public int DetalleID { get; set; }

        public int PedidoID { get; set; }  // Clave foránea con Pedidos
        public int ProductoID { get; set; }  // Clave foránea con Productos
        public int Cantidad { get; set; }  // Cantidad de productos en el pedido
        public decimal PrecioUnitario { get; set; }  // Precio de cada unidad
        public decimal? Subtotal { get; set; }  // Puede ser nulo

        // Relación con Pedidos
        [ForeignKey("PedidoID")]
        public Pedido Pedido { get; set; }

        // Relación con Productos
        [ForeignKey("ProductoID")]
        public Producto Producto { get; set; }
    }
}
