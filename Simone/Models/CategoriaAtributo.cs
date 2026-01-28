using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text.Json;

namespace Simone.Models
{
    /// <summary>
    /// Atributos personalizados por categoría (estilo MercadoLibre)
    /// Permite definir qué campos tiene cada categoría:
    /// - Vestidos: Largo, Escote, Manga
    /// - Zapatos: Altura Tacón, Material, Punta
    /// - Maquillaje: Acabado, Cobertura, Tono
    /// </summary>
    public class CategoriaAtributo
    {
        [Key]
        public int AtributoID { get; set; }

        // ==================== RELACIÓN CON CATEGORÍA ====================

        [Required]
        public int CategoriaID { get; set; }

        [ForeignKey(nameof(CategoriaID))]
        public virtual Categoria Categoria { get; set; } = null!;

        // ==================== DEFINICIÓN DEL ATRIBUTO ====================

        /// <summary>
        /// Nombre visible del atributo
        /// Ejemplo: "Largo", "Tipo de Tacón", "Acabado"
        /// </summary>
        [Required]
        [StringLength(100)]
        public string Nombre { get; set; } = string.Empty;

        /// <summary>
        /// Nombre técnico (para código, sin espacios)
        /// Ejemplo: "largo", "tipo_tacon", "acabado"
        /// </summary>
        [Required]
        [StringLength(100)]
        public string NombreTecnico { get; set; } = string.Empty;

        /// <summary>
        /// Descripción/ayuda para el vendedor
        /// </summary>
        [StringLength(300)]
        public string? Descripcion { get; set; }

        // ==================== TIPO DE CAMPO ====================

        /// <summary>
        /// Tipo de input en formulario:
        /// - "select": Dropdown (una opción)
        /// - "multiselect": Múltiples opciones
        /// - "text": Texto libre
        /// - "number": Numérico
        /// - "range": Rango (ej: 0-100)
        /// - "color": Selector color
        /// - "boolean": Sí/No
        /// </summary>
        [Required]
        [StringLength(50)]
        public string TipoCampo { get; set; } = "select";

        /// <summary>
        /// Opciones disponibles (JSON array)
        /// Para select/multiselect
        /// Ejemplo: ["Corto","Midi","Largo","Maxi"]
        /// </summary>
        [StringLength(2000)]
        public string? OpcionesJson { get; set; }

        /// <summary>
        /// Unidad de medida (opcional)
        /// Ejemplo: "cm", "kg", "ml"
        /// </summary>
        [StringLength(20)]
        public string? Unidad { get; set; }

        // ==================== VALIDACIONES ====================

        /// <summary>
        /// ¿Es obligatorio al publicar producto?
        /// </summary>
        [Required]
        public bool Obligatorio { get; set; } = false;

        /// <summary>
        /// ¿Se puede usar para filtrar en catálogo?
        /// </summary>
        [Required]
        public bool Filtrable { get; set; } = true;

        /// <summary>
        /// ¿Se muestra en ficha del producto?
        /// </summary>
        [Required]
        public bool MostrarEnFicha { get; set; } = true;

        /// <summary>
        /// ¿Se muestra en tarjeta (listado)?
        /// Solo para atributos MUY importantes como Talla/Color
        /// </summary>
        public bool MostrarEnTarjeta { get; set; } = false;

        /// <summary>
        /// Valor mínimo (para number/range)
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal? ValorMinimo { get; set; }

        /// <summary>
        /// Valor máximo (para number/range)
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal? ValorMaximo { get; set; }

        /// <summary>
        /// Patrón regex para validar texto
        /// </summary>
        [StringLength(200)]
        public string? PatronValidacion { get; set; }

        /// <summary>
        /// Mensaje de error personalizado
        /// </summary>
        [StringLength(200)]
        public string? MensajeError { get; set; }

        // ==================== VISUALIZACIÓN ====================

        /// <summary>
        /// Orden de visualización (menor = primero)
        /// </summary>
        [Required]
        public int Orden { get; set; } = 0;

        /// <summary>
        /// Ícono (Font Awesome)
        /// Ejemplo: "fas fa-ruler-vertical"
        /// ⚠️ CAMBIO IMPORTANTE: Se renombró de "Icono" a "IconoClass" 
        /// para compatibilidad con el servicio CategoriaAtributoService y las vistas
        /// </summary>
        [StringLength(100)]
        public string? IconoClass { get; set; }

        /// <summary>
        /// Grupo/sección (para organizar)
        /// Ejemplo: "Especificaciones", "Dimensiones"
        /// </summary>
        [StringLength(100)]
        public string? Grupo { get; set; }

        // ==================== ESTADO ====================

        [Required]
        public bool Activo { get; set; } = true;

        public DateTime CreadoUtc { get; set; } = DateTime.UtcNow;

        public DateTime? ModificadoUtc { get; set; }

        // ==================== ENTERPRISE: ANALYTICS ====================

        /// <summary>
        /// Contador: cuántos productos usan este atributo
        /// </summary>
        public long UsageCount { get; set; } = 0;

        /// <summary>
        /// Contador: cuántas veces se ha usado este filtro
        /// </summary>
        public long FilterClickCount { get; set; } = 0;

