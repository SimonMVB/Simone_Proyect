using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Simone.Data;
using Simone.Models;
using Simone.Services;
using Simone.ModelBinders;
using System.Globalization;
using System.Linq;

var builder = WebApplication.CreateBuilder(args);

// ============================================================================
// 1) CONFIGURACIÓN DE BASE DE DATOS
// ============================================================================
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("La cadena de conexión 'DefaultConnection' no está configurada.");

builder.Services.AddDbContext<TiendaDbContext>(options =>
    options.UseSqlServer(
        connectionString,
        sql => sql.MigrationsAssembly(typeof(TiendaDbContext).Assembly.FullName)
    )
);

// ============================================================================
// 2) CONFIGURACIÓN DE IDENTITY
// ============================================================================
builder.Services.AddIdentity<Usuario, Roles>(options =>
{
    // Lockout configuration
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.AllowedForNewUsers = true;

    // Password requirements
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;

    // Sign-in requirements
    options.SignIn.RequireConfirmedEmail = false;
})
.AddEntityFrameworkStores<TiendaDbContext>()
.AddDefaultTokenProviders();

// ============================================================================
// 3) CONFIGURACIÓN DE SESIÓN Y COOKIES
// ============================================================================
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

builder.Services.AddHttpContextAccessor();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Cuenta/Login";
    options.AccessDeniedPath = "/Cuenta/AccesoDenegado";
    options.LogoutPath = "/Cuenta/Logout";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
});

// ============================================================================
// 4) CONFIGURACIÓN DE CACHÉ
// ============================================================================
builder.Services.AddMemoryCache(options =>
{
    options.CompactionPercentage = 0.25;
});

// ============================================================================
// 5) CONFIGURACIÓN DE LÍMITES DE SUBIDA DE ARCHIVOS
// ============================================================================
const long maxFileSize = 64L * 1024 * 1024; // 64 MB

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = maxFileSize;
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartHeadersLengthLimit = int.MaxValue;
    options.MemoryBufferThreshold = int.MaxValue;
});

builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = maxFileSize;
});

builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = maxFileSize;
});

// ============================================================================
// 6) REGISTRO DE SERVICIOS DE DOMINIO
// ============================================================================

// ────────────────────────────────────────────────────────────────────────────
// CATEGORÍAS Y PRODUCTOS (Sistema Fusionado)
// ────────────────────────────────────────────────────────────────────────────
builder.Services.AddScoped<CategoriasService>();
builder.Services.AddScoped<SubcategoriasService>();

// ✅ Atributos dinámicos (fusionado - usa Categorias directamente)
builder.Services.AddScoped<CategoriaAtributoService>();
builder.Services.AddScoped<ProductoAtributoService>();

// Productos
builder.Services.AddScoped<ProductosService>();

// ────────────────────────────────────────────────────────────────────────────
// CARRITO Y PAGOS
// ────────────────────────────────────────────────────────────────────────────
builder.Services.AddScoped<ICarritoService, CarritoService>();
builder.Services.AddScoped<CarritoService>();
builder.Services.AddScoped<CarritoActionFilter>();
builder.Services.AddScoped<PagosResolver>();

// ────────────────────────────────────────────────────────────────────────────
// ENVÍOS
// ────────────────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IEnviosConfigService, EnviosConfigService>();
builder.Services.AddScoped<EnviosResolver>();
builder.Services.AddScoped<EnviosCarritoService>();

// ────────────────────────────────────────────────────────────────────────────
// PROVEEDORES Y BANCOS
// ────────────────────────────────────────────────────────────────────────────
builder.Services.AddScoped<ProveedorService>();
builder.Services.AddSingleton<IBancosConfigService, BancosConfigService>();

// ────────────────────────────────────────────────────────────────────────────
// UTILIDADES Y LOGGING
// ────────────────────────────────────────────────────────────────────────────
builder.Services.AddScoped<DatabaseSeeder>();
builder.Services.AddScoped<LogService>();

// ============================================================================
// 7) CONFIGURACIÓN DE MVC Y MODEL BINDERS
// ============================================================================
builder.Services.AddControllersWithViews(options =>
{
    options.ModelBinderProviders.Insert(0, new InvariantDecimalModelBinderProvider());
    options.Filters.Add<CarritoActionFilter>();
});

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[]
    {
        new CultureInfo("en-US"),
        new CultureInfo("es-EC"),
    };

    options.DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture("en-US");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
});

// ============================================================================
// 8) CONSTRUCCIÓN DE LA APLICACIÓN
// ============================================================================
var app = builder.Build();

// ============================================================================
// 9) CONFIGURACIÓN DEL PIPELINE DE MIDDLEWARE
// ============================================================================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

