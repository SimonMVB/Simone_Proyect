using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http.Features;
using Simone.Data;
using Simone.Models;
using Simone.Services;
using System.Linq;
// (opcional si no tienes las global usings)
// using System.IO;

var builder = WebApplication.CreateBuilder(args);

// 1) Cadena de conexión
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("La cadena de conexión 'DefaultConnection' no está configurada.");

// 2) DB + Identity
builder.Services.AddDbContext<TiendaDbContext>(options =>
    options.UseSqlServer(
        connectionString,
        sql => sql.MigrationsAssembly(typeof(TiendaDbContext).Assembly.FullName)
    )
);

builder.Services.AddIdentity<Usuario, Roles>(options =>
{
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.AllowedForNewUsers = true;

    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;

    options.SignIn.RequireConfirmedEmail = false;
})
.AddEntityFrameworkStores<TiendaDbContext>()
.AddDefaultTokenProviders();

// 3) Sesión, HttpContext y cookies
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

// (Opcional) Límite de subida por encima de 5MB para el request completo
builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 10 * 1024 * 1024;  // 10MB request total
});

// 4) Servicios de dominio
builder.Services.AddScoped<PagosResolver>();     // Simone.Services.PagosResolver
builder.Services.AddScoped<CategoriasService>();
builder.Services.AddScoped<SubcategoriasService>();
builder.Services.AddScoped<ProveedorService>();
builder.Services.AddScoped<ProductosService>();
builder.Services.AddScoped<CarritoService>();
builder.Services.AddScoped<DatabaseSeeder>();
builder.Services.AddScoped<LogService>();
builder.Services.AddScoped<CarritoActionFilter>();


// 🔧 Bancos (IO a archivos)
builder.Services.AddSingleton<IBancosConfigService, BancosConfigService>();

// 🔧 Envíos (IO a archivos + resolución + cálculo carrito)  👈 NUEVO
builder.Services.AddSingleton<IEnviosConfigService, EnviosConfigService>();
builder.Services.AddScoped<EnviosResolver>();
builder.Services.AddScoped<EnviosCarritoService>();


// ❌ No registrar resolvers duplicados bajo otros namespaces
// builder.Services.AddScoped<Simone.ViewModels.Pagos.PagosResolver>();

// 5) MVC + filtro global
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<CarritoActionFilter>();
});

var app = builder.Build();

// 6) Pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// 🔧 Orden recomendado: Auth → Authorize → Session
app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// ✅ PROBLEMA #2 CORREGIDO: Mejor manejo de errores en el proceso de inicialización
// 7) Seed inicial (roles, admin, datos base) + asegurar carpetas
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        logger.LogInformation("=== INICIANDO PROCESO DE INICIALIZACIÓN ===");

        // Asegurar carpetas de trabajo (necesarias para bancos y comprobantes)
        var env = services.GetRequiredService<IWebHostEnvironment>();
        var appDataPath = Path.Combine(env.ContentRootPath, "App_Data");
        var comprobantesPath = Path.Combine(env.WebRootPath, "uploads", "comprobantes");

        Directory.CreateDirectory(appDataPath);
        Directory.CreateDirectory(comprobantesPath);
        logger.LogInformation("✅ Carpetas de trabajo verificadas");

        // 1) Migrar primero
        logger.LogInformation("🔄 Aplicando migraciones de base de datos...");
        var db = services.GetRequiredService<TiendaDbContext>();
        await db.Database.MigrateAsync();
        logger.LogInformation("✅ Migraciones aplicadas correctamente");

        // 2) Seed de Identity (roles + admin)
        logger.LogInformation("👥 Creando roles y usuario administrador...");
        await CrearRolesYAdmin(services, logger);
        logger.LogInformation("✅ Roles y administrador verificados");

        // 3) Seed de dominio (CON TRANSACCIONES - Problema #2 resuelto)
        logger.LogInformation("📦 Inicializando datos del dominio...");
        var seeder = services.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedCategoriesAndSubcategoriesAsync();
        logger.LogInformation("✅ Datos del dominio inicializados correctamente");

        logger.LogInformation("=== INICIALIZACIÓN COMPLETADA EXITOSAMENTE ===");
    }
    catch (DbUpdateException dbEx)
    {
        // Error específico de base de datos
        logger.LogError(dbEx, "❌ ERROR DE BASE DE DATOS durante la inicialización");
        logger.LogError("Detalles: {Message}", dbEx.InnerException?.Message ?? dbEx.Message);

        // En desarrollo, mostrar el error completo
        if (app.Environment.IsDevelopment())
        {
            logger.LogError("Stack trace: {StackTrace}", dbEx.StackTrace);
        }
    }
    catch (InvalidOperationException opEx)
    {
        // Error de operación inválida (generalmente configuración)
        logger.LogError(opEx, "❌ ERROR DE CONFIGURACIÓN durante la inicialización");
        logger.LogError("Verifica tu cadena de conexión y configuración de servicios");
    }
    catch (Exception ex)
    {
        // Cualquier otro error
        logger.LogError(ex, "❌ ERROR INESPERADO durante la inicialización");

        // En desarrollo, mostrar el error completo
        if (app.Environment.IsDevelopment())
        {
            logger.LogError("Tipo de error: {Type}", ex.GetType().Name);
            logger.LogError("Stack trace: {StackTrace}", ex.StackTrace);
        }

        // ⚠️ IMPORTANTE: En producción, podrías querer que la app NO inicie si falla el seed
        // Descomenta la siguiente línea si quieres que la aplicación se detenga al fallar:
        // throw;
    }
}

