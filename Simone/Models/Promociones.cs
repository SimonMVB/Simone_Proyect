using System;

namespace Simone.Models
{
    public class Promociones
    {
        public int PromocionID { get; set; }  // Clave primaria
        public string? CodigoCupon { get; set; }  // Código del cupón (puede ser nulo)
        public string? Descripcion { get; set; }  // Descripción de la promoción (puede ser nulo)
        public decimal? Descuento { get; set; }  // Porcentaje o cantidad de descuento (puede ser nulo)
        public DateTime? FechaInicio { get; set; }  // Fecha de inicio de la promoción (puede ser nulo)
        public DateTime? FechaFin { get; set; }  // Fecha de finalización de la promoción (puede ser nulo)
    }
}
