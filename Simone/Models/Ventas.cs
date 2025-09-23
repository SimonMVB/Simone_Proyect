using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Simone.Models
{
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

        // Detalle
        public ICollection<DetalleVentas> DetalleVentas { get; set; } = new List<DetalleVentas>();
    }
}
