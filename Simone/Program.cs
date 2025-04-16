using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Simone.Data;
using Simone.Models;
using System.Linq;

var builder = WebApplication.CreateBuilder(args);

// 1. Configuración de la cadena de conexión desde appsettings.json
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("La cadena de conexión a la base de datos no está configurada.");
}

// 2. Configuración de servicios

// 2.1. Configurar el servicio de sesión con opciones de seguridad y persistencia
builder.Services.AddSession(options =>
{
    // Tiempo de inactividad antes de expirar la sesión (30 minutos)
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    // La cookie solo se podrá acceder a través de HTTP para evitar accesos desde scripts
    options.Cookie.HttpOnly = true;
    // Indica que la cookie es esencial para el funcionamiento de la app
    options.Cookie.IsEssential = true;
    // Opcional: Puedes asignar un nombre específico a la cookie de sesión
    // options.Cookie.Name = ".Simone.Session";
});

// 2.2. Agregar HttpContextAccessor para poder acceder al HttpContext en otros servicios
builder.Services.AddHttpContextAccessor();

// 2.3. Configurar el contexto de la base de datos usando SQL Server
builder.Services.AddDbContext<TiendaDbContext>(options =>
    options.UseSqlServer(connectionString));

// 2.4. Configurar Identity con Entity Framework y opciones de seguridad para contraseñas
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

// 2.5. Configurar la cookie de autenticación para definir las rutas de Login y Acceso Denegado
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Cuenta/Login";
    options.AccessDeniedPath = "/Cuenta/AccesoDenegado";
});

// 2.6. Agregar soporte para controladores con vistas
builder.Services.AddControllersWithViews();

var app = builder.Build();

// 3. Configuración del pipeline de middleware

// 3.1. Configuración de manejo de errores y HSTS según el entorno
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error"); // Redirige a una página de error
    app.UseHsts(); // HSTS: agrega cabeceras de seguridad para HTTPS
}

// 3.2. Forzar HTTPS y servir archivos estáticos
app.UseHttpsRedirection();
app.UseStaticFiles();

// 3.3. Configurar el enrutamiento
app.UseRouting();

// 3.4. Agregar el middleware de sesión
// Esto hace que la sesión esté disponible en cada solicitud para poder almacenar y recuperar datos.
app.UseSession();

// 3.5. Configurar la autenticación y autorización
app.UseAuthentication();
app.UseAuthorization();

// 3.6. Mapeo de rutas para los controladores
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// 4. Creación inicial de roles y usuario administrador (se ejecuta antes de iniciar la app)
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

// 5. Ejecutar la aplicación
app.Run();


// 6. Método asíncrono para crear roles y el usuario administrador por defecto
async Task CrearRolesYAdmin(IServiceProvider serviceProvider, ILogger logger)
{
    var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = serviceProvider.GetRequiredService<UserManager<Usuario>>();

    // Definir los roles que se deben crear
    string[] roles = { "Administrador", "Vendedor", "Cliente" };

    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            var result = await roleManager.CreateAsync(new IdentityRole(role));
            if (!result.Succeeded)
            {
                // Usar LINQ para obtener las descripciones de los errores
                var errores = string.Join(", ", result.Errors.Select(e => e.Description));
                logger.LogError($"Error al crear el rol {role}: {errores}");
            }
        }
    }

    // Crear un usuario administrador por defecto si no existe
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
            // Asignar el rol de Administrador al usuario creado
            await userManager.AddToRoleAsync(adminUser, "Administrador");
            logger.LogInformation("Administrador creado con éxito.");
        }
        else
        {
            var errores = string.Join(", ", result.Errors.Select(e => e.Description));
            logger.LogError($"Error al crear el administrador: {errores}");
        }
    }
}
