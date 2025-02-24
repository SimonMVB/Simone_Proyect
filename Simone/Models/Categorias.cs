using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using Simone.Models;

namespace Simone.Models
{
    public class Categorias
    {
        [Key]
        public int CategoriaID { get; set; } // Clave primaria

        [Required(ErrorMessage = "El nombre de la categoría es obligatorio.")]
        [StringLength(100, ErrorMessage = "El nombre de la categoría no puede exceder los 100 caracteres.")]
        public string NombreCategoria { get; set; } // Nombre de la categoría

        // Relación con Subcategorias
        public ICollection<Subcategorias> Subcategoria { get; set; }
    }
}
