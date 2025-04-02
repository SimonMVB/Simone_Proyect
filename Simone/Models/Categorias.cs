using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Simone.Models
{
    public class Categorias
    {
        [Key]
        public int CategoriaID { get; set; }

        [Required(ErrorMessage = "El nombre de la categoría es obligatorio.")]
        [StringLength(100, ErrorMessage = "El nombre de la categoría no puede exceder los 100 caracteres.")]
        public string NombreCategoria { get; set; }

        // ✅ Relación con Subcategorías
        public virtual ICollection<Subcategorias> Subcategoria { get; set; } = new List<Subcategorias>();

        // ✅ Relación con Productos
        public virtual ICollection<Productos> Productos { get; set; } = new List<Productos>();
    }
}
