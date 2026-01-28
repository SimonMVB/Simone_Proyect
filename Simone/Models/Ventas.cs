using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Simone.Models
{
    /// <summary>
    /// Modelo de Ventas con campos de pago por transferencia
    /// ACTUALIZADO: Agregados Depositante, Banco, ComprobanteUrl para persistir datos de pago
    /// </summary>
    public class Ventas
    {
        [Key]
        public int VentaID { get; set; }

        // --- Vendedor/Empleado (opcional) ---
        public string? EmpleadoID { get; set; }

        [ForeignKey(nameof(EmpleadoID))]
        public virtual Usuario? Empleado { get; set; }

        // --- Comprador: ahora centralizado en Usuario (reemplaza ClienteID/Cliente) ---
        [Required]
        public string UsuarioId { get; set; } = default!;

        [ForeignKey(nameof(UsuarioId))]
        public virtual Usuario Usuario { get; set; } = default!;

        // --- Datos de la venta ---
        [Required, StringLength(30)]
        public string Estado { get; set; } = "Pendiente";

        public DateTime FechaVenta { get; set; } = DateTime.UtcNow;

        [Required, StringLength(50)]
        public string MetodoPago { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Total { get; set; }

        // =====================================================================
        // NUEVOS CAMPOS - Datos de pago por transferencia/depósito
        // =====================================================================

        /// <summary>
        /// Nombre del depositante (quien realizó la transferencia)
        /// </summary>
        [StringLength(200)]
        public string? Depositante { get; set; }

        /// <summary>
        /// Banco/entidad donde se realizó el depósito
        /// Puede ser código o nombre del banco
        /// </summary>
        [StringLength(100)]
        public string? Banco { get; set; }

        /// <summary>
        /// URL relativa del comprobante de pago (imagen o PDF)
        /// Ejemplo: /uploads/comprobantes/venta-123.jpg
        /// </summary>
        [StringLength(500)]
        public string? ComprobanteUrl { get; set; }

        // =====================================================================

        // Detalle
        public ICollection<DetalleVentas> DetalleVentas { get; set; } = new List<DetalleVentas>();
    }
}
