using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Simone.Models
{
    /// <summary>
    /// Configuración general de envío: envío gratis, descuentos, recogida en Hub.
    /// </summary>
    public class ConfiguracionEnvioTienda
    {
        public int ConfigId { get; set; }

        // Una de estas dos debe tener valor (la otra null)
        public int? AlianzaId { get; set; }
        public int? VendedorId { get; set; }

        // -------- Envío gratis --------
        [Column(TypeName = "decimal(18,2)")]
        public decimal? EnvioGratisDesde { get; set; }  // Monto mínimo ($50)

        [StringLength(50)]
        public string? EnvioGratisAplicaA { get; set; }  // "Todos", "MiProvincia", "MiCiudad"

        // -------- Descuentos por volumen --------
        [Column(TypeName = "decimal(5,2)")]
        public decimal DescuentoVolumen3 { get; set; } = 0;  // % descuento 3+ prendas

        [Column(TypeName = "decimal(5,2)")]
        public decimal DescuentoVolumen5 { get; set; } = 0;  // % descuento 5+ prendas

        [Column(TypeName = "decimal(5,2)")]
        public decimal DescuentoVolumen10 { get; set; } = 0;  // % descuento 10+ prendas

        // -------- Otros --------
        [Column(TypeName = "decimal(18,2)")]
        public decimal CargoPreparacion { get; set; } = 0;  // Cargo fijo por pedido

        public bool PermiteRecogidaHub { get; set; } = true;

        [Column(TypeName = "decimal(5,2)")]
        public decimal DescuentoRecogida { get; set; } = 100;  // 100 = gratis

        // -------- Navegación --------
        public virtual AlianzaEnvio? Alianza { get; set; }
        public virtual Vendedor? Vendedor { get; set; }

        // -------- Propiedades calculadas --------
        public bool EsDeAlianza => AlianzaId.HasValue;
        public bool TieneEnvioGratis => EnvioGratisDesde.HasValue && EnvioGratisDesde > 0;
        public bool TieneDescuentoVolumen => DescuentoVolumen3 > 0 || DescuentoVolumen5 > 0 || DescuentoVolumen10 > 0;
    }
}