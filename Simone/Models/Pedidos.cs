using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace Simone.Models
{
    /// <summary>
    /// Modelo de Pedido mejorado
    /// Soporta tracking de vendedor, estados detallados y comisiones
    /// </summary>
    public class Pedido
    {
        [Key]
        public int PedidoID { get; set; }

        // ==================== NÚMERO DE ORDEN ====================

        /// <summary>
        /// Número de orden legible (ej: "ORD-2026-001234")
        /// </summary>
        [StringLength(50)]
        public string? NumeroOrden { get; set; }

        // ==================== CLIENTE ====================

        /// <summary>
        /// ID del cliente que realiza el pedido
        /// </summary>
        [Required]
        [StringLength(450)]
        public string UsuarioId { get; set; } = null!;

        /// <summary>
        /// Nombre del cliente (desnormalizado para reportes)
        /// </summary>
        [StringLength(200)]
        public string? NombreCliente { get; set; }

        /// <summary>
        /// Email del cliente
        /// </summary>
        [StringLength(200)]
        public string? EmailCliente { get; set; }

        /// <summary>
        /// Teléfono del cliente
        /// </summary>
        [StringLength(50)]
        public string? TelefonoCliente { get; set; }

        // ==================== VENDEDOR ====================

        /// <summary>
        /// ID del vendedor principal (si es mono-vendedor)
        /// Para multi-vendedor, se calcula desde los detalles
        /// </summary>
        [StringLength(450)]
        public string? VendedorId { get; set; }

        // ==================== FECHAS ====================

        /// <summary>
        /// Fecha de creación del pedido
        /// </summary>
        public DateTime FechaPedido { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Fecha de pago confirmado
        /// </summary>
        public DateTime? FechaPago { get; set; }

        /// <summary>
        /// Fecha de envío
        /// </summary>
        public DateTime? FechaEnvio { get; set; }

        /// <summary>
        /// Fecha de entrega
        /// </summary>
        public DateTime? FechaEntrega { get; set; }

        /// <summary>
        /// Fecha de cancelación (si aplica)
        /// </summary>
        public DateTime? FechaCancelacion { get; set; }

        // ==================== ESTADOS ====================

        /// <summary>
        /// Estado general del pedido
        /// Pendiente, Confirmado, EnProceso, Enviado, Entregado, Cancelado
        /// </summary>
        [Required]
        [StringLength(50)]
        public string EstadoPedido { get; set; } = EstadosPedido.Pendiente;

        /// <summary>
        /// Estado del pago
        /// Pendiente, Verificando, Pagado, Fallido, Reembolsado
        /// </summary>
        [StringLength(50)]
        public string EstadoPago { get; set; } = EstadosPago.Pendiente;

        // ==================== ENVÍO ====================

        /// <summary>
        /// Método de envío seleccionado
        /// </summary>
        [StringLength(100)]
        public string? MetodoEnvio { get; set; }

        /// <summary>
        /// Dirección de envío completa
        /// </summary>
        [StringLength(500)]
        public string? DireccionEnvio { get; set; }

        /// <summary>
        /// Ciudad de envío
        /// </summary>
        [StringLength(100)]
        public string? CiudadEnvio { get; set; }

        /// <summary>
        /// Provincia/Estado de envío
        /// </summary>
        [StringLength(100)]
        public string? ProvinciaEnvio { get; set; }

        /// <summary>
        /// Código postal
        /// </summary>
        [StringLength(20)]
        public string? CodigoPostalEnvio { get; set; }

        /// <summary>
        /// Referencia de la dirección
        /// </summary>
        [StringLength(300)]
        public string? ReferenciaEnvio { get; set; }

        /// <summary>
        /// Número de guía/tracking del envío
        /// </summary>
        [StringLength(100)]
        public string? NumeroGuia { get; set; }

        /// <summary>
        /// Empresa de transporte
        /// </summary>
        [StringLength(100)]
        public string? Transportadora { get; set; }

        // ==================== MONTOS ====================

        /// <summary>
        /// Subtotal (suma de productos)
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal Subtotal { get; set; }

        /// <summary>
        /// Costo de envío
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal CostoEnvio { get; set; } = 0;

        /// <summary>
        /// Descuento aplicado
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal Descuento { get; set; } = 0;

        /// <summary>
        /// Impuestos (IVA, etc.)
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal Impuestos { get; set; } = 0;

        /// <summary>
        /// Total del pedido
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal Total { get; set; }

        // ==================== PAGO ====================

        /// <summary>
        /// Método de pago utilizado
        /// </summary>
        [StringLength(100)]
        public string? MetodoPago { get; set; }

        /// <summary>
        /// Referencia de la transacción de pago
        /// </summary>
        [StringLength(200)]
        public string? ReferenciaPago { get; set; }

        /// <summary>
        /// Comprobante de pago (ruta de imagen)
        /// </summary>
        [StringLength(500)]
        public string? ComprobantePago { get; set; }

        // ==================== COMISIONES ====================

        /// <summary>
        /// ¿Ya se calculó la comisión de este pedido?
        /// </summary>
        public bool ComisionCalculada { get; set; } = false;

        /// <summary>
        /// ID del pago de comisión donde se incluyó este pedido
        /// </summary>
        public int? PagoComisionId { get; set; }

        // ==================== NOTAS ====================

        /// <summary>
        /// Notas del cliente
        /// </summary>
        [StringLength(1000)]
        public string? NotasCliente { get; set; }

        /// <summary>
        /// Notas internas (solo admin/vendedor)
        /// </summary>
        [StringLength(1000)]
        public string? NotasInternas { get; set; }

        /// <summary>
        /// Motivo de cancelación (si aplica)
        /// </summary>
        [StringLength(500)]
        public string? MotivoCancelacion { get; set; }

        // ==================== METADATA ====================

        /// <summary>
        /// IP desde donde se realizó el pedido
        /// </summary>
        [StringLength(50)]
        public string? IpOrigen { get; set; }

        /// <summary>
        /// User Agent del navegador
        /// </summary>
        [StringLength(500)]
        public string? UserAgent { get; set; }

        /// <summary>
        /// Última modificación
        /// </summary>
        public DateTime? ModificadoUtc { get; set; }

        // ==================== NAVEGACIÓN ====================

        [ForeignKey(nameof(UsuarioId))]
        public virtual Usuario? Usuario { get; set; }

        [ForeignKey(nameof(VendedorId))]
        public virtual Usuario? Vendedor { get; set; }

        /// <summary>
        /// Detalles/líneas del pedido
        /// </summary>
        public virtual ICollection<DetallesPedido> DetallesPedido { get; set; }
            = new List<DetallesPedido>();

        /// <summary>
        /// Historial de cambios de estado
        /// </summary>
        public virtual ICollection<PedidoHistorial> Historial { get; set; }
            = new List<PedidoHistorial>();

        // ==================== PROPIEDADES CALCULADAS ====================

        /// <summary>
        /// Cantidad de productos en el pedido
        /// </summary>
        [NotMapped]
        public int CantidadProductos => DetallesPedido?.Sum(d => d.Cantidad) ?? 0;

        /// <summary>
        /// Cantidad de líneas/items diferentes
        /// </summary>
        [NotMapped]
        public int CantidadItems => DetallesPedido?.Count ?? 0;

        /// <summary>
        /// ¿El pedido está pagado?
        /// </summary>
        [NotMapped]
        public bool EstaPagado => EstadoPago == EstadosPago.Pagado;

        /// <summary>
        /// ¿El pedido está cancelado?
        /// </summary>
        [NotMapped]
        public bool EstaCancelado => EstadoPedido == EstadosPedido.Cancelado;

        /// <summary>
        /// ¿El pedido puede ser cancelado?
        /// </summary>
        [NotMapped]
        public bool PuedeCancelarse =>
            EstadoPedido == EstadosPedido.Pendiente ||
            EstadoPedido == EstadosPedido.Confirmado;

        /// <summary>
        /// ¿El pedido puede ser enviado?
        /// </summary>
        [NotMapped]
        public bool PuedeEnviarse =>
            EstaPagado &&
            (EstadoPedido == EstadosPedido.Confirmado || EstadoPedido == EstadosPedido.EnProceso);

        /// <summary>
        /// Clase CSS según estado del pedido
        /// </summary>
        [NotMapped]
        public string EstadoPedidoClase => EstadoPedido switch
        {
            "Pendiente" => "warning",
            "Confirmado" => "info",
            "EnProceso" => "primary",
            "Enviado" => "info",
            "Entregado" => "success",
            "Cancelado" => "danger",
            _ => "secondary"
        };

        /// <summary>
        /// Clase CSS según estado de pago
        /// </summary>
        [NotMapped]
        public string EstadoPagoClase => EstadoPago switch
        {
            "Pendiente" => "warning",
            "Verificando" => "info",
            "Pagado" => "success",
            "Fallido" => "danger",
            "Reembolsado" => "secondary",
            _ => "secondary"
        };

        /// <summary>
        /// Icono según estado del pedido
        /// </summary>
        [NotMapped]
        public string EstadoPedidoIcono => EstadoPedido switch
        {
            "Pendiente" => "fas fa-clock",
            "Confirmado" => "fas fa-check",
            "EnProceso" => "fas fa-cog fa-spin",
            "Enviado" => "fas fa-truck",
            "Entregado" => "fas fa-check-double",
            "Cancelado" => "fas fa-times",
            _ => "fas fa-question"
        };

        /// <summary>
        /// Días desde que se realizó el pedido
        /// </summary>
        [NotMapped]
        public int DiasDesdeCreacion => (int)(DateTime.UtcNow - FechaPedido).TotalDays;

        // ==================== MÉTODOS ====================

        /// <summary>
        /// Generar número de orden único
        /// </summary>
        public void GenerarNumeroOrden()
        {
            if (string.IsNullOrEmpty(NumeroOrden))
            {
                NumeroOrden = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{PedidoID:D6}";
            }
        }

        /// <summary>
        /// Calcular totales desde los detalles
        /// </summary>
        public void CalcularTotales()
        {
            Subtotal = DetallesPedido?.Sum(d => d.Subtotal) ?? 0;
            Total = Subtotal + CostoEnvio + Impuestos - Descuento;
        }

        /// <summary>
        /// Confirmar el pedido
        /// </summary>
        public void Confirmar()
        {
            EstadoPedido = EstadosPedido.Confirmado;
            ModificadoUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Marcar como pagado
        /// </summary>
        public void MarcarPagado(string? referencia = null)
        {
            EstadoPago = EstadosPago.Pagado;
            FechaPago = DateTime.UtcNow;
            ReferenciaPago = referencia;
            ModificadoUtc = DateTime.UtcNow;

            if (EstadoPedido == EstadosPedido.Pendiente)
                EstadoPedido = EstadosPedido.Confirmado;
        }

        /// <summary>
        /// Marcar como enviado
        /// </summary>
        public void MarcarEnviado(string? numeroGuia = null, string? transportadora = null)
        {
            EstadoPedido = EstadosPedido.Enviado;
            FechaEnvio = DateTime.UtcNow;
            NumeroGuia = numeroGuia;
            Transportadora = transportadora;
            ModificadoUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Marcar como entregado
        /// </summary>
        public void MarcarEntregado()
        {
            EstadoPedido = EstadosPedido.Entregado;
            FechaEntrega = DateTime.UtcNow;
            ModificadoUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Cancelar el pedido
        /// </summary>
        public void Cancelar(string motivo)
        {
            EstadoPedido = EstadosPedido.Cancelado;
            FechaCancelacion = DateTime.UtcNow;
            MotivoCancelacion = motivo;
            ModificadoUtc = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Historial de cambios de estado del pedido
    /// </summary>
    public class PedidoHistorial
    {
        [Key]
        public int HistorialId { get; set; }

        public int PedidoID { get; set; }

        [StringLength(50)]
        public string EstadoAnterior { get; set; } = "";

        [StringLength(50)]
        public string EstadoNuevo { get; set; } = "";

        [StringLength(500)]
        public string? Comentario { get; set; }

        [StringLength(450)]
        public string? UsuarioId { get; set; }

        public DateTime FechaCambio { get; set; } = DateTime.UtcNow;

        // Navegación
        [ForeignKey(nameof(PedidoID))]
        public virtual Pedido? Pedido { get; set; }

        [ForeignKey(nameof(UsuarioId))]
        public virtual Usuario? Usuario { get; set; }
    }

    /// <summary>
    /// Estados posibles del pedido
    /// </summary>
    public static class EstadosPedido
    {
        public const string Pendiente = "Pendiente";
        public const string Confirmado = "Confirmado";
        public const string EnProceso = "EnProceso";
        public const string Enviado = "Enviado";
        public const string Entregado = "Entregado";
        public const string Cancelado = "Cancelado";

        public static readonly string[] Todos =
            { Pendiente, Confirmado, EnProceso, Enviado, Entregado, Cancelado };

        public static readonly Dictionary<string, string> Nombres = new()
        {
            { Pendiente, "Pendiente" },
            { Confirmado, "Confirmado" },
            { EnProceso, "En Proceso" },
            { Enviado, "Enviado" },
            { Entregado, "Entregado" },
            { Cancelado, "Cancelado" }
        };
    }

    /// <summary>
    /// Estados posibles del pago
    /// </summary>
    public static class EstadosPago
    {
        public const string Pendiente = "Pendiente";
        public const string Verificando = "Verificando";
        public const string Pagado = "Pagado";
        public const string Fallido = "Fallido";
        public const string Reembolsado = "Reembolsado";

        public static readonly string[] Todos =
            { Pendiente, Verificando, Pagado, Fallido, Reembolsado };
    }
}