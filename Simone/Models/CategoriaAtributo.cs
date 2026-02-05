using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text.Json;

namespace Simone.Models
{
    /// <summary>
    /// Atributos personalizados por categoría (estilo Amazon/MercadoLibre)
    /// Permite definir qué campos tiene cada categoría:
    /// - Vestidos: Largo, Escote, Manga
    /// - Zapatos: Altura Tacón, Material, Punta
    /// - Maquillaje: Acabado, Cobertura, Tono
    /// 
    /// ACTUALIZADO: Ahora apunta a Categorias (modelo fusionado)
    /// </summary>
    public class CategoriaAtributo
    {
        // ==================== IDENTIFICACIÓN ====================

        [Key]
        public int AtributoID { get; set; }

        // ==================== RELACIÓN CON CATEGORÍA ====================
        // ⚠️ CAMBIO: Ahora apunta a Categorias (fusionado) en lugar de Categoria (enterprise)

        [Required]
        [Display(Name = "Categoría")]
        public int CategoriaID { get; set; }

        /// <summary>
        /// Categoría a la que pertenece este atributo
        /// </summary>
        [ForeignKey(nameof(CategoriaID))]
        public virtual Categorias? Categoria { get; set; }

        // ==================== DEFINICIÓN DEL ATRIBUTO ====================

        /// <summary>
        /// Nombre visible del atributo
        /// Ejemplo: "Largo", "Tipo de Tacón", "Acabado"
        /// </summary>
        [Required(ErrorMessage = "El nombre es obligatorio")]
        [StringLength(100)]
        [Display(Name = "Nombre")]
        public string Nombre { get; set; } = string.Empty;

        /// <summary>
        /// Nombre técnico (para código, sin espacios)
        /// Ejemplo: "largo", "tipo_tacon", "acabado"
        /// </summary>
        [Required]
        [StringLength(100)]
        [Display(Name = "Nombre Técnico")]
        public string NombreTecnico { get; set; } = string.Empty;

        /// <summary>
        /// Descripción/ayuda para el vendedor
        /// </summary>
        [StringLength(300)]
        [Display(Name = "Descripción")]
        public string? Descripcion { get; set; }

        // ==================== TIPO DE CAMPO ====================

        /// <summary>
        /// Tipo de input en formulario:
        /// - "select": Dropdown (una opción)
        /// - "multiselect": Múltiples opciones
        /// - "text": Texto libre
        /// - "textarea": Texto largo
        /// - "number": Numérico
        /// - "range": Rango (ej: 0-100)
        /// - "color": Selector color
        /// - "checkbox": Sí/No
        /// - "date": Fecha
        /// </summary>
        [Required]
        [StringLength(50)]
        [Display(Name = "Tipo de Campo")]
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
        [Display(Name = "Unidad")]
        public string? Unidad { get; set; }

        // ==================== VALIDACIONES ====================

        /// <summary>
        /// ¿Es obligatorio al publicar producto?
        /// </summary>
        [Required]
        [Display(Name = "Obligatorio")]
        public bool Obligatorio { get; set; } = false;

        /// <summary>
        /// ¿Se puede usar para filtrar en catálogo?
        /// </summary>
        [Required]
        [Display(Name = "Filtrable")]
        public bool Filtrable { get; set; } = true;

        /// <summary>
        /// ¿Se muestra en ficha del producto?
        /// </summary>
        [Required]
        [Display(Name = "Mostrar en Ficha")]
        public bool MostrarEnFicha { get; set; } = true;

        /// <summary>
        /// ¿Se muestra en tarjeta (listado)?
        /// Solo para atributos MUY importantes como Talla/Color
        /// </summary>
        [Display(Name = "Mostrar en Tarjeta")]
        public bool MostrarEnTarjeta { get; set; } = false;

        /// <summary>
        /// Valor mínimo (para number/range)
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Valor Mínimo")]
        public decimal? ValorMinimo { get; set; }

        /// <summary>
        /// Valor máximo (para number/range)
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Valor Máximo")]
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
        [Display(Name = "Orden")]
        public int Orden { get; set; } = 0;

        /// <summary>
        /// Ícono (Font Awesome)
        /// Ejemplo: "fas fa-ruler-vertical"
        /// </summary>
        [StringLength(100)]
        [Display(Name = "Ícono")]
        public string? IconoClass { get; set; }