app.UseRequestLocalization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// ============================================================================
// 10) INICIALIZACIÓN DE LA BASE DE DATOS Y SEED
// ============================================================================
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        logger.LogInformation("╔══════════════════════════════════════════════════════════╗");
        logger.LogInformation("║     INICIANDO CONFIGURACIÓN DE BASE DE DATOS            ║");
        logger.LogInformation("╚══════════════════════════════════════════════════════════╝");

        // ────────────────────────────────────────────────────────────────
        // PASO 1: Verificar conexión a base de datos
        // ────────────────────────────────────────────────────────────────
        logger.LogInformation("🔌 Verificando conexión a base de datos...");
        var db = services.GetRequiredService<TiendaDbContext>();

        if (await db.Database.CanConnectAsync())
        {
            logger.LogInformation("✅ Conexión exitosa a la base de datos");
        }
        else
        {
            logger.LogError("❌ No se pudo conectar a la base de datos");
            throw new InvalidOperationException("No se puede conectar a la base de datos");
        }

        // ────────────────────────────────────────────────────────────────
        // PASO 2: Aplicar migraciones
        // ────────────────────────────────────────────────────────────────
        logger.LogInformation("🔄 Aplicando migraciones de base de datos...");

        var pendingMigrations = await db.Database.GetPendingMigrationsAsync();
        var pendingCount = pendingMigrations.Count();

        if (pendingCount > 0)
        {
            logger.LogInformation("  ℹ️ Migraciones pendientes: {Count}", pendingCount);
            foreach (var migration in pendingMigrations)
            {
                logger.LogDebug("    • {Migration}", migration);
            }

            await db.Database.MigrateAsync();
            logger.LogInformation("✅ Migraciones aplicadas correctamente");
        }
        else
        {
            logger.LogInformation("✅ Base de datos actualizada (sin migraciones pendientes)");
        }

        // ────────────────────────────────────────────────────────────────
        // PASO 3: Crear roles y usuario administrador
        // ────────────────────────────────────────────────────────────────
        logger.LogInformation("👥 Verificando roles y usuario administrador...");
        await CrearRolesYAdmin(services, logger);
        logger.LogInformation("✅ Roles y administrador verificados");

        // ────────────────────────────────────────────────────────────────
        // PASO 4: Seed de datos del dominio
        // ────────────────────────────────────────────────────────────────
        logger.LogInformation("📦 Inicializando datos del dominio...");
        var seeder = services.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedCategoriesAndSubcategoriesAsync();
        logger.LogInformation("✅ Datos del dominio inicializados correctamente");

        // ────────────────────────────────────────────────────────────────
        // PASO 5: Verificar sistema de categorías (fusionado)
        // ────────────────────────────────────────────────────────────────
        logger.LogInformation("🏢 Verificando sistema de categorías...");

        // ✅ CORREGIDO: Usar DbContext directamente en lugar de CategoriaEnterpriseService
        var totalCategorias = await db.Categorias.CountAsync();
        var categoriasActivas = await db.Categorias.CountAsync(c => c.Activo);
        var totalAtributos = await db.CategoriaAtributos.CountAsync();
        var atributosActivos = await db.CategoriaAtributos.CountAsync(a => a.Activo);

        logger.LogInformation("  📊 Categorías: {Total} total, {Activas} activas", totalCategorias, categoriasActivas);
        logger.LogInformation("  📊 Atributos: {Total} total, {Activos} activos", totalAtributos, atributosActivos);

        if (totalCategorias > 0)
        {
            logger.LogInformation("✅ Sistema de categorías operativo");
        }
        else
        {
            logger.LogWarning("⚠️ No hay categorías. El seeder debería haberlas creado.");
        }

        logger.LogInformation("╔══════════════════════════════════════════════════════════╗");
        logger.LogInformation("║     ✅ INICIALIZACIÓN COMPLETADA EXITOSAMENTE           ║");
        logger.LogInformation("╚══════════════════════════════════════════════════════════╝");
    }
    catch (DbUpdateException dbEx)
    {
        logger.LogError("╔══════════════════════════════════════════════════════════╗");
        logger.LogError("║     ❌ ERROR DE BASE DE DATOS                            ║");
        logger.LogError("╚══════════════════════════════════════════════════════════╝");
        logger.LogError(dbEx, "Error durante la inicialización de la base de datos");
        logger.LogError("📋 Detalles: {Message}", dbEx.InnerException?.Message ?? dbEx.Message);

        if (app.Environment.IsDevelopment())
        {
            logger.LogError("🔍 Stack trace: {StackTrace}", dbEx.StackTrace);
        }

        logger.LogError("💡 Sugerencias:");
        logger.LogError("  • Verifica la cadena de conexión en appsettings.json");
        logger.LogError("  • Verifica que SQL Server esté ejecutándose");
        logger.LogError("  • Ejecuta: dotnet ef migrations add FusionCategoriasAtributos");
        logger.LogError("  • Ejecuta: dotnet ef database update");
    }
    catch (InvalidOperationException opEx)
    {
        logger.LogError("╔══════════════════════════════════════════════════════════╗");
        logger.LogError("║     ❌ ERROR DE CONFIGURACIÓN                            ║");
        logger.LogError("╚══════════════════════════════════════════════════════════╝");
        logger.LogError(opEx, "Error de configuración durante la inicialización");
        logger.LogError("📋 Mensaje: {Message}", opEx.Message);

        logger.LogError("💡 Sugerencias:");
        logger.LogError("  • Verifica que todos los servicios estén registrados en Program.cs");
        logger.LogError("  • Verifica las dependencias inyectadas en los constructores");
    }
    catch (Exception ex)
    {
        logger.LogError("╔══════════════════════════════════════════════════════════╗");
        logger.LogError("║     ❌ ERROR INESPERADO                                  ║");
        logger.LogError("╚══════════════════════════════════════════════════════════╝");
        logger.LogError(ex, "Error inesperado durante la inicialización");
        logger.LogError("📋 Tipo: {Type}", ex.GetType().Name);
        logger.LogError("📋 Mensaje: {Message}", ex.Message);

        if (app.Environment.IsDevelopment())
        {
            logger.LogError("🔍 Stack trace: {StackTrace}", ex.StackTrace);
        }
    }
}

// ============================================================================
// 11) INICIAR LA APLICACIÓN
// ============================================================================
app.Run();

// ============================================================================
// MÉTODOS AUXILIARES
// ============================================================================

/// <summary>
/// Crea los roles del sistema y el usuario administrador inicial
/// </summary>
static async Task CrearRolesYAdmin(IServiceProvider serviceProvider, ILogger logger)
{
    var roleManager = serviceProvider.GetRequiredService<RoleManager<Roles>>();
    var userManager = serviceProvider.GetRequiredService<UserManager<Usuario>>();

    string[] roles = { "Administrador", "Vendedor", "Cliente" };
    string[] descripcion =
    {
        "Administrador del sistema con acceso completo",
        "Vendedor con acceso al panel de gestión",
        "Cliente con acceso a la tienda"
    };

    for (var i = 0; i < roles.Length; i++)
    {
        if (!await roleManager.RoleExistsAsync(roles[i]))
        {
            var create = await roleManager.CreateAsync(new Roles(roles[i], descripcion[i]));

            if (!create.Succeeded)
            {
                var errores = string.Join(", ", create.Errors.Select(e => e.Description));
                logger.LogError("  ❌ Error al crear el rol '{Rol}': {Errores}", roles[i], errores);
                throw new InvalidOperationException($"No se pudo crear el rol {roles[i]}: {errores}");
            }

            logger.LogInformation("  ✓ Rol '{Rol}' creado exitosamente", roles[i]);
        }
        else
        {
            logger.LogDebug("  ℹ️ Rol '{Rol}' ya existe", roles[i]);
        }
    }

    var adminEmail = "admin@tienda.com";
    var adminPassword = "Admin123!";
    var existingAdmin = await userManager.FindByEmailAsync(adminEmail);

    if (existingAdmin == null)
    {
        var adminRole = await roleManager.FindByNameAsync("Administrador");

        var adminUser = new Usuario
        {
            UserName = adminEmail,
            Email = adminEmail,
            NombreCompleto = "Administrador General",
            EmailConfirmed = true,
            RolID = adminRole?.Id ?? string.Empty,
            Direccion = "Sistema",
            Telefono = "N/A",
            Referencia = "Usuario administrador por defecto",
            Activo = true,
        };

        var result = await userManager.CreateAsync(adminUser, adminPassword);

        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, "Administrador");
            logger.LogInformation("  ✓ Usuario administrador '{Email}' creado con éxito", adminEmail);
            logger.LogWarning("  ⚠️ IMPORTANTE: Cambia la contraseña del admin en producción");
            logger.LogInformation("  📧 Email: {Email}", adminEmail);
            logger.LogInformation("  🔑 Password: {Password}", adminPassword);
        }
        else
        {
            var errores = string.Join(", ", result.Errors.Select(e => e.Description));
            logger.LogError("  ❌ Error al crear el admin: {Errores}", errores);
            throw new InvalidOperationException($"No se pudo crear el usuario administrador: {errores}");
        }
    }
    else
    {
        logger.LogDebug("  ℹ️ Usuario administrador '{Email}' ya existe", adminEmail);

        if (!await userManager.IsInRoleAsync(existingAdmin, "Administrador"))
        {
            await userManager.AddToRoleAsync(existingAdmin, "Administrador");
            logger.LogInformation("  ✓ Rol 'Administrador' asignado al usuario existente");
        }

        if (!existingAdmin.Activo)
        {
            existingAdmin.Activo = true;
            await userManager.UpdateAsync(existingAdmin);
            logger.LogInformation("  ✓ Usuario administrador activado");
        }
    }
}