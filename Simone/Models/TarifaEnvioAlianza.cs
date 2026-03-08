using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Simone.Models
{
    /// <summary>
    /// Tarifa de envío por destino (provincia/ciudad).
    /// Puede pertenecer a una Alianza O a un Vendedor individual (nunca ambos, nunca ninguno).
    /// </summary>
    public class TarifaEnvioAlianza : IValidatableObject
    {
        public int TarifaId { get; set; }

        // Una de estas dos debe tener valor (la otra null)
        public int? AlianzaId { get; set; }      // Para tarifas de alianza
        public int? VendedorId { get; set; }     // Para tiendas sin alianza

        [Required, StringLength(100)]
        public string Provincia { get; set; } = string.Empty;  // "Azuay"

        [StringLength(100)]
        public string? Ciudad { get; set; }  // "Cuenca" o null = toda la provincia

        [Column(TypeName = "decimal(18,2)")]
        public decimal PrecioBase { get; set; }  // $5.50

        [Column(TypeName = "decimal(18,2)")]
        public decimal PesoIncluidoKg { get; set; } = 1.0m;  // 1 kg incluido

        [Column(TypeName = "decimal(18,2)")]
        public decimal PrecioPorKgExtra { get; set; }  // $1.20 por kg adicional

        public int DiasEntregaEstimados { get; set; } = 3;  // 3-5 días

        public bool Activo { get; set; } = true;

        // -------- Navegación --------
        public virtual AlianzaEnvio? Alianza { get; set; }
        public virtual Vendedor? Vendedor { get; set; }

        // -------- Propiedades calculadas --------
        public bool EsDeAlianza => AlianzaId.HasValue;
        public bool EsDeVendedor => VendedorId.HasValue;
        public string DestinoCompleto => Ciudad != null ? $"{Ciudad}, {Provincia}" : $"{Provincia} (toda)";

        // -------- Validación XOR: exactamente uno de AlianzaId / VendedorId debe tener valor --------
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!AlianzaId.HasValue && !VendedorId.HasValue)
                yield return new ValidationResult(
                    "La tarifa debe estar asociada a una Alianza o a un Vendedor.",
                    new[] { nameof(AlianzaId), nameof(VendedorId) });

            if (AlianzaId.HasValue && VendedorId.HasValue)
                yield return new ValidationResult(
                    "La tarifa no puede pertenecer simultáneamente a una Alianza y a un Vendedor.",
                    new[] { nameof(AlianzaId), nameof(VendedorId) });
        }
    }
}