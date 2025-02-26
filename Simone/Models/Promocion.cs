using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Simone.Models
{
    public class Promocion
    {
        [Key]
        public int PromocionID { get; set; }

        [Required]
        [StringLength(50)]
        public string CodigoCupon { get; set; }

        [Required]
        [StringLength(100)]
        public string Descripcion { get; set; }

        [Required]
        public decimal Descuento { get; set; }

        public DateTime? FechaInicio { get; set; }

        public DateTime? FechaFin { get; set; }

        // Relación con CuponesUsados
        public ICollection<CuponesUsados> CuponesUsados { get; set; }
    }
}
