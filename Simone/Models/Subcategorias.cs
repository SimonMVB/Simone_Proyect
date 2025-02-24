using System;
using System.Collections.Generic;

namespace Simone.Models
{
    public class Subcategorias
    {
        public int SubcategoriaID { get; set; }  // Clave primaria
        public int CategoriaID { get; set; }  // Clave foránea con Categorias
        public string NombreSubcategoria { get; set; }  // Nombre de la subcategoría

        // Relación con Categorías
        public Categorias Categoria { get; set; }

        // Relación con Productos
        public ICollection<Productos> Productos { get; set; }
    }
}
