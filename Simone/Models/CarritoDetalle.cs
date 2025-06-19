using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Simone.Models
{
    /// <summary>
    /// Representa el detalle de un carrito, incluyendo el producto, la cantidad y el precio unitario.
    /// </summary>
    public class CarritoDetalle
    {
        /// <summary>
        /// Identificador �nico del detalle del carrito.
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int CarritoDetalleID { get; set; }

        /// <summary>
        /// Identificador del carrito al que pertenece este detalle.
        /// </summary>
        [Required(ErrorMessage = "El CarritoID es obligatorio.")]
        [ForeignKey(nameof(Carrito))]
        public int CarritoID { get; set; }

        /// <summary>
        /// Carrito al que pertenece el detalle.
        /// </summary>
        public virtual Carrito Carrito { get; set; } = null!;

        /// <summary>
        /// Identificador del producto.
        /// </summary>
        [Required(ErrorMessage = "El ProductoID es obligatorio.")]
        [ForeignKey(nameof(Producto))]
        public int ProductoID { get; set; }

        /// <summary>
        /// Producto asociado al detalle.
        /// </summary>
        public virtual Producto Producto { get; set; } = null;

        /// <summary>
        /// Cantidad de unidades del producto en el carrito.
        /// </summary>
        [Required(ErrorMessage = "La cantidad es obligatoria.")]
        [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser al menos 1.")]
        public int Cantidad { get; set; }

        /// <summary>
        /// Precio unitario del producto al momento de agregarlo al carrito.
        /// El setter es interno para evitar asignaciones externas inesperadas.
        /// </summary>
        [Required(ErrorMessage = "El precio es obligatorio.")]
        [Column(TypeName = "decimal(18,2)")]
        [Range(typeof(decimal), "0.01", "9999999999999", ErrorMessage = "El precio debe ser mayor que cero.")]
        [DataType(DataType.Currency)]
        public decimal Precio { get; internal set; }

        /// <summary>
        /// Fecha y hora en la que se agreg� el producto al carrito.
        /// </summary>
        [Required]
        public DateTime FechaAgregado { get; set; } = DateTime.Now;

        /// <summary>
        /// Propiedad calculada que devuelve el total de este detalle (Cantidad * Precio).
        /// No se mapea a la base de datos.
        /// </summary>
        [NotMapped]
        public decimal Total => Cantidad * Precio;

        
    }
}
