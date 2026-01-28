using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace Simone.Models
{
    public class Producto
    {
        // ==================== CLAVE ====================
        [Key]
        public int ProductoID { get; set; }

        // ==================== DATOS BASE ====================
        [Required]
        [StringLength(200)]
        public string Nombre { get; set; } = null!;

        [Required]
        public DateTime FechaAgregado { get; set; } = DateTime.UtcNow;

        [StringLength(2000)]
        public string? Descripcion { get; set; }

        // ⚠️ Compatibilidad con vistas/lógica antigua (mantener por ahora)
        [StringLength(30)]
        public string? Talla { get; set; }

        [StringLength(30)]
        public string? Color { get; set; }

        [StringLength(120)]
        public string? Marca { get; set; }

        // Imagen principal heredada (fallback)
        [StringLength(300)]
        public string? ImagenPath { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal PrecioCompra { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal PrecioVenta { get; set; }

        // Stock simple (si usas variantes mira StockDisponible)
        [Required]
        public int Stock { get; set; }

        // ==================== FOREIGN KEYS ====================

        /// <summary>
        /// Proveedor opcional
        /// </summary>
        public int? ProveedorID { get; set; }

        /// <summary>
        /// Categoría (nuevo sistema jerárquico)
        /// </summary>
        [Required]
        public int CategoriaID { get; set; }

        /// <summary>
        /// Subcategoría (legacy, nullable para compatibilidad)
        /// ⚠️ DEPRECADO: Usar solo CategoriaID con nuevo sistema
        /// </summary>
        [Obsolete("Usar solo CategoriaID con nueva jerarquía infinita")]
        public int? SubcategoriaID { get; set; }

        /// <summary>
        /// Vendedor dueño del producto
        /// </summary>
        [Required]
        [StringLength(450)]
        public string VendedorID { get; set; } = null!;

        // ==================== NAVEGACIÓN ====================

        /// <summary>
        /// Categoría (nuevo sistema jerárquico enterprise)
        /// </summary>
        [ForeignKey(nameof(CategoriaID))]
        public virtual Categoria Categoria { get; set; } = null!;

        /// <summary>
        /// Proveedor (opcional)
        /// </summary>
        [ForeignKey(nameof(ProveedorID))]
        public virtual Proveedores? Proveedor { get; set; }

        /// <summary>
        /// Subcategoría (legacy, nullable para compatibilidad)
        /// ⚠️ DEPRECADO: Mantener solo durante migración
        /// </summary>
        [Obsolete("Usar Categoria con nueva jerarquía")]
        [ForeignKey(nameof(SubcategoriaID))]
        public virtual Subcategorias? Subcategoria { get; set; }

        /// <summary>
        /// Vendedor/Usuario dueño
        /// </summary>
        [ForeignKey(nameof(VendedorID))]
        public virtual Usuario Usuario { get; set; } = null!;

        // ==================== COLECCIONES LEGACY (NO ELIMINAR) ====================
        public virtual ICollection<ImagenesProductos> ImagenesProductos { get; set; }
            = new List<ImagenesProductos>();

        public virtual ICollection<MovimientosInventario> MovimientosInventario { get; set; }
            = new List<MovimientosInventario>();

        public virtual ICollection<DetalleVentas> DetalleVentas { get; set; }
            = new List<DetalleVentas>();

        public virtual ICollection<DetallesPedido> DetallesPedido { get; set; }
            = new List<DetallesPedido>();

        public virtual ICollection<DetallesCompra> DetallesCompra { get; set; }
            = new List<DetallesCompra>();

        public virtual ICollection<Reseñas> Reseñas { get; set; }
            = new List<Reseñas>();

        public virtual ICollection<CarritoDetalle> CarritoDetalles { get; set; }
            = new List<CarritoDetalle>();

        // ==================== NUEVO ESQUEMA MODERNO ====================

        /// <summary>
        /// Galería de imágenes moderna (múltiples imágenes)
        /// </summary>
        public virtual ICollection<ProductoImagen> Imagenes { get; set; }
            = new List<ProductoImagen>();

        /// <summary>
        /// Variantes (Color + Talla + stock/precio opcional)
        /// </summary>
        public virtual ICollection<ProductoVariante> Variantes { get; set; }
            = new List<ProductoVariante>();

        /// <summary>
        /// ⭐ NUEVO: Valores de atributos dinámicos
        /// Ejemplo: Largo: "Maxi", Escote: "V", Material: "Seda"
        /// </summary>
        public virtual ICollection<ProductoAtributoValor> AtributosValores { get; set; }
            = new List<ProductoAtributoValor>();

        // ==================== PROPIEDADES CALCULADAS - STOCK & PRECIO ====================

        /// <summary>
        /// Stock total: usa variantes si existen; si no, stock histórico
        /// </summary>
        [NotMapped]
        public int StockDisponible =>
            (Variantes != null && Variantes.Any())
                ? Variantes.Sum(v => v.Stock)
                : Stock;

        /// <summary>
        /// Precio mínimo entre variantes
        /// </summary>
        [NotMapped]
        public decimal? PrecioVentaMinVariante =>
            Variantes?
                .Select(v => v.PrecioVenta)
                .Where(p => p.HasValue)
                .Select(p => p!.Value)
                .DefaultIfEmpty()
                .Min();

        /// <summary>
        /// Precio máximo entre variantes
        /// </summary>
        [NotMapped]
        public decimal? PrecioVentaMaxVariante =>
            Variantes?
                .Select(v => v.PrecioVenta)
                .Where(p => p.HasValue)
                .Select(p => p!.Value)
                .DefaultIfEmpty()
                .Max();

        // ==================== PROPIEDADES CALCULADAS - IMÁGENES ====================

        /// <summary>
        /// Imagen principal: 1) destacada de galería, 2) por orden, 3) ImagenPath legacy
        /// </summary>
        [NotMapped]
        public string? ImagenPrincipalPath =>
            (Imagenes != null && Imagenes.Count > 0
                ? Imagenes
                    .OrderByDescending(i => i.Principal)
                    .ThenBy(i => i.Orden)
                    .Select(i => i.Path)
                    .FirstOrDefault()
                : null)
            ?? ImagenPath;

        /// <summary>
        /// Imagen principal con placeholder seguro (útil para UI)
        /// </summary>
        [NotMapped]
        public string ImagenPrincipalOrPlaceholder =>
            ImagenPrincipalPath ?? "/images/product-placeholder.png";

        /// <summary>
        /// Galería ordenada (paths) para la vista
        /// </summary>
        [NotMapped]
        public IEnumerable<string> GaleriaPaths =>
            (Imagenes ?? Enumerable.Empty<ProductoImagen>())
                .OrderByDescending(i => i.Principal)
                .ThenBy(i => i.Orden)
                .Select(i => i.Path);

        /// <summary>
        /// ¿Tiene variantes?
        /// </summary>
        [NotMapped]
        public bool TieneVariantes => Variantes != null && Variantes.Count > 0;

        /// <summary>
        /// ¿Tiene imágenes?
        /// </summary>
        [NotMapped]
        public bool TieneImagenes => Imagenes != null && Imagenes.Count > 0;

        // ==================== PROPIEDADES CALCULADAS - CATEGORÍAS ENTERPRISE ====================

        /// <summary>
        /// Breadcrumbs completos de la categoría
        /// Ejemplo: "Mujer > Ropa > Vestidos > Vestidos de Noche"
        /// </summary>
        [NotMapped]
        public string RutaCategorias =>
            Categoria?.NombreCompleto ?? "Sin categoría";

        /// <summary>
        /// Slug completo para URL
        /// Ejemplo: "mujer/ropa/vestidos/vestidos-de-noche"
        /// </summary>
        [NotMapped]
        public string SlugCompleto =>
            Categoria?.Path ?? "sin-categoria";

        /// <summary>
        /// URL simple del producto
        /// Ejemplo: "/p/vestido-rojo-de-noche-12345"
        /// </summary>
        [NotMapped]
        public string UrlProducto =>
            $"/p/{NormalizarParaUrl(Nombre)}-{ProductoID}";

        /// <summary>
        /// URL canónica con categoría completa (mejor SEO)
        /// Ejemplo: "/c/mujer/ropa/vestidos/p/vestido-rojo-12345"
        /// </summary>
        [NotMapped]
        public string UrlCanonica =>
            $"/c/{SlugCompleto}/p/{NormalizarParaUrl(Nombre)}-{ProductoID}";

        // ==================== PROPIEDADES CALCULADAS - ATRIBUTOS DINÁMICOS ====================

        /// <summary>
        /// Obtener todos los atributos como diccionario
        /// Útil para mostrar especificaciones
        /// Ejemplo: { "Largo": "Maxi", "Escote": "V", "Material": "Seda" }
        /// </summary>
        [NotMapped]
        public Dictionary<string, string> AtributosDiccionario =>
            AtributosValores?
                .Where(av => av.Atributo != null)
                .OrderBy(av => av.Atributo.Orden)
                .ToDictionary(
                    av => av.Atributo.Nombre,
                    av => av.ValorFormateado
                ) ?? new Dictionary<string, string>();

        /// <summary>
        /// Atributos filtrables para mostrar en tarjeta (listado)
        /// Solo atributos marcados como MostrarEnTarjeta
        /// </summary>
        [NotMapped]
        public Dictionary<string, string> AtributosTarjeta =>
            AtributosValores?
                .Where(av => av.Atributo != null && av.Atributo.MostrarEnTarjeta)
                .OrderBy(av => av.Atributo.Orden)
                .ToDictionary(
                    av => av.Atributo.Nombre,
                    av => av.ValorFormateado
                ) ?? new Dictionary<string, string>();

        /// <summary>
        /// Atributos agrupados por grupo/sección
        /// Ejemplo: { "Especificaciones": {...}, "Dimensiones": {...} }
        /// </summary>
        [NotMapped]
        public Dictionary<string, Dictionary<string, string>> AtributosAgrupados =>
            AtributosValores?
                .Where(av => av.Atributo != null && av.Atributo.MostrarEnFicha)
                .OrderBy(av => av.Atributo.Orden)
                .GroupBy(av => av.Atributo.Grupo ?? "General")
                .ToDictionary(
                    g => g.Key,
                    g => g.ToDictionary(
                        av => av.Atributo.Nombre,
                        av => av.ValorFormateado
                    )
                ) ?? new Dictionary<string, Dictionary<string, string>>();

        // ==================== MÉTODOS AUXILIARES - ATRIBUTOS ====================

        /// <summary>
        /// Obtener valor de un atributo específico por nombre técnico
        /// Ejemplo: ObtenerValorAtributo("largo") → "Maxi"
        /// </summary>
        public string? ObtenerValorAtributo(string nombreTecnicoAtributo)
        {
            return AtributosValores?
                .FirstOrDefault(av => av.Atributo?.NombreTecnico == nombreTecnicoAtributo)?
                .Valor;
        }

        /// <summary>
        /// Obtener valor de un atributo específico por ID
        /// </summary>
        public string? ObtenerValorAtributoPorID(int atributoID)
        {
            return AtributosValores?
                .FirstOrDefault(av => av.AtributoID == atributoID)?
                .Valor;
        }

        /// <summary>
        /// ¿Tiene un atributo específico?
        /// </summary>
        public bool TieneAtributo(string nombreTecnicoAtributo)
        {
            return AtributosValores?
                .Any(av => av.Atributo?.NombreTecnico == nombreTecnicoAtributo) ?? false;
        }

        /// <summary>
        /// Obtener atributos filtrables (para búsqueda/filtrado)
        /// </summary>
        [NotMapped]
        public IEnumerable<ProductoAtributoValor> AtributosFiltrables =>
            AtributosValores?
                .Where(av => av.Atributo != null && av.Atributo.Filtrable)
                .OrderBy(av => av.Atributo.Orden)
            ?? Enumerable.Empty<ProductoAtributoValor>();

        // ==================== MÉTODOS AUXILIARES - HELPERS ====================

        /// <summary>
        /// Normalizar texto para URL (quitar acentos, espacios, etc)
        /// </summary>
        private string NormalizarParaUrl(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
                return "producto";

            // Convertir a lowercase
            texto = texto.ToLowerInvariant();

            // Reemplazar caracteres especiales comunes
            texto = texto
                .Replace("á", "a").Replace("é", "e").Replace("í", "i")
                .Replace("ó", "o").Replace("ú", "u").Replace("ñ", "n")
                .Replace(" ", "-").Replace("_", "-");

            // Eliminar caracteres no válidos
            texto = System.Text.RegularExpressions.Regex.Replace(texto, @"[^a-z0-9\-]", "");

            // Eliminar guiones múltiples
            texto = System.Text.RegularExpressions.Regex.Replace(texto, @"-+", "-");

            // Trim guiones al inicio/final
            texto = texto.Trim('-');

            return string.IsNullOrEmpty(texto) ? "producto" : texto;
        }

        /// <summary>
        /// ¿Producto disponible para compra?
        /// </summary>
        [NotMapped]
        public bool EstaDisponible => StockDisponible > 0;

        /// <summary>
        /// ¿Producto en oferta? (si hay variantes con descuento)
        /// </summary>
        [NotMapped]
        public bool EstaEnOferta =>
            PrecioVentaMinVariante.HasValue &&
            PrecioVentaMinVariante.Value < PrecioVenta;

        /// <summary>
        /// Porcentaje de descuento (si aplica)
        /// </summary>
        [NotMapped]
        public decimal? PorcentajeDescuento
        {
            get
            {
                if (!PrecioVentaMinVariante.HasValue || PrecioVentaMinVariante.Value >= PrecioVenta)
                    return null;

                var descuento = ((PrecioVenta - PrecioVentaMinVariante.Value) / PrecioVenta) * 100;
                return Math.Round(descuento, 0);
            }
        }

        /// <summary>
        /// Rating promedio de reseñas
        /// </summary>
        [NotMapped]
        public decimal? RatingPromedio =>
            Reseñas != null && Reseñas.Any()
                ? (decimal?)Reseñas.Average(r => r.Calificacion)
                : null;

        /// <summary>
        /// Cantidad de reseñas
        /// </summary>
        [NotMapped]
        public int CantidadReseñas => Reseñas?.Count ?? 0;
    }
}
