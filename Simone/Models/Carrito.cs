using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Simone.Models
{
    /// <summary>
    /// Representa el carrito de compras de un cliente.
    /// </summary>
    public class Carrito
    {
        /// <summary>
        /// Clave primaria del carrito.
        /// </summary>
        [Key]
        public int CarritoID { get; set; }

        /// <summary>
        /// Identificador del cliente al que pertenece el carrito.
        /// </summary>
        [Required]
        public string ClienteID { get; set; } = "";

        /// <summary>
        /// Fecha de creación del carrito.
        /// </summary>
        [Required]
        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        /// <summary>
        /// Estado del carrito ("En Uso", "Vacio", "Cerrado").
        /// </summary>
        [Required]
        [StringLength(50)]
        public string EstadoCarrito { get; set; } = "Vacio";

        /// <summary>
        /// Colección de detalles del carrito.
        /// </summary>
        public virtual ICollection<CarritoDetalle> CarritoDetalles { get; set; } = new List<CarritoDetalle>();

        // Navigation property for the Cliente (Usuario)
        [ForeignKey("ClienteID")]
        public virtual Usuario Cliente { get; set; }
    }
}
