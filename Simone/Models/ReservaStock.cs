using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Simone.Models
{
    /// <summary>
    /// Reserva temporal de stock para el carrito POS.
    /// Evita vender el mismo artículo simultáneamente en digital y físico.
    /// Expira automáticamente si la venta no se confirma.
    /// </summary>
    public class ReservaStock
    {
        [Key]
        public int ReservaStockId { get; set; }

        /// <summary>Producto reservado</summary>
        [Required]
        public int ProductoID { get; set; }

        [ForeignKey(nameof(ProductoID))]
        public Producto? Producto { get; set; }

        /// <summary>Variante reservada (null si el producto no tiene variantes)</summary>
        public int? ProductoVarianteID { get; set; }

        [ForeignKey(nameof(ProductoVarianteID))]
        public ProductoVariante? Variante { get; set; }

        /// <summary>Cantidad reservada</summary>
        [Required]
        public int Cantidad { get; set; }

        /// <summary>Vendedor que tiene abierta la reserva</summary>
        [StringLength(450)]
        public string? UsuarioId { get; set; }

        /// <summary>Canal de origen: "pos" o "online"</summary>
        [StringLength(20)]
        public string Canal { get; set; } = "pos";

        /// <summary>Identificador de sesión POS (para agrupar ítems de un carrito)</summary>
        [StringLength(64)]
        public string? SesionPosId { get; set; }

        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

        /// <summary>La reserva expira automáticamente si no se confirma</summary>
        public DateTime Expiracion { get; set; } = DateTime.UtcNow.AddMinutes(30);

        /// <summary>True cuando la venta POS fue confirmada y el stock ya fue descontado</summary>
        public bool Confirmada { get; set; } = false;

        /// <summary>¿La reserva sigue activa?</summary>
        [NotMapped]
        public bool EsActiva => !Confirmada && DateTime.UtcNow < Expiracion;
    }
}
