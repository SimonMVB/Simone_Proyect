using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Simone.Models
{
    public class Pedido
    {
        [Key]
        public int PedidoID { get; set; }

        // 🔁 FK a AspNetUsers
        [Required]
        public string UsuarioId { get; set; } = default!;

        [ForeignKey(nameof(UsuarioId))]
        public Usuario Usuario { get; set; } = null!;

        // Fechas/estado
        public DateTime FechaPedido { get; set; } = DateTime.UtcNow;

        [Required, StringLength(50)]
        public string EstadoPedido { get; set; } = "Pendiente";

        // Envío
        [StringLength(50)]
        public string? MetodoEnvio { get; set; }

        [StringLength(200)]
        public string? DireccionEnvio { get; set; }

        // Total
        [Column(TypeName = "decimal(18,2)")]
        public decimal Total { get; set; }

        // Detalles
        public ICollection<DetallesPedido> DetallesPedido { get; set; } = new HashSet<DetallesPedido>();
    }
}
