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
                .OrderBy(a => a.Orden)
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
                .OrderBy(a => a.Orden)
                .ToListAsync();
        }

        /// <summary>
        /// Obtener atributos filtrables de una categoría
        /// </summary>
        public async Task<List<CategoriaAtributo>> ObtenerFiltrablesAsync(int categoriaId)
        {
            return await _context.CategoriaAtributos
                .Where(a => a.CategoriaID == categoriaId && a.Activo && a.Filtrable)
                .OrderBy(a => a.Orden)
                .ToListAsync();
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

                // Verificar que la categoría existe
                var categoria = await _context.CategoriasEnterprise
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

                // Guardar
                _context.CategoriaAtributos.Add(atributo);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Atributo creado: {Nombre} para categoría {CategoriaID}",
                    atributo.Nombre, atributo.CategoriaID);

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
                existente.Activo = atributo.Activo;

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
                    return (false, $"No se puede eliminar. Tiene {atributo.ValoresProductos.Count} valor(es) en productos");
                }

                _context.CategoriaAtributos.Remove(atributo);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Atributo eliminado: ID {ID}", id);
                return (true, "Atributo eliminado exitosamente");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar atributo ID: {ID}", id);
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
                return new List<string>();
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
                var atributo = await ObtenerPorIdAsync(atributoId);
                if (atributo == null)
                    return (false, "Atributo no encontrado");

                var opciones = ParsearOpciones(atributo.OpcionesJson);

                if (opciones.Contains(opcion))
                    return (false, "La opción ya existe");

                opciones.Add(opcion);
                atributo.OpcionesJson = SerializarOpciones(opciones);

                await _context.SaveChangesAsync();
                return (true, "Opción agregada exitosamente");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al agregar opción");
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

                await _context.SaveChangesAsync();
                return (true, "Opción eliminada exitosamente");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al remover opción");
                return (false, "Error al eliminar la opción");
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

            return nombre
                .ToLowerInvariant()
                .Replace(" ", "_")
                .Replace("á", "a").Replace("é", "e").Replace("í", "i")
                .Replace("ó", "o").Replace("ú", "u").Replace("ñ", "n")
                .Replace("ü", "u");
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
            var tiposValidos = new[] { "text", "number", "select", "multiselect", "checkbox", "color", "date" };
            if (!tiposValidos.Contains(atributo.TipoCampo.ToLower()))
                return (false, "Tipo de campo no válido");

            // Para select y multiselect, debe tener opciones
            if ((atributo.TipoCampo == "select" || atributo.TipoCampo == "multiselect"))
            {
                var opciones = ParsearOpciones(atributo.OpcionesJson);
                if (opciones.Count == 0)
                    return (false, "Los campos select/multiselect deben tener opciones");
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
                    a.NombreTecnico.ToLower().Contains(termino));
            }

            return await query.OrderBy(a => a.Categoria.Nombre).ThenBy(a => a.Orden).ToListAsync();
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
                await _context.SaveChangesAsync();

                return (true, "Orden actualizado");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cambiar orden");
                return (false, "Error al cambiar el orden");
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

                var duplicado = new CategoriaAtributo
                {
                    CategoriaID = original.CategoriaID,
                    Nombre = $"{original.Nombre} (Copia)",
                    NombreTecnico = $"{original.NombreTecnico}_copia",
                    Descripcion = original.Descripcion,
                    TipoCampo = original.TipoCampo,
                    OpcionesJson = original.OpcionesJson,
                    Unidad = original.Unidad,
                    IconoClass = original.IconoClass,
                    Grupo = original.Grupo,
                    Orden = original.Orden + 1,
                    Obligatorio = original.Obligatorio,
                    Filtrable = original.Filtrable,
                    MostrarEnFicha = original.MostrarEnFicha,
                    MostrarEnTarjeta = original.MostrarEnTarjeta,
                    Activo = false // Duplicados inician inactivos
                };

                return await CrearAsync(duplicado);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al duplicar atributo");
                return (false, "Error al duplicar el atributo", null);
            }
        }
    }
}
