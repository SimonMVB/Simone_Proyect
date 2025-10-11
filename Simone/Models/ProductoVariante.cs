using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Simone.Models
{
    public class ProductoVariante
    {
        // --------------------- Clave ---------------------
        [Key]
        public int ProductoVarianteID { get; set; }

        // --------------------- FKs -----------------------
        [Required]
        public int ProductoID { get; set; }

        // --------------------- Datos de la variante ----------------
        [Required, MaxLength(50)]
        public string Color { get; set; } = null!;

        [Required, MaxLength(20)]
        public string Talla { get; set; } = null!;

        /// <summary>
        /// Overrides de precio a nivel variante (opcionales).
        /// Si es null, se usan los del Producto.
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal? PrecioCompra { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? PrecioVenta { get; set; }

        [Required]
        [Range(0, int.MaxValue, ErrorMessage = "El stock debe ser 0 o mayor.")]
        public int Stock { get; set; }

        public string? SKU { get; set; }
        public string? ImagenPath { get; set; }

        // --------------------- Navegación ----------------
        [ForeignKey(nameof(ProductoID))]
        public virtual Producto Producto { get; set; } = null!;

        // Conexiones opcionales si llevas inventario / ventas por variante
        public virtual ICollection<MovimientosInventario> MovimientosInventario { get; set; } = new List<MovimientosInventario>();
        public virtual ICollection<CarritoDetalle> CarritoDetalles { get; set; } = new List<CarritoDetalle>();
        public virtual ICollection<DetalleVentas> DetalleVentas { get; set; } = new List<DetalleVentas>();

        // --------------------- Conveniencias (no mapeadas) ----------------

        /// <summary>
        /// ¿La variante define su propio precio de venta?
        /// </summary>
        [NotMapped]
        public bool TienePrecioPropio => PrecioVenta.HasValue && PrecioVenta.Value > 0m;

        /// <summary>
        /// Precio de compra efectivo (override si existe; si no, del producto).
        /// </summary>
        [NotMapped]
        public decimal PrecioCompraEfectivo =>
            PrecioCompra ?? (Producto?.PrecioCompra ?? 0m);

        /// <summary>
        /// Precio de venta efectivo (override si existe; si no, del producto).
        /// </summary>
        [NotMapped]
        public decimal PrecioVentaEfectivo =>
            PrecioVenta ?? (Producto?.PrecioVenta ?? 0m);

        /// <summary>
        /// Margen efectivo (venta - compra) usando precios efectivos.
        /// </summary>
        [NotMapped]
        public decimal MargenEfectivo => PrecioVentaEfectivo - PrecioCompraEfectivo;

        /// <summary>
        /// Imagen a mostrar: propia si existe; de lo contrario, la principal del producto
        /// (con placeholder seguro si tampoco hay).
        /// </summary>
        [NotMapped]
        public string ImagenEfectiva =>
            !string.IsNullOrWhiteSpace(ImagenPath)
                ? ImagenPath!
                : (Producto?.ImagenPrincipalOrPlaceholder ?? "/images/product-placeholder.png");

        /// <summary>
        /// Etiqueta amigable para UI (chips, tablas, etc.)
        /// </summary>
        [NotMapped]
        public string Etiqueta => $"{Color} / {Talla}";

        /// <summary>
        /// ¿Tiene stock (>0)?
        /// </summary>
        [NotMapped]
        public bool TieneStock => Stock > 0;
    }
}
