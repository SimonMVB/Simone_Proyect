using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text.Json;

namespace Simone.Models
{
    /// <summary>
    /// Valor de un atributo para un producto específico
    /// Ejemplo: Producto "Vestido Rojo" tiene Largo="Midi", Escote="V", etc.
    /// 
    /// Relación: Producto (1) ─── (*) ProductoAtributoValor (*) ─── (1) CategoriaAtributo
    /// </summary>
    [Table("ProductoAtributoValores")]
    public class ProductoAtributoValor
    {
        // ==================== IDENTIFICACIÓN ====================

        [Key]
        public int ValorID { get; set; }

        // ==================== RELACIÓN CON PRODUCTO ====================

        /// <summary>
        /// ID del producto al que pertenece este valor
        /// </summary>
        [Required]
        public int ProductoID { get; set; }

        /// <summary>
        /// Producto al que pertenece
        /// </summary>
        [ForeignKey(nameof(ProductoID))]
        public virtual Producto Producto { get; set; } = null!;

        // ==================== RELACIÓN CON ATRIBUTO ====================

        /// <summary>
        /// ID del atributo cuyo valor estamos guardando
        /// </summary>
        [Required]
        public int AtributoID { get; set; }

        /// <summary>
        /// Atributo al que pertenece este valor
        /// </summary>
        [ForeignKey(nameof(AtributoID))]
        public virtual CategoriaAtributo Atributo { get; set; } = null!;

        // ==================== VALOR ====================

        /// <summary>
        /// Valor del atributo almacenado como string
        /// Formatos según tipo:
        /// - text: el texto directo
        /// - number: número como string "15.5"
        /// - select: la opción elegida "Midi"
        /// - multiselect: JSON array ["opcion1","opcion2"]
        /// - checkbox: "true" o "false"
        /// - color: código hex "#FF0000"
        /// - date: fecha ISO "2024-01-15"
        /// - textarea: texto largo
        /// </summary>
        [Required]
        [Column(TypeName = "nvarchar(max)")]
        public string Valor { get; set; } = string.Empty;

        /// <summary>
        /// Valor formateado para mostrar en UI
        /// Ejemplo: para number con unidad "cm" → "15 cm"
        /// Se genera automáticamente pero puede personalizarse
        /// </summary>
        [Column(TypeName = "nvarchar(500)")]
        public string? ValorMostrable { get; set; }

        // ==================== METADATA ====================

        /// <summary>
        /// Orden de visualización (hereda del atributo si no se especifica)
        /// </summary>
        public int Orden { get; set; } = 0;

        /// <summary>
        /// Fecha de creación
        /// </summary>
        public DateTime CreadoUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Fecha de última modificación
        /// </summary>
        public DateTime? ModificadoUtc { get; set; }

        // ==================== PROPIEDADES CALCULADAS ====================

        /// <summary>
        /// Valor formateado para mostrar (alias para compatibilidad)
        /// </summary>
        [NotMapped]
        public string ValorFormateado => ObtenerValorFormateado();

        /// <summary>
        /// Valor como número (alias para compatibilidad)
        /// </summary>
        [NotMapped]
        public decimal? ValorNumerico => ValorComoNumero;

