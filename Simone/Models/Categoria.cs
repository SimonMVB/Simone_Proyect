using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace Simone.Models
{
    /// <summary>
    /// Sistema de categorías jerárquico enterprise (estilo Amazon/MercadoLibre)
    /// - Jerarquía infinita auto-referencial
    /// - SEO optimizado
    /// - Analytics integrado
    /// - Multi-idioma ready
    /// </summary>
    public class Categoria
    {
        // ==================== IDENTIFICACIÓN ====================
        [Key]
        public int CategoriaID { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio")]
        [StringLength(100)]
        public string Nombre { get; set; } = string.Empty;

        /// <summary>
        /// Slug para URLs SEO friendly (lowercase, sin espacios)
        /// Ejemplo: "vestidos-de-noche"
        /// </summary>
        [Required]
        [StringLength(150)]
        public string Slug { get; set; } = string.Empty;

        // ==================== JERARQUÍA (Auto-referencial) ====================

        /// <summary>
        /// ID del padre (null = categoría raíz)
        /// Permite infinitos niveles: Mujer > Ropa > Vestidos > Noche > Largos
        /// </summary>
        public int? CategoriaPadreID { get; set; }

        [ForeignKey(nameof(CategoriaPadreID))]
        public virtual Categoria? CategoriaPadre { get; set; }

        /// <summary>
        /// Categorías hijas (subcategorías)
        /// </summary>
        public virtual ICollection<Categoria> CategoriasHijas { get; set; } = new List<Categoria>();

        // ==================== PATH & NAVEGACIÓN ====================

        /// <summary>
        /// Path completo para queries rápidas
        /// Ejemplo: "mujer/ropa/vestidos/noche"
        /// Se genera automáticamente
        /// </summary>
        [Required]
        [StringLength(500)]
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Nivel en jerarquía (0=raíz, 1=depto, 2=cat, 3=subcat...)
        /// </summary>
        [Required]
        public int Nivel { get; set; } = 0;

        // ==================== CONTENIDO & SEO ====================

        [StringLength(2000)]
        public string? Descripcion { get; set; }

        [StringLength(300)]
        public string? MetaDescripcion { get; set; }

        [StringLength(500)]
        public string? MetaKeywords { get; set; }

        // ==================== VISUALIZACIÓN ====================

        /// <summary>
        /// Clase de ícono (Font Awesome)
        /// Ejemplo: "fa-solid fa-shirt"
        /// </summary>
        [StringLength(100)]
        public string? IconoClass { get; set; }

        /// <summary>
        /// Imagen principal (banner/header)
        /// </summary>
        [StringLength(300)]
        public string? ImagenPath { get; set; }

        /// <summary>
        /// Miniatura para navegación
        /// </summary>
        [StringLength(300)]
        public string? ImagenThumbnail { get; set; }

        // ==================== ESTADO & ORDEN ====================

        /// <summary>
        /// Orden de visualización (menor = primero)
        /// </summary>
        public int Orden { get; set; } = 0;

        /// <summary>
        /// Activa en el sitio
        /// </summary>
        public bool Activa { get; set; } = true;

        /// <summary>
        /// Mostrar en menú principal
        /// </summary>
        public bool MostrarEnMenu { get; set; } = true;

        /// <summary>
        /// Categoría destacada (home, banners)
        /// </summary>
        public bool Destacada { get; set; } = false;

        // ==================== AUDITORÍA ====================

        public DateTime CreadoUtc { get; set; } = DateTime.UtcNow;

        public DateTime? ModificadoUtc { get; set; }

        // ==================== ENTERPRISE: ANALYTICS ====================

        /// <summary>
        /// Contador de vistas (incrementar en cada visita)
        /// </summary>
        public long ViewCount { get; set; } = 0;

        /// <summary>
        /// Tasa de conversión (ventas/visitas)
        /// </summary>
        [Column(TypeName = "decimal(5,4)")]
        public decimal ConversionRate { get; set; } = 0;

        /// <summary>
        /// Es trending actualmente
        /// </summary>
        public bool Trending { get; set; } = false;

        /// <summary>
        /// Score de trending (auto-calculado)
        /// </summary>
        [Column(TypeName = "decimal(10,2)")]
        public decimal TrendingScore { get; set; } = 0;

        /// <summary>
        /// Contador de búsquedas que llegan a esta categoría
        /// </summary>
        public long SearchCount { get; set; } = 0;

        /// <summary>
        /// Última indexación en Elasticsearch
        /// </summary>
        public DateTime? LastIndexedUtc { get; set; }

        // ==================== RELACIONES ====================

        /// <summary>
        /// Productos en esta categoría
        /// </summary>
        public virtual ICollection<Producto> Productos { get; set; } = new List<Producto>();

        /// <summary>
        /// Atributos personalizados (Largo, Escote, Material, etc)
        /// </summary>
        public virtual ICollection<CategoriaAtributo> Atributos { get; set; } = new List<CategoriaAtributo>();

        // ==================== PROPIEDADES CALCULADAS ====================

        /// <summary>
        /// Nombre completo con jerarquía
        /// Ejemplo: "Mujer > Ropa > Vestidos > Noche"
        /// </summary>
        [NotMapped]
        public string NombreCompleto
        {
            get
            {
                var nombres = new List<string>();
                var actual = this;

                while (actual != null)
                {
                    nombres.Insert(0, actual.Nombre);
                    actual = actual.CategoriaPadre;
                }

                return string.Join(" > ", nombres);
            }
        }

        /// <summary>
        /// Breadcrumbs como lista
        /// </summary>
        [NotMapped]
        public IEnumerable<Categoria> Breadcrumbs
        {
            get
            {
                var breadcrumbs = new List<Categoria>();
                var actual = this;

                while (actual != null)
                {
                    breadcrumbs.Insert(0, actual);
                    actual = actual.CategoriaPadre;
                }

                return breadcrumbs;
            }
        }

        /// <summary>
        /// URL completa: /c/mujer/ropa/vestidos/noche
        /// </summary>
        [NotMapped]
        public string Url => $"/c/{Path}";

        /// <summary>
        /// Tiene categorías hijas
        /// </summary>
        [NotMapped]
        public bool TieneHijas => CategoriasHijas != null && CategoriasHijas.Any();

        /// <summary>
        /// Es raíz (no tiene padre)
        /// </summary>
        [NotMapped]
        public bool EsRaiz => !CategoriaPadreID.HasValue;

        /// <summary>
        /// Es hoja (no tiene hijas)
        /// </summary>
        [NotMapped]
        public bool EsHoja => !TieneHijas;

        /// <summary>
        /// Total de productos (incluye subcategorías)
        /// </summary>
        [NotMapped]
        public int TotalProductos
        {
            get
            {
                int total = Productos?.Count ?? 0;

                if (CategoriasHijas != null)
                {
                    foreach (var hija in CategoriasHijas)
                    {
                        total += hija.TotalProductos;
                    }
                }

                return total;
            }
        }

        // ==================== MÉTODOS AUXILIARES ====================

        /// <summary>
        /// Generar Path automáticamente basado en jerarquía
        /// Llamar antes de guardar
        /// </summary>
        public void GenerarPath()
        {
            if (CategoriaPadre != null)
            {
                Path = $"{CategoriaPadre.Path}/{Slug}";
                Nivel = CategoriaPadre.Nivel + 1;
            }
            else
            {
                Path = Slug;
                Nivel = 0;
            }
        }

        /// <summary>
        /// Obtener todas las categorías descendientes (recursivo)
        /// </summary>
        public IEnumerable<Categoria> ObtenerDescendientes()
        {
            var descendientes = new List<Categoria>();

            if (CategoriasHijas != null)
            {
                foreach (var hija in CategoriasHijas)
                {
                    descendientes.Add(hija);
                    descendientes.AddRange(hija.ObtenerDescendientes());
                }
            }

            return descendientes;
        }

        /// <summary>
        /// Obtener IDs de descendientes (para queries)
        /// </summary>
        public List<int> ObtenerIDsDescendientes()
        {
            return ObtenerDescendientes().Select(c => c.CategoriaID).ToList();
        }

        /// <summary>
        /// Obtener ancestros (padre, abuelo, etc)
        /// </summary>
        public IEnumerable<Categoria> ObtenerAncestros()
        {
            var ancestros = new List<Categoria>();
            var actual = CategoriaPadre;

            while (actual != null)
            {
                ancestros.Insert(0, actual);
                actual = actual.CategoriaPadre;
            }

            return ancestros;
        }

        /// <summary>
        /// Validar que no es su propio padre (prevenir loops)
        /// </summary>
        public bool EsJerarquiaValida()
        {
            if (!CategoriaPadreID.HasValue)
                return true;

            if (CategoriaPadreID == CategoriaID)
                return false;

            var ancestros = ObtenerAncestros();
            return !ancestros.Any(a => a.CategoriaID == CategoriaID);
        }

        /// <summary>
        /// Incrementar contador de vistas
        /// </summary>
        public void IncrementarVistas()
        {
            ViewCount++;
        }

        /// <summary>
        /// Calcular trending score
        /// Formula: (Vistas últimas 7 días * 0.6) + (Búsquedas * 0.3) + (Conversión * 0.1)
        /// </summary>
        public void CalcularTrendingScore()
        {
            // Este método se puede llamar desde un job en background
            // Por ahora dejamos la estructura
            // TrendingScore = calculatedScore;
            // Trending = TrendingScore > 100;
        }
    }
}
