using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Simone.Data;
using Simone.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Simone.Services
{
    /// <summary>
    /// Servicio para gestionar categorías enterprise con jerarquía infinita
    /// </summary>
    public class CategoriaEnterpriseService
    {
        private readonly TiendaDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<CategoriaEnterpriseService> _logger;

        private const string CACHE_KEY_ALL = "categorias_all";
        private const string CACHE_KEY_TREE = "categorias_tree";
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(30);

        public CategoriaEnterpriseService(
            TiendaDbContext context,
            IMemoryCache cache,
            ILogger<CategoriaEnterpriseService> logger)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
        }

        // ==================== OBTENER CATEGORÍAS ====================

        /// <summary>
        /// Obtener todas las categorías (con cache)
        /// </summary>
        public async Task<List<Categoria>> ObtenerTodasAsync()
        {
            return await _cache.GetOrCreateAsync(CACHE_KEY_ALL, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = _cacheExpiration;
                return await _context.CategoriasEnterprise
                    .AsNoTracking()
                    .OrderBy(c => c.Path)
                    .ToListAsync();
            }) ?? new List<Categoria>();
        }

        /// <summary>
        /// Obtener categorías activas
        /// </summary>
        public async Task<List<Categoria>> ObtenerActivasAsync()
        {
            var todas = await ObtenerTodasAsync();
            return todas.Where(c => c.Activa).ToList();
        }

        /// <summary>
        /// Obtener categoría por ID
        /// </summary>
        public async Task<Categoria?> ObtenerPorIdAsync(int id)
        {
            return await _context.CategoriasEnterprise
                .Include(c => c.CategoriaPadre)
                .Include(c => c.Atributos)
                .FirstOrDefaultAsync(c => c.CategoriaID == id);
        }

        /// <summary>
        /// Obtener categorías raíz (nivel 0)
        /// </summary>
        public async Task<List<Categoria>> ObtenerRaicesAsync()
        {
            return await _context.CategoriasEnterprise
                .Where(c => c.CategoriaPadreID == null && c.Activa)
                .OrderBy(c => c.Orden)
                .ToListAsync();
        }

        /// <summary>
        /// Obtener hijas directas
        /// </summary>
        public async Task<List<Categoria>> ObtenerHijasAsync(int categoriaId)
        {
            return await _context.CategoriasEnterprise
                .Where(c => c.CategoriaPadreID == categoriaId && c.Activa)
                .OrderBy(c => c.Orden)
                .ToListAsync();
        }

        /// <summary>
        /// Obtener árbol completo (con cache)
        /// </summary>
        public async Task<List<Categoria>> ObtenerArbolAsync()
        {
            return await _cache.GetOrCreateAsync(CACHE_KEY_TREE, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = _cacheExpiration;

                var todas = await ObtenerTodasAsync();
                var raices = todas.Where(c => c.CategoriaPadreID == null).OrderBy(c => c.Orden).ToList();

                foreach (var raiz in raices)
                {
                    CargarHijasRecursivo(raiz, todas);
                }

                return raices;
            }) ?? new List<Categoria>();
        }

        /// <summary>
        /// Cargar hijas recursivamente
        /// </summary>
        private void CargarHijasRecursivo(Categoria categoria, List<Categoria> todas)
        {
            var hijas = todas
                .Where(c => c.CategoriaPadreID == categoria.CategoriaID)
                .OrderBy(c => c.Orden)
                .ToList();

            categoria.CategoriasHijas = hijas;

            foreach (var hija in hijas)
            {
                CargarHijasRecursivo(hija, todas);
            }
        }

        // ==================== CREAR / ACTUALIZAR / ELIMINAR ====================

        /// <summary>
        /// Crear categoría
        /// </summary>
        public async Task<(bool Exito, string Mensaje, Categoria? Categoria)> CrearAsync(Categoria categoria)
        {
            try
            {
                // Validar
                if (string.IsNullOrWhiteSpace(categoria.Nombre))
                    return (false, "El nombre es obligatorio", null);

                // Generar slug
                if (string.IsNullOrWhiteSpace(categoria.Slug))
                    categoria.Slug = GenerarSlug(categoria.Nombre);

                // Verificar slug único
                var existe = await _context.CategoriasEnterprise
                    .AnyAsync(c => c.Slug == categoria.Slug && c.CategoriaPadreID == categoria.CategoriaPadreID);

                if (existe)
                    return (false, "Ya existe una categoría con ese nombre en el mismo nivel", null);

                // Establecer nivel
                if (categoria.CategoriaPadreID.HasValue)
                {
                    var padre = await ObtenerPorIdAsync(categoria.CategoriaPadreID.Value);
                    if (padre == null)
                        return (false, "Categoría padre no encontrada", null);

                    categoria.Nivel = padre.Nivel + 1;
                }
                else
                {
                    categoria.Nivel = 0;
                }

                // Generar path
                categoria.GenerarPath();

                // Guardar
                _context.CategoriasEnterprise.Add(categoria);
                await _context.SaveChangesAsync();

                LimpiarCache();
                _logger.LogInformation("Categoría creada: {Nombre} (ID: {Id})", categoria.Nombre, categoria.CategoriaID);

                return (true, "Categoría creada exitosamente", categoria);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear categoría");
                return (false, "Error al crear la categoría", null);
            }
        }

        /// <summary>
        /// Actualizar categoría
        /// </summary>
        public async Task<(bool Exito, string Mensaje)> ActualizarAsync(Categoria categoria)
        {
            try
            {
                var existente = await _context.CategoriasEnterprise
                    .FirstOrDefaultAsync(c => c.CategoriaID == categoria.CategoriaID);

                if (existente == null)
                    return (false, "Categoría no encontrada");

                // Validar cambio de padre (evitar ciclos)
                if (categoria.CategoriaPadreID.HasValue && categoria.CategoriaPadreID == categoria.CategoriaID)
                    return (false, "Una categoría no puede ser su propio padre");

                // Actualizar campos
                existente.Nombre = categoria.Nombre;
                existente.Slug = string.IsNullOrWhiteSpace(categoria.Slug) ? GenerarSlug(categoria.Nombre) : categoria.Slug;
                existente.Descripcion = categoria.Descripcion;
                existente.IconoClass = categoria.IconoClass;
                existente.ImagenPath = categoria.ImagenPath;
                existente.MetaDescripcion = categoria.MetaDescripcion;
                existente.Orden = categoria.Orden;
                existente.Activa = categoria.Activa;
                existente.MostrarEnMenu = categoria.MostrarEnMenu;
                existente.Destacada = categoria.Destacada;
                existente.CategoriaPadreID = categoria.CategoriaPadreID;
                existente.ModificadoUtc = DateTime.UtcNow;

                // Recalcular nivel y path
                if (existente.CategoriaPadreID.HasValue)
                {
                    var padre = await ObtenerPorIdAsync(existente.CategoriaPadreID.Value);
                    existente.Nivel = padre?.Nivel + 1 ?? 0;
                }
                else
                {
                    existente.Nivel = 0;
                }

                existente.GenerarPath();

                await _context.SaveChangesAsync();
                LimpiarCache();

                _logger.LogInformation("Categoría actualizada: {Nombre} (ID: {Id})", existente.Nombre, existente.CategoriaID);
                return (true, "Categoría actualizada exitosamente");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar categoría ID: {Id}", categoria.CategoriaID);
                return (false, "Error al actualizar la categoría");
            }
        }

        /// <summary>
        /// Eliminar categoría
        /// </summary>
        public async Task<(bool Exito, string Mensaje)> EliminarAsync(int id)
        {
            try
            {
                var categoria = await _context.CategoriasEnterprise
                    .Include(c => c.CategoriasHijas)
                    .FirstOrDefaultAsync(c => c.CategoriaID == id);

                if (categoria == null)
                    return (false, "Categoría no encontrada");

                if (categoria.CategoriasHijas.Any())
                    return (false, $"No se puede eliminar. Tiene {categoria.CategoriasHijas.Count} subcategorías");

                var tieneProductos = await _context.Productos.AnyAsync(p => p.CategoriaID == id);
                if (tieneProductos)
                    return (false, "No se puede eliminar. Tiene productos asociados");

                _context.CategoriasEnterprise.Remove(categoria);
                await _context.SaveChangesAsync();

                LimpiarCache();
                _logger.LogInformation("Categoría eliminada: ID {Id}", id);

                return (true, "Categoría eliminada exitosamente");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar categoría ID: {Id}", id);
                return (false, "Error al eliminar la categoría");
            }
        }

        // ==================== UTILIDADES ====================

        /// <summary>
        /// Generar slug desde nombre
        /// </summary>
        public string GenerarSlug(string nombre)
        {
            if (string.IsNullOrWhiteSpace(nombre))
                return "";

            return nombre
                .ToLowerInvariant()
                .Replace("á", "a").Replace("é", "e").Replace("í", "i")
                .Replace("ó", "o").Replace("ú", "u").Replace("ñ", "n")
                .Replace(" ", "-")
                .Replace("_", "-")
                .Trim('-');
        }

        /// <summary>
        /// Buscar categorías
        /// </summary>
        public async Task<List<Categoria>> BuscarAsync(string termino)
        {
            if (string.IsNullOrWhiteSpace(termino))
                return new List<Categoria>();

            termino = termino.ToLower();

            return await _context.CategoriasEnterprise
                .Where(c => c.Nombre.ToLower().Contains(termino) ||
                           (c.Descripcion != null && c.Descripcion.ToLower().Contains(termino)))
                .OrderBy(c => c.Nombre)
                .Take(20)
                .ToListAsync();
        }

        /// <summary>
        /// Obtener breadcrumbs
        /// </summary>
        public async Task<List<Categoria>> ObtenerBreadcrumbsAsync(int categoriaId)
        {
            var breadcrumbs = new List<Categoria>();
            var actual = await ObtenerPorIdAsync(categoriaId);

            while (actual != null)
            {
                breadcrumbs.Insert(0, actual);

                if (actual.CategoriaPadreID.HasValue)
                    actual = await ObtenerPorIdAsync(actual.CategoriaPadreID.Value);
                else
                    actual = null;
            }

            return breadcrumbs;
        }

        /// <summary>
        /// Obtener descendientes
        /// </summary>
        public async Task<List<int>> ObtenerDescendientesIdsAsync(int categoriaId)
        {
            var categoria = await ObtenerPorIdAsync(categoriaId);
            if (categoria == null)
                return new List<int>();

            return await _context.CategoriasEnterprise
                .Where(c => c.Path.StartsWith(categoria.Path + "/"))
                .Select(c => c.CategoriaID)
                .ToListAsync();
        }

        /// <summary>
        /// Limpiar cache
        /// </summary>
        private void LimpiarCache()
        {
            _cache.Remove(CACHE_KEY_ALL);
            _cache.Remove(CACHE_KEY_TREE);
        }
    }
}
