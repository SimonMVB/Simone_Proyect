using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace Simone.Models
{
    /// <summary>
    /// Valor de un atributo para un producto específico
    /// Ejemplo: Producto "Vestido Rojo" tiene Largo="Midi", Escote="V", etc.
    /// 
    /// VERSIÓN CORREGIDA con propiedades adicionales para compatibilidad
    /// </summary>
    [Table("ProductoAtributoValores")]
    public class ProductoAtributoValor
    {
        [Key]
        public int ValorID { get; set; }

        // ==================== RELACIONES ====================

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
        /// Valor del atributo
        /// - Para text: el texto
        /// - Para number: el número como string
        /// - Para select: la opción elegida
        /// - Para multiselect: JSON array ["opcion1","opcion2"]
        /// - Para checkbox: "true" o "false"
        /// - Para color: código hex "#FF0000"
        /// - Para date: fecha ISO "2024-01-15"
        /// </summary>
        [Required]
        [Column(TypeName = "nvarchar(max)")]
        public string Valor { get; set; } = string.Empty;

        /// <summary>
        /// Valor mostrable (formateado para UI)
        /// Ejemplo: para number con unidad "cm" → "15 cm"
        /// </summary>
        [Column(TypeName = "nvarchar(500)")]
        public string? ValorMostrable { get; set; }

        // ==================== METADATA ====================

        /// <summary>
        /// Orden de visualización
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
        /// ✅ NUEVO: Alias para compatibilidad
        /// Retorna el valor formateado para mostrar
        /// </summary>
        [NotMapped]
        public string ValorFormateado
        {
            get => ObtenerValorFormateado();
        }

        /// <summary>
        /// ✅ NUEVO: Alias para compatibilidad
        /// Retorna el valor como número
        /// </summary>
        [NotMapped]
        public decimal? ValorNumerico
        {
            get => ValorComoNumero;
        }

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
                        return System.Text.Json.JsonSerializer.Deserialize<List<string>>(Valor);
                    }
                    catch
                    {
                        return null;
                    }
                }

                // Para otros tipos, retornar como lista de 1 elemento
                return new List<string> { Valor };
            }
        }

        /// <summary>
        /// Obtener valor como número (para number)
        /// </summary>
        [NotMapped]
        public decimal? ValorComoNumero
        {
            get
            {
                if (decimal.TryParse(Valor, out var numero))
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
                return Valor?.ToLower() == "true" || Valor == "1";
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

        // ==================== MÉTODOS AUXILIARES ====================

        /// <summary>
        /// Obtener valor formateado para mostrar en UI
        /// </summary>
        public string ObtenerValorFormateado()
        {
            if (!string.IsNullOrWhiteSpace(ValorMostrable))
                return ValorMostrable;

            if (Atributo == null)
                return Valor;

            // Formatear según tipo de campo
            switch (Atributo.TipoCampo)
            {
                case "number":
                    var numero = ValorComoNumero;
                    if (numero.HasValue)
                    {
                        var valorFormateado = numero.Value.ToString("N2");
                        if (!string.IsNullOrWhiteSpace(Atributo.Unidad))
                            return $"{valorFormateado} {Atributo.Unidad}";
                        return valorFormateado;
                    }
                    break;

                case "checkbox":
                    return ValorComoBooleano ? "Sí" : "No";

                case "date":
                    var fecha = ValorComoFecha;
                    if (fecha.HasValue)
                        return fecha.Value.ToString("dd/MM/yyyy");
                    break;

                case "multiselect":
                    var lista = ValorComoLista;
                    if (lista != null && lista.Any())
                        return string.Join(", ", lista);
                    break;

                case "color":
                    return $"<span style='background-color:{Valor}; padding:2px 8px; border:1px solid #ccc;'>{Valor}</span>";
            }

            return Valor;
        }

        /// <summary>
        /// Validar que el valor cumple con las reglas del atributo
        /// </summary>
        public (bool EsValido, string? MensajeError) Validar()
        {
            if (Atributo == null)
                return (false, "Atributo no cargado");

            return Atributo.ValidarValor(Valor);
        }

        /// <summary>
        /// Establecer valor desde lista (para multiselect)
        /// </summary>
        public void EstablecerValorDesdeLista(List<string> valores)
        {
            Valor = System.Text.Json.JsonSerializer.Serialize(valores);
            ValorMostrable = string.Join(", ", valores);
        }

        /// <summary>
        /// Establecer valor desde número
        /// </summary>
        public void EstablecerValorDesdeNumero(decimal numero)
        {
            Valor = numero.ToString();

            if (Atributo != null && !string.IsNullOrWhiteSpace(Atributo.Unidad))
                ValorMostrable = $"{numero} {Atributo.Unidad}";
            else
                ValorMostrable = numero.ToString();
        }

        /// <summary>
        /// Establecer valor desde booleano
        /// </summary>
        public void EstablecerValorDesdeBooleano(bool valor)
        {
            Valor = valor.ToString().ToLower();
            ValorMostrable = valor ? "Sí" : "No";
        }

        /// <summary>
        /// Establecer valor desde fecha
        /// </summary>
        public void EstablecerValorDesdeFecha(DateTime fecha)
        {
            Valor = fecha.ToString("yyyy-MM-dd");
            ValorMostrable = fecha.ToString("dd/MM/yyyy");
        }
    }
}
