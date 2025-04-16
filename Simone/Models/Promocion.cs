using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Simone.Models
{
    /// <summary>
    /// Representa una promoción o cupón que puede ser aplicado por un cliente.
    /// </summary>
    public class Promocion
    {
        [Key]
        public int PromocionID { get; set; }

        /// <summary>
        /// Código único del cupón.
        /// </summary>
        [Required(ErrorMessage = "El código del cupón es obligatorio.")]
        [StringLength(50, ErrorMessage = "El código del cupón no debe exceder los 50 caracteres.")]
        public string CodigoCupon { get; set; } = null!;

        /// <summary>
        /// Descripción de la promoción.
        /// </summary>
        [Required(ErrorMessage = "La descripción es obligatoria.")]
        [StringLength(100, ErrorMessage = "La descripción no debe exceder los 100 caracteres.")]
        public string Descripcion { get; set; } = null!;

        /// <summary>
        /// Valor del descuento (puede ser porcentaje o monto).
        /// </summary>
        [Required(ErrorMessage = "El descuento es obligatorio.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "El descuento debe ser mayor a 0.")]
        public decimal Descuento { get; set; }

        /// <summary>
        /// Fecha de inicio de validez de la promoción.
        /// </summary>
        public DateTime? FechaInicio { get; set; }

        /// <summary>
        /// Fecha de finalización de la promoción.
        /// </summary>
        public DateTime? FechaFin { get; set; }

        /// <summary>
        /// Relación con los cupones usados por clientes.
        /// </summary>
        public virtual ICollection<CuponesUsados> CuponesUsados { get; set; } = new List<CuponesUsados>();
    }
}
