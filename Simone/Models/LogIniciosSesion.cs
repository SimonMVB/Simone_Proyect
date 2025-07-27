using System;
using System.ComponentModel.DataAnnotations;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Simone.Models
{
    public class LogIniciosSesion
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]  // Auto-incremental
        public int LogID { get; set; }

        [Required(ErrorMessage = "El nombre de usuario es obligatorio")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "El usuario debe tener entre 3 y 100 caracteres")]
        public string Usuario { get; set; }

        [DataType(DataType.DateTime)]
        [Display(Name = "Fecha de Inicio")]
        public DateTime FechaInicio { get; set; } = DateTime.Now;  // Valor por defecto

        [Required]
        [Display(Name = "Inicio Exitoso")]
        public bool? Exitoso { get; set; }  // No debería ser nulo, es información crítica

        [Required(ErrorMessage = "La dirección IP es obligatoria")]
        [StringLength(45, ErrorMessage = "La dirección IP no puede exceder 45 caracteres")]  // IPv6 max length
        [RegularExpression(@"^(?:[0-9]{1,3}\.){3}[0-9]{1,3}|([0-9a-fA-F]{1,4}:){7}[0-9a-fA-F]{1,4}$",
            ErrorMessage = "Formato de IP no válido")]
        public string DireccionIP { get; set; }

        [StringLength(255)]
        [Display(Name = "Dispositivo/Navegador")]
        public string? UserAgent { get; set; }  // Nuevo campo útil para auditoría

        [StringLength(50)]
        public string? Localizacion { get; set; }  // Podría determinarse por IP
    }
}