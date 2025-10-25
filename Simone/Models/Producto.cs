using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace Simone.Models
{
    public class Producto
    {
        // --------------------- Clave ---------------------
        [Key]
        public int ProductoID { get; set; }

        // --------------------- Datos base ----------------
        [Required, StringLength(200)]
        public string Nombre { get; set; } = null!;

        [Required]
        public DateTime FechaAgregado { get; set; } = DateTime.UtcNow;

        [StringLength(2000)]
        public string? Descripcion { get; set; }

        // ⚠️ Compatibilidad con vistas/lógica antigua
        [StringLength(30)]
        public string? Talla { get; set; }

        [StringLength(30)]
        public string? Color { get; set; }

        [StringLength(120)]
        public string? Marca { get; set; }

        // Imagen “principal” heredada (fallback)
        [StringLength(300)]
        public string? ImagenPath { get; set; }

        [Required, Column(TypeName = "decimal(18,2)")]
        public decimal PrecioCompra { get; set; }

        [Required, Column(TypeName = "decimal(18,2)")]
        public decimal PrecioVenta { get; set; }

        // Stock simple (si usas variantes mira StockDisponible)
        [Required]
        public int Stock { get; set; }

        // --------------------- FKs -----------------------
        public int ProveedorID { get; set; }
        public int SubcategoriaID { get; set; }
        public int CategoriaID { get; set; }
        [Required, StringLength(450)]
        public string VendedorID { get; set; } = null!;

        // --------------------- Navegación ----------------
        [ForeignKey(nameof(CategoriaID))]
        public virtual Categorias Categoria { get; set; } = null!;

        [ForeignKey(nameof(ProveedorID))]
        public virtual Proveedores Proveedor { get; set; } = null!;

        [ForeignKey(nameof(SubcategoriaID))]
        public virtual Subcategorias Subcategoria { get; set; } = null!;

        [ForeignKey(nameof(VendedorID))]
        public virtual Usuario Usuario { get; set; } = null!;

        // ====== Colecciones legacy (NO eliminar) ======
        public virtual ICollection<ImagenesProductos> ImagenesProductos { get; set; } = new List<ImagenesProductos>();
        public virtual ICollection<MovimientosInventario> MovimientosInventario { get; set; } = new List<MovimientosInventario>();
        public virtual ICollection<DetalleVentas> DetalleVentas { get; set; } = new List<DetalleVentas>();
        public virtual ICollection<DetallesPedido> DetallesPedido { get; set; } = new List<DetallesPedido>();
        public virtual ICollection<DetallesCompra> DetallesCompra { get; set; } = new List<DetallesCompra>();
        public virtual ICollection<Reseñas> Reseñas { get; set; } = new List<Reseñas>();
        public virtual ICollection<CarritoDetalle> CarritoDetalles { get; set; } = new List<CarritoDetalle>();
        // ==============================================

        // ====== Nuevo esquema ======
        // Galería moderna (múltiples imágenes)
        public virtual ICollection<ProductoImagen> Imagenes { get; set; } = new List<ProductoImagen>();

        // Variantes (Color/Talla + stock/precio opcional)
        public virtual ICollection<ProductoVariante> Variantes { get; set; } = new List<ProductoVariante>();
        // ===========================

        // ----------------- Conveniencias de lectura -----------------

        // Stock total: usa variantes si existen; si no, stock histórico
        [NotMapped]
        public int StockDisponible =>
            (Variantes != null && Variantes.Any())
                ? Variantes.Sum(v => v.Stock)
                : Stock;

        // Imagen principal: 1) destacada de la galería, 2) por orden, 3) ImagenPath legacy
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

        // Imagen principal con placeholder seguro (útil para la UI)
        [NotMapped]
        public string ImagenPrincipalOrPlaceholder =>
            ImagenPrincipalPath ?? "/images/product-placeholder.png";

        // Galería ordenada (paths) para la vista
        [NotMapped]
        public IEnumerable<string> GaleriaPaths =>
            (Imagenes ?? Enumerable.Empty<ProductoImagen>())
                .OrderByDescending(i => i.Principal)
                .ThenBy(i => i.Orden)
                .Select(i => i.Path);

        // ¿Tiene variantes / imágenes?
        [NotMapped] public bool TieneVariantes => Variantes != null && Variantes.Count > 0;
        [NotMapped] public bool TieneImagenes => Imagenes != null && Imagenes.Count > 0;

        // Precio mínimo/máximo entre variantes (si definiste PrecioVenta por variante)
        [NotMapped]
        public decimal? PrecioVentaMinVariante =>
            Variantes?.Select(v => v.PrecioVenta).Where(p => p.HasValue).Select(p => p!.Value).DefaultIfEmpty().Min();

        [NotMapped]
        public decimal? PrecioVentaMaxVariante =>
            Variantes?.Select(v => v.PrecioVenta).Where(p => p.HasValue).Select(p => p!.Value).DefaultIfEmpty().Max();
    }
}
    
