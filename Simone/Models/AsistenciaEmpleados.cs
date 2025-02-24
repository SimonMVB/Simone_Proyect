using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Simone.Models;

namespace Simone.Models
{
    public class AsistenciaEmpleados
    {
        [Key]
        public int AsistenciaID { get; set; } // Clave primaria

        // Relación con Empleados
        [Required]
        public int EmpleadoID { get; set; }
        public Empleados Empleado { get; set; }

        [Required]
        public DateTime Fecha { get; set; }  // Fecha de la asistencia

        public TimeSpan? HoraEntrada { get; set; } // Hora opcional de entrada
        public TimeSpan? HoraSalida { get; set; } // Hora opcional de salida
    }
}
