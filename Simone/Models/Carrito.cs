using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Simone.Models
{
    /// <summary>
    /// Carrito de compras perteneciente a un usuario.
    /// </summary>
    public class Carrito
    {
        /// <summary>Clave primaria del carrito.</summary>
        [Key]
        public int CarritoID { get; set; }

        /// <summary>Id del usuario (AspNetUsers.Id) propietario del carrito.</summary>
        [Required]
        [Column("ClienteID")]
        public string UsuarioId { get; set; } = default!;

        /// <summary>Fecha de creación.</summary>
        [Required]
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

        /// <summary>Estado del carrito ("En Uso", "Vacio", "Cerrado").</summary>
        [Required, StringLength(50)]
        public string EstadoCarrito { get; set; } = "Vacio";

        /// <summary>Detalles del carrito.</summary>
        public virtual ICollection<CarritoDetalle> CarritoDetalles { get; set; } = new HashSet<CarritoDetalle>();

        /// <summary>Navegación al usuario propietario.</summary>
        [ForeignKey(nameof(UsuarioId))]
        public virtual Usuario Usuario { get; set; } = null!;
    }
}
