using System;
using System.ComponentModel.DataAnnotations.Schema;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Simone.Models
{
    public class DetallesCompra
    {
        [Key] // ✅ Definir la clave primaria
        public int DetalleCompraID { get; set; }

        public int CompraID { get; set; }  // Clave foránea con Compras
        public int ProductoID { get; set; }  // Clave foránea con Productos
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal? Subtotal { get; set; }

        // Relación con Compras
        [ForeignKey("CompraID")]
        public Compras Compra { get; set; }

        // Relación con Productos
        [ForeignKey("ProductoID")]
        public Productos Producto { get; set; }
    }
}