        /// <summary>
        /// Grupo/sección (para organizar)
        /// Ejemplo: "Especificaciones", "Dimensiones"
        /// </summary>
        [StringLength(100)]
        [Display(Name = "Grupo")]
        public string? Grupo { get; set; }

        // ==================== ESTADO ====================

        [Required]
        [Display(Name = "Activo")]
        public bool Activo { get; set; } = true;

        /// <summary>
        /// Fecha de creación
        /// </summary>
        public DateTime CreadoUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Fecha de última modificación
        /// </summary>
        public DateTime? ModificadoUtc { get; set; }

        // ==================== ANALYTICS ====================

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
                    // Intentar split por coma como fallback
                    return OpcionesJson.Split(',').Select(o => o.Trim()).ToList();
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

        /// <summary>
        /// ¿Es de tipo selección?
        /// </summary>
        [NotMapped]
        public bool EsSeleccion =>
            TipoCampo == "select" || TipoCampo == "multiselect";

        /// <summary>
        /// Nombre de la categoría
        /// </summary>
        [NotMapped]
        public string NombreCategoria => Categoria?.Nombre ?? "Sin categoría";

        /// <summary>
        /// Total de productos que usan este atributo
        /// </summary>
        [NotMapped]
        public int TotalProductos => ValoresProductos?.Count ?? 0;

        /// <summary>
        /// ¿Tiene rango definido?
        /// </summary>
        [NotMapped]
        public bool TieneRango => ValorMinimo.HasValue || ValorMaximo.HasValue;

        /// <summary>
        /// Descripción del rango (para UI)
        /// </summary>
        [NotMapped]
        public string? DescripcionRango
        {
            get
            {
                if (!TieneRango) return null;

                if (ValorMinimo.HasValue && ValorMaximo.HasValue)
                    return $"{ValorMinimo} - {ValorMaximo} {Unidad}".Trim();
                if (ValorMinimo.HasValue)
                    return $"Mín: {ValorMinimo} {Unidad}".Trim();
                if (ValorMaximo.HasValue)
                    return $"Máx: {ValorMaximo} {Unidad}".Trim();

                return null;
            }
        }

        // ==================== MÉTODOS DE VALIDACIÓN ====================

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
            return TipoCampo switch
            {
                "select" => ValidarSelect(valor),
                "multiselect" => ValidarMultiselect(valor),
                "number" or "range" => ValidarNumero(valor),
                "text" or "textarea" => ValidarTexto(valor),
                "date" => ValidarFecha(valor),
                "color" => ValidarColor(valor),
                _ => (true, null)
            };
        }

        private (bool, string?) ValidarSelect(string valor)
        {
            var opciones = OpcionesLista;
            if (opciones != null && opciones.Count > 0 && !opciones.Contains(valor))
            {
                return (false, $"'{valor}' no es una opción válida para {Nombre}");
            }
            return (true, null);
        }

        private (bool, string?) ValidarMultiselect(string valor)
        {
            var opciones = OpcionesLista;
            if (opciones == null || opciones.Count == 0)
                return (true, null);

            try
            {
                var valores = JsonSerializer.Deserialize<List<string>>(valor);
                if (valores != null)
                {
                    var invalidos = valores.Where(v => !opciones.Contains(v)).ToList();
                    if (invalidos.Any())
                    {
                        return (false, $"Opciones inválidas para {Nombre}: {string.Join(", ", invalidos)}");
                    }
                }
            }
            catch
            {
                // Si no es JSON válido, validar como valor simple
                return ValidarSelect(valor);
            }

            return (true, null);
        }

        private (bool, string?) ValidarNumero(string valor)
        {
            if (!decimal.TryParse(valor, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var numero))
            {
                return (false, $"{Nombre} debe ser un número válido");
            }

            if (ValorMinimo.HasValue && numero < ValorMinimo.Value)
            {
                return (false, MensajeError ?? $"{Nombre} debe ser mayor o igual a {ValorMinimo}");
            }

            if (ValorMaximo.HasValue && numero > ValorMaximo.Value)
            {
                return (false, MensajeError ?? $"{Nombre} debe ser menor o igual a {ValorMaximo}");
            }

            return (true, null);
        }

