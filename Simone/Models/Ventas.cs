using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Simone.Models
{
    public class Ventas
    {
        [Key]
        public int VentaID { get; set; }

        // Relación con Empleado (Usuario como empleado)
        public string EmpleadoID { get; set; }
        public virtual Usuario Empleado { get; set; }

        public string Estado { get; set; }

        // Relación con Cliente (Usuario como cliente)
        public string ClienteID { get; set; }  // Foreign Key, ensure this matches the column name in the database
        public virtual Usuario Clientes { get; set; }  // Navigation property to Cliente

        public DateTime FechaVenta { get; set; }

        [Required]
        [StringLength(50)]
        public string MetodoPago { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal Total { get; set; }

        public virtual ICollection<DetalleVentas> DetalleVentas { get; set; }
    }
}