app.Run();

// ----- Helpers -----
static async Task CrearRolesYAdmin(IServiceProvider serviceProvider, ILogger logger)
{
    var roleManager = serviceProvider.GetRequiredService<RoleManager<Roles>>();
    var userManager = serviceProvider.GetRequiredService<UserManager<Usuario>>();

    string[] roles = { "Administrador", "Vendedor", "Cliente" };
    string[] descripcion = { "Administrador del sistema", "Vendedor del sistema", "Cliente del sistema" };

    for (var i = 0; i < roles.Length; i++)
    {
        if (!await roleManager.RoleExistsAsync(roles[i]))
        {
            var create = await roleManager.CreateAsync(new Roles(roles[i], descripcion[i]));
            if (!create.Succeeded)
            {
                var errores = string.Join(", ", create.Errors.Select(e => e.Description));
                logger.LogError("❌ Error al crear el rol {Rol}: {Err}", roles[i], errores);

                // Lanzar excepción si no se pueden crear los roles (son críticos)
                throw new InvalidOperationException($"No se pudo crear el rol {roles[i]}: {errores}");
            }
            else
            {
                logger.LogInformation("✅ Rol '{Rol}' creado exitosamente", roles[i]);
            }
        }
        else
        {
            logger.LogDebug("ℹ️ Rol '{Rol}' ya existe", roles[i]);
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
            Direccion = "NAN",
            Telefono = "NAN",
            Referencia = "NAN",
        };

        var result = await userManager.CreateAsync(adminUser, adminPassword);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, "Administrador");
            logger.LogInformation("✅ Usuario administrador '{Email}' creado con éxito", adminEmail);
        }
        else
        {
            var errores = string.Join(", ", result.Errors.Select(e => e.Description));
            logger.LogError("❌ Error al crear el admin: {Err}", errores);

            // Lanzar excepción si no se puede crear el admin (es crítico)
            throw new InvalidOperationException($"No se pudo crear el usuario administrador: {errores}");
        }
    }
    else
    {
        logger.LogDebug("ℹ️ Usuario administrador '{Email}' ya existe", adminEmail);

        // Garantizar que el admin tenga el rol, por si ya existía
        if (!await userManager.IsInRoleAsync(existingAdmin, "Administrador"))
        {
            await userManager.AddToRoleAsync(existingAdmin, "Administrador");
            logger.LogInformation("✅ Rol 'Administrador' asignado al usuario existente");
        }
    }
}
