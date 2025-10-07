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

// 7) Seed inicial (roles, admin, datos base) + asegurar carpetas
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        // Asegurar carpetas de trabajo (necesarias para bancos y comprobantes)
        var env = services.GetRequiredService<IWebHostEnvironment>();
        Directory.CreateDirectory(Path.Combine(env.ContentRootPath, "App_Data"));
        Directory.CreateDirectory(Path.Combine(env.WebRootPath, "uploads", "comprobantes"));

        // 1) Migrar primero
        var db = services.GetRequiredService<TiendaDbContext>();
        await db.Database.MigrateAsync();

        // 2) Seed de Identity (roles + admin)
        await CrearRolesYAdmin(services, logger);

        // 3) Seed de dominio
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
        // Garantizar que el admin tenga el rol, por si ya existía
        if (!await userManager.IsInRoleAsync(existingAdmin, "Administrador"))
        {
            await userManager.AddToRoleAsync(existingAdmin, "Administrador");
        }
    }
}
