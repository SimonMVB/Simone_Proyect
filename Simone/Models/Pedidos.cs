using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Simone.Models
{
    public class Pedido
    {
        [Key]  // ✅ Definir clave primaria
        public int PedidoID { get; set; }

        public int ClienteID { get; set; }  // Clave foránea con Clientes
        public DateTime FechaPedido { get; set; }  // Puede ser nulo
        public string EstadoPedido { get; set; }  // Estado del pedido
        public string? MetodoEnvio { get; set; }  // Puede ser nulo
        public string? DireccionEnvio { get; set; }  // Puede ser nulo
        public decimal Total { get; set; }  // Puede ser nulo

        // Relación con Clientes
        public Cliente Cliente { get; set; }

        // Relación con DetallesPedido (Lista de productos en el pedido)
        public ICollection<DetallesPedido> DetallesPedido { get; set; } = new List<DetallesPedido>();
    }
}
