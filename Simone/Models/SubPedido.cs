using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace Simone.Models
{
    /// <summary>
    /// Pedido individual de una tienda dentro de un EnvioConsolidado.
    /// Cada tienda prepara su SubPedido independientemente.
    /// </summary>
    public class SubPedido
    {
        [Key]
        public int SubPedidoId { get; set; }

        [Required, StringLength(20)]
        public string Codigo { get; set; } = string.Empty;  // "SP-20250219-001"

        // -------- Relaciones principales --------
        public int EnvioId { get; set; }  // EnvioConsolidado al que pertenece

        public int VendedorId { get; set; }  // Tienda que prepara

        public int? AlianzaId { get; set; }  // Alianza (si aplica)

        // -------- Estado --------
        [Required, StringLength(50)]
        public string Estado { get; set; } = EstadosSubPedido.Pendiente;

        // -------- Costos --------
        [Column(TypeName = "decimal(18,2)")]
        public decimal Subtotal { get; set; }  // Suma de items

        [Column(TypeName = "decimal(18,2)")]
        public decimal CostoEnvioProporcional { get; set; }  // Parte del envío

        [Column(TypeName = "decimal(18,2)")]
        public decimal Total { get; set; }

        // -------- Peso --------
        [Column(TypeName = "decimal(18,3)")]
        public decimal PesoEstimadoKg { get; set; }

        // -------- Fechas --------
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
        public DateTime? FechaPreparacionInicio { get; set; }
        public DateTime? FechaListo { get; set; }
        public DateTime? FechaEnvioHub { get; set; }
        public DateTime? FechaRecepcionHub { get; set; }

        // -------- Notas --------
        [StringLength(500)]
        public string? NotasVendedor { get; set; }

        [StringLength(500)]
        public string? NotasHub { get; set; }

        // -------- Navegación --------
        [ForeignKey(nameof(EnvioId))]
        public virtual EnvioConsolidado Envio { get; set; } = null!;

        [ForeignKey(nameof(VendedorId))]
        public virtual Vendedor Vendedor { get; set; } = null!;

        [ForeignKey(nameof(AlianzaId))]
        public virtual AlianzaEnvio? Alianza { get; set; }

        public virtual ICollection<SubPedidoItem> Items { get; set; } = new List<SubPedidoItem>();
        public virtual ICollection<SubPedidoHistorial> Historial { get; set; } = new List<SubPedidoHistorial>();

        // -------- Propiedades calculadas --------
        [NotMapped]
        public int TotalItems => Items?.Sum(i => i.Cantidad) ?? 0;

        [NotMapped]
        public int TotalLineas => Items?.Count ?? 0;

        [NotMapped]
        public string NombreTienda => Vendedor?.Nombre ?? "Sin tienda";

        [NotMapped]
        public string EstadoDisplay => Estado switch
        {
            "Pendiente" => "Pendiente",
            "Preparando" => "Preparando",
            "Listo" => "Listo para enviar",
            "EnCaminoHub" => "En camino al Hub",
            "EnHub" => "Recibido en Hub",
            "Entregado" => "Entregado",
            "Cancelado" => "Cancelado",
            _ => Estado
        };

        [NotMapped]
        public string EstadoColor => Estado switch
        {
            "Pendiente" => "secondary",
            "Preparando" => "info",
            "Listo" => "primary",
            "EnCaminoHub" => "warning",
            "EnHub" => "success",
            "Entregado" => "success",
            "Cancelado" => "danger",
            _ => "secondary"
        };

        [NotMapped]
        public string EstadoIcono => Estado switch
        {
            "Pendiente" => "fa-clock",
            "Preparando" => "fa-box-open",
            "Listo" => "fa-check",
            "EnCaminoHub" => "fa-truck",
            "EnHub" => "fa-warehouse",
            "Entregado" => "fa-check-double",
            "Cancelado" => "fa-times",
            _ => "fa-question"
        };

        // -------- Métodos --------
        public void GenerarCodigo()
        {
            if (string.IsNullOrEmpty(Codigo))
            {
                Codigo = $"SP-{DateTime.UtcNow:yyyyMMdd}-{SubPedidoId:D4}";
            }
        }

        public void CalcularTotales()
        {
            Subtotal = Items?.Sum(i => i.Subtotal) ?? 0;
            PesoEstimadoKg = Items?.Sum(i => i.PesoTotalKg) ?? 0;
            Total = Subtotal + CostoEnvioProporcional;
        }

        public void CambiarEstado(string nuevoEstado, string? usuarioId, string? tipoUsuario, string? comentario = null)
        {
            var historial = new SubPedidoHistorial
            {
                SubPedidoId = SubPedidoId,
                EstadoAnterior = Estado,
                EstadoNuevo = nuevoEstado,
                UsuarioId = usuarioId,
                TipoUsuario = tipoUsuario,
                Comentario = comentario,
                FechaCambio = DateTime.UtcNow
            };

            Historial.Add(historial);

            // Actualizar fechas según el estado
            switch (nuevoEstado)
            {
                case EstadosSubPedido.Preparando:
                    FechaPreparacionInicio = DateTime.UtcNow;
                    break;
                case EstadosSubPedido.Listo:
                    FechaListo = DateTime.UtcNow;
                    break;
                case EstadosSubPedido.EnCaminoHub:
                    FechaEnvioHub = DateTime.UtcNow;
                    break;
                case EstadosSubPedido.EnHub:
                    FechaRecepcionHub = DateTime.UtcNow;
                    break;
            }

            Estado = nuevoEstado;
        }
    }

    /// <summary>
    /// Estados del SubPedido
    /// </summary>
    public static class EstadosSubPedido
    {
        public const string Pendiente = "Pendiente";
        public const string Preparando = "Preparando";
        public const string Listo = "Listo";
        public const string EnCaminoHub = "EnCaminoHub";
        public const string EnHub = "EnHub";
        public const string Entregado = "Entregado";
        public const string Cancelado = "Cancelado";

        public static readonly string[] Todos =
            { Pendiente, Preparando, Listo, EnCaminoHub, EnHub, Entregado, Cancelado };
    }
}