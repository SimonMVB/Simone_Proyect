using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Simone.Data;
using Simone.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Simone.Services
{
    /// <summary>
    /// Servicio para gestionar valores de atributos en productos
    /// </summary>
    public class ProductoAtributoService
    {
        private readonly TiendaDbContext _context;
        private readonly CategoriaAtributoService _atributoService;
        private readonly ILogger<ProductoAtributoService> _logger;

        public ProductoAtributoService(
            TiendaDbContext context,
            CategoriaAtributoService atributoService,
            ILogger<ProductoAtributoService> logger)
        {
            _context = context;
            _atributoService = atributoService;
            _logger = logger;
        }

        // ==================== OBTENER VALORES ====================

        /// <summary>
        /// Obtener todos los valores de un producto
        /// </summary>
        public async Task<List<ProductoAtributoValor>> ObtenerValoresPorProductoAsync(int productoId)
        {
            return await _context.ProductoAtributoValores
                .Include(v => v.Atributo)
                .Where(v => v.ProductoID == productoId)
                .OrderBy(v => v.Atributo.Orden)
                .ToListAsync();
        }

        /// <summary>
        /// Obtener valor específico de un producto+atributo
        /// </summary>
        public async Task<ProductoAtributoValor?> ObtenerValorAsync(int productoId, int atributoId)
        {
            return await _context.ProductoAtributoValores
                .Include(v => v.Atributo)
                .FirstOrDefaultAsync(v => v.ProductoID == productoId && v.AtributoID == atributoId);
        }

        /// <summary>
        /// Obtener valores de productos múltiples (para listados)
        /// </summary>
        public async Task<Dictionary<int, List<ProductoAtributoValor>>> ObtenerValoresPorProductosAsync(List<int> productoIds)
        {
            var valores = await _context.ProductoAtributoValores
                .Include(v => v.Atributo)
                .Where(v => productoIds.Contains(v.ProductoID))
                .OrderBy(v => v.Atributo.Orden)
                .ToListAsync();

            return valores
                .GroupBy(v => v.ProductoID)
                .ToDictionary(g => g.Key, g => g.ToList());
        }

        // ==================== GUARDAR VALORES ====================

        /// <summary>
        /// Guardar valores de atributos para un producto
        /// Recibe diccionario: {atributoId: valor}
        /// </summary>
        public async Task<(bool Exito, string Mensaje, List<string> Errores)> GuardarValoresAsync(
            int productoId,
            Dictionary<int, string> valores)
        {
            var errores = new List<string>();

            try
            {
                // Verificar que el producto existe
                var producto = await _context.Productos
                    .Include(p => p.Categoria)
                    .FirstOrDefaultAsync(p => p.ProductoID == productoId);

                if (producto == null)
                    return (false, "Producto no encontrado", errores);

                // Obtener atributos de la categoría
                var atributos = await _atributoService.ObtenerActivosPorCategoriaAsync(producto.CategoriaID);

                // Validar cada valor
                foreach (var atributo in atributos)
                {
                    // Verificar si el atributo es obligatorio
                    if (atributo.Obligatorio && (!valores.ContainsKey(atributo.AtributoID) ||
                        string.IsNullOrWhiteSpace(valores[atributo.AtributoID])))
                    {
                        errores.Add($"{atributo.Nombre} es obligatorio");
                        continue;
                    }

                    // Si no es obligatorio y no tiene valor, continuar
                    if (!valores.ContainsKey(atributo.AtributoID) ||
                        string.IsNullOrWhiteSpace(valores[atributo.AtributoID]))
                        continue;

                    // Validar el valor
                    var valor = valores[atributo.AtributoID];
                    var (esValido, mensajeError) = atributo.ValidarValor(valor);

                    if (!esValido)
                    {
                        errores.Add(mensajeError ?? $"Valor inválido para {atributo.Nombre}");
                    }
                }

                // Si hay errores de validación, retornar
                if (errores.Any())
                    return (false, "Errores de validación", errores);

                // Obtener valores existentes
                var valoresExistentes = await ObtenerValoresPorProductoAsync(productoId);

                // Actualizar o crear valores
                foreach (var kvp in valores)
                {
                    var atributoId = kvp.Key;
                    var valor = kvp.Value;

                    if (string.IsNullOrWhiteSpace(valor))
                        continue;

                    var valorExistente = valoresExistentes.FirstOrDefault(v => v.AtributoID == atributoId);
                    var atributo = atributos.FirstOrDefault(a => a.AtributoID == atributoId);

                    if (atributo == null)
                        continue;

                    if (valorExistente != null)
                    {
                        // Actualizar valor existente
                        valorExistente.Valor = valor;
                        valorExistente.ValorMostrable = FormatearValor(valor, atributo);
                        valorExistente.ModificadoUtc = DateTime.UtcNow;
                    }
                    else
                    {
                        // Crear nuevo valor
                        var nuevoValor = new ProductoAtributoValor
                        {
                            ProductoID = productoId,
                            AtributoID = atributoId,
                            Valor = valor,
                            ValorMostrable = FormatearValor(valor, atributo),
                            Orden = atributo.Orden
                        };
                        _context.ProductoAtributoValores.Add(nuevoValor);
                    }

                    // Incrementar contador de uso del atributo
                    atributo.IncrementarUso();
                }

                // Eliminar valores que ya no están en el diccionario
                var atributosIds = valores.Keys.ToList();
                var valoresAEliminar = valoresExistentes
                    .Where(v => !atributosIds.Contains(v.AtributoID))
                    .ToList();

                _context.ProductoAtributoValores.RemoveRange(valoresAEliminar);

                await _context.SaveChangesAsync();

                _logger.LogInformation("Valores de atributos guardados para producto {ProductoID}", productoId);
                return (true, "Valores guardados exitosamente", errores);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar valores de atributos para producto {ProductoID}", productoId);
                return (false, "Error al guardar los valores", new List<string> { ex.Message });
            }
        }

        /// <summary>
        /// Eliminar todos los valores de un producto
        /// </summary>
        public async Task<bool> EliminarValoresDeProductoAsync(int productoId)
        {
            try
            {
                var valores = await _context.ProductoAtributoValores
                    .Where(v => v.ProductoID == productoId)
                    .ToListAsync();

                _context.ProductoAtributoValores.RemoveRange(valores);
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar valores de producto {ProductoID}", productoId);
                return false;
            }
        }

        // ==================== BÚSQUEDA Y FILTRADO ====================

        /// <summary>
        /// Buscar productos por valor de atributo
        /// </summary>
        public async Task<List<int>> BuscarProductosPorAtributoAsync(int atributoId, string valor)
        {
            return await _context.ProductoAtributoValores
                .Where(v => v.AtributoID == atributoId && v.Valor == valor)
                .Select(v => v.ProductoID)
                .ToListAsync();
        }

        /// <summary>
        /// Buscar productos por múltiples filtros de atributos
        /// Diccionario: {atributoId: valores[]}
        /// </summary>
        public async Task<List<int>> BuscarProductosPorFiltrosAsync(Dictionary<int, List<string>> filtros)
        {
            if (!filtros.Any())
                return new List<int>();

            // Empezar con todos los productos
            IQueryable<int> query = _context.Productos.Select(p => p.ProductoID);

            // Aplicar cada filtro (AND logic)
            foreach (var filtro in filtros)
            {
                var atributoId = filtro.Key;
                var valores = filtro.Value;

                if (!valores.Any())
                    continue;

                // Productos que tienen alguno de los valores (OR logic dentro del mismo atributo)
                var productosConValor = _context.ProductoAtributoValores
                    .Where(v => v.AtributoID == atributoId && valores.Contains(v.Valor))
                    .Select(v => v.ProductoID);

                // Intersección (AND logic entre atributos)
                query = query.Intersect(productosConValor);
            }

            return await query.ToListAsync();
        }

        /// <summary>
        /// Obtener valores únicos de un atributo (para generar filtros)
        /// </summary>
        public async Task<List<string>> ObtenerValoresUnicosDeAtributoAsync(int atributoId)
        {
            return await _context.ProductoAtributoValores
                .Where(v => v.AtributoID == atributoId)
                .Select(v => v.Valor)
                .Distinct()
                .OrderBy(v => v)
                .ToListAsync();
        }

        /// <summary>
        /// Obtener conteo de productos por valor de atributo
        /// Para mostrar "(45)" al lado de cada opción de filtro
        /// </summary>
        public async Task<Dictionary<string, int>> ObtenerConteoPorValorAsync(int atributoId)
        {
            return await _context.ProductoAtributoValores
                .Where(v => v.AtributoID == atributoId)
                .GroupBy(v => v.Valor)
                .Select(g => new { Valor = g.Key, Conteo = g.Count() })
                .ToDictionaryAsync(x => x.Valor, x => x.Conteo);
        }

        // ==================== UTILIDADES ====================

        /// <summary>
        /// Formatear valor para mostrar en UI
        /// </summary>
        private string FormatearValor(string valor, CategoriaAtributo atributo)
        {
            if (string.IsNullOrWhiteSpace(valor))
                return string.Empty;

            switch (atributo.TipoCampo)
            {
                case "number":
                    if (decimal.TryParse(valor, out var numero))
                    {
                        var formatted = numero.ToString("N2");
                        if (!string.IsNullOrWhiteSpace(atributo.Unidad))
                            return $"{formatted} {atributo.Unidad}";
                        return formatted;
                    }
                    break;

                case "checkbox":
                    return (valor.ToLower() == "true" || valor == "1") ? "Sí" : "No";

                case "date":
                    if (DateTime.TryParse(valor, out var fecha))
                        return fecha.ToString("dd/MM/yyyy");
                    break;

                case "multiselect":
                    try
                    {
                        var lista = JsonSerializer.Deserialize<List<string>>(valor);
                        if (lista != null && lista.Any())
                            return string.Join(", ", lista);
                    }
                    catch { }
                    break;
            }

            return valor;
        }

        /// <summary>
        /// Copiar valores de un producto a otro
        /// </summary>
        public async Task<bool> CopiarValoresAsync(int productoOrigenId, int productoDestinoId)
        {
            try
            {
                var valoresOrigen = await ObtenerValoresPorProductoAsync(productoOrigenId);

                foreach (var valorOrigen in valoresOrigen)
                {
                    var nuevoValor = new ProductoAtributoValor
                    {
                        ProductoID = productoDestinoId,
                        AtributoID = valorOrigen.AtributoID,
                        Valor = valorOrigen.Valor,
                        ValorMostrable = valorOrigen.ValorMostrable,
                        Orden = valorOrigen.Orden
                    };

                    _context.ProductoAtributoValores.Add(nuevoValor);
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al copiar valores de producto {OrigenID} a {DestinoID}",
                    productoOrigenId, productoDestinoId);
                return false;
            }
        }
    }
}
