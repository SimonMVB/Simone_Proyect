using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace Simone.Models
{
    /// <summary>
    /// Sistema de categorías fusionado (Original + Enterprise)
    /// Combina la estructura simple original con las capacidades enterprise
    /// - Mantiene relación con Subcategorias (sistema original)
    /// - Agrega jerarquía opcional auto-referencial (enterprise)
    /// - SEO optimizado
    /// - Analytics integrado
    /// - Atributos dinámicos
    /// </summary>
    public class Categorias
    {
        // ==================== IDENTIFICACIÓN (Original) ====================

        [Key]
        public int CategoriaID { get; set; }

        [Required(ErrorMessage = "El nombre de la categoría es obligatorio.")]
        [StringLength(100, ErrorMessage = "El nombre no puede exceder 100 caracteres.")]
        public string Nombre { get; set; } = string.Empty;

        /// <summary>
        /// Slug para URLs SEO friendly (lowercase, sin espacios)
        /// Ejemplo: "vestidos-de-noche"
        /// </summary>
        [StringLength(150)]
        public string? Slug { get; set; }

        // ==================== JERARQUÍA OPCIONAL (Enterprise) ====================
        // Permite usar jerarquía auto-referencial SI se desea, sin romper el sistema de Subcategorias

        /// <summary>
        /// ID del padre (null = categoría raíz)
        /// Permite infinitos niveles: Mujer > Ropa > Vestidos > Noche > Largos
        /// OPCIONAL: Solo usar si se quiere jerarquía adicional
        /// </summary>
        public int? CategoriaPadreID { get; set; }

        [ForeignKey(nameof(CategoriaPadreID))]
        public virtual Categorias? CategoriaPadre { get; set; }

        /// <summary>
        /// Categorías hijas (para jerarquía auto-referencial)
        /// </summary>
        public virtual ICollection<Categorias> CategoriasHijas { get; set; } = new List<Categorias>();

        // ==================== PATH & NAVEGACIÓN (Enterprise) ====================

        /// <summary>
        /// Path completo para queries rápidas
        /// Ejemplo: "mujer/ropa/vestidos/noche"
        /// Se genera automáticamente
        /// </summary>
        [StringLength(500)]
        public string? Path { get; set; }

        /// <summary>
        /// Nivel en jerarquía (0=raíz, 1=depto, 2=cat, 3=subcat...)
        /// </summary>
        public int Nivel { get; set; } = 0;

        // ==================== CONTENIDO & SEO (Enterprise) ====================

        /// <summary>
        /// Descripción de la categoría
        /// </summary>
        [StringLength(2000)]
        public string? Descripcion { get; set; }

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

        // ==================== VISUALIZACIÓN (Enterprise) ====================

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

        // ==================== ESTADO & ORDEN (Enterprise) ====================

        /// <summary>
        /// Orden de visualización (menor = primero)
        /// </summary>
        public int Orden { get; set; } = 0;

        /// <summary>
        /// Activa en el sitio
        /// </summary>
        public bool Activo { get; set; } = true;

        /// <summary>
        /// Mostrar en menú principal
        /// </summary>
        public bool MostrarEnMenu { get; set; } = true;

        /// <summary>
        /// Categoría destacada (home, banners)
        /// </summary>
        public bool Destacada { get; set; } = false;

        // ==================== AUDITORÍA (Enterprise) ====================

        /// <summary>
        /// Fecha de creación
        /// </summary>
        public DateTime? CreadoUtc { get; set; }

        /// <summary>
        /// Fecha de última modificación
        /// </summary>
        public DateTime? ModificadoUtc { get; set; }

        // ==================== ANALYTICS (Enterprise) ====================

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

        // ==================== RELACIONES ORIGINALES ====================

        /// <summary>
        /// Subcategorías de esta categoría (SISTEMA ORIGINAL)
        /// Mantiene compatibilidad con el sistema existente
        /// </summary>
        public virtual ICollection<Subcategorias> Subcategoria { get; set; } = new List<Subcategorias>();

        /// <summary>
        /// Productos en esta categoría
        /// </summary>
        public virtual ICollection<Producto> Productos { get; set; } = new List<Producto>();

        // ==================== RELACIONES ENTERPRISE ====================

        /// <summary>
        /// Atributos personalizados (Largo, Escote, Material, etc)
        /// Para campos dinámicos estilo Amazon/MercadoLibre
        /// </summary>
        public virtual ICollection<CategoriaAtributo> Atributos { get; set; } = new List<CategoriaAtributo>();

        // ==================== PROPIEDADES CALCULADAS ====================

        /// <summary>
        /// Nombre completo con jerarquía (si usa jerarquía auto-referencial)
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
        public IEnumerable<Categorias> Breadcrumbs
        {
            get
            {
                var breadcrumbs = new List<Categorias>();
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
        public string Url => !string.IsNullOrEmpty(Path) ? $"/c/{Path}" : $"/categorias/{Slug ?? CategoriaID.ToString()}";

        /// <summary>
        /// Tiene categorías hijas (jerarquía auto-referencial)
        /// </summary>
        [NotMapped]
        public bool TieneCategoriasHijas => CategoriasHijas != null && CategoriasHijas.Any();

        /// <summary>
        /// Tiene subcategorías (sistema original)
        /// </summary>
        [NotMapped]
        public bool TieneSubcategorias => Subcategoria != null && Subcategoria.Any();

        /// <summary>
        /// Es raíz (no tiene padre en jerarquía auto-referencial)
        /// </summary>
        [NotMapped]
        public bool EsRaiz => !CategoriaPadreID.HasValue;

        /// <summary>
        /// Es hoja (no tiene hijas en jerarquía auto-referencial)
        /// </summary>
        [NotMapped]
        public bool EsHoja => !TieneCategoriasHijas;

        /// <summary>
        /// Total de subcategorías (sistema original)
        /// </summary>
        [NotMapped]
        public int TotalSubcategorias => Subcategoria?.Count ?? 0;

        /// <summary>
        /// Total de productos directos
        /// </summary>
        [NotMapped]
        public int TotalProductosDirectos => Productos?.Count ?? 0;

        /// <summary>
        /// Total de productos (incluye subcategorías y categorías hijas)
        /// </summary>
        [NotMapped]
        public int TotalProductos
        {
            get
            {
                int total = Productos?.Count ?? 0;

                // Contar productos de subcategorías (sistema original)
                if (Subcategoria != null)
                {
                    foreach (var sub in Subcategoria)
                    {
                        total += sub.Productos?.Count ?? 0;
                    }
                }

                // Contar productos de categorías hijas (sistema enterprise)
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

        /// <summary>
        /// Total de atributos configurados
        /// </summary>
        [NotMapped]
        public int TotalAtributos => Atributos?.Count ?? 0;

        // ==================== MÉTODOS AUXILIARES ====================

        /// <summary>
        /// Generar Slug automáticamente desde el nombre
        /// </summary>
        public void GenerarSlug()
        {
            if (string.IsNullOrWhiteSpace(Nombre))
                return;

            Slug = Nombre
                .ToLowerInvariant()
                .Replace(" ", "-")
                .Replace("á", "a").Replace("é", "e").Replace("í", "i")
                .Replace("ó", "o").Replace("ú", "u").Replace("ñ", "n")
                .Replace("ü", "u");

            // Eliminar caracteres especiales
            Slug = System.Text.RegularExpressions.Regex.Replace(Slug, @"[^a-z0-9\-]", "");
        }

        /// <summary>
        /// Generar Path automáticamente basado en jerarquía
        /// Llamar antes de guardar
        /// </summary>
        public void GenerarPath()
        {
            if (string.IsNullOrWhiteSpace(Slug))
                GenerarSlug();

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
        /// Obtener todas las categorías descendientes (recursivo, jerarquía enterprise)
        /// </summary>
        public IEnumerable<Categorias> ObtenerDescendientes()
        {
            var descendientes = new List<Categorias>();

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
        public IEnumerable<Categorias> ObtenerAncestros()
        {
            var ancestros = new List<Categorias>();
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
        /// Incrementar contador de búsquedas
        /// </summary>
        public void IncrementarBusquedas()
        {
            SearchCount++;
        }

        /// <summary>
        /// Calcular trending score
        /// Formula: (Vistas últimas 7 días * 0.6) + (Búsquedas * 0.3) + (Conversión * 0.1)
        /// </summary>
        public void CalcularTrendingScore()
        {
            // Este método se puede llamar desde un job en background
            // Por ahora es un placeholder para implementación futura
            // TrendingScore = calculatedScore;
            // Trending = TrendingScore > 100;
        }

        /// <summary>
        /// Actualizar fecha de modificación
        /// </summary>
        public void MarcarModificado()
        {
            ModificadoUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Clonar categoría (útil para duplicar)
        /// </summary>
        public Categorias Clonar()
        {
            return new Categorias
            {
                Nombre = $"{Nombre} (Copia)",
                Slug = $"{Slug}-copia",
                Descripcion = Descripcion,
                MetaDescripcion = MetaDescripcion,
                MetaKeywords = MetaKeywords,
                IconoClass = IconoClass,
                ImagenPath = ImagenPath,
                ImagenThumbnail = ImagenThumbnail,
                Orden = Orden + 1,
                Activo = false, // Clones inician inactivos
                MostrarEnMenu = MostrarEnMenu,
                CreadoUtc = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Verificar si la categoría tiene contenido (productos o subcategorías)
        /// </summary>
        public bool TieneContenido()
        {
            return TotalProductos > 0 || TotalSubcategorias > 0 || TieneCategoriasHijas;
        }

        /// <summary>
        /// Obtener todas las subcategorías activas (sistema original)
        /// </summary>
        public IEnumerable<Subcategorias> ObtenerSubcategoriasActivas()
        {
            return Subcategoria?.Where(s => s.Activo) ?? Enumerable.Empty<Subcategorias>();
        }
    }
}