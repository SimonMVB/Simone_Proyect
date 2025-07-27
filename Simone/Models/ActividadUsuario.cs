using System;
using System.ComponentModel.DataAnnotations;

namespace Simone.Models
{
    public class ActividadUsuario
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UsuarioId { get; set; }

        // Propiedad de navegación
        public virtual Usuario Usuario { get; set; }

        [Required, StringLength(100)]
        public string Accion { get; set; }

        [Required]
        public DateTime Fecha { get; set; } = DateTime.Now;

        [StringLength(500)]
        public string Detalles { get; set; }
    }
}