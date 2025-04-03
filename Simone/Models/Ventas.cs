using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Simone.Models
{
    public class Ventas
    {
        [Key]
        public int VentaID { get; set; }  // Clave primaria

        // Relación con Empleados (opcional)
        public int? EmpleadoID { get; set; }
        public Empleados Empleado { get; set; }
        public string Estado { get; set; }

        // Relación con Clientes (opcional)
        public int? ClienteID { get; set; }
        public Cliente Clientes { get; set; }

        public DateTime FechaVenta { get; set; }  // Fecha en la que se realizó la venta

        [Required]
        [StringLength(50)]
        public string MetodoPago { get; set; }  // Método de pago utilizado en la venta

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal Total { get; set; }  // Total de la venta

        // Relación con DetalleVentas
        public ICollection<DetalleVentas> DetallesVenta { get; set; }
        public virtual ICollection<DetalleVentas> DetalleVentas { get; set; } = new List<DetalleVentas>();
    }
}
