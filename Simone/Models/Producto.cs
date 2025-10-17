using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace Simone.Models
{
    public class Producto
    {
        [Key] public int ProductoID { get; set; }

        [Required, StringLength(200)]
        public string Nombre { get; set; } = null!;

        [Required]
        public DateTime FechaAgregado { get; set; } = DateTime.UtcNow;

        [StringLength(2000)]
        public string? Descripcion { get; set; }

        [StringLength(30)] public string? Talla { get; set; }
        [StringLength(30)] public string? Color { get; set; }
        [StringLength(120)] public string? Marca { get; set; }

        [StringLength(300)]
        public string? ImagenPath { get; set; }

        [Required, Column(TypeName = "decimal(18,2)")]
        [Range(typeof(decimal), "0.00", "999999999999.99", ErrorMessage = "PrecioCompra inválido.")]
        public decimal PrecioCompra { get; set; }

        [Required, Column(TypeName = "decimal(18,2)")]
        [Range(typeof(decimal), "0.00", "999999999999.99", ErrorMessage = "PrecioVenta inválido.")]
        public decimal PrecioVenta { get; set; }

        [Required]
        [Range(0, int.MaxValue, ErrorMessage = "Stock inválido.")]
        public int Stock { get; set; }

        public int ProveedorID { get; set; }
        public int SubcategoriaID { get; set; }
        public int CategoriaID { get; set; }

        [Required, StringLength(450)]
        public string VendedorID { get; set; } = null!;

        [ForeignKey(nameof(CategoriaID))] public virtual Categorias Categoria { get; set; } = null!;
        [ForeignKey(nameof(ProveedorID))] public virtual Proveedores Proveedor { get; set; } = null!;
        [ForeignKey(nameof(SubcategoriaID))] public virtual Subcategorias Subcategoria { get; set; } = null!;
        [ForeignKey(nameof(VendedorID))] public virtual Usuario Usuario { get; set; } = null!;

        // Legacy (no tocar)
        public virtual ICollection<ImagenesProductos> ImagenesProductos { get; set; } = new List<ImagenesProductos>();
        public virtual ICollection<MovimientosInventario> MovimientosInventario { get; set; } = new List<MovimientosInventario>();
        public virtual ICollection<DetalleVentas> DetalleVentas { get; set; } = new List<DetalleVentas>();
        public virtual ICollection<DetallesPedido> DetallesPedido { get; set; } = new List<DetallesPedido>();
        public virtual ICollection<DetallesCompra> DetallesCompra { get; set; } = new List<DetallesCompra>();
        public virtual ICollection<Reseñas> Reseñas { get; set; } = new List<Reseñas>();
        public virtual ICollection<CarritoDetalle> CarritoDetalles { get; set; } = new List<CarritoDetalle>();

        // Nuevo esquema (opcional, consistente con tu dominio)
        public virtual ICollection<ProductoImagen> Imagenes { get; set; } = new List<ProductoImagen>();
        public virtual ICollection<ProductoVariante> Variantes { get; set; } = new List<ProductoVariante>();

        // ---------- Conveniencias ----------
        [NotMapped]
        public int StockDisponible =>
            (Variantes?.Any() == true) ? Variantes.Sum(v => v.Stock) : Stock;

        [NotMapped]
        public string? ImagenPrincipalPath =>
            (Imagenes?.Any() == true
                ? Imagenes
                    .OrderByDescending(i => i.Principal)
                    .ThenBy(i => i.Orden)
                    .Select(i => i.Path)
                    .FirstOrDefault()
                : null)
            ?? ImagenPath;

        [NotMapped]
        public string ImagenPrincipalOrPlaceholder =>
            ImagenPrincipalPath ?? "/images/product-placeholder.png";

        [NotMapped]
        public IEnumerable<string> GaleriaPaths =>
            (Imagenes ?? Enumerable.Empty<ProductoImagen>())
                .OrderByDescending(i => i.Principal)
                .ThenBy(i => i.Orden)
                .Select(i => i.Path);

        [NotMapped] public bool TieneVariantes => Variantes?.Any() == true;
        [NotMapped] public bool TieneImagenes => Imagenes?.Any() == true;

        [NotMapped]
        public bool TienePrecioEnVariantes =>
            Variantes?.Any(v => v.PrecioVenta.HasValue && v.PrecioVenta.Value > 0m) == true;

        // Devuelven 0.00 cuando no hay precios en variantes (más robusto en vistas)
        [NotMapped]
        public decimal? PrecioVentaMinVariante =>
            Variantes?
                .Where(v => v.PrecioVenta.HasValue)
                .Select(v => v.PrecioVenta!.Value)
                .DefaultIfEmpty(0m)
                .Min();

        [NotMapped]
        public decimal? PrecioVentaMaxVariante =>
            Variantes?
                .Where(v => v.PrecioVenta.HasValue)
                .Select(v => v.PrecioVenta!.Value)
                .DefaultIfEmpty(0m)
                .Max();

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
