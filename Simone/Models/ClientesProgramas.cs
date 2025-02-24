using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Simone.Models
{
    public class ClientesProgramas
    {
        [Key]
        [Column(Order = 1)]
        public int ClienteID { get; set; }  // Clave foránea con Cliente

        [Key]
        [Column(Order = 2)]
        public int ProgramaID { get; set; }  // Clave foránea con ProgramasFidelizacion

        public DateTime? FechaInicio { get; set; }  // Puede ser nulo

        // Relaciones
        [ForeignKey("ClienteID")]
        public virtual Cliente Cliente { get; set; }

        [ForeignKey("ProgramaID")]
        public virtual ProgramasFidelizacion Programa { get; set; }
    }
}
