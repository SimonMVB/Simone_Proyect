// Models/ProductoImagen.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Simone.Models
{
    public class ProductoImagen
    {
        [Key]
        public int ProductoImagenID { get; set; }

        [Required]
        public int ProductoID { get; set; }

        [Required, StringLength(300)]
        public string Path { get; set; } = null!;   // e.g. "/images/Productos/123/abc.webp"

        /// <summary>
        /// Orden relativo dentro de la galería (0..n). Útil para UI.
        /// </summary>
        public int Orden { get; set; } = 0;

        /// <summary>
        /// Marca esta imagen como principal/portada.
        /// </summary>
        public bool Principal { get; set; } = false;

        // (Opcionales) Metadatos útiles si luego quieres mostrarlos/validar
        public int? Ancho { get; set; }
        public int? Alto { get; set; }
        public long? PesoBytes { get; set; }

        [StringLength(100)]
        public string? ContentType { get; set; }    // image/jpeg, image/png, etc.

        public DateTime FechaSubidaUtc { get; set; } = DateTime.UtcNow;

        // Navegación
        [ForeignKey(nameof(ProductoID))]
        public virtual Producto Producto { get; set; } = null!;
    }
}