        private (bool, string?) ValidarTexto(string valor)
        {
            if (!string.IsNullOrWhiteSpace(PatronValidacion))
            {
                try
                {
                    if (!System.Text.RegularExpressions.Regex.IsMatch(valor, PatronValidacion))
                    {
                        return (false, MensajeError ?? $"{Nombre} tiene un formato inválido");
                    }
                }
                catch
                {
                    // Patrón inválido, ignorar validación
                }
            }
            return (true, null);
        }

        private (bool, string?) ValidarFecha(string valor)
        {
            if (!DateTime.TryParse(valor, out _))
            {
                return (false, $"{Nombre} debe ser una fecha válida");
            }
            return (true, null);
        }

        private (bool, string?) ValidarColor(string valor)
        {
            // Validar formato hex #RRGGBB o #RGB
            var colorPattern = @"^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$";
            if (!System.Text.RegularExpressions.Regex.IsMatch(valor, colorPattern))
            {
                return (false, $"{Nombre} debe ser un color válido (ej: #FF0000)");
            }
            return (true, null);
        }

        // ==================== MÉTODOS DE OPCIONES ====================

        /// <summary>
        /// Establecer opciones desde lista de strings
        /// </summary>
        public void EstablecerOpciones(List<string> opciones)
        {
            if (opciones == null || !opciones.Any())
            {
                OpcionesJson = "[]";
            }
            else
            {
                OpcionesJson = JsonSerializer.Serialize(opciones);
            }
            ModificadoUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Establecer opciones desde string separado por comas
        /// </summary>
        public void EstablecerOpcionesDesdeString(string opcionesSeparadas, char separador = ',')
        {
            if (string.IsNullOrWhiteSpace(opcionesSeparadas))
            {
                OpcionesJson = "[]";
            }
            else
            {
                var lista = opcionesSeparadas.Split(separador)
                    .Select(o => o.Trim())
                    .Where(o => !string.IsNullOrEmpty(o))
                    .ToList();
                EstablecerOpciones(lista);
            }
        }

        /// <summary>
        /// Agregar una opción a las existentes
        /// </summary>
        public bool AgregarOpcion(string opcion)
        {
            if (string.IsNullOrWhiteSpace(opcion))
                return false;

            var opciones = OpcionesLista ?? new List<string>();
            if (opciones.Contains(opcion))
                return false;

            opciones.Add(opcion.Trim());
            EstablecerOpciones(opciones);
            return true;
        }

        /// <summary>
        /// Eliminar una opción
        /// </summary>
        public bool EliminarOpcion(string opcion)
        {
            var opciones = OpcionesLista;
            if (opciones == null || !opciones.Contains(opcion))
                return false;

            opciones.Remove(opcion);
            EstablecerOpciones(opciones);
            return true;
        }

        /// <summary>
        /// Reordenar opciones
        /// </summary>
        public void ReordenarOpciones(List<string> nuevoOrden)
        {
            EstablecerOpciones(nuevoOrden);
        }

        // ==================== MÉTODOS AUXILIARES ====================

        /// <summary>
        /// Generar nombre técnico desde el nombre
        /// </summary>
        public void GenerarNombreTecnico()
        {
            if (string.IsNullOrWhiteSpace(Nombre))
                return;

            NombreTecnico = Nombre
                .ToLowerInvariant()
                .Replace(" ", "_")
                .Replace("á", "a").Replace("é", "e").Replace("í", "i")
                .Replace("ó", "o").Replace("ú", "u").Replace("ñ", "n")
                .Replace("ü", "u");

            NombreTecnico = System.Text.RegularExpressions.Regex.Replace(
                NombreTecnico, @"[^a-z0-9_]", "");
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
        /// Actualizar fecha de modificación
        /// </summary>
        public void MarcarModificado()
        {
            ModificadoUtc = DateTime.UtcNow;
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
                IconoClass = IconoClass,
                Grupo = Grupo,
                Activo = true,
                CreadoUtc = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Representación en string para debugging
        /// </summary>
        public override string ToString()
        {
            return $"{Nombre} ({TipoCampo}) - {NombreCategoria}";
        }
    }
}
