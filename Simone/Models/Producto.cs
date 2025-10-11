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

        // ⚠️ Compatibilidad con vistas/lógica antigua (producto simple)
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
        [Range(typeof(decimal), "0.00", "999999999999.99", ErrorMessage = "PrecioCompra inválido.")]
        public decimal PrecioCompra { get; set; }

        [Required, Column(TypeName = "decimal(18,2)")]
        [Range(typeof(decimal), "0.00", "999999999999.99", ErrorMessage = "PrecioVenta inválido.")]
        public decimal PrecioVenta { get; set; }

        // Stock simple (si usas variantes mira StockDisponible)
        [Required]
        [Range(0, int.MaxValue, ErrorMessage = "Stock inválido.")]
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

        /// <summary>
        /// Stock total: usa variantes si existen; si no, stock simple.
        /// </summary>
        [NotMapped]
        public int StockDisponible =>
            (Variantes != null && Variantes.Any())
                ? Variantes.Sum(v => v.Stock)
                : Stock;

        /// <summary>
        /// Imagen principal efectiva: 1) destacada, 2) por orden, 3) ImagenPath legacy.
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
        /// Imagen principal con placeholder seguro (útil para la UI).
        /// </summary>
        [NotMapped]
        public string ImagenPrincipalOrPlaceholder =>
            ImagenPrincipalPath ?? "/images/product-placeholder.png";

        /// <summary>
        /// Galería ordenada (solo paths) para vistas.
        /// </summary>
        [NotMapped]
        public IEnumerable<string> GaleriaPaths =>
            (Imagenes ?? Enumerable.Empty<ProductoImagen>())
                .OrderByDescending(i => i.Principal)
                .ThenBy(i => i.Orden)
                .Select(i => i.Path);

        [NotMapped] public bool TieneVariantes => Variantes != null && Variantes.Count > 0;
        [NotMapped] public bool TieneImagenes  => Imagenes  != null && Imagenes.Count  > 0;

        /// <summary>
        /// ¿Hay al menos una variante con precio definido?
        /// </summary>
        [NotMapped]
        public bool TienePrecioEnVariantes =>
            Variantes?.Any(v => v.PrecioVenta.HasValue && v.PrecioVenta.Value > 0m) == true;

        /// <summary>
        /// Precio mínimo/máximo entre variantes (si alguna tiene precio definido).
        /// Si no hay precios en variantes, dan 0.00 (no rompe vistas).
        /// </summary>
        [NotMapped]
        public decimal? PrecioVentaMinVariante =>
            Variantes?
                .Where(v => v.PrecioVenta.HasValue)
                .Select(v => v.PrecioVenta!.Value)
                .DefaultIfEmpty()  // 0m
                .Min();

        [NotMapped]
        public decimal? PrecioVentaMaxVariante =>
            Variantes?
                .Where(v => v.PrecioVenta.HasValue)
                .Select(v => v.PrecioVenta!.Value)
                .DefaultIfEmpty()  // 0m
                .Max();

        /// <summary>
        /// Precio a mostrar al público: si hay variantes con precio, usa el menor;
        /// de lo contrario, el PrecioVenta del producto (que ya trae el +15% según tu lógica de PanelController).
        /// </summary>
        [NotMapped]
        public decimal PrecioVentaEfectivo =>
            TienePrecioEnVariantes
                ? Variantes!
                    .Where(v => v.PrecioVenta.HasValue)
                    .Select(v => v.PrecioVenta!.Value)
                    .DefaultIfEmpty(0m)
                    .Min()
                : PrecioVenta;
    }
}
