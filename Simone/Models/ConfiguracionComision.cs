using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Simone.Models
{
    /// <summary>
    /// Configuración de comisiones - Sistema flexible
    /// Permite configurar comisiones por: Global, Vendedor, Categoría o Escalonado
    /// </summary>
    public class ConfiguracionComision
    {
        [Key]
        public int ConfiguracionId { get; set; }

        // ==================== TIPO DE CONFIGURACIÓN ====================

        /// <summary>
        /// Tipo de comisión: Global, PorVendedor, PorCategoria, Escalonado
        /// </summary>
        [Required]
        [StringLength(50)]
        public string TipoComision { get; set; } = "Global";

        // ==================== REFERENCIAS (Opcionales según tipo) ====================

        /// <summary>
        /// ID del vendedor (solo si TipoComision = "PorVendedor")
        /// </summary>
        [StringLength(450)]
        public string? VendedorId { get; set; }

        /// <summary>
        /// ID de la categoría (solo si TipoComision = "PorCategoria")
        /// </summary>
        public int? CategoriaId { get; set; }

        // ==================== PORCENTAJE ====================

        /// <summary>
        /// Porcentaje de comisión (ej: 10 = 10%)
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(5,2)")]
        [Range(0, 100)]
        public decimal Porcentaje { get; set; } = 10m;

        // ==================== ESCALONADO (Opcional) ====================

        /// <summary>
        /// Monto mínimo de ventas para aplicar este % (solo si TipoComision = "Escalonado")
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal? MontoMinimo { get; set; }

        /// <summary>
        /// Monto máximo de ventas para aplicar este % (solo si TipoComision = "Escalonado")
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal? MontoMaximo { get; set; }

        // ==================== VIGENCIA ====================

        /// <summary>
        /// Fecha desde la cual aplica esta configuración
        /// </summary>
        public DateTime FechaInicio { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Fecha hasta la cual aplica (null = vigente indefinidamente)
        /// </summary>
        public DateTime? FechaFin { get; set; }

        /// <summary>
        /// ¿Está activa esta configuración?
        /// </summary>
        public bool Activo { get; set; } = true;

        // ==================== METADATA ====================

        /// <summary>
        /// Descripción o nota sobre esta configuración
        /// </summary>
        [StringLength(500)]
        public string? Descripcion { get; set; }

        /// <summary>
        /// Fecha de creación del registro
        /// </summary>
        public DateTime CreadoUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Última modificación
        /// </summary>
        public DateTime? ModificadoUtc { get; set; }

        /// <summary>
        /// Usuario que creó/modificó
        /// </summary>
        [StringLength(450)]
        public string? CreadoPor { get; set; }

        // ==================== NAVEGACIÓN ====================

        [ForeignKey(nameof(VendedorId))]
        public virtual Usuario? Vendedor { get; set; }

        [ForeignKey(nameof(CategoriaId))]
        public virtual Categorias? Categoria { get; set; }

        // ==================== PROPIEDADES CALCULADAS ====================

        /// <summary>
        /// ¿Está vigente actualmente?
        /// </summary>
        [NotMapped]
        public bool EstaVigente =>
            Activo &&
            FechaInicio <= DateTime.UtcNow &&
            (!FechaFin.HasValue || FechaFin.Value >= DateTime.UtcNow);

        /// <summary>
        /// Descripción legible del tipo de comisión
        /// </summary>
        [NotMapped]
        public string TipoDescripcion => TipoComision switch
        {
            "Global" => "Comisión global para todos",
            "PorVendedor" => $"Comisión específica para vendedor",
            "PorCategoria" => $"Comisión por categoría",
            "Escalonado" => $"Comisión escalonada (${MontoMinimo:N0} - ${MontoMaximo:N0})",
            _ => TipoComision
        };

        /// <summary>
        /// Texto formateado del porcentaje
        /// </summary>
        [NotMapped]
        public string PorcentajeTexto => $"{Porcentaje:N2}%";
    }

    /// <summary>
    /// Tipos de comisión disponibles
    /// </summary>
    public static class TiposComision
    {
        public const string Global = "Global";
        public const string PorVendedor = "PorVendedor";
        public const string PorCategoria = "PorCategoria";
        public const string Escalonado = "Escalonado";

        public static readonly string[] Todos = { Global, PorVendedor, PorCategoria, Escalonado };
    }
}