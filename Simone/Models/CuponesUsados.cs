using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Simone.Models
{
    /// <summary>
    /// Representa el uso de un cupón por parte de un cliente.
    /// </summary>
    public class CuponesUsados
    {
        [Key, Column(Order = 1)]
        public int ClienteID { get; set; }

        [Key, Column(Order = 2)]
        public int PromocionID { get; set; }

        /// <summary>
        /// Fecha en la que el cupón fue usado.
        /// </summary>
        public DateTime? FechaUso { get; set; } = DateTime.Now;

        /// <summary>
        /// Cliente que utilizó el cupón.
        /// </summary>
        [ForeignKey(nameof(ClienteID))]
        public virtual Cliente Cliente { get; set; } = null!;

        /// <summary>
        /// Promoción aplicada.
        /// </summary>
        [ForeignKey(nameof(PromocionID))]
        public virtual Promocion Promocion { get; set; } = null!;
    }
}
