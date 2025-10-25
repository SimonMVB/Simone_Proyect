using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Simone.Models
{
    public class ProductoVariante
    {
        [Key]
        public int ProductoVarianteID { get; set; }

        [Required]
        public int ProductoID { get; set; }

        [Required, MaxLength(50)]
        public string Color { get; set; }

        [Required, MaxLength(20)]
        public string Talla { get; set; }

        // Opcional: override de precios por variante (si es null, usar los del Producto)
        [Column(TypeName = "decimal(18,2)")]
        public decimal? PrecioCompra { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? PrecioVenta { get; set; }

        [Required]
        public int Stock { get; set; }

        public string? SKU { get; set; }
        public string? ImagenPath { get; set; }

        // 🔄 Navegación
        [ForeignKey("ProductoID")]
        public virtual Producto Producto { get; set; }

        // Si conectas movimientos, carrito y ventas a la variante:
        public virtual ICollection<MovimientosInventario> MovimientosInventario { get; set; } = new List<MovimientosInventario>();
        public virtual ICollection<CarritoDetalle> CarritoDetalles { get; set; } = new List<CarritoDetalle>();
        public virtual ICollection<DetalleVentas> DetalleVentas { get; set; } = new List<DetalleVentas>();
    }
}
