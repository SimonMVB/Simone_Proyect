using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Simone.Data;
using Simone.Models;
using Simone.Services;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

// =====================================
// 1) Conexión
// =====================================
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("La cadena de conexión 'DefaultConnection' no está configurada.");

// =====================================
// 2) DB + Identity
// =====================================
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

// =====================================
// 3) Localización (UI en es-EC)
// =====================================
var requestLocalizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("es-EC")
    .AddSupportedCultures("es-EC")
    .AddSupportedUICultures("es-EC");

// mantenemos los providers habituales (querystring/cookie/Accept-Language)
requestLocalizationOptions.RequestCultureProviders = new List<IRequestCultureProvider>
{
    new QueryStringRequestCultureProvider(),
    new CookieRequestCultureProvider(),
    new AcceptLanguageHeaderRequestCultureProvider()
};

// =====================================
// 4) Sesión, límites de subida, cookies
// =====================================
builder.Services.AddSession(o =>
{
    o.IdleTimeout = TimeSpan.FromMinutes(30);
    o.Cookie.HttpOnly = true;
    o.Cookie.IsEssential = true;
    o.Cookie.SameSite = SameSiteMode.Lax;
});

builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 64L * 1024 * 1024; // 64 MB
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

// =====================================
// 5) Servicios de dominio
// =====================================
builder.Services.AddScoped<PagosResolver>();
builder.Services.AddScoped<CategoriasService>();
builder.Services.AddScoped<SubcategoriasService>();
builder.Services.AddScoped<ProveedorService>();
builder.Services.AddScoped<ProductosService>();
builder.Services.AddScoped<CarritoService>();
builder.Services.AddScoped<DatabaseSeeder>();
builder.Services.AddScoped<LogService>();
builder.Services.AddScoped<CarritoActionFilter>();

// Configs que leen/escriben archivos
builder.Services.AddSingleton<IBancosConfigService, BancosConfigService>();
builder.Services.AddSingleton<IEnviosConfigService, EnviosConfigService>();
builder.Services.AddScoped<EnviosResolver>();
builder.Services.AddScoped<EnviosCarritoService>();

// =====================================
// 6) MVC + Model Binder de decimales
// =====================================
var mvcBuilder = builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<CarritoActionFilter>();
    // inserta nuestro binder al principio para que gane prioridad
    options.ModelBinderProviders.Insert(0, new Simone.Infrastructure.DecimalModelBinderProvider());
});

if (builder.Environment.IsDevelopment())
{
    try { mvcBuilder.AddRazorRuntimeCompilation(); } catch { /* ignorar si falta el paquete */ }
}

// =====================================
// 7) Asegurar wwwroot antes de Build
// =====================================
var contentRoot = builder.Environment.ContentRootPath;
var webRoot = Path.Combine(contentRoot, "wwwroot");
Directory.CreateDirectory(webRoot);
builder.WebHost.UseWebRoot("wwwroot");

var app = builder.Build();

// (Opcional) Cultura por defecto de los hilos: UI en es-EC
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("es-EC");
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("es-EC");

// =====================================
// 8) Pipeline
// =====================================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

/* ===========================================================
   Redirección canónica: neoagora.ec -> www.neoagora.ec (301)
   =========================================================== */
app.Use(async (ctx, next) =>
{
    if (ctx.Request.Host.Host.Equals("neoagora.ec", StringComparison.OrdinalIgnoreCase))
    {
        var url = $"https://www.neoagora.ec{ctx.Request.PathBase}{ctx.Request.Path}{ctx.Request.QueryString}";
        ctx.Response.Redirect(url, permanent: true);
        return;
    }
    await next();
});

app.UseHttpsRedirection();
app.UseRequestLocalization(requestLocalizationOptions);

