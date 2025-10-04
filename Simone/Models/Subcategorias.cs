using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;               // ✅ DataAnnotations
using System.ComponentModel.DataAnnotations.Schema;       // ✅ ForeignKey

namespace Simone.Models
{
    public class Subcategorias
    {
        [Key] // ✅ Clave primaria
        public int SubcategoriaID { get; set; }

        // ===== Relación con Categorías =====
        [Required]
        public int CategoriaID { get; set; }              // FK -> Categorias

        [ForeignKey(nameof(CategoriaID))]
        public Categorias Categoria { get; set; }         // Navegación a Categoría

        // ===== Propietario (multi-vendedor) =====
        // FK -> AspNetUsers (Usuario.Id). Esto permite que cada vendedor tenga sus propias subcategorías.
        [Required]
        [StringLength(450)]                               // Identity Id (nvarchar(450))
        public string VendedorID { get; set; } = string.Empty;

        [ForeignKey(nameof(VendedorID))]
        public Usuario Usuario { get; set; }              // Vendedor dueño de la subcategoría

        // ===== Datos =====
        [Required(ErrorMessage = "El nombre de la subcategoría es obligatorio.")]
        [StringLength(100, ErrorMessage = "El nombre no puede exceder 100 caracteres.")]
        public string NombreSubcategoria { get; set; } = string.Empty;

        // ===== Relación con Productos =====
        public ICollection<Producto> Productos { get; set; } = new List<Producto>();
    }
}
