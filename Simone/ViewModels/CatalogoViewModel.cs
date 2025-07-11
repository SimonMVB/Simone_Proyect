using Simone.Models;

public class CatalogoViewModel
{
    /// <summary>
    /// Lista de categorías para el filtro en el catálogo.
    /// </summary>
    public List<Categorias> Categorias { get; set; } = new();

    /// <summary>
    /// ID de la categoría seleccionada por el usuario. Es opcional.
    /// </summary>
    public int? SelectedCategoriaID { get; set; }

    /// <summary>
    /// Lista de subcategorías disponibles para la categoría seleccionada.
    /// </summary>
    public List<Subcategorias> Subcategorias { get; set; } = new();

    /// <summary>
    /// Lista de IDs de subcategorías seleccionadas por el usuario.
    /// </summary>
    public List<int> SelectedSubcategoriaIDs { get; set; } = new();

    /// <summary>
    /// ID del producto que se está visualizando o seleccionando.
    /// </summary>
    public int ProductoID { get; set; }

    /// <summary>
    /// Cantidad del producto a añadir al carrito.
    /// </summary>
    public int Cantidad { get; set; }

    /// <summary>
    /// Lista de productos que se muestran en el catálogo según el filtro.
    /// </summary>
    public List<Producto> Productos { get; set; } = new();

    /// <summary>
    /// Número de la página actual para la paginación.
    /// </summary>
    public int PageNumber { get; set; } = 1;

    /// <summary>
    /// Número de productos por página para la paginación.
    /// </summary>
    public int PageSize { get; set; } = 20;

    /// <summary>
    /// Total de productos encontrados según los filtros.
    /// </summary>
    public int TotalProducts { get; set; }

    /// <summary>
    /// Total de páginas disponibles para la paginación.
    /// </summary>
    public int TotalPages => (int)Math.Ceiling(TotalProducts / (double)PageSize);
}
