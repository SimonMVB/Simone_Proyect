using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Simone.Models
{
    public class Ventas
    {
        [Key]
        public int VentaID { get; set; }

        public string EmpleadoID { get; set; }
        public virtual Usuario Empleado { get; set; }

        public string Estado { get; set; }
        public int ClienteID { get; set; }
        [ForeignKey("ClienteID")]
        public virtual Cliente Clientes { get; set; }

        public DateTime FechaVenta { get; set; }

        [Required]
        [StringLength(50)]
        public string MetodoPago { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal Total { get; set; }

        // SOLO ESTA colección, SIN "DetallesVenta" object
        public virtual ICollection<DetalleVentas> DetalleVentas { get; set; }
    }
}
