using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Simone.Models
{
    /// <summary>
    /// Define el peso estimado por categoría de producto.
    /// Usado para calcular el costo de envío.
    /// </summary>
    public class PesoCategoria
    {
        public int PesoCategoriaId { get; set; }

        // Una de estas dos debe tener valor (la otra null)
        public int? AlianzaId { get; set; }      // Para config de alianza
        public int? VendedorId { get; set; }     // Para tiendas sin alianza

        public int CategoriaId { get; set; }

        [Column(TypeName = "decimal(18,3)")]
        public decimal PesoBaseKg { get; set; } = 0.25m;  // Peso primera unidad

        [Column(TypeName = "decimal(18,3)")]
        public decimal PesoAdicionalKg { get; set; } = 0.20m;  // Peso por unidad adicional

        // -------- Navegación --------
        public virtual AlianzaEnvio? Alianza { get; set; }
        public virtual Vendedor? Vendedor { get; set; }
        public virtual Categorias Categoria { get; set; } = null!;

        // -------- Propiedades calculadas --------
        public bool EsDeAlianza => AlianzaId.HasValue;
        public string NombreCategoria => Categoria?.Nombre ?? "Sin categoría";
    }
}