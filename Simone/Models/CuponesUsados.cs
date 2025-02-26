using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Simone.Models
{
    public class CuponesUsados
    {
        [Key, Column(Order = 1)]
        public int ClienteID { get; set; }

        [Key, Column(Order = 2)]
        public int PromocionID { get; set; }

        public DateTime? FechaUso { get; set; }

        // Relación con Clientes
        [ForeignKey("ClienteID")]
        public Cliente Cliente { get; set; }

        // Relación con Promociones
        [ForeignKey("PromocionID")]
        public Promocion Promocion { get; set; }
    }
}
