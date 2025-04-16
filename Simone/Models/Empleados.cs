using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Simone.Models
{
    public class Empleados
    {
        [Key]
        public int EmpleadoID { get; set; }

        [Required]
        public string Nombre { get; set; } = string.Empty;

        [Required]
        public string Apellido { get; set; } = string.Empty;

        public string? Direccion { get; set; }
        public string? Telefono { get; set; }
        public string? Email { get; set; }

        // ✅ Corregido: ahora es string para que sea compatible con IdentityRole.Id
        public string? RolID { get; set; }

        [ForeignKey("RolID")]
        public Roles? Rol { get; set; }

        public DateTime? FechaContratacion { get; set; }
        public decimal? Salario { get; set; }

        public ICollection<Comisiones> Comisiones { get; set; } = new List<Comisiones>();
        public ICollection<Gastos> Gastos { get; set; } = new List<Gastos>();
    }
}
