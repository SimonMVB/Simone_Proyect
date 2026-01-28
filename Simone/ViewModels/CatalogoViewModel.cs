using System;
using System.Collections.Generic;
using Simone.Models;

/// <summary>
/// ViewModel del catálogo con filtros, paginación y variantes.
/// Robusto frente a nulls del model binder (listas y diccionarios nunca quedan en null).
/// </summary>
public class CatalogoViewModel
{
    // ===== Categorías / Subcategorías =====
    private List<Categorias>? _categorias;
    private List<Subcategorias>? _subcategorias;
    private List<int>? _selectedSubcategoriaIDs;

    public List<Categorias> Categorias
    {
        get => _categorias ??= new();
        set => _categorias = value ?? new();
    }

    public int? SelectedCategoriaID { get; set; }

    public List<Subcategorias> Subcategorias
    {
        get => _subcategorias ??= new();
        set => _subcategorias = value ?? new();
    }

    public List<int> SelectedSubcategoriaIDs
    {
        get => _selectedSubcategoriaIDs ??= new();
        set => _selectedSubcategoriaIDs = value ?? new();
    }

    // ===== Filtros por variantes (Colores/Tallas) =====
    private List<string>? _coloresDisponibles;
    private List<string>? _tallasDisponibles;
    private List<string>? _coloresSeleccionados;
    private List<string>? _tallasSeleccionadas;

    /// <summary>Valores disponibles para pintar el filtro de colores.</summary>
    public List<string> ColoresDisponibles
    {
        get => _coloresDisponibles ??= new();
        set => _coloresDisponibles = value ?? new();
    }

    /// <summary>Valores disponibles para pintar el filtro de tallas.</summary>
    public List<string> TallasDisponibles
    {
        get => _tallasDisponibles ??= new();
        set => _tallasDisponibles = value ?? new();
    }

    /// <summary>Selección actual de colores (desde querystring).</summary>
    public List<string> ColoresSeleccionados
    {
        get => _coloresSeleccionados ??= new();
        set => _coloresSeleccionados = value ?? new();
    }

    /// <summary>Selección actual de tallas (desde querystring).</summary>
    public List<string> TallasSeleccionadas
    {
        get => _tallasSeleccionadas ??= new();
        set => _tallasSeleccionadas = value ?? new();
    }

    /// <summary>Si true, muestra solo productos/variantes con stock &gt; 0.</summary>
    public bool SoloDisponibles { get; set; } = false;

    // ===== Catálogo (lista paginada) =====
    private List<Producto>? _productos;
    public List<Producto> Productos
    {
        get => _productos ??= new();
        set => _productos = value ?? new();
    }

    // ===== Variantes por producto (para la grilla) =====
    private Dictionary<int, List<ProductoVariante>>? _variantesPorProducto;
    /// <summary>Diccionario: ProductoID -&gt; Variantes del producto.</summary>
    public Dictionary<int, List<ProductoVariante>> VariantesPorProducto
    {
        get => _variantesPorProducto ??= new();
        set => _variantesPorProducto = value ?? new();
    }

    // ===== Paginación =====
    private int _pageNumber = 1;
    private int _pageSize = 20;

    public int PageNumber
    {
        get => _pageNumber <= 0 ? 1 : _pageNumber;
        set => _pageNumber = value <= 0 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize <= 0 ? 20 : _pageSize;
        set => _pageSize = value <= 0 ? 20 : value;
    }

    public int TotalProducts { get; set; }

    /// <summary>Total de páginas (redondeo hacia arriba). Seguro ante divisiones por 0.</summary>
    public int TotalPages
    {
        get
        {
            var ps = PageSize <= 0 ? 1 : PageSize;
            var tp = Math.Max(0, TotalProducts);
            return ps == 0 ? 0 : (int)Math.Ceiling(tp / (double)ps);
        }
    }

    // ===== Favoritos =====
    private List<int>? _productoIDsFavoritos;
    public List<int> ProductoIDsFavoritos
    {
        get => _productoIDsFavoritos ??= new();
        set => _productoIDsFavoritos = value ?? new();
    }

    // ===== Ordenamiento =====
    /// <summary>Valor actual del sort ("" | precio_asc | precio_desc | nuevos | mas_vendidos).</summary>
    public string? Sort { get; set; }

    // ===== Post al carrito =====
    public int ProductoID { get; set; }
    public int Cantidad { get; set; } = 1;
    public string? SearchTerm { get; set; }
    public decimal? PrecioMin { get; set; }
    public decimal? PrecioMax { get; set; }

    /// <summary>Variante seleccionada (nullable para productos sin variantes). La vista envía "ProductoVarianteID".</summary>
    public int? ProductoVarianteID { get; set; }
}
