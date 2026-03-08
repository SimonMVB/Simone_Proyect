using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace Simone.Models
{
    /// <summary>
    /// Envío que agrupa múltiples SubPedidos de diferentes tiendas.
    /// Se consolida en un Hub para enviar al cliente.
    /// Conecta con Pedido para trazabilidad completa.
    /// </summary>
    public class EnvioConsolidado
    {
        [Key]
        public int EnvioId { get; set; }


        /// <summary>
        /// ID de la Venta asociada (alternativo a PedidoId)
        /// </summary>
        public int? VentaId { get; set; }

        [ForeignKey(nameof(VentaId))]
        public virtual Ventas? Venta { get; set; }

        [Required, StringLength(20)]
        public string Codigo { get; set; } = string.Empty;  // "ENV-20250219-001"

        // -------- Conexión con Pedido original --------
        public int? PedidoId { get; set; }

        [Required]
        public string ClienteId { get; set; } = string.Empty;

        public int? HubId { get; set; }  // Hub donde se consolida

        // -------- Dirección de entrega (copiada del Pedido) --------
        [Required, StringLength(100)]
        public string Provincia { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string Ciudad { get; set; } = string.Empty;

        [Required, StringLength(500)]
        public string DireccionEntrega { get; set; } = string.Empty;

        [StringLength(20)]
        public string? TelefonoContacto { get; set; }

        [StringLength(300)]
        public string? ReferenciaEntrega { get; set; }

        // -------- Estado general --------
        [Required, StringLength(50)]
        public string Estado { get; set; } = EstadosEnvioConsolidado.Pendiente;

        // -------- Costos --------
        [Column(TypeName = "decimal(18,2)")]
        public decimal CostoEnvioTotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DescuentoEnvio { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal CostoEnvioFinal { get; set; }

        /// <summary>
        /// Notas internas del envío
        /// </summary>
        [StringLength(500)]
        public string? Notas { get; set; }


        // -------- Peso total --------
        [Column(TypeName = "decimal(18,3)")]
        public decimal PesoTotalKg { get; set; }

        // -------- Fechas --------
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
        public DateTime? FechaConsolidacion { get; set; }  // Cuando todos llegaron al Hub
        public DateTime? FechaEnvio { get; set; }          // Cuando salió del Hub
        public DateTime? FechaEntrega { get; set; }        // Cuando se entregó

        // -------- Tracking --------
        [StringLength(100)]
        public string? NumeroGuia { get; set; }

        [StringLength(100)]
        public string? Transportista { get; set; }

        // -------- Notas --------
        [StringLength(500)]
        public string? NotasCliente { get; set; }

        [StringLength(500)]
        public string? NotasInternas { get; set; }

        // -------- Navegación --------
        [ForeignKey(nameof(PedidoId))]
        public virtual Pedido? Pedido { get; set; }

        [ForeignKey(nameof(ClienteId))]
        public virtual Usuario? Cliente { get; set; }

        [ForeignKey(nameof(HubId))]
        public virtual HubEnvio? Hub { get; set; }

        public virtual ICollection<SubPedido> SubPedidos { get; set; } = new List<SubPedido>();

        // -------- Propiedades calculadas --------
        [NotMapped]
        public int TotalSubPedidos => SubPedidos?.Count ?? 0;

        [NotMapped]
        public int TotalTiendas => SubPedidos?.Select(s => s.VendedorId).Distinct().Count() ?? 0;

        [NotMapped]
        public int TotalProductos => SubPedidos?.Sum(s => s.TotalItems) ?? 0;

        [NotMapped]
        public bool TodosEnHub => SubPedidos?.All(s => s.Estado == EstadosSubPedido.EnHub) ?? false;

        [NotMapped]
        public bool PuedeEnviarse => TodosEnHub && Estado == EstadosEnvioConsolidado.EnHub;

        [NotMapped]
        public string EstadoDisplay => Estado switch
        {
            "Pendiente" => "Pendiente de preparación",
            "EnProceso" => "Tiendas preparando",
            "EnHub" => "Listo para enviar",
            "EnCamino" => "En camino",
            "Entregado" => "Entregado",
            "Cancelado" => "Cancelado",
            _ => Estado
        };

        [NotMapped]
        public string EstadoColor => Estado switch
        {
            "Pendiente" => "secondary",
            "EnProceso" => "info",
            "EnHub" => "primary",
            "EnCamino" => "warning",
            "Entregado" => "success",
            "Cancelado" => "danger",
            _ => "secondary"
        };

        [NotMapped]
        public string EstadoIcono => Estado switch
        {
            "Pendiente" => "fa-clock",
            "EnProceso" => "fa-box-open",
            "EnHub" => "fa-warehouse",
            "EnCamino" => "fa-truck",
            "Entregado" => "fa-check-double",
            "Cancelado" => "fa-times-circle",
            _ => "fa-question"
        };

        // -------- Métodos --------
        public void GenerarCodigo()
        {
            if (string.IsNullOrEmpty(Codigo))
            {
                // No usar EnvioId aquí: puede ser 0 si se llama antes de SaveChanges.
                // Usar timestamp + random, igual que GenerarCodigoEnvio() en el servicio.
                var random = new Random().Next(1000, 9999);
                Codigo = $"ENV-{DateTime.UtcNow:yyyyMMdd}-{random}";
            }
        }

        public void CalcularCostoFinal()
        {
            CostoEnvioFinal = CostoEnvioTotal - DescuentoEnvio;
            if (CostoEnvioFinal < 0) CostoEnvioFinal = 0;
        }

        public void ActualizarEstado()
        {
            if (SubPedidos == null || !SubPedidos.Any()) return;

            if (SubPedidos.All(s => s.Estado == EstadosSubPedido.Cancelado))
            {
                Estado = EstadosEnvioConsolidado.Cancelado;
            }
            else if (SubPedidos.All(s => s.Estado == EstadosSubPedido.EnHub))
            {
                Estado = EstadosEnvioConsolidado.EnHub;
            }
            else if (SubPedidos.Any(s => s.Estado != EstadosSubPedido.Pendiente))
            {
                Estado = EstadosEnvioConsolidado.EnProceso;
            }
        }
    }

    /// <summary>
    /// Estados del envío consolidado
    /// </summary>
    public static class EstadosEnvioConsolidado
    {
        public const string Pendiente = "Pendiente";
        public const string EnProceso = "EnProceso";
        public const string EnHub = "EnHub";
        public const string EnCamino = "EnCamino";
        public const string Entregado = "Entregado";
        public const string Cancelado = "Cancelado";

        public static readonly string[] Todos =
            { Pendiente, EnProceso, EnHub, EnCamino, Entregado, Cancelado };
    }
}