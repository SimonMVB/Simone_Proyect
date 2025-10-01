using System.ComponentModel.DataAnnotations;

namespace Simone.Configuration
{
    public class CuentaBancaria
    {
        [Required, MaxLength(50)]
        public string Codigo { get; set; } = "";          // "pichincha", "guayaquil"

        [Required, MaxLength(120)]
        public string Nombre { get; set; } = "";          // "Banco Pichincha"

        [Required, MaxLength(64)]
        public string Numero { get; set; } = "";          // "2210704773"

        [Required, MaxLength(40)]
        public string Tipo { get; set; } = "Cuenta de Ahorros"; // o "Cuenta Corriente"

        [MaxLength(120)]
        public string? Titular { get; set; }              // Titular de la cuenta

        [MaxLength(20)]
        public string? Ruc { get; set; }                  // RUC/Cédula del titular

        [MaxLength(200)]
        public string? LogoPath { get; set; }             // "/images/Bancos/pichincha.webp"

        public bool Activo { get; set; } = true;
    }
}
