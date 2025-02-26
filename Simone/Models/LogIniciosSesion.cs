using System;
using System.ComponentModel.DataAnnotations;

namespace Simone.Models
{
    public class LogIniciosSesion
    {
        [Key]  // ✅ Definir clave primaria
        public int LogID { get; set; }

        [Required]
        public string Usuario { get; set; }  // Nombre del usuario que inició sesión

        public DateTime? FechaInicio { get; set; }  // Puede ser nulo
        public bool? Exitoso { get; set; }  // Puede ser nulo
    }
}