// Archivos estáticos con caché de 24h
if (Directory.Exists(webRoot))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(webRoot),
        OnPrepareResponse = ctx =>
        {
            const int seconds = 60 * 60 * 24;
            ctx.Context.Response.Headers["Cache-Control"] = $"public,max-age={seconds}";
        }
    });
}

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// =====================================
// 9) Migraciones + Seed + Carpetas
// =====================================
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        var env = services.GetRequiredService<IWebHostEnvironment>();
        var wr = env.WebRootPath ?? webRoot;

        // Carpetas necesarias
        Directory.CreateDirectory(Path.Combine(env.ContentRootPath, "App_Data"));
        Directory.CreateDirectory(Path.Combine(wr, "uploads", "comprobantes"));
        Directory.CreateDirectory(Path.Combine(wr, "images", "Productos"));

        var db = services.GetRequiredService<TiendaDbContext>();

        // Aplica migraciones si existen
        var pending = await db.Database.GetPendingMigrationsAsync();
        if (pending.Any())
        {
            logger.LogInformation("Aplicando {count} migraciones pendientes...", pending.Count());
            await db.Database.MigrateAsync();
        }
        else
        {
            // Si no hay migraciones y la BD está vacía, crea el esquema
            var creator = db.Database.GetService<IRelationalDatabaseCreator>();
            var hasTables = await creator.HasTablesAsync();
            if (!hasTables)
            {
                logger.LogWarning("BD sin tablas y sin migraciones. Ejecutando EnsureCreated...");
                await db.Database.EnsureCreatedAsync();
            }
        }

        // Seed de Identity
        await Simone.Infrastructure.Bootstrapper.CrearRolesYAdmin(services, logger);

        // Seed de dominio
        var seeder = services.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedCategoriesAndSubcategoriesAsync();

        logger.LogInformation("Iniciado correctamente.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error al inicializar datos.");
    }
}

app.Run();


// =====================================================================
// Infraestructura (Binder de decimales y Bootstrapper para el seeding)
// =====================================================================
namespace Simone.Infrastructure
{
    /// <summary>
    /// Model binder provider para que los decimales funcionen con punto o coma,
    /// y se ignoren separadores de miles.
    /// </summary>
    public class DecimalModelBinderProvider : IModelBinderProvider
    {
        public IModelBinder GetBinder(ModelBinderProviderContext context)
        {
            if (context.Metadata.ModelType == typeof(decimal) || context.Metadata.ModelType == typeof(decimal?))
            {
                return new DecimalModelBinder();
            }
            return null!;
        }
    }

    public class DecimalModelBinder : IModelBinder
    {
        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            var valueProviderResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);

            if (valueProviderResult == ValueProviderResult.None)
                return Task.CompletedTask;

            bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valueProviderResult);

            var value = valueProviderResult.FirstValue;
            if (string.IsNullOrWhiteSpace(value))
                return Task.CompletedTask;

            // Normalización:
            // - quitar espacios
            // - quitar separadores de miles comunes (punto o coma, cuando luego hay decimales)
            // - forzar separador decimal punto
            value = value.Trim();

            // Si trae ambos "," y ".", intentemos detectar cuál es el decimal (el último símbolo suele ser el decimal)
            // Para simplificar: quitamos miles y dejamos un único separador como '.'
            // 1.234,56  -> 1234.56
            // 1,234.56  -> 1234.56
            // 1234,56   -> 1234.56
            // 1234.56   -> 1234.56
            var hasComma = value.Contains(',');
            var hasDot = value.Contains('.');

            if (hasComma && hasDot)
            {
                // Usamos el último de los dos como separador decimal, el otro lo eliminamos
                var lastComma = value.LastIndexOf(',');
                var lastDot = value.LastIndexOf('.');
                if (lastComma > lastDot)
                {
                    // coma = decimal, punto = miles
                    value = value.Replace(".", string.Empty).Replace(',', '.');
                }
                else
                {
                    // punto = decimal, coma = miles
                    value = value.Replace(",", string.Empty);
                }
            }
            else if (hasComma && !hasDot)
            {
                // Solo coma -> tratar como decimal
                value = value.Replace(',', '.');
            }
            else
            {
                // Solo punto o ninguno: ya está bien
            }

            if (decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var result))
            {
                bindingContext.Result = ModelBindingResult.Success(Math.Round(result, 2));
            }
            else
            {
                bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, "Valor decimal inválido");
            }

            return Task.CompletedTask;
        }
    }

    public static class Bootstrapper
    {
        /// <summary>
        /// Crea roles básicos y un usuario admin si no existe.
        /// </summary>
        public static async Task CrearRolesYAdmin(IServiceProvider serviceProvider, ILogger logger)
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
                        logger.LogError("Error al crear el rol {Rol}: {Err}", roles[i], errores);
                    }
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
                    logger.LogInformation("Administrador creado con éxito.");
                }
                else
                {
                    var errores = string.Join(", ", result.Errors.Select(e => e.Description));
                    logger.LogError("Error al crear el admin: {Err}", errores);
                }
            }
            else
            {
                if (!await userManager.IsInRoleAsync(existingAdmin, "Administrador"))
                {
                    await userManager.AddToRoleAsync(existingAdmin, "Administrador");
                }
            }
        }
    }
}
