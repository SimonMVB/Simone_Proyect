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
    /// Servicio para gestionar atributos de categorías
    /// ACTUALIZADO: Ahora usa Categorias (modelo fusionado)
    /// </summary>
    public class CategoriaAtributoService
    {
        private readonly TiendaDbContext _context;
        private readonly ILogger<CategoriaAtributoService> _logger;

        public CategoriaAtributoService(
            TiendaDbContext context,
            ILogger<CategoriaAtributoService> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ==================== OBTENER ATRIBUTOS ====================

        /// <summary>
        /// Obtener todos los atributos de una categoría
        /// </summary>
        public async Task<List<CategoriaAtributo>> ObtenerPorCategoriaAsync(int categoriaId)
        {
            return await _context.CategoriaAtributos
                .Include(a => a.Categoria)
                .Where(a => a.CategoriaID == categoriaId)
                .OrderBy(a => a.Grupo)
                .ThenBy(a => a.Orden)
                .ToListAsync();
        }

        /// <summary>
        /// Obtener atributo por ID
        /// </summary>
        public async Task<CategoriaAtributo?> ObtenerPorIdAsync(int id)
        {
            return await _context.CategoriaAtributos
                .Include(a => a.Categoria)
                .FirstOrDefaultAsync(a => a.AtributoID == id);
        }

        /// <summary>
        /// Obtener atributos activos de una categoría
        /// </summary>
        public async Task<List<CategoriaAtributo>> ObtenerActivosPorCategoriaAsync(int categoriaId)
        {
            return await _context.CategoriaAtributos
                .Where(a => a.CategoriaID == categoriaId && a.Activo)
                .OrderBy(a => a.Grupo)
                .ThenBy(a => a.Orden)
                .ToListAsync();
        }

        /// <summary>
        /// Obtener atributos filtrables de una categoría
        /// </summary>
        public async Task<List<CategoriaAtributo>> ObtenerFiltrablesAsync(int categoriaId)
        {
            return await _context.CategoriaAtributos
                .Where(a => a.CategoriaID == categoriaId && a.Activo && a.Filtrable)
                .OrderBy(a => a.Grupo)
                .ThenBy(a => a.Orden)
                .ToListAsync();
        }

        /// <summary>
        /// Obtener atributos obligatorios de una categoría
        /// </summary>
        public async Task<List<CategoriaAtributo>> ObtenerObligatoriosAsync(int categoriaId)
        {
            return await _context.CategoriaAtributos
                .Where(a => a.CategoriaID == categoriaId && a.Activo && a.Obligatorio)
                .OrderBy(a => a.Orden)
                .ToListAsync();
        }

        /// <summary>
        /// Obtener atributos para mostrar en ficha de producto
        /// </summary>
        public async Task<List<CategoriaAtributo>> ObtenerParaFichaAsync(int categoriaId)
        {
            return await _context.CategoriaAtributos
                .Where(a => a.CategoriaID == categoriaId && a.Activo && a.MostrarEnFicha)
                .OrderBy(a => a.Grupo)
                .ThenBy(a => a.Orden)
                .ToListAsync();
        }

        /// <summary>
        /// Obtener atributos para mostrar en tarjeta de producto
        /// </summary>
        public async Task<List<CategoriaAtributo>> ObtenerParaTarjetaAsync(int categoriaId)
        {
            return await _context.CategoriaAtributos
                .Where(a => a.CategoriaID == categoriaId && a.Activo && a.MostrarEnTarjeta)
                .OrderBy(a => a.Orden)
                .ToListAsync();
        }

        /// <summary>
        /// Obtener atributos agrupados por Grupo
        /// </summary>
        public async Task<Dictionary<string, List<CategoriaAtributo>>> ObtenerAgrupadosAsync(int categoriaId)
        {
            var atributos = await ObtenerActivosPorCategoriaAsync(categoriaId);

            return atributos
                .GroupBy(a => a.Grupo ?? "General")
                .ToDictionary(g => g.Key, g => g.ToList());
        }

        // ==================== CREAR / ACTUALIZAR / ELIMINAR ====================

        /// <summary>
        /// Crear nuevo atributo
        /// </summary>
        public async Task<(bool Exito, string Mensaje, CategoriaAtributo? Atributo)> CrearAsync(CategoriaAtributo atributo)
        {
            try
            {
                // Validar
                var (esValido, error) = ValidarAtributo(atributo);
                if (!esValido)
                    return (false, error!, null);

                // ✅ CAMBIO: Ahora usa Categorias en lugar de CategoriasEnterprise
                var categoria = await _context.Categorias
                    .FindAsync(atributo.CategoriaID);

                if (categoria == null)
                    return (false, "Categoría no encontrada", null);

                // Generar nombre técnico si no existe
                if (string.IsNullOrWhiteSpace(atributo.NombreTecnico))
                {
                    atributo.NombreTecnico = GenerarNombreTecnico(atributo.Nombre);
                }

                // Verificar nombre técnico único en la categoría
                var nombreExiste = await _context.CategoriaAtributos
                    .AnyAsync(a => a.CategoriaID == atributo.CategoriaID &&
                                   a.NombreTecnico == atributo.NombreTecnico);

                if (nombreExiste)
                    return (false, "Ya existe un atributo con ese nombre técnico en esta categoría", null);

                // Si no se especificó orden, usar el siguiente disponible
                if (atributo.Orden == 0)
                {
                    var maxOrden = await _context.CategoriaAtributos
                        .Where(a => a.CategoriaID == atributo.CategoriaID)
                        .MaxAsync(a => (int?)a.Orden) ?? 0;

                    atributo.Orden = maxOrden + 1;
                }

                // Establecer fecha de creación
                atributo.CreadoUtc = DateTime.UtcNow;

                // Guardar
                _context.CategoriaAtributos.Add(atributo);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Atributo creado: {Nombre} para categoría {CategoriaID} ({CategoriaNombre})",
                    atributo.Nombre, atributo.CategoriaID, categoria.Nombre);

                return (true, "Atributo creado exitosamente", atributo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear atributo: {Nombre}", atributo.Nombre);
                return (false, "Error al crear el atributo", null);
            }
        }

        /// <summary>
        /// Actualizar atributo existente
        /// </summary>
        public async Task<(bool Exito, string Mensaje)> ActualizarAsync(CategoriaAtributo atributo)
        {
            try
            {
                var existente = await _context.CategoriaAtributos
                    .FirstOrDefaultAsync(a => a.AtributoID == atributo.AtributoID);

                if (existente == null)
                    return (false, "Atributo no encontrado");

                // Validar
                var (esValido, error) = ValidarAtributo(atributo);
                if (!esValido)
                    return (false, error!);

                // Verificar nombre técnico único (excluyendo el actual)
                var nombreExiste = await _context.CategoriaAtributos
                    .AnyAsync(a => a.CategoriaID == atributo.CategoriaID &&
                                   a.NombreTecnico == atributo.NombreTecnico &&
                                   a.AtributoID != atributo.AtributoID);

                if (nombreExiste)
                    return (false, "Ya existe otro atributo con ese nombre técnico");

                // Actualizar campos
                existente.Nombre = atributo.Nombre;
                existente.NombreTecnico = atributo.NombreTecnico;
                existente.Descripcion = atributo.Descripcion;
                existente.TipoCampo = atributo.TipoCampo;
                existente.OpcionesJson = atributo.OpcionesJson;
                existente.Unidad = atributo.Unidad;
                existente.IconoClass = atributo.IconoClass;
                existente.Grupo = atributo.Grupo;
                existente.Orden = atributo.Orden;
                existente.Obligatorio = atributo.Obligatorio;
                existente.Filtrable = atributo.Filtrable;
                existente.MostrarEnFicha = atributo.MostrarEnFicha;
                existente.MostrarEnTarjeta = atributo.MostrarEnTarjeta;
                existente.ValorMinimo = atributo.ValorMinimo;
                existente.ValorMaximo = atributo.ValorMaximo;
                existente.PatronValidacion = atributo.PatronValidacion;
                existente.MensajeError = atributo.MensajeError;
                existente.Activo = atributo.Activo;
                existente.ModificadoUtc = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Atributo actualizado: {Nombre} (ID: {ID})",
                    existente.Nombre, existente.AtributoID);

                return (true, "Atributo actualizado exitosamente");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar atributo ID: {ID}", atributo.AtributoID);
                return (false, "Error al actualizar el atributo");
            }
        }

        /// <summary>
        /// Eliminar atributo
        /// </summary>
        public async Task<(bool Exito, string Mensaje)> EliminarAsync(int id)
        {
            try
            {
                var atributo = await _context.CategoriaAtributos
                    .Include(a => a.ValoresProductos)
                    .FirstOrDefaultAsync(a => a.AtributoID == id);

                if (atributo == null)
                    return (false, "Atributo no encontrado");

                // Verificar si tiene valores en productos
                if (atributo.ValoresProductos.Any())
                {
                    return (false, $"No se puede eliminar. Tiene {atributo.ValoresProductos.Count} valor(es) asignado(s) a productos. Primero elimine esos valores.");
                }

                _context.CategoriaAtributos.Remove(atributo);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Atributo eliminado: {Nombre} (ID: {ID})", atributo.Nombre, id);
                return (true, "Atributo eliminado exitosamente");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar atributo ID: {ID}", id);
                return (false, "Error al eliminar el atributo");
            }
        }

        /// <summary>
        /// Eliminar atributo forzando (elimina también valores en productos)
        /// </summary>
        public async Task<(bool Exito, string Mensaje)> EliminarForzadoAsync(int id)
        {
            try
            {
                var atributo = await _context.CategoriaAtributos
                    .Include(a => a.ValoresProductos)
                    .FirstOrDefaultAsync(a => a.AtributoID == id);

                if (atributo == null)
                    return (false, "Atributo no encontrado");

                // Eliminar valores primero
                if (atributo.ValoresProductos.Any())
                {
                    _context.ProductoAtributoValores.RemoveRange(atributo.ValoresProductos);
                    _logger.LogWarning("Eliminando {Count} valores de productos para atributo {ID}",
                        atributo.ValoresProductos.Count, id);
                }

                _context.CategoriaAtributos.Remove(atributo);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Atributo eliminado (forzado): {Nombre} (ID: {ID})", atributo.Nombre, id);
                return (true, "Atributo y sus valores eliminados exitosamente");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar forzado atributo ID: {ID}", id);
                return (false, "Error al eliminar el atributo");
            }
        }

        // ==================== GESTIÓN DE OPCIONES ====================

        /// <summary>
        /// Parsear opciones JSON a lista
        /// </summary>
        public List<string> ParsearOpciones(string? opcionesJson)
        {
            if (string.IsNullOrWhiteSpace(opcionesJson))
                return new List<string>();

            try
            {
                return JsonSerializer.Deserialize<List<string>>(opcionesJson) ?? new List<string>();
            }
            catch
            {
                // Intentar split por coma como fallback
                return opcionesJson.Split(',').Select(o => o.Trim()).Where(o => !string.IsNullOrEmpty(o)).ToList();
            }
        }

        /// <summary>
        /// Convertir lista de opciones a JSON
        /// </summary>
        public string SerializarOpciones(List<string> opciones)
        {
            if (opciones == null || !opciones.Any())
                return "[]";

            return JsonSerializer.Serialize(opciones);
        }

        /// <summary>
        /// Agregar opción a atributo
        /// </summary>
        public async Task<(bool Exito, string Mensaje)> AgregarOpcionAsync(int atributoId, string opcion)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(opcion))
                    return (false, "La opción no puede estar vacía");

                var atributo = await ObtenerPorIdAsync(atributoId);
                if (atributo == null)
                    return (false, "Atributo no encontrado");

                var opciones = ParsearOpciones(atributo.OpcionesJson);

                if (opciones.Contains(opcion.Trim(), StringComparer.OrdinalIgnoreCase))
                    return (false, "La opción ya existe");

                opciones.Add(opcion.Trim());
                atributo.OpcionesJson = SerializarOpciones(opciones);
                atributo.ModificadoUtc = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return (true, "Opción agregada exitosamente");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al agregar opción al atributo {ID}", atributoId);
                return (false, "Error al agregar la opción");
            }
        }

        /// <summary>
        /// Remover opción de atributo
        /// </summary>
        public async Task<(bool Exito, string Mensaje)> RemoverOpcionAsync(int atributoId, string opcion)
        {
            try
            {
                var atributo = await ObtenerPorIdAsync(atributoId);
                if (atributo == null)
                    return (false, "Atributo no encontrado");

                var opciones = ParsearOpciones(atributo.OpcionesJson);

                if (!opciones.Remove(opcion))
                    return (false, "Opción no encontrada");

                atributo.OpcionesJson = SerializarOpciones(opciones);
                atributo.ModificadoUtc = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return (true, "Opción eliminada exitosamente");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al remover opción del atributo {ID}", atributoId);
                return (false, "Error al eliminar la opción");
            }
        }

        /// <summary>
        /// Reemplazar todas las opciones
        /// </summary>
        public async Task<(bool Exito, string Mensaje)> ReemplazarOpcionesAsync(int atributoId, List<string> nuevasOpciones)
        {
            try
            {
                var atributo = await ObtenerPorIdAsync(atributoId);
                if (atributo == null)
                    return (false, "Atributo no encontrado");

                atributo.OpcionesJson = SerializarOpciones(nuevasOpciones);
                atributo.ModificadoUtc = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return (true, "Opciones actualizadas exitosamente");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al reemplazar opciones del atributo {ID}", atributoId);
                return (false, "Error al actualizar las opciones");
            }
        }

        // ==================== UTILIDADES ====================

        /// <summary>
        /// Generar nombre técnico desde nombre
        /// </summary>
        public string GenerarNombreTecnico(string nombre)
        {
            if (string.IsNullOrWhiteSpace(nombre))
                return "";

            var resultado = nombre
                .ToLowerInvariant()
                .Trim()
                .Replace(" ", "_")
                .Replace("á", "a").Replace("é", "e").Replace("í", "i")
                .Replace("ó", "o").Replace("ú", "u").Replace("ñ", "n")
                .Replace("ü", "u").Replace("Á", "a").Replace("É", "e")
                .Replace("Í", "i").Replace("Ó", "o").Replace("Ú", "u")
                .Replace("Ñ", "n");

            // Eliminar caracteres especiales
            return System.Text.RegularExpressions.Regex.Replace(resultado, @"[^a-z0-9_]", "");
        }

        /// <summary>
        /// Validar atributo
        /// </summary>
        private (bool EsValido, string? Error) ValidarAtributo(CategoriaAtributo atributo)
        {
            if (string.IsNullOrWhiteSpace(atributo.Nombre))
                return (false, "El nombre es obligatorio");

            if (atributo.Nombre.Length > 100)
                return (false, "El nombre no puede exceder 100 caracteres");

            if (string.IsNullOrWhiteSpace(atributo.NombreTecnico))
                atributo.NombreTecnico = GenerarNombreTecnico(atributo.Nombre);

            if (string.IsNullOrWhiteSpace(atributo.TipoCampo))
                return (false, "El tipo de campo es obligatorio");

            // Validar tipos de campo válidos
            var tiposValidos = new[] { "text", "textarea", "number", "range", "select", "multiselect", "checkbox", "color", "date" };
            if (!tiposValidos.Contains(atributo.TipoCampo.ToLower()))
                return (false, $"Tipo de campo no válido. Tipos permitidos: {string.Join(", ", tiposValidos)}");

            // Normalizar tipo de campo
            atributo.TipoCampo = atributo.TipoCampo.ToLower();

            // Para select y multiselect, debe tener opciones
            if (atributo.TipoCampo == "select" || atributo.TipoCampo == "multiselect")
            {
                var opciones = ParsearOpciones(atributo.OpcionesJson);
                if (opciones.Count == 0)
                    return (false, "Los campos select/multiselect deben tener al menos una opción");
            }

            // Validar rango numérico
            if (atributo.ValorMinimo.HasValue && atributo.ValorMaximo.HasValue)
            {
                if (atributo.ValorMinimo > atributo.ValorMaximo)
                    return (false, "El valor mínimo no puede ser mayor que el máximo");
            }

            return (true, null);
        }

        /// <summary>
        /// Buscar atributos por texto
        /// </summary>
        public async Task<List<CategoriaAtributo>> BuscarAsync(string termino, int? categoriaId = null)
        {
            var query = _context.CategoriaAtributos
                .Include(a => a.Categoria)
                .AsQueryable();

            if (categoriaId.HasValue)
            {
                query = query.Where(a => a.CategoriaID == categoriaId.Value);
            }

            if (!string.IsNullOrWhiteSpace(termino))
            {
                termino = termino.ToLower();
                query = query.Where(a =>
                    a.Nombre.ToLower().Contains(termino) ||
                    a.NombreTecnico.ToLower().Contains(termino) ||
                    (a.Descripcion != null && a.Descripcion.ToLower().Contains(termino)));
            }

            return await query
                .OrderBy(a => a.Categoria!.Nombre)
                .ThenBy(a => a.Grupo)
                .ThenBy(a => a.Orden)
                .ToListAsync();
        }

        /// <summary>
        /// Cambiar orden de atributos
        /// </summary>
        public async Task<(bool Exito, string Mensaje)> CambiarOrdenAsync(int atributoId, int nuevoOrden)
        {
            try
            {
                var atributo = await ObtenerPorIdAsync(atributoId);
                if (atributo == null)
                    return (false, "Atributo no encontrado");

                atributo.Orden = nuevoOrden;
                atributo.ModificadoUtc = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return (true, "Orden actualizado");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cambiar orden del atributo {ID}", atributoId);
                return (false, "Error al cambiar el orden");
            }
        }

        /// <summary>
        /// Reordenar todos los atributos de una categoría
        /// </summary>
        public async Task<(bool Exito, string Mensaje)> ReordenarAtributosAsync(int categoriaId, List<int> ordenIds)
        {
            try
            {
                var atributos = await _context.CategoriaAtributos
                    .Where(a => a.CategoriaID == categoriaId)
                    .ToListAsync();

                for (int i = 0; i < ordenIds.Count; i++)
                {
                    var atributo = atributos.FirstOrDefault(a => a.AtributoID == ordenIds[i]);
                    if (atributo != null)
                    {
                        atributo.Orden = i + 1;
                        atributo.ModificadoUtc = DateTime.UtcNow;
                    }
                }

                await _context.SaveChangesAsync();
                return (true, "Orden actualizado exitosamente");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al reordenar atributos de categoría {ID}", categoriaId);
                return (false, "Error al reordenar los atributos");
            }
        }

        /// <summary>
        /// Duplicar atributo
        /// </summary>
        public async Task<(bool Exito, string Mensaje, CategoriaAtributo? Atributo)> DuplicarAsync(int atributoId)
        {
            try
            {
                var original = await ObtenerPorIdAsync(atributoId);
                if (original == null)
                    return (false, "Atributo no encontrado", null);

                var duplicado = original.Clonar(original.CategoriaID);

                // Asegurar nombre técnico único
                var baseNombreTecnico = duplicado.NombreTecnico;
                var contador = 1;
                while (await _context.CategoriaAtributos.AnyAsync(a =>
                    a.CategoriaID == duplicado.CategoriaID &&
                    a.NombreTecnico == duplicado.NombreTecnico))
                {
                    duplicado.NombreTecnico = $"{baseNombreTecnico}_{contador++}";
                }

                _context.CategoriaAtributos.Add(duplicado);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Atributo duplicado: {Original} → {Nuevo}",
                    original.Nombre, duplicado.Nombre);

                return (true, "Atributo duplicado exitosamente", duplicado);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al duplicar atributo {ID}", atributoId);
                return (false, "Error al duplicar el atributo", null);
            }
        }

        /// <summary>
        /// Copiar atributos de una categoría a otra
        /// </summary>
        public async Task<(bool Exito, string Mensaje, int Copiados)> CopiarAtributosAsync(int categoriaOrigenId, int categoriaDestinoId)
        {
            try
            {
                // Verificar categoría destino existe
                var categoriaDestino = await _context.Categorias.FindAsync(categoriaDestinoId);
                if (categoriaDestino == null)
                    return (false, "Categoría destino no encontrada", 0);

                var atributosOrigen = await ObtenerPorCategoriaAsync(categoriaOrigenId);
                if (!atributosOrigen.Any())
                    return (false, "La categoría origen no tiene atributos", 0);

                var copiados = 0;
                foreach (var original in atributosOrigen)
                {
                    var copia = original.Clonar(categoriaDestinoId);
                    copia.Activo = true; // Activar copias

                    // Verificar nombre técnico único
                    var baseNombreTecnico = copia.NombreTecnico;
                    var contador = 1;
                    while (await _context.CategoriaAtributos.AnyAsync(a =>
                        a.CategoriaID == categoriaDestinoId &&
                        a.NombreTecnico == copia.NombreTecnico))
                    {
                        copia.NombreTecnico = $"{baseNombreTecnico}_{contador++}";
                    }

                    _context.CategoriaAtributos.Add(copia);
                    copiados++;
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Copiados {Count} atributos de categoría {Origen} a {Destino}",
                    copiados, categoriaOrigenId, categoriaDestinoId);

                return (true, $"{copiados} atributo(s) copiado(s) exitosamente", copiados);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al copiar atributos de {Origen} a {Destino}",
                    categoriaOrigenId, categoriaDestinoId);
                return (false, "Error al copiar los atributos", 0);
            }
        }

        /// <summary>
        /// Obtener estadísticas de atributos por categoría
        /// </summary>
        public async Task<(int Total, int Activos, int Obligatorios, int Filtrables)> ObtenerEstadisticasAsync(int categoriaId)
        {
            var atributos = await _context.CategoriaAtributos
                .Where(a => a.CategoriaID == categoriaId)
                .ToListAsync();

            return (
                Total: atributos.Count,
                Activos: atributos.Count(a => a.Activo),
                Obligatorios: atributos.Count(a => a.Obligatorio),
                Filtrables: atributos.Count(a => a.Filtrable)
            );
        }

        /// <summary>
        /// Toggle estado activo
        /// </summary>
        public async Task<(bool Exito, string Mensaje, bool NuevoEstado)> ToggleActivoAsync(int atributoId)
        {
            try
            {
                var atributo = await _context.CategoriaAtributos.FindAsync(atributoId);
                if (atributo == null)
                    return (false, "Atributo no encontrado", false);

                atributo.Activo = !atributo.Activo;
                atributo.ModificadoUtc = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                var estado = atributo.Activo ? "activado" : "desactivado";
                return (true, $"Atributo {estado}", atributo.Activo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al toggle activo del atributo {ID}", atributoId);
                return (false, "Error al cambiar el estado", false);
            }
        }
    }
}
