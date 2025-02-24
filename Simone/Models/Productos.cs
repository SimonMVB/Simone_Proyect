using System;
using System.Collections.Generic;

namespace Simone.Models
{
    public class Productos
    {
        public int ProductoID { get; set; }  // Clave primaria
        public string NombreProducto { get; set; }  // Nombre del producto
        public string? Descripcion { get; set; }  // Puede ser nulo
        public string? Talla { get; set; }  // Puede ser nulo
        public string? Color { get; set; }  // Puede ser nulo
        public string? Marca { get; set; }  // Puede ser nulo
        public decimal PrecioCompra { get; set; }  // Precio de compra
        public decimal PrecioVenta { get; set; }  // Precio de venta
        public int ProveedorID { get; set; }  // Clave foránea con Proveedores
        public int SubcategoriaID { get; set; }  // Clave foránea con Subcategorias
        public int Stock { get; set; }  // Cantidad en inventario

        // Relaciones con otras tablas
        public Proveedores Proveedor { get; set; }
        public Subcategorias Subcategoria { get; set; }

        // Relación con otras entidades dependientes
        public ICollection<ImagenesProductos> ImagenesProductos { get; set; }
        public ICollection<MovimientosInventario> MovimientosInventario { get; set; }
        public ICollection<DetalleVentas> DetalleVentas { get; set; }
        public ICollection<DetallesPedido> DetallesPedido { get; set; }
        public ICollection<DetallesCompra> DetallesCompra { get; set; }
        public ICollection<Reseñas> Reseñas { get; set; }
        public object FechaAgregado { get; internal set; }
        public object CarritoDetalles { get; internal set; }
    }
}
