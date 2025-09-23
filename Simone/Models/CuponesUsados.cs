using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Simone.Models
{
    /// <summary>
    /// Representa el uso de un cupón por parte de un usuario.
    /// </summary>
    public class CuponesUsados
    {
        // Clave compuesta: UsuarioId + PromocionID
        [Key, Column(Order = 0)]
        [Required]
        public string UsuarioId { get; set; } = default!;

        [Key, Column(Order = 1)]
        [Required]
        public int PromocionID { get; set; }

        /// <summary>Fecha en la que el cupón fue usado.</summary>
        public DateTime FechaUso { get; set; } = DateTime.UtcNow;

        /// <summary>Usuario que utilizó el cupón.</summary>
        [ForeignKey(nameof(UsuarioId))]
        public virtual Usuario Usuario { get; set; } = null!;

        /// <summary>Promoción aplicada.</summary>
        [ForeignKey(nameof(PromocionID))]
        public virtual Promocion Promocion { get; set; } = null!;
    }
}
