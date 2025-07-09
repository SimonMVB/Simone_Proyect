using Simone.Models;

public class CatalogoViewModel
{
    public List<Categorias> Categorias { get; set; } = new();
    public int? SelectedCategoriaID { get; set; }

    public List<Subcategorias> Subcategorias { get; set; } = new();
    public List<int> SelectedSubcategoriaIDs { get; set; } = new();
    public int ProductoID { get; set; }
    public int Cantidad { get; set; }

    public List<Producto> Productos { get; set; } = new();

    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalProducts { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalProducts / (double)PageSize);
    public List<int> ProductoIDsFavoritos { get; set; } = new();

}
