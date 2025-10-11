using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Simone.Models
{
    /// <summary>
    /// Representa el detalle de un carrito, incluyendo el producto, la cantidad y el precio unitario.
    /// Soporta variantes (Color+Talla) mediante ProductoVarianteID.
    /// </summary>
    public class CarritoDetalle
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int CarritoDetalleID { get; set; }



        // === Carrito ===
        [Required(ErrorMessage = "El CarritoID es obligatorio.")]
        [ForeignKey(nameof(Carrito))]
        public int CarritoID { get; set; }
        public virtual Carrito Carrito { get; set; } = null!;

        // === Producto base ===
        [Required(ErrorMessage = "El ProductoID es obligatorio.")]
        public int ProductoID { get; set; }
        public virtual Producto? Producto { get; set; }

        // === NUEVO: Variante seleccionada (Color+Talla) ===
        // Nullable para migrar sin romper datos existentes; en nuevas altas, debe venir seteado cuando el producto tenga variantes.
        public int? ProductoVarianteID { get; set; }

        [ForeignKey(nameof(ProductoVarianteID))]
        public virtual ProductoVariante? Variante { get; set; }

        // === Cantidad y precio "congelado" al momento de agregar al carrito ===
        [Required(ErrorMessage = "La cantidad es obligatoria.")]
        [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser al menos 1.")]
        public int Cantidad { get; set; }

        [Required(ErrorMessage = "El precio es obligatorio.")]
        [Column(TypeName = "decimal(18,2)")]
        [Range(typeof(decimal), "0.01", "9999999999999", ErrorMessage = "El precio debe ser mayor que cero.")]
        [DataType(DataType.Currency)]
        public decimal Precio { get; internal set; }

        [Required]
        public DateTime FechaAgregado { get; set; } = DateTime.Now;

        // === Conveniencia ===
        [NotMapped]
        public decimal Total => Cantidad * Precio;
    }
}
