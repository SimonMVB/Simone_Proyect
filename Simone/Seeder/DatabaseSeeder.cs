using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Simone.Data;
using Simone.Models;

/// <summary>
/// Seeder para inicializar datos base del sistema con soporte de transacciones,
/// optimización de queries, y logging estructurado.
/// 
/// ACTUALIZADO: Soporta modelos fusionados (Categorias, Subcategorias)
/// </summary>
public class DatabaseSeeder
{
    private readonly TiendaDbContext _context;
    private readonly UserManager<Usuario> _userManager;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(
        TiendaDbContext context,
        UserManager<Usuario> userManager,
        ILogger<DatabaseSeeder> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #region Public Methods

    /// <summary>
    /// Ejecuta todo el proceso de seeding dentro de una transacción ACID.
    /// Si cualquier operación falla, todas las operaciones se revierten.
    /// </summary>
    public async Task SeedCategoriesAndSubcategoriesAsync()
    {
        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            _logger.LogInformation("🚀 Iniciando proceso de inicialización de datos con transacción");

            await SeedCategoriesAsync();
            await SeedSubcategoriesAsync();
            await SeedAdminCarritoAsync();

            await transaction.CommitAsync();
            _logger.LogInformation("✅ Transacción confirmada exitosamente. Todos los datos fueron guardados");
        }
        catch (DbUpdateException dbEx)
        {
            await transaction.RollbackAsync();
            _logger.LogError(dbEx, "❌ Error de base de datos. Transacción revertida. Inner: {Inner}",
                dbEx.InnerException?.Message ?? "N/A");
            throw new InvalidOperationException("Error al guardar datos. Cambios revertidos.", dbEx);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "❌ Error inesperado durante el seeding. Transacción revertida");
            throw;
        }
    }

    /// <summary>
    /// Limpia duplicados de subcategorías (ejecutar si hay errores de clave duplicada)
    /// </summary>
    public async Task LimpiarDuplicadosAsync()
    {
        _logger.LogInformation("🧹 Buscando subcategorías duplicadas...");

        var duplicados = await _context.Subcategorias
            .GroupBy(s => new { s.CategoriaID, s.NombreSubcategoria })
            .Where(g => g.Count() > 1)
            .Select(g => new
            {
                g.Key.CategoriaID,
                g.Key.NombreSubcategoria,
                Count = g.Count(),
                IdsAEliminar = g.OrderBy(x => x.SubcategoriaID).Skip(1).Select(x => x.SubcategoriaID).ToList()
            })
            .ToListAsync();

        if (!duplicados.Any())
        {
            _logger.LogInformation("✅ No se encontraron duplicados");
            return;
        }

        _logger.LogWarning("⚠️ Encontrados {Count} grupos de duplicados", duplicados.Count);

        foreach (var dup in duplicados)
        {
            _logger.LogWarning("  - Categoría {CatId}: '{Nombre}' tiene {Count} copias. Eliminando IDs: {Ids}",
                dup.CategoriaID, dup.NombreSubcategoria, dup.Count,
                string.Join(", ", dup.IdsAEliminar));

            var aEliminar = await _context.Subcategorias
                .Where(s => dup.IdsAEliminar.Contains(s.SubcategoriaID))
                .ToListAsync();

            _context.Subcategorias.RemoveRange(aEliminar);
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("✅ Duplicados eliminados");
    }

    #endregion

    #region Private Seeding Methods

    /// <summary>
    /// Crea o actualiza las categorías principales del sistema
    /// Ahora soporta los nuevos campos del modelo fusionado
    /// </summary>
    private async Task SeedCategoriesAsync()
    {
        _logger.LogInformation("📂 Procesando categorías...");

        var categoriasConfig = GetCategoriasConfiguracion();

        // Cargar categorías existentes
        var nombresConfig = categoriasConfig.Select(c => c.Nombre).ToList();
        var categoriasExistentes = await _context.Categorias
            .Where(c => nombresConfig.Contains(c.Nombre))
            .ToDictionaryAsync(c => c.Nombre, StringComparer.OrdinalIgnoreCase);

        var categoriasNuevas = new List<Categorias>();
        var categoriasActualizadas = 0;

        foreach (var config in categoriasConfig)
        {
            if (categoriasExistentes.TryGetValue(config.Nombre, out var existente))
            {
                // Actualizar campos si están vacíos
                var actualizado = false;

                if (string.IsNullOrEmpty(existente.Slug))
                {
                    existente.Slug = GenerarSlug(config.Nombre);
                    actualizado = true;
                }

                if (string.IsNullOrEmpty(existente.IconoClass) && !string.IsNullOrEmpty(config.Icono))
                {
                    existente.IconoClass = config.Icono;
                    actualizado = true;
                }

                if (!existente.Activo)
                {
                    existente.Activo = true;
                    actualizado = true;
                }

                if (actualizado)
                {
                    existente.ModificadoUtc = DateTime.UtcNow;
                    _context.Categorias.Update(existente);
                    categoriasActualizadas++;
                }
            }
            else
            {
                categoriasNuevas.Add(new Categorias
                {
                    Nombre = config.Nombre,
                    Slug = GenerarSlug(config.Nombre),
                    IconoClass = config.Icono,
                    Orden = config.Orden,
                    Activo = true,
                    MostrarEnMenu = true,
                    CreadoUtc = DateTime.UtcNow
                });
            }
        }

        if (categoriasNuevas.Any())
        {
            await _context.Categorias.AddRangeAsync(categoriasNuevas);
            _logger.LogInformation("➕ Agregando {Count} categorías nuevas: {Names}",
                categoriasNuevas.Count,
                string.Join(", ", categoriasNuevas.Select(c => c.Nombre)));
        }

        if (categoriasActualizadas > 0)
        {
            _logger.LogInformation("🔄 Actualizando {Count} categorías", categoriasActualizadas);
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("✅ Categorías procesadas: {New} nuevas, {Updated} actualizadas",
            categoriasNuevas.Count, categoriasActualizadas);
    }

    /// <summary>
    /// Crea o actualiza las subcategorías asociadas a cada categoría
    /// ✅ CORREGIDO: Usa ToLookup en lugar de ToDictionary para evitar error de clave duplicada
    /// </summary>
    private async Task SeedSubcategoriesAsync()
    {
        _logger.LogInformation("📑 Procesando subcategorías...");

        // Obtener usuario válido para asignar como vendedor (ahora opcional)
        var vendedorId = await ObtenerVendedorValidoAsync();

        var subcategoriasPorCategoria = GetSubcategoriasConfiguracion();

        // Cargar categorías
        var categorias = await _context.Categorias
            .ToDictionaryAsync(c => c.Nombre, c => c.CategoriaID, StringComparer.OrdinalIgnoreCase);

        // ✅ CORRECCIÓN: Usar ToLookup en lugar de ToDictionary para manejar duplicados
        var subcategoriasExistentes = (await _context.Subcategorias.ToListAsync())
            .ToLookup(s => $"{s.CategoriaID}_{s.NombreSubcategoria}");

        var subcategoriasNuevas = new List<Subcategorias>();
        var subcategoriasActualizadas = 0;
        var categoriasNoEncontradas = new List<string>();

        foreach (var (categoriaNombre, subcats) in subcategoriasPorCategoria)
        {
            if (!categorias.TryGetValue(categoriaNombre, out var categoriaId))
            {
                categoriasNoEncontradas.Add(categoriaNombre);
                continue;
            }

            var orden = 1;
            foreach (var subcatNombre in subcats)
            {
                var key = $"{categoriaId}_{subcatNombre}";
                var existentes = subcategoriasExistentes[key].ToList();

                if (existentes.Any())
                {
                    // Tomar el primero y actualizar si necesario
                    var existente = existentes.First();
                    if (ActualizarSubcategoriaExistente(existente, subcatNombre, vendedorId, orden))
                    {
                        subcategoriasActualizadas++;
                    }
                }
                else
                {
                    subcategoriasNuevas.Add(new Subcategorias
                    {
                        CategoriaID = categoriaId,
                        NombreSubcategoria = subcatNombre,
                        Slug = GenerarSlug(subcatNombre),
                        VendedorID = vendedorId, // Puede ser null (subcategoría global)
                        Orden = orden,
                        Activo = true,
                        MostrarEnMenu = true,
                        CreadoUtc = DateTime.UtcNow
                    });
                }

                orden++;
            }
        }

        if (categoriasNoEncontradas.Any())
        {
            _logger.LogWarning("⚠️ Categorías no encontradas: {Categories}",
                string.Join(", ", categoriasNoEncontradas));
        }

        if (subcategoriasNuevas.Any())
        {
            await _context.Subcategorias.AddRangeAsync(subcategoriasNuevas);
            _logger.LogInformation("➕ Agregando {Count} subcategorías nuevas", subcategoriasNuevas.Count);
        }

        if (subcategoriasActualizadas > 0)
        {
            _logger.LogInformation("🔄 Actualizando {Count} subcategorías", subcategoriasActualizadas);
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("✅ Subcategorías procesadas: {New} nuevas, {Updated} actualizadas",
            subcategoriasNuevas.Count, subcategoriasActualizadas);
    }

    /// <summary>
    /// Crea un carrito vacío para el usuario administrador si no tiene uno activo
    /// </summary>
    private async Task SeedAdminCarritoAsync()
    {
        _logger.LogInformation("🛒 Verificando carrito de administrador...");

        var adminUser = await ObtenerUsuarioAdminAsync();
        if (adminUser == null)
        {
            _logger.LogWarning("⚠️ No se encontró usuario administrador. No se creará carrito");
            return;
        }

        var carritoExistente = await _context.Carrito
            .Where(c => c.UsuarioId == adminUser.Id && c.EstadoCarrito != "Cerrado")
            .FirstOrDefaultAsync();

        if (carritoExistente != null)
        {
            _logger.LogInformation("ℹ️ Usuario '{User}' ya tiene carrito activo (ID: {Id})",
                adminUser.UserName, carritoExistente.CarritoID);
            return;
        }

        var adminCarrito = new Carrito
        {
            UsuarioId = adminUser.Id,
            FechaCreacion = DateTime.UtcNow,
            EstadoCarrito = "Vacio"
        };

        _context.Carrito.Add(adminCarrito);
        await _context.SaveChangesAsync();

        _logger.LogInformation("✅ Carrito creado para '{User}' (ID: {Id})",
            adminUser.UserName, adminCarrito.CarritoID);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Genera un slug URL-friendly desde un nombre
    /// </summary>
    private static string GenerarSlug(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            return string.Empty;

        return nombre
            .ToLowerInvariant()
            .Trim()
            .Replace(" ", "-")
            .Replace("á", "a").Replace("é", "e").Replace("í", "i")
            .Replace("ó", "o").Replace("ú", "u").Replace("ñ", "n")
            .Replace("ü", "u").Replace("'", "")
            .Replace(".", "").Replace(",", "");
    }

    /// <summary>
    /// Obtiene un usuario válido para asignar como vendedor
    /// Retorna null si no hay usuarios (permitido para subcategorías globales)
    /// </summary>
    private async Task<string?> ObtenerVendedorValidoAsync()
    {
        var criterios = new[] { "admin@tienda.com", "admin@simone.com", "admin" };

        foreach (var criterio in criterios)
        {
            var usuario = await _userManager.Users
                .FirstOrDefaultAsync(u => u.UserName == criterio || u.Email == criterio);

            if (usuario != null)
            {
                _logger.LogInformation("✅ Usuario para vendedor: '{User}'", usuario.UserName);
                return usuario.Id;
            }
        }

        var primerUsuario = await _userManager.Users.FirstOrDefaultAsync();
        if (primerUsuario != null)
        {
            _logger.LogInformation("ℹ️ Usando primer usuario: '{User}'", primerUsuario.UserName);
            return primerUsuario.Id;
        }

        _logger.LogWarning("⚠️ No hay usuarios. Subcategorías serán globales (sin vendedor)");
        return null;
    }

    /// <summary>
    /// Obtiene el usuario administrador del sistema
    /// </summary>
    private async Task<Usuario?> ObtenerUsuarioAdminAsync()
    {
        var criterios = new[] { "admin@tienda.com", "admin@simone.com", "admin" };

        foreach (var criterio in criterios)
        {
            var usuario = await _userManager.Users
                .FirstOrDefaultAsync(u => u.UserName == criterio || u.Email == criterio);

            if (usuario != null)
                return usuario;
        }

        return await _userManager.Users.FirstOrDefaultAsync();
    }

    /// <summary>
    /// Actualiza una subcategoría existente si es necesario
    /// </summary>
    private bool ActualizarSubcategoriaExistente(Subcategorias existente, string nombre, string? vendedorId, int orden)
    {
        var actualizado = false;

        if (existente.NombreSubcategoria != nombre)
        {
            existente.NombreSubcategoria = nombre;
            actualizado = true;
        }

        if (string.IsNullOrEmpty(existente.Slug))
        {
            existente.Slug = GenerarSlug(nombre);
            actualizado = true;
        }

        if (existente.Orden == 0)
        {
            existente.Orden = orden;
            actualizado = true;
        }

        if (!existente.Activo)
        {
            existente.Activo = true;
            actualizado = true;
        }

        if (actualizado)
        {
            existente.ModificadoUtc = DateTime.UtcNow;
            _context.Subcategorias.Update(existente);
        }

        return actualizado;
    }

    #endregion

    #region Configuration

    /// <summary>
    /// Record para configuración de categoría
    /// </summary>
    private record CategoriaConfig(string Nombre, string? Icono, int Orden);

    /// <summary>
    /// Obtiene la configuración de categorías con iconos y orden
    /// </summary>
    private static List<CategoriaConfig> GetCategoriasConfiguracion()
    {
        return new List<CategoriaConfig>
        {
            new("Blusas", "fas fa-tshirt", 1),
            new("Tops", "fas fa-vest", 2),
            new("Body's", "fas fa-vest-patches", 3),
            new("Trajes de Baño", "fas fa-water", 4),
            new("Conjuntos", "fas fa-layer-group", 5),
            new("Vestidos", "fas fa-person-dress", 6),
            new("Faldas", "fas fa-person-dress", 7),
            new("Pantalones", "fas fa-socks", 8),
            new("Jeans", "fas fa-jeans", 9),
            new("Bolsas", "fas fa-bag-shopping", 10)
        };
    }

    /// <summary>
    /// Obtiene la configuración de subcategorías por categoría
    /// </summary>
    private static Dictionary<string, List<string>> GetSubcategoriasConfiguracion()
    {
        return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            {
                "Blusas",
                new List<string> { "Manga larga", "Manga corta", "Sin manga", "Campesina", "Formal" }
            },
            {
                "Tops",
                new List<string> { "Crop top", "Tank top", "Halter", "Básico", "Con tirantes" }
            },
            {
                "Body's",
                new List<string> { "Manga larga", "Manga corta", "Sin manga", "Encaje", "Liso" }
            },
            {
                "Trajes de Baño",
                new List<string> { "Bikini", "Entero", "Tankini", "Monokini", "High waist" }
            },
            {
                "Conjuntos",
                new List<string> { "Casual", "Formal", "Deportivo", "Dos piezas", "Coordinado" }
            },
            {
                "Vestidos",
                new List<string> { "Casual", "Fiesta", "Cóctel", "Largo", "Midi", "Mini" }
            },
            {
                "Faldas",
                new List<string> { "Mini", "Midi", "Larga", "Lápiz", "Plisada", "Acampanada" }
            },
            {
                "Pantalones",
                new List<string> { "Casual", "Formal", "Deportivo", "Palazzo", "Cargo", "Chino" }
            },
            {
                "Jeans",
                new List<string> { "Skinny", "Boyfriend", "Mom", "Bootcut", "Flare", "Straight" }
            },
            {
                "Bolsas",
                new List<string> { "Crossbody", "Clutch", "Tote", "Mochila", "Bandolera", "Shopper" }
            }
        };
    }

    #endregion
}