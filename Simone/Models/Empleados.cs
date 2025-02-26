using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Simone.Models
{
    public class Empleados
    {
        [Key] // ✅ Definir clave primaria
        public int EmpleadoID { get; set; }

        public string Nombre { get; set; }
        public string Apellido { get; set; }
        public string? Direccion { get; set; }
        public string? Telefono { get; set; }
        public string? Email { get; set; }
        public int RolID { get; set; }
        public DateTime? FechaContratacion { get; set; }
        public decimal? Salario { get; set; }

        // Relación con Roles
        [ForeignKey("RolID")]
        public Roles Rol { get; set; }

        public ICollection<Comisiones> Comisiones { get; set; } = new List<Comisiones>();
        public ICollection<Gastos> Gastos { get; set; } = new List<Gastos>();
    }
}
