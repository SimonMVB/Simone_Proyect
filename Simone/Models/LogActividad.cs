
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Simone.Models
{
    public class LogActividad
    {
        [Key]
        public int LogActividadID { get; set; }

        [Required]
        public string UsuarioID { get; set; }

        [Required]
        [StringLength(300)]
        public string Accion { get; set; }

        [StringLength(100)]
        public string IP { get; set; }

        public DateTime FechaHora { get; set; } = DateTime.Now;

        [ForeignKey("UsuarioID")]
        public Usuario Usuario { get; set; }
    }
}