        /// <summary>
        /// Obtener valor como lista (para multiselect)
        /// </summary>
        [NotMapped]
        public List<string>? ValorComoLista
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Valor))
                    return null;

                // Si el atributo es multiselect, parsear JSON
                if (Atributo?.TipoCampo == "multiselect")
                {
                    try
                    {
                        return JsonSerializer.Deserialize<List<string>>(Valor);
                    }
                    catch
                    {
                        // Si falla el parseo, intentar split por coma
                        return Valor.Split(',').Select(v => v.Trim()).ToList();
                    }
                }

                // Para otros tipos, retornar como lista de 1 elemento
                return new List<string> { Valor };
            }
        }

        /// <summary>
        /// Obtener valor como número (para number/range)
        /// </summary>
        [NotMapped]
        public decimal? ValorComoNumero
        {
            get
            {
                if (decimal.TryParse(Valor, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var numero))
                    return numero;
                return null;
            }
        }

        /// <summary>
        /// Obtener valor como booleano (para checkbox)
        /// </summary>
        [NotMapped]
        public bool ValorComoBooleano
        {
            get
            {
                return Valor?.ToLowerInvariant() == "true"
                    || Valor == "1"
                    || Valor?.ToLowerInvariant() == "yes"
                    || Valor?.ToLowerInvariant() == "sí";
            }
        }

        /// <summary>
        /// Obtener valor como fecha (para date)
        /// </summary>
        [NotMapped]
        public DateTime? ValorComoFecha
        {
            get
            {
                if (DateTime.TryParse(Valor, out var fecha))
                    return fecha;
                return null;
            }
        }

        /// <summary>
        /// Nombre del atributo (para mostrar)
        /// </summary>
        [NotMapped]
        public string NombreAtributo => Atributo?.Nombre ?? "Atributo";

        /// <summary>
        /// Tipo de campo del atributo
        /// </summary>
        [NotMapped]
        public string TipoCampo => Atributo?.TipoCampo ?? "text";

        /// <summary>
        /// Unidad del atributo (si aplica)
        /// </summary>
        [NotMapped]
        public string? Unidad => Atributo?.Unidad;

        /// <summary>
        /// ¿El valor está vacío?
        /// </summary>
        [NotMapped]
        public bool EstaVacio => string.IsNullOrWhiteSpace(Valor);

        /// <summary>
        /// ¿Es un atributo obligatorio?
        /// </summary>
        [NotMapped]
        public bool EsObligatorio => Atributo?.Obligatorio ?? false;

        // ==================== MÉTODOS DE FORMATEO ====================

        /// <summary>
        /// Obtener valor formateado para mostrar en UI
        /// </summary>
        public string ObtenerValorFormateado()
        {
            // Si ya tenemos valor mostrable personalizado, usarlo
            if (!string.IsNullOrWhiteSpace(ValorMostrable))
                return ValorMostrable;

            // Si no hay valor, retornar vacío
            if (string.IsNullOrWhiteSpace(Valor))
                return string.Empty;

            // Si no tenemos atributo cargado, retornar valor directo
            if (Atributo == null)
                return Valor;

            // Formatear según tipo de campo
            return Atributo.TipoCampo switch
            {
                "number" or "range" => FormatearNumero(),
                "checkbox" => ValorComoBooleano ? "Sí" : "No",
                "date" => FormatearFecha(),
                "multiselect" => FormatearMultiselect(),
                "color" => FormatearColor(),
                _ => Valor
            };
        }

        private string FormatearNumero()
        {
            var numero = ValorComoNumero;
            if (!numero.HasValue)
                return Valor;

            var valorFormateado = numero.Value % 1 == 0
                ? numero.Value.ToString("N0")
                : numero.Value.ToString("N2");

            if (!string.IsNullOrWhiteSpace(Atributo?.Unidad))
                return $"{valorFormateado} {Atributo.Unidad}";

            return valorFormateado;
        }

        private string FormatearFecha()
        {
            var fecha = ValorComoFecha;
            return fecha?.ToString("dd/MM/yyyy") ?? Valor;
        }

        private string FormatearMultiselect()
        {
            var lista = ValorComoLista;
            return lista != null && lista.Any()
                ? string.Join(", ", lista)
                : Valor;
        }

        private string FormatearColor()
        {
            // Para UI HTML
            return $"<span style='display:inline-flex;align-items:center;gap:4px;'>" +
                   $"<span style='background-color:{Valor};width:16px;height:16px;border-radius:3px;border:1px solid #ccc;display:inline-block;'></span>" +
                   $"{Valor}</span>";
        }

        /// <summary>
        /// Obtener valor para HTML (seguro para inyección)
        /// </summary>
        public string ObtenerValorParaHtml()
        {
            if (Atributo?.TipoCampo == "color")
                return FormatearColor();

            return System.Net.WebUtility.HtmlEncode(ObtenerValorFormateado());
        }

        // ==================== MÉTODOS DE VALIDACIÓN ====================

        /// <summary>
        /// Validar que el valor cumple con las reglas del atributo
        /// </summary>
        public (bool EsValido, string? MensajeError) Validar()
        {
            if (Atributo == null)
                return (false, "Atributo no cargado");

            // Si es obligatorio y está vacío
            if (Atributo.Obligatorio && string.IsNullOrWhiteSpace(Valor))
                return (false, $"{Atributo.Nombre} es obligatorio");

            // Validar usando el método del atributo
            return Atributo.ValidarValor(Valor);
        }

        /// <summary>
        /// Verificar si el valor es válido (sin mensaje)
        /// </summary>
        public bool EsValido()
        {
            var (esValido, _) = Validar();
            return esValido;
        }

        // ==================== MÉTODOS DE ESTABLECIMIENTO ====================

        /// <summary>
        /// Establecer valor desde string (genérico)
        /// </summary>
        public void EstablecerValor(string valor)
        {
            Valor = valor?.Trim() ?? string.Empty;
            ValorMostrable = null; // Regenerar al obtener
            ModificadoUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Establecer valor desde lista (para multiselect)
        /// </summary>
        public void EstablecerValorDesdeLista(List<string> valores)
        {
            if (valores == null || !valores.Any())
            {
                Valor = "[]";
                ValorMostrable = string.Empty;
            }
            else
            {
                Valor = JsonSerializer.Serialize(valores);
                ValorMostrable = string.Join(", ", valores);
            }
            ModificadoUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Establecer valor desde número
        /// </summary>
        public void EstablecerValorDesdeNumero(decimal numero)
        {
            Valor = numero.ToString(System.Globalization.CultureInfo.InvariantCulture);

            if (Atributo != null && !string.IsNullOrWhiteSpace(Atributo.Unidad))
                ValorMostrable = $"{numero} {Atributo.Unidad}";
            else
                ValorMostrable = numero.ToString();

            ModificadoUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Establecer valor desde booleano
        /// </summary>
        public void EstablecerValorDesdeBooleano(bool valor)
        {
            Valor = valor.ToString().ToLowerInvariant();
            ValorMostrable = valor ? "Sí" : "No";
            ModificadoUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Establecer valor desde fecha
        /// </summary>
        public void EstablecerValorDesdeFecha(DateTime fecha)
        {
            Valor = fecha.ToString("yyyy-MM-dd");
            ValorMostrable = fecha.ToString("dd/MM/yyyy");
            ModificadoUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Establecer valor desde color (hex)
        /// </summary>
        public void EstablecerValorDesdeColor(string colorHex)
        {
            // Asegurar formato #RRGGBB
            if (!string.IsNullOrWhiteSpace(colorHex) && !colorHex.StartsWith("#"))
                colorHex = "#" + colorHex;

            Valor = colorHex ?? string.Empty;
            ValorMostrable = colorHex;
            ModificadoUtc = DateTime.UtcNow;
        }

        // ==================== MÉTODOS AUXILIARES ====================

        /// <summary>
        /// Clonar para otro producto
        /// </summary>
        public ProductoAtributoValor Clonar(int nuevoProductoId)
        {
            return new ProductoAtributoValor
            {
                ProductoID = nuevoProductoId,
                AtributoID = AtributoID,
                Valor = Valor,
                ValorMostrable = ValorMostrable,
                Orden = Orden,
                CreadoUtc = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Actualizar fecha de modificación
        /// </summary>
        public void MarcarModificado()
        {
            ModificadoUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Representación en string para debugging
        /// </summary>
        public override string ToString()
        {
            return $"{NombreAtributo}: {ValorFormateado}";
        }
    }
}
