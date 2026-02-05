using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Simone.Models
{
    /// <summary>
    /// Modelo de Ventas con soporte completo para pagos por transferencia/depósito.
    /// Incluye campos para persistir datos de pago directamente en la BD.
    /// </summary>
    public class Ventas
    {
        [Key]
        public int VentaID { get; set; }

        #region Vendedor/Empleado (opcional)

        public string? EmpleadoID { get; set; }

        [ForeignKey(nameof(EmpleadoID))]
        public virtual Usuario? Empleado { get; set; }

        #endregion

        #region Comprador

        [Required]
        public string UsuarioId { get; set; } = default!;

        [ForeignKey(nameof(UsuarioId))]
        public virtual Usuario Usuario { get; set; } = default!;

        #endregion

        #region Datos de la Venta

        [Required]
        [StringLength(30)]
        public string Estado { get; set; } = "Pendiente";

        public DateTime FechaVenta { get; set; } = DateTime.UtcNow;

        [Required]
        [StringLength(50)]
        public string MetodoPago { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Total { get; set; }

        #endregion

        #region Datos de Pago por Transferencia/Depósito

        /// <summary>
        /// Nombre completo de quien realizó la transferencia/depósito.
        /// </summary>
        [StringLength(200)]
        public string? Depositante { get; set; }

        /// <summary>
        /// Nombre o código del banco donde se realizó el depósito.
        /// Ejemplo: "Banco Pichincha", "BP", etc.
        /// </summary>
        [StringLength(100)]
        public string? Banco { get; set; }

        /// <summary>
        /// URL relativa del comprobante de pago subido (imagen o PDF).
        /// Ejemplo: /uploads/comprobantes/venta-123.jpg
        /// </summary>
        [StringLength(500)]
        public string? ComprobanteUrl { get; set; }

        #endregion

        #region Navegación

        public virtual ICollection<DetalleVentas> DetalleVentas { get; set; } = new List<DetalleVentas>();

        #endregion
    }
}
