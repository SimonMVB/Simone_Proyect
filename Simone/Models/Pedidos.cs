using System;
using System.Collections.Generic;

namespace Simone.Models
{
    public class Pedidos
    {
        public int PedidoID { get; set; }  // Clave primaria
        public int ClienteID { get; set; }  // Clave foránea con Clientes
        public DateTime? FechaPedido { get; set; }  // Puede ser nulo
        public string EstadoPedido { get; set; }  // Estado del pedido
        public string? MetodoEnvio { get; set; }  // Puede ser nulo
        public string? DireccionEnvio { get; set; }  // Puede ser nulo
        public decimal? Total { get; set; }  // Puede ser nulo

        // Relación con Clientes
        public Cliente Cliente { get; set; }

        // Relación con DetallesPedido (Lista de productos en el pedido)
        public ICollection<DetallesPedido> DetallesPedido { get; set; }
    }
}
