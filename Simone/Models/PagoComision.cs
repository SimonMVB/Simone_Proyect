using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Simone.Models
{
    /// <summary>
    /// Registro de pagos de comisiones a vendedores
    /// Pagos quincenales: 1-15 y 16-fin de mes
    /// </summary>
    public class PagoComision
    {
        [Key]
        public int PagoId { get; set; }

        // ==================== VENDEDOR ====================

        /// <summary>
        /// ID del vendedor al que se le paga
        /// </summary>
        [Required]
        [StringLength(450)]
        public string VendedorId { get; set; } = null!;

        // ==================== PERÍODO ====================

        /// <summary>
        /// Inicio del período de liquidación
        /// </summary>
        [Required]
        public DateTime PeriodoInicio { get; set; }

        /// <summary>
        /// Fin del período de liquidación
        /// </summary>
        [Required]
        public DateTime PeriodoFin { get; set; }

        /// <summary>
        /// Número de quincena (1 o 2)
        /// 1 = días 1-15, 2 = días 16-fin de mes
        /// </summary>
        public int NumeroQuincena { get; set; }

        /// <summary>
        /// Año del período
        /// </summary>
        public int Anio { get; set; }

        /// <summary>
        /// Mes del período
        /// </summary>
        public int Mes { get; set; }

        // ==================== MONTOS ====================

        /// <summary>
        /// Total de ventas en el período
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal MontoVentas { get; set; }

        /// <summary>
        /// Cantidad de pedidos en el período
        /// </summary>
        public int CantidadPedidos { get; set; }

        /// <summary>
        /// Cantidad de productos vendidos
        /// </summary>
        public int CantidadProductos { get; set; }

        /// <summary>
        /// Porcentaje de comisión aplicado
        /// </summary>
        [Column(TypeName = "decimal(5,2)")]
        public decimal PorcentajeAplicado { get; set; }

        /// <summary>
        /// Monto de comisión calculado
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal MontoComision { get; set; }

        /// <summary>
        /// Deducciones (devoluciones, cancelaciones, etc.)
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal Deducciones { get; set; } = 0;

        /// <summary>
        /// Bonificaciones adicionales
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal Bonificaciones { get; set; } = 0;

        /// <summary>
        /// Monto final a pagar (Comisión - Deducciones + Bonificaciones)
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal MontoFinal { get; set; }

        // ==================== ESTADO ====================

        /// <summary>
        /// Estado del pago: Pendiente, Aprobado, Pagado, Cancelado
        /// </summary>
        [Required]
        [StringLength(50)]
        public string Estado { get; set; } = "Pendiente";

        // ==================== PAGO ====================

        /// <summary>
        /// Fecha en que se realizó el pago
        /// </summary>
        public DateTime? FechaPago { get; set; }

        /// <summary>
        /// Método de pago utilizado
        /// </summary>
        [StringLength(100)]
        public string? MetodoPago { get; set; }

        /// <summary>
        /// Número de comprobante/referencia del pago
        /// </summary>
        [StringLength(200)]
        public string? NumeroComprobante { get; set; }

        /// <summary>
        /// Banco o entidad de pago
        /// </summary>
        [StringLength(200)]
        public string? BancoEntidad { get; set; }

        /// <summary>
        /// Ruta del comprobante adjunto (imagen/PDF)
        /// </summary>
        [StringLength(500)]
        public string? ComprobanteAdjunto { get; set; }

        // ==================== NOTAS ====================

        /// <summary>
        /// Notas u observaciones
        /// </summary>
        [StringLength(1000)]
        public string? Notas { get; set; }

        // ==================== METADATA ====================

        /// <summary>
        /// Fecha de creación del registro
        /// </summary>
        public DateTime CreadoUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Fecha de última modificación
        /// </summary>
        public DateTime? ModificadoUtc { get; set; }

        /// <summary>
        /// Usuario que creó el registro
        /// </summary>
        [StringLength(450)]
        public string? CreadoPor { get; set; }

        /// <summary>
        /// Usuario que aprobó el pago
        /// </summary>
        [StringLength(450)]
        public string? AprobadoPor { get; set; }

        /// <summary>
        /// Usuario que marcó como pagado
        /// </summary>
        [StringLength(450)]
        public string? PagadoPor { get; set; }

        // ==================== NAVEGACIÓN ====================

        [ForeignKey(nameof(VendedorId))]
        public virtual Usuario? Vendedor { get; set; }

        /// <summary>
        /// Detalle de pedidos incluidos en esta liquidación
        /// </summary>
        public virtual ICollection<PagoComisionDetalle> Detalles { get; set; }
            = new List<PagoComisionDetalle>();

        // ==================== PROPIEDADES CALCULADAS ====================

        /// <summary>
        /// Nombre del período (ej: "Feb 2026 - Q1")
        /// </summary>
        [NotMapped]
        public string PeriodoNombre =>
            $"{ObtenerNombreMes(Mes)} {Anio} - Q{NumeroQuincena}";

        /// <summary>
        /// Rango de fechas formateado
        /// </summary>
        [NotMapped]
        public string RangoFechas =>
            $"{PeriodoInicio:dd/MM/yyyy} - {PeriodoFin:dd/MM/yyyy}";

        /// <summary>
        /// ¿Está pendiente de pago?
        /// </summary>
        [NotMapped]
        public bool EstaPendiente => Estado == EstadosPagoComision.Pendiente;

        /// <summary>
        /// ¿Ya fue pagado?
        /// </summary>
        [NotMapped]
        public bool EstaPagado => Estado == EstadosPagoComision.Pagado;

        /// <summary>
        /// Clase CSS según estado
        /// </summary>
        [NotMapped]
        public string EstadoClase => Estado switch
        {
            "Pendiente" => "warning",
            "Aprobado" => "info",
            "Pagado" => "success",
            "Cancelado" => "danger",
            _ => "secondary"
        };

        /// <summary>
        /// Icono según estado
        /// </summary>
        [NotMapped]
        public string EstadoIcono => Estado switch
        {
            "Pendiente" => "fas fa-clock",
            "Aprobado" => "fas fa-check",
            "Pagado" => "fas fa-check-double",
            "Cancelado" => "fas fa-times",
            _ => "fas fa-question"
        };

        // ==================== MÉTODOS ====================

        /// <summary>
        /// Calcular monto final
        /// </summary>
        public void CalcularMontoFinal()
        {
            MontoFinal = MontoComision - Deducciones + Bonificaciones;
        }

        /// <summary>
        /// Marcar como aprobado
        /// </summary>
        public void Aprobar(string aprobadoPor)
        {
            Estado = EstadosPagoComision.Aprobado;
            AprobadoPor = aprobadoPor;
            ModificadoUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Marcar como pagado
        /// </summary>
        public void MarcarPagado(string pagadoPor, string metodoPago, string? comprobante = null)
        {
            Estado = EstadosPagoComision.Pagado;
            FechaPago = DateTime.UtcNow;
            PagadoPor = pagadoPor;
            MetodoPago = metodoPago;
            NumeroComprobante = comprobante;
            ModificadoUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Cancelar pago
        /// </summary>
        public void Cancelar(string motivo)
        {
            Estado = EstadosPagoComision.Cancelado;
            Notas = string.IsNullOrEmpty(Notas) ? motivo : $"{Notas}\n[Cancelado]: {motivo}";
            ModificadoUtc = DateTime.UtcNow;
        }

        private static string ObtenerNombreMes(int mes) => mes switch
        {
            1 => "Ene",
            2 => "Feb",
            3 => "Mar",
            4 => "Abr",
            5 => "May",
            6 => "Jun",
            7 => "Jul",
            8 => "Ago",
            9 => "Sep",
            10 => "Oct",
            11 => "Nov",
            12 => "Dic",
            _ => "???"
        };
    }

    /// <summary>
    /// Detalle de pedidos incluidos en un pago de comisión
    /// </summary>
    public class PagoComisionDetalle
    {
        [Key]
        public int DetalleId { get; set; }

        /// <summary>
        /// ID del pago padre
        /// </summary>
        public int PagoId { get; set; }

        /// <summary>
        /// ID del pedido incluido
        /// </summary>
        public int PedidoId { get; set; }

        /// <summary>
        /// Monto del pedido
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal MontoPedido { get; set; }

        /// <summary>
        /// Comisión generada por este pedido
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal ComisionPedido { get; set; }

        // Navegación
        [ForeignKey(nameof(PagoId))]
        public virtual PagoComision? Pago { get; set; }

        [ForeignKey(nameof(PedidoId))]
        public virtual Pedido? Pedido { get; set; }
    }

    /// <summary>
    /// Estados posibles de un pago de comisión
    /// </summary>
    public static class EstadosPagoComision
    {
        public const string Pendiente = "Pendiente";
        public const string Aprobado = "Aprobado";
        public const string Pagado = "Pagado";
        public const string Cancelado = "Cancelado";

        public static readonly string[] Todos = { Pendiente, Aprobado, Pagado, Cancelado };
    }
}