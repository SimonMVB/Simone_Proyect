using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Localization;
using Simone.Data;
using Simone.Models;
using Simone.Services;
using System.Globalization;
using System.IO;
using System.Linq;

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
// 3) Cultura (decimales con coma) + Sesión/Cookies/Form
// =====================================
// Cultura base (puedes cambiar a es-ES si prefieres)
var appCulture = new CultureInfo("es-EC")
{
    NumberFormat =
    {
        NumberDecimalSeparator = ",",
        NumberGroupSeparator = "."
    }
};
CultureInfo.DefaultThreadCurrentCulture = appCulture;
CultureInfo.DefaultThreadCurrentUICulture = appCulture;

var requestLocalizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture(appCulture.Name)
    .AddSupportedCultures(appCulture.Name)
    .AddSupportedUICultures(appCulture.Name);

builder.Services.AddSession(o =>
{
    o.IdleTimeout = TimeSpan.FromMinutes(30);
    o.Cookie.HttpOnly = true;
    o.Cookie.IsEssential = true;
    o.Cookie.SameSite = SameSiteMode.Lax;
});

// Límite de subida alineado con el formulario (64 MB)
builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 64L * 1024 * 1024;
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
// 4) Servicios de dominio
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
// 5) MVC + filtro global + (opcional) runtime compilation en Dev
// =====================================
var mvc = builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<CarritoActionFilter>();
});

if (builder.Environment.IsDevelopment())
{
    try { mvc.AddRazorRuntimeCompilation(); } catch { /* ignorar si no está el paquete */ }
}

// =====================================
// 5.1) Asegurar wwwroot ANTES de Build/UseStaticFiles
// =====================================
var contentRoot = builder.Environment.ContentRootPath;
var webRoot = Path.Combine(contentRoot, "wwwroot");
Directory.CreateDirectory(webRoot);        // crea wwwroot si no existe
builder.WebHost.UseWebRoot("wwwroot");

var app = builder.Build();

// =====================================
// 6) Pipeline
// =====================================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Archivos estáticos con caché moderada (24h)
if (Directory.Exists(webRoot))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(webRoot),
        OnPrepareResponse = ctx =>
        {
            // Evita re-descargas innecesarias de imágenes/CSS/JS
            const int seconds = 60 * 60 * 24; // 1 día
            ctx.Context.Response.Headers["Cache-Control"] = $"public,max-age={seconds}";
        }
    });
}

app.UseRequestLocalization(requestLocalizationOptions);
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// =====================================
// 7) Migraciones + Seed + Carpetas de trabajo
// =====================================
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        var env = services.GetRequiredService<IWebHostEnvironment>();
        var wr = env.WebRootPath ?? webRoot;

        // Carpetas necesarias para IO
        Directory.CreateDirectory(Path.Combine(env.ContentRootPath, "App_Data"));
        Directory.CreateDirectory(Path.Combine(wr, "uploads", "comprobantes"));
        Directory.CreateDirectory(Path.Combine(wr, "images", "Productos"));

        // Migraciones
        var db = services.GetRequiredService<TiendaDbContext>();
        await db.Database.MigrateAsync();

        // Seed Identity
        await CrearRolesYAdmin(services, logger);

        // Seed dominio
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

// =====================================
// Helpers
// =====================================
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
