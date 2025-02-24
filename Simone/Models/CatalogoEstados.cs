using System.ComponentModel.DataAnnotations;
using Simone.Models;

namespace Simone.Models
{
    public class CatalogoEstados
    {
        [Key]
        public int EstadoID { get; set; } // Clave primaria

        [Required]
        [StringLength(100, ErrorMessage = "El nombre del estado no puede exceder los 100 caracteres.")]
        public string NombreEstado { get; set; } // Nombre del estado
    }
}
