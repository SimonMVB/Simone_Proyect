using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Simone.Models
{
    /// <summary>
    /// Registro de cambios de estado de un SubPedido.
    /// Timeline completo de la preparación.
    /// </summary>
    public class SubPedidoHistorial
    {
        [Key]
        public int HistorialId { get; set; }

        public int SubPedidoId { get; set; }

        [Required, StringLength(50)]
        public string EstadoAnterior { get; set; } = string.Empty;

        [Required, StringLength(50)]
        public string EstadoNuevo { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Comentario { get; set; }

        public string? UsuarioId { get; set; }

        [StringLength(50)]
        public string? TipoUsuario { get; set; }  // "Vendedor", "Hub", "Sistema", "Admin"

        public DateTime FechaCambio { get; set; } = DateTime.UtcNow;

        // -------- Navegación --------
        [ForeignKey(nameof(SubPedidoId))]
        public virtual SubPedido SubPedido { get; set; } = null!;

        [ForeignKey(nameof(UsuarioId))]
        public virtual Usuario? Usuario { get; set; }

        // -------- Propiedades calculadas --------
        [NotMapped]
        public string CambioResumen => $"{EstadoAnterior} → {EstadoNuevo}";

        [NotMapped]
        public string IconoCambio => EstadoNuevo switch
        {
            "Preparando" => "fa-box-open",
            "Listo" => "fa-check",
            "EnCaminoHub" => "fa-truck",
            "EnHub" => "fa-warehouse",
            "Entregado" => "fa-check-double",
            "Cancelado" => "fa-times-circle",
            _ => "fa-circle"
        };

        [NotMapped]
        public string ColorCambio => EstadoNuevo switch
        {
            "Preparando" => "info",
            "Listo" => "primary",
            "EnCaminoHub" => "warning",
            "EnHub" => "success",
            "Entregado" => "success",
            "Cancelado" => "danger",
            _ => "secondary"
        };
    }
}