using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Simone.Models
{
    public class Comisiones
    {
        [Key] // Definir clave primaria
        public int ComisionID { get; set; }

        public int VentaID { get; set; }
        public int EmpleadoID { get; set; }
        public decimal PorcentajeComision { get; set; }
        public decimal MontoComision { get; set; }
        public DateTime FechaGeneracion { get; set; }
        public bool Pagada { get; set; }

        // Relaciones
        [ForeignKey("VentaID")]
        public virtual Ventas Venta { get; set; }

        [ForeignKey("EmpleadoID")]
        public virtual Empleados Empleado { get; set; }
    }
}
