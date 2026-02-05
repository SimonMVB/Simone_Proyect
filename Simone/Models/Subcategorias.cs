using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace Simone.Models
{
    /// <summary>
    /// Subcategorías de productos
    /// Permite organización detallada dentro de cada categoría
    /// Soporta multi-vendedor (cada vendedor puede tener sus propias subcategorías)
    /// </summary>
    public class Subcategorias
    {
        // ==================== IDENTIFICACIÓN ====================

        [Key]
        public int SubcategoriaID { get; set; }

        [Required(ErrorMessage = "El nombre de la subcategoría es obligatorio.")]
        [StringLength(100, ErrorMessage = "El nombre no puede exceder 100 caracteres.")]
        [Display(Name = "Nombre")]
        public string NombreSubcategoria { get; set; } = string.Empty;

        /// <summary>
        /// Slug para URLs amigables (ej: "blusas-manga-larga")
        /// </summary>
        [StringLength(150)]
        public string? Slug { get; set; }

        /// <summary>
        /// Descripción de la subcategoría
        /// </summary>
        [StringLength(500)]
        [Display(Name = "Descripción")]
        public string? Descripcion { get; set; }

        // ==================== RELACIÓN CON CATEGORÍA ====================

        [Required(ErrorMessage = "La categoría es obligatoria.")]
        [Display(Name = "Categoría")]
        public int CategoriaID { get; set; }

        [ForeignKey(nameof(CategoriaID))]
        public virtual Categorias? Categoria { get; set; }

        // ==================== PROPIETARIO (MULTI-VENDEDOR) ====================

        /// <summary>
        /// FK -> AspNetUsers (Usuario.Id)
        /// Permite que cada vendedor tenga sus propias subcategorías
        /// NULL = subcategoría global/admin
        /// </summary>
        [StringLength(450)]
        [Display(Name = "Vendedor")]
        public string? VendedorID { get; set; }

        [ForeignKey(nameof(VendedorID))]
        public virtual Usuario? Usuario { get; set; }

        // ==================== VISUALIZACIÓN ====================

        /// <summary>
        /// Clase de ícono Font Awesome (ej: "fas fa-shirt")
        /// </summary>
        [StringLength(100)]
        public string? IconoClass { get; set; }

        /// <summary>
        /// Ruta de imagen/banner de la subcategoría
        /// </summary>
        [StringLength(300)]
        public string? ImagenPath { get; set; }

        /// <summary>
        /// Orden de visualización (menor = primero)
        /// </summary>
        [Display(Name = "Orden")]
        public int Orden { get; set; } = 0;

        // ==================== ESTADO ====================

        /// <summary>
        /// Si la subcategoría está activa
        /// </summary>
        [Display(Name = "Activo")]
        public bool Activo { get; set; } = true;

        /// <summary>
        /// Mostrar en menú/navegación
        /// </summary>
        public bool MostrarEnMenu { get; set; } = true;

        /// <summary>
        /// Subcategoría destacada
        /// </summary>
        public bool Destacada { get; set; } = false;

        // ==================== SEO ====================

        /// <summary>
        /// Meta descripción para SEO
        /// </summary>
        [StringLength(300)]
        public string? MetaDescripcion { get; set; }

        /// <summary>
        /// Palabras clave para SEO
        /// </summary>
        [StringLength(500)]
        public string? MetaKeywords { get; set; }

        // ==================== AUDITORÍA ====================

        /// <summary>
        /// Fecha de creación
        /// </summary>
        public DateTime? CreadoUtc { get; set; }

        /// <summary>
        /// Fecha de última modificación
        /// </summary>
        public DateTime? ModificadoUtc { get; set; }

        // ==================== ANALYTICS ====================

        /// <summary>
        /// Contador de vistas
        /// </summary>
        public long ViewCount { get; set; } = 0;

        // ==================== RELACIONES ====================

        /// <summary>
        /// Productos en esta subcategoría
        /// </summary>
        public virtual ICollection<Producto> Productos { get; set; } = new List<Producto>();

        // ==================== PROPIEDADES CALCULADAS ====================

        /// <summary>
        /// Nombre completo: "Categoría > Subcategoría"
        /// </summary>
        [NotMapped]
        public string NombreCompleto => Categoria != null
            ? $"{Categoria.Nombre} > {NombreSubcategoria}"
            : NombreSubcategoria;

        /// <summary>
        /// Total de productos en esta subcategoría
        /// </summary>
        [NotMapped]
        public int TotalProductos => Productos?.Count ?? 0;

        /// <summary>
        /// Total de productos activos (con stock)
        /// </summary>
        [NotMapped]
        public int TotalProductosActivos => Productos?.Count(p => p.Stock > 0) ?? 0;

        /// <summary>
        /// Tiene productos
        /// </summary>
        [NotMapped]
        public bool TieneProductos => TotalProductos > 0;

        /// <summary>
        /// URL de la subcategoría
        /// </summary>
        [NotMapped]
        public string Url => !string.IsNullOrEmpty(Slug)
            ? $"/subcategorias/{Slug}"
            : $"/subcategorias/{SubcategoriaID}";

        /// <summary>
        /// Es subcategoría de vendedor (no global)
        /// </summary>
        [NotMapped]
        public bool EsDeVendedor => !string.IsNullOrEmpty(VendedorID);

        /// <summary>
        /// Es subcategoría global (admin)
        /// </summary>
        [NotMapped]
        public bool EsGlobal => string.IsNullOrEmpty(VendedorID);

        /// <summary>
        /// Nombre del vendedor (si aplica)
        /// </summary>
        [NotMapped]
        public string? NombreVendedor => Usuario?.NombreCompleto ?? Usuario?.UserName;

        // ==================== MÉTODOS AUXILIARES ====================

        /// <summary>
        /// Generar Slug automáticamente desde el nombre
        /// </summary>
        public void GenerarSlug()
        {
            if (string.IsNullOrWhiteSpace(NombreSubcategoria))
                return;

            Slug = NombreSubcategoria
                .ToLowerInvariant()
                .Replace(" ", "-")
                .Replace("á", "a").Replace("é", "e").Replace("í", "i")
                .Replace("ó", "o").Replace("ú", "u").Replace("ñ", "n")
                .Replace("ü", "u");

            // Eliminar caracteres especiales
            Slug = System.Text.RegularExpressions.Regex.Replace(Slug, @"[^a-z0-9\-]", "");
        }

        /// <summary>
        /// Incrementar contador de vistas
        /// </summary>
        public void IncrementarVistas()
        {
            ViewCount++;
        }

        /// <summary>
        /// Actualizar fecha de modificación
        /// </summary>
        public void MarcarModificado()
        {
            ModificadoUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Clonar subcategoría (útil para duplicar)
        /// </summary>
        public Subcategorias Clonar(string? nuevoVendedorId = null)
        {
            return new Subcategorias
            {
                CategoriaID = CategoriaID,
                VendedorID = nuevoVendedorId ?? VendedorID,
                NombreSubcategoria = $"{NombreSubcategoria} (Copia)",
                Slug = $"{Slug}-copia",
                Descripcion = Descripcion,
                IconoClass = IconoClass,
                ImagenPath = ImagenPath,
                Orden = Orden + 1,
                Activo = false, // Clones inician inactivos
                MostrarEnMenu = MostrarEnMenu,
                MetaDescripcion = MetaDescripcion,
                MetaKeywords = MetaKeywords,
                CreadoUtc = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Verificar si pertenece a un vendedor específico
        /// </summary>
        public bool PerteneceAVendedor(string vendedorId)
        {
            return VendedorID == vendedorId;
        }

        /// <summary>
        /// Verificar si el usuario puede editar (es dueño o admin)
        /// </summary>
        public bool PuedeEditar(string userId, bool esAdmin)
        {
            return esAdmin || PerteneceAVendedor(userId);
        }
    }
}
