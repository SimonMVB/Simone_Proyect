using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;  // ✅ Agregar para usar [Key]

namespace Simone.Models
{
    public class Productos
    {
        [Key]  // ✅ Definir clave primaria
        public int ProductoID { get; set; }

        public string NombreProducto { get; set; }
        public DateTime FechaAgregado { get; set; }
        public string? Descripcion { get; set; }
        public string? Talla { get; set; }
        public string? Color { get; set; }
        public string? Marca { get; set; }
        public decimal PrecioCompra { get; set; }
        public decimal PrecioVenta { get; set; }
        public int ProveedorID { get; set; }
        public int SubcategoriaID { get; set; }
        public int Stock { get; set; }

        // Relaciones con otras tablas
        public Proveedores Proveedor { get; set; }
        public Subcategorias Subcategoria { get; set; }

        public ICollection<ImagenesProductos> ImagenesProductos { get; set; } = new List<ImagenesProductos>();
        public ICollection<MovimientosInventario> MovimientosInventario { get; set; } = new List<MovimientosInventario>();
        public ICollection<DetalleVentas> DetalleVentas { get; set; } = new List<DetalleVentas>();
        public ICollection<DetallesPedido> DetallesPedido { get; set; } = new List<DetallesPedido>();
        public ICollection<DetallesCompra> DetallesCompra { get; set; } = new List<DetallesCompra>();
        public ICollection<Reseñas> Reseñas { get; set; } = new List<Reseñas>();
        public ICollection<CarritoDetalle> CarritoDetalles { get; set; } = new List<CarritoDetalle>();
    }
}