        /// <summary>
        /// ¿Fue sugerido por IA/ML?
        /// </summary>
        public bool AISuggested { get; set; } = false;

        // ==================== RELACIONES ====================

        /// <summary>
        /// Valores asignados a productos
        /// </summary>
        public virtual ICollection<ProductoAtributoValor> ValoresProductos { get; set; }
            = new List<ProductoAtributoValor>();

        // ==================== PROPIEDADES CALCULADAS ====================

        /// <summary>
        /// Obtener opciones como lista de strings
        /// </summary>
        [NotMapped]
        public List<string>? OpcionesLista
        {
            get
            {
                if (string.IsNullOrWhiteSpace(OpcionesJson))
                    return null;

                try
                {
                    return JsonSerializer.Deserialize<List<string>>(OpcionesJson);
                }
                catch
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// ¿Tiene opciones predefinidas?
        /// </summary>
        [NotMapped]
        public bool TieneOpciones => !string.IsNullOrWhiteSpace(OpcionesJson);

        /// <summary>
        /// ¿Requiere validación numérica?
        /// </summary>
        [NotMapped]
        public bool RequiereValidacionNumerica =>
            TipoCampo == "number" || TipoCampo == "range";

        // ==================== MÉTODOS AUXILIARES ====================

        /// <summary>
        /// Validar si un valor es válido para este atributo
        /// </summary>
        public (bool EsValido, string? MensajeError) ValidarValor(string? valor)
        {
            // Obligatorio
            if (Obligatorio && string.IsNullOrWhiteSpace(valor))
            {
                return (false, MensajeError ?? $"{Nombre} es obligatorio");
            }

            if (string.IsNullOrWhiteSpace(valor))
            {
                return (true, null); // Opcional y vacío = OK
            }

            // Validar según tipo
            switch (TipoCampo)
            {
                case "select":
                case "multiselect":
                    var opciones = OpcionesLista;
                    if (opciones != null && !opciones.Contains(valor))
                    {
                        return (false, $"'{valor}' no es válido para {Nombre}");
                    }
                    break;

                case "number":
                case "range":
                    if (!decimal.TryParse(valor, out var numero))
                    {
                        return (false, $"{Nombre} debe ser un número");
                    }
                    if (ValorMinimo.HasValue && numero < ValorMinimo.Value)
                    {
                        return (false, $"{Nombre} debe ser ≥ {ValorMinimo}");
                    }
                    if (ValorMaximo.HasValue && numero > ValorMaximo.Value)
                    {
                        return (false, $"{Nombre} debe ser ≤ {ValorMaximo}");
                    }
                    break;

                case "text":
                    if (!string.IsNullOrWhiteSpace(PatronValidacion))
                    {
                        if (!System.Text.RegularExpressions.Regex.IsMatch(valor, PatronValidacion))
                        {
                            return (false, MensajeError ?? $"{Nombre} tiene formato inválido");
                        }
                    }
                    break;
            }

            return (true, null);
        }

        /// <summary>
        /// Establecer opciones desde lista de strings
        /// </summary>
        public void EstablecerOpciones(List<string> opciones)
        {
            OpcionesJson = JsonSerializer.Serialize(opciones);
        }

        /// <summary>
        /// Agregar una opción a las existentes
        /// </summary>
        public void AgregarOpcion(string opcion)
        {
            var opciones = OpcionesLista ?? new List<string>();
            if (!opciones.Contains(opcion))
            {
                opciones.Add(opcion);
                EstablecerOpciones(opciones);
            }
        }

        /// <summary>
        /// Eliminar una opción
        /// </summary>
        public void EliminarOpcion(string opcion)
        {
            var opciones = OpcionesLista;
            if (opciones != null && opciones.Contains(opcion))
            {
                opciones.Remove(opcion);
                EstablecerOpciones(opciones);
            }
        }

        /// <summary>
        /// Incrementar contador de uso
        /// </summary>
        public void IncrementarUso()
        {
            UsageCount++;
        }

        /// <summary>
        /// Incrementar contador de clicks en filtro
        /// </summary>
        public void IncrementarFilterClick()
        {
            FilterClickCount++;
        }

        /// <summary>
        /// Clonar atributo para otra categoría
        /// </summary>
        public CategoriaAtributo Clonar(int nuevaCategoriaID)
        {
            return new CategoriaAtributo
            {
                CategoriaID = nuevaCategoriaID,
                Nombre = Nombre,
                NombreTecnico = NombreTecnico,
                Descripcion = Descripcion,
                TipoCampo = TipoCampo,
                OpcionesJson = OpcionesJson,
                Unidad = Unidad,
                Obligatorio = Obligatorio,
                Filtrable = Filtrable,
                MostrarEnFicha = MostrarEnFicha,
                MostrarEnTarjeta = MostrarEnTarjeta,
                ValorMinimo = ValorMinimo,
                ValorMaximo = ValorMaximo,
                PatronValidacion = PatronValidacion,
                MensajeError = MensajeError,
                Orden = Orden,
                IconoClass = IconoClass,  // ✅ CAMBIÓ: Era "Icono"
                Grupo = Grupo,
                Activo = Activo
            };
        }
    }
}
