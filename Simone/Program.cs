using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Simone.Data;
using Simone.Models;

var builder = WebApplication.CreateBuilder(args);

// ✅ Agregar la configuración de la cadena de conexión desde appsettings.json
var connectionString = builder.Configuration.GetConnectionString("TiendaDB");

if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("La cadena de conexión a la base de datos no está configurada.");
}

// ✅ Configurar la conexión a la base de datos
builder.Services.AddDbContext<TiendaDbContext>(options =>
    options.UseSqlServer(connectionString));

// ✅ Configurar Identity con Entity Framework (Evita múltiples registros)
builder.Services.AddIdentity<Usuario, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
})
.AddEntityFrameworkStores<TiendaDbContext>()
.AddDefaultTokenProviders();

// ✅ Configurar la cookie de autenticación
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Cuenta/Login";
    options.AccessDeniedPath = "/Cuenta/AccesoDenegado";
});

builder.Services.AddControllersWithViews();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// ✅ Creación de Roles y Administrador Antes de `app.Run();`
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        await CrearRolesYAdmin(services, logger);
    }
    catch (Exception ex)
    {
        logger.LogError($"Error al inicializar roles y administrador: {ex.Message}");
    }
}

app.Run();

// ✅ Método para crear roles y usuario administrador
async Task CrearRolesYAdmin(IServiceProvider serviceProvider, ILogger logger)
{
    var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = serviceProvider.GetRequiredService<UserManager<Usuario>>();

    string[] roles = { "Administrador", "Vendedor", "Cliente" };

    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            var result = await roleManager.CreateAsync(new IdentityRole(role));
            if (!result.Succeeded)
            {
                logger.LogError($"Error al crear el rol {role}: {string.Join(", ", result.Errors)}");
            }
        }
    }

    // ✅ Crear usuario administrador por defecto
    string adminEmail = "admin@tienda.com";
    string adminPassword = "Admin123!";

    var existingAdmin = await userManager.FindByEmailAsync(adminEmail);
    if (existingAdmin == null)
    {
        var adminUser = new Usuario
        {
            UserName = adminEmail,
            Email = adminEmail,
            NombreCompleto = "Administrador General",
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(adminUser, adminPassword);

        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, "Administrador");
            logger.LogInformation("Administrador creado con éxito.");
        }
        else
        {
            logger.LogError($"Error al crear el administrador: {string.Join(", ", result.Errors)}");
        }
    }
}
