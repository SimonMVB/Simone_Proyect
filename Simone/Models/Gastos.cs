using System;

namespace Simone.Models
{
    public class Gastos
    {
        public int GastoID { get; set; }  // Clave primaria
        public int EmpleadoID { get; set; }  // Clave foránea con Empleados
        public string Concepto { get; set; }  // Descripción del gasto
        public decimal Monto { get; set; }  // Monto del gasto
        public DateTime FechaGasto { get; set; }  // Fecha en que se realizó el gasto
        public string? Observaciones { get; set; }  // Puede ser nulo

        // Relación con Empleados
        public Empleados Empleado { get; set; }
    }
}
