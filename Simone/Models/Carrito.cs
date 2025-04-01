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
        public int ClienteID { get; set; }

        /// <summary>
        /// Cliente asociado al carrito.
        /// </summary>
        public virtual Cliente Cliente { get; set; } = null!;

        /// <summary>
        /// Fecha de creación del carrito.
        /// </summary>
        [Required]
        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        /// <summary>
        /// Estado del carrito (por ejemplo, "Activo", "Procesado", "Cancelado").
        /// </summary>
        [Required]
        [StringLength(50)]
        public string EstadoCarrito { get; set; } = "Activo";

        /// <summary>
        /// Colección de detalles del carrito.
        /// </summary>
        public virtual ICollection<CarritoDetalle> CarritoDetalles { get; set; } = new List<CarritoDetalle>();

    }
}

