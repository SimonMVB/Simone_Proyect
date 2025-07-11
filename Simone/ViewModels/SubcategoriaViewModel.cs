using Microsoft.AspNetCore.Mvc.Rendering;

namespace Simone.ViewModels
{
    public class SubcategoriaViewModel
    {
        public int SubcategoriaID { get; set; }
        public string NombreSubcategoria { get; set; }
        public int CategoriaID { get; set; }

        // Additional properties for category options
        public List<SelectListItem> Categorias { get; set; }
    }
}
