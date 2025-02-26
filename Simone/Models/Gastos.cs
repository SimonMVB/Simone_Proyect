using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Simone.Models
{
    public class Gastos
    {
        [Key]  // ✅ Definir clave primaria
        public int GastoID { get; set; }

        public int EmpleadoID { get; set; }  // Clave foránea con Empleados
        public string Concepto { get; set; }  // Descripción del gasto
        public decimal Monto { get; set; }  // Monto del gasto
        public DateTime FechaGasto { get; set; }  // Fecha en que se realizó el gasto
        public string? Observaciones { get; set; }  // Puede ser nulo

        // Relación con Empleados
        [ForeignKey("EmpleadoID")]
        public Empleados Empleado { get; set; }
    }
}
