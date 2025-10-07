using System.ComponentModel.DataAnnotations;

namespace Simone.Configuration
{
    /// <summary>
    /// Regla de tarifa de envío por destino.
    /// - Si Ciudad es null o vacía, la regla aplica a TODA la provincia.
    /// - Precio en USD (0 – 9999.99).
    /// </summary>
    public class TarifaEnvioRegla
    {
        [Required, MaxLength(120)]
        public string Provincia { get; set; } = string.Empty;

        [MaxLength(120)]
        public string? Ciudad { get; set; }   // null/"" => provincia completa

        [Range(0, 9999.99)]
        [DataType(DataType.Currency)]
        public decimal Precio { get; set; }

        public bool Activo { get; set; } = true;

        [MaxLength(120)]
        public string? Nota { get; set; }
    }
}
