using System;

namespace Simone.Models
{
    public class Empleados
    {
        public int EmpleadoID { get; set; }  // Clave primaria
        public string Nombre { get; set; }  // Nombre del empleado
        public string Apellido { get; set; }  // Apellido del empleado
        public string? Direccion { get; set; }  // Puede ser nulo
        public string? Telefono { get; set; }  // Puede ser nulo
        public string? Email { get; set; }  // Puede ser nulo
        public int RolID { get; set; }  // Clave foránea con Roles
        public DateTime? FechaContratacion { get; set; }  // Puede ser nulo
        public decimal? Salario { get; set; }  // Puede ser nulo

        // Relación con Roles
        public Roles Rol { get; set; }
        public object Comisiones { get; internal set; }
        public object Gastos { get; internal set; }
    }
}

