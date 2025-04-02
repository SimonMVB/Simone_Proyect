using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Simone.Models
{
    public class Productos
    {
        [Key]
        public int ProductoID { get; set; }

        [Required]
        public string NombreProducto { get; set; }

        [Required]
        public DateTime FechaAgregado { get; set; }

        public string? Descripcion { get; set; }
        public string? Talla { get; set; }
        public string? Color { get; set; }
        public string? Marca { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal PrecioCompra { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal PrecioVenta { get; set; }

        [Required]
        public int Stock { get; set; }

        // 🔗 Relaciones (claves foráneas)
        public int ProveedorID { get; set; }
        public int SubcategoriaID { get; set; }
        public int CategoriaID { get; set; }  // ✅ Clave foránea hacia Categoría

        // 🔄 Propiedades de navegación
        [ForeignKey("CategoriaID")]
        public virtual Categorias Categoria { get; set; }

        [ForeignKey("ProveedorID")]
        public virtual Proveedores Proveedor { get; set; }

        [ForeignKey("SubcategoriaID")]
        public virtual Subcategorias Subcategoria { get; set; }

        public virtual ICollection<ImagenesProductos> ImagenesProductos { get; set; } = new List<ImagenesProductos>();
        public virtual ICollection<MovimientosInventario> MovimientosInventario { get; set; } = new List<MovimientosInventario>();
        public virtual ICollection<DetalleVentas> DetalleVentas { get; set; } = new List<DetalleVentas>();
        public virtual ICollection<DetallesPedido> DetallesPedido { get; set; } = new List<DetallesPedido>();
        public virtual ICollection<DetallesCompra> DetallesCompra { get; set; } = new List<DetallesCompra>();
        public virtual ICollection<Reseñas> Reseñas { get; set; } = new List<Reseñas>();
        public virtual ICollection<CarritoDetalle> CarritoDetalles { get; set; } = new List<CarritoDetalle>();
    }
}
