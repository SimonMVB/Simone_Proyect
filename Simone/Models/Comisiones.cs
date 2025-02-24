using System;
using Simone.Models;

namespace Simone.Models
{
    public class Comisiones
    {
        public int ComisionID { get; set; }  // Clave primaria
        public int VentaID { get; set; }  // Clave foránea con Ventas
        public int EmpleadoID { get; set; }  // Clave foránea con Empleados
        public decimal PorcentajeComision { get; set; }  // Porcentaje de comisión
        public decimal MontoComision { get; set; }  // Monto total de la comisión
        public DateTime FechaGeneracion { get; set; }  // Fecha en que se generó la comisión
        public bool Pagada { get; set; }  // Indica si la comisión ha sido pagada

        // Relación con Ventas
        public Ventas Venta { get; set; }

        // Relación con Empleados
        public Empleados Empleado { get; set; }
    }
}
