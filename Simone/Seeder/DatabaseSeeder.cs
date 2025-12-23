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
/// </summary>
public class DatabaseSeeder
{
    private readonly TiendaDbContext _context;
    private readonly UserManager<Usuario> _userManager;
    private readonly ILogger<DatabaseSeeder> _logger;

    // Constantes de configuración
    private const int BATCH_SIZE = 1000; // Para futuras optimizaciones si crece el dataset

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
    /// <exception cref="InvalidOperationException">Cuando no se puede completar el seeding</exception>
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
            _logger.LogError(dbEx, "❌ Error de base de datos. Transacción revertida. Inner exception: {InnerMessage}",
                dbEx.InnerException?.Message ?? "N/A");
            throw new InvalidOperationException("Error al guardar datos en la base de datos. Todos los cambios fueron revertidos.", dbEx);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "❌ Error inesperado durante el seeding. Transacción revertida");
            throw;
        }
    }

    #endregion

    #region Private Seeding Methods

    /// <summary>
    /// Crea o actualiza las categorías principales del sistema
    /// </summary>
    private async Task SeedCategoriesAsync()
    {
        _logger.LogInformation("📂 Procesando categorías...");

        var categoriasNombres = GetCategoriasConfiguracion();

        // Cargar todas las categorías existentes de una vez (evitar N+1)
        var categoriasExistentes = await _context.Categorias
            .Where(c => categoriasNombres.Contains(c.Nombre))
            .ToDictionaryAsync(c => c.Nombre, StringComparer.OrdinalIgnoreCase);

        var categoriasNuevas = new List<Categorias>();
        var categoriasActualizadas = 0;

        foreach (var nombre in categoriasNombres)
        {
            if (categoriasExistentes.TryGetValue(nombre, out var existente))
            {
                if (existente.Nombre != nombre)
                {
                    existente.Nombre = nombre;
                    _context.Categorias.Update(existente);
                    categoriasActualizadas++;
                }
            }
            else
            {
                categoriasNuevas.Add(new Categorias { Nombre = nombre });
            }
        }

        // Inserción en batch
        if (categoriasNuevas.Any())
        {
            await _context.Categorias.AddRangeAsync(categoriasNuevas);
            _logger.LogInformation("➕ Agregando {Count} categorías nuevas: {Categories}",
                categoriasNuevas.Count,
                string.Join(", ", categoriasNuevas.Select(c => c.Nombre)));
        }

        if (categoriasActualizadas > 0)
        {
            _logger.LogInformation("🔄 Actualizando {Count} categorías existentes", categoriasActualizadas);
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("✅ {Total} categorías procesadas ({New} nuevas, {Updated} actualizadas)",
            categoriasNombres.Length,
            categoriasNuevas.Count,
            categoriasActualizadas);
    }

    /// <summary>
    /// Crea o actualiza las subcategorías asociadas a cada categoría
    /// </summary>
    private async Task SeedSubcategoriesAsync()
    {
        _logger.LogInformation("📑 Procesando subcategorías...");

        // Obtener usuario válido para asignar como vendedor
        var vendedorId = await ObtenerVendedorValidoAsync();
        if (string.IsNullOrEmpty(vendedorId))
        {
            _logger.LogWarning("⚠️ No se puede crear subcategorías sin un usuario válido. Saltando este paso");
            return;
        }

        var subcategoriasPorCategoria = GetSubcategoriasConfiguracion();

        // Cargar todas las entidades necesarias de una vez (evitar N+1)
        var categorias = await _context.Categorias
            .ToDictionaryAsync(c => c.Nombre, c => c.CategoriaID, StringComparer.OrdinalIgnoreCase);

        var subcategoriasExistentes = await _context.Subcategorias
            .ToDictionaryAsync(s => $"{s.CategoriaID}_{s.NombreSubcategoria}");

        var subcategoriasNuevas = new List<Subcategorias>();
        var subcategoriasActualizadas = 0;
        var categoriasNoEncontradas = new List<string>();

        // Procesar cada categoría y sus subcategorías
        foreach (var (categoriaNombre, subcats) in subcategoriasPorCategoria)
        {
            if (!categorias.TryGetValue(categoriaNombre, out var categoriaId))
            {
                categoriasNoEncontradas.Add(categoriaNombre);
                continue;
            }

            foreach (var subcatNombre in subcats)
            {
                var key = $"{categoriaId}_{subcatNombre}";

                if (subcategoriasExistentes.TryGetValue(key, out var existente))
                {
                    // Actualizar solo si es necesario
                    if (ActualizarSubcategoriaExistente(existente, subcatNombre, vendedorId))
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
                        VendedorID = vendedorId
                    });
                }
            }
        }

        // Reportar categorías no encontradas
        if (categoriasNoEncontradas.Any())
        {
            _logger.LogWarning("⚠️ Categorías no encontradas: {Categories}. Sus subcategorías fueron omitidas",
                string.Join(", ", categoriasNoEncontradas));
        }

        // Inserción en batch
        if (subcategoriasNuevas.Any())
        {
            await _context.Subcategorias.AddRangeAsync(subcategoriasNuevas);
            _logger.LogInformation("➕ Agregando {Count} subcategorías nuevas", subcategoriasNuevas.Count);
        }

        if (subcategoriasActualizadas > 0)
        {
            _logger.LogInformation("🔄 Actualizando {Count} subcategorías existentes", subcategoriasActualizadas);
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("✅ Subcategorías procesadas: {New} nuevas, {Updated} actualizadas",
            subcategoriasNuevas.Count,
            subcategoriasActualizadas);
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
            _logger.LogInformation("ℹ️ Usuario '{Username}' ya tiene un carrito activo (ID: {CarritoId})",
                adminUser.UserName,
                carritoExistente.CarritoID);
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

        _logger.LogInformation("✅ Carrito creado para usuario '{Username}' (ID: {CarritoId})",
            adminUser.UserName,
            adminCarrito.CarritoID);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Obtiene un usuario válido para asignar como vendedor, siguiendo una jerarquía de búsqueda
    /// </summary>
    /// <returns>ID del usuario o null si no se encuentra ninguno</returns>
    private async Task<string?> ObtenerVendedorValidoAsync()
    {
        // Jerarquía de búsqueda
        var criterios = new[]
        {
            "admin@tienda.com",
            "admin@simone.com",
            "admin"
        };

        // Intentar encontrar por criterios específicos
        foreach (var criterio in criterios)
        {
            var usuario = await _userManager.Users
                .FirstOrDefaultAsync(u => u.UserName == criterio || u.Email == criterio);

            if (usuario != null)
            {
                _logger.LogInformation("✅ Usuario encontrado: '{Username}' (ID: {UserId})",
                    usuario.UserName,
                    usuario.Id);
                return usuario.Id;
            }
        }

        // Si no se encuentra, usar el primer usuario disponible
        var primerUsuario = await _userManager.Users.FirstOrDefaultAsync();
        if (primerUsuario != null)
        {
            _logger.LogInformation("ℹ️ Usando primer usuario disponible: '{Username}' (ID: {UserId})",
                primerUsuario.UserName,
                primerUsuario.Id);
            return primerUsuario.Id;
        }

        _logger.LogWarning("⚠️ No hay usuarios registrados en el sistema");
        return null;
    }

    /// <summary>
    /// Obtiene el usuario administrador del sistema
    /// </summary>
    /// <returns>Usuario administrador o null si no existe</returns>
    private async Task<Usuario?> ObtenerUsuarioAdminAsync()
    {
        var criterios = new[]
        {
            "admin@tienda.com",
            "admin@simone.com",
            "admin"
        };

        foreach (var criterio in criterios)
        {
            var usuario = await _userManager.Users
                .FirstOrDefaultAsync(u => u.UserName == criterio || u.Email == criterio);

            if (usuario != null)
            {
                return usuario;
            }
        }

        // Fallback: primer usuario en el sistema
        return await _userManager.Users.FirstOrDefaultAsync();
    }

    /// <summary>
    /// Actualiza una subcategoría existente si es necesario
    /// </summary>
    /// <returns>True si se realizó alguna actualización</returns>
    private bool ActualizarSubcategoriaExistente(Subcategorias existente, string nuevoNombre, string vendedorId)
    {
        var actualizado = false;

        if (existente.NombreSubcategoria != nuevoNombre)
        {
            existente.NombreSubcategoria = nuevoNombre;
            actualizado = true;
        }

        if (string.IsNullOrEmpty(existente.VendedorID))
        {
            existente.VendedorID = vendedorId;
            actualizado = true;
        }

        if (actualizado)
        {
            _context.Subcategorias.Update(existente);
        }

        return actualizado;
    }

    #endregion

    #region Configuration Methods

    /// <summary>
    /// Obtiene la configuración de categorías del sistema.
    /// TODO: Considerar mover esto a un archivo de configuración JSON o base de datos
    /// </summary>
    private static string[] GetCategoriasConfiguracion()
    {
        return new[]
        {
            "Blusas",
            "Tops",
            "Body's",
            "Trajes de Baño",
            "Conjuntos",
            "Vestidos",
            "Faldas",
            "Pantalones",
            "Jeans",
            "Bolsas"
        };
    }

    /// <summary>
    /// Obtiene la configuración de subcategorías por categoría.
    /// TODO: Considerar mover esto a un archivo de configuración JSON o base de datos
    /// </summary>
    private static Dictionary<string, List<string>> GetSubcategoriasConfiguracion()
    {
        return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            {
                "Blusas",
                new List<string>
                {
                    "Manga larga",
                    "Manga corta",
                    "Sin manga",
                    "Campesina",
                    "Formal"
                }
            },
            {
                "Tops",
                new List<string>
                {
                    "Crop top",
                    "Tank top",
                    "Halter",
                    "Básico",
                    "Con tirantes"
                }
            },
            {
                "Body's",
                new List<string>
                {
                    "Manga larga",
                    "Manga corta",
                    "Sin manga",
                    "Encaje",
                    "Liso"
                }
            },
            {
                "Trajes de Baño",
                new List<string>
                {
                    "Bikini",
                    "Entero",
                    "Tankini",
                    "Monokini",
                    "High waist"
                }
            },
            {
                "Conjuntos",
                new List<string>
                {
                    "Casual",
                    "Formal",
                    "Deportivo",
                    "Dos piezas",
                    "Coordinado"
                }
            },
            {
                "Vestidos",
                new List<string>
                {
                    "Casual",
                    "Fiesta",
                    "Cóctel",
                    "Largo",
                    "Midi",
                    "Mini"
                }
            },
            {
                "Faldas",
                new List<string>
                {
                    "Mini",
                    "Midi",
                    "Larga",
                    "Lápiz",
                    "Plisada",
                    "Acampanada"
                }
            },
            {
                "Pantalones",
                new List<string>
                {
                    "Casual",
                    "Formal",
                    "Deportivo",
                    "Palazzo",
                    "Cargo",
                    "Chino"
                }
            },
            {
                "Jeans",
                new List<string>
                {
                    "Skinny",
                    "Boyfriend",
                    "Mom",
                    "Bootcut",
                    "Flare",
                    "Straight"
                }
            },
            {
                "Bolsas",
                new List<string>
                {
                    "Crossbody",
                    "Clutch",
                    "Tote",
                    "Mochila",
                    "Bandolera",
                    "Shopper"
                }
            }
        };
    }

    #endregion
}
