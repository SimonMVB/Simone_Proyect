using Microsoft.AspNetCore.Mvc.Rendering;

namespace Simone.ViewModels
{
    public class ProductoViewModel
    {
        public string NombreProducto { get; set; }
        public string Descripcion { get; set; }
        public string Talla { get; set; }
        public string Color { get; set; }
        public string Marca { get; set; }
        public decimal PrecioCompra { get; set; }
        public decimal PrecioVenta { get; set; }
        public int ProveedorID { get; set; }
        public int CategoriaID { get; set; }
        public int SubcategoriaID { get; set; }
        public int Stock { get; set; }
        public IFormFile Imagen { get; set; }  // For image upload

        // Additional properties for categories and suppliers
        public List<SelectListItem> Categorias { get; set; }
        public List<SelectListItem> Proveedores { get; set; }
        public List<SelectListItem> Subcategorias { get; set; }
    }
}
