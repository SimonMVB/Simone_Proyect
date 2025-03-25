using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Simone.Models;

namespace Simone.Models
{
    public class CarritoDetalle
    {
        [Key]
        public int CarritoDetalleID { get; set; } // Clave primaria

        // Relación con Carrito
        [Required]
        public int CarritoID { get; set; }
        public Carrito Carrito { get; set; }

        // Relación con Producto
        [Required]
        public int ProductoID { get; set; }
        public Productos Producto { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser al menos 1.")]
        public int Cantidad { get; set; } // Cantidad del producto en el carrito

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal PrecioUnitario { get; set; } // Precio unitario al momento de agregar

        [Required]
        public DateTime FechaAgregado { get; set; } = DateTime.Now; // Fecha de agregado
    }
}

//COMPARAR CON:
//using System.ComponentModel.DataAnnotations;

//namespace Simone.Models
//{
//    public class CarritoDetalle
//    {
//        [Key]
//        public int CarritoDetalleID { get; set; }

//        public int ProductoID { get; set; }
//        public virtual Producto Producto { get; set; }

//        public int Cantidad { get; set; }
//        public decimal Precio { get; set; }

//        // Total del producto (Precio * Cantidad)
//        public decimal Total => Precio * Cantidad;
//    }
//}
