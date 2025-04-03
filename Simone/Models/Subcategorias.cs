using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;  // ✅ Agregar directiva para Key
using System.ComponentModel.DataAnnotations.Schema;

namespace Simone.Models
{
    public class Subcategorias
    {
        [Key]  // ✅ Define la clave primaria correctamente
        public int SubcategoriaID { get; set; }

        public int CategoriaID { get; set; }  // Clave foránea con Categorias

        public string NombreSubcategoria { get; set; }  // Nombre de la subcategoría

        // Relación con Categorías
        [ForeignKey("CategoriaID")]
        public Categorias Categoria { get; set; }

        // Relación con Productos
        public ICollection<Producto> Productos { get; set; }
    }
}
