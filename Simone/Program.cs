using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Simone.Data;
using Simone.Models;
using Simone.Services;
using System.Linq;

var builder = WebApplication.CreateBuilder(args);

// 1. Configuración de la cadena de conexión a la base de datos desde appsettings.json
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("La cadena de conexión a la base de datos no está configurada.");
}
// 2.5. Servicios adicionales necesarios para la lógica de negocio



// 2. Configuración de los servicios de la aplicación

// 2.1. Configuración del servicio de sesión con opciones de seguridad y persistencia
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Establece el tiempo de inactividad antes de expirar la sesión
    options.Cookie.HttpOnly = true;  // Hace la cookie solo accesible vía HTTP, lo que mejora la seguridad
    options.Cookie.IsEssential = true;  // Marca la cookie como esencial para la aplicación
});

// 2.2. Agregar HttpContextAccessor para poder acceder al contexto HTTP en otros servicios
builder.Services.AddHttpContextAccessor();

// 2.3. Configuración de la base de datos con SQL Server
builder.Services.AddDbContext<TiendaDbContext>(options =>
    options.UseSqlServer(connectionString)); // Establece la conexión a la base de datos

// 2.4. Configuración de Identity para la gestión de usuarios y roles, con opciones de seguridad de contraseñas
builder.Services.AddIdentity<Usuario, Roles>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
})
.AddEntityFrameworkStores<TiendaDbContext>()
.AddDefaultTokenProviders();

// 2.5. Servicios adicionales necesarios para la lógica de negocio
builder.Services.AddScoped<RoleManager<Roles>>();  // Gestionar roles de usuario
builder.Services.AddScoped<CategoriasService>();  // Servicio para la gestión de categorías
builder.Services.AddScoped<SubcategoriasService>();  // Servicio para la gestión de subcategorías
builder.Services.AddScoped<ProveedorService>();  // Servicio para la gestión de proveedores
builder.Services.AddScoped<ProductosService>();  // Servicio para la gestión de productos
builder.Services.AddScoped<CarritoService>();  // Servicio para la gestión de carrito de compras
builder.Services.AddScoped<DatabaseSeeder>(); // Servicio para añadir elementos a la base de datos en el inicio 
builder.Services.AddScoped<LogService>();
// Servicio para la gestión de logs
// 2.6. Configuración de la cookie de autenticación para definir las rutas de Login y Acceso Denegado
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Cuenta/Login";  // Ruta para la página de inicio de sesión
    options.AccessDeniedPath = "/Cuenta/AccesoDenegado";  // Ruta para la página de acceso denegado
    options.LogoutPath = "/Cuenta/Logout";  // Ruta para cerrar sesión
    options.SlidingExpiration = true;  // Habilita la expiración deslizante de la sesión
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);  // Duración de la sesión
});

// 2.7. Agregar soporte para controladores con vistas
builder.Services.AddControllersWithViews(options =>
{
    // Registrar el filtro de acción global que maneja la lógica del carrito
    options.Filters.Add<CarritoActionFilter>();
});

var app = builder.Build();

// 3. Configuración del pipeline de middleware

// 3.1. Configuración de manejo de errores y HSTS (Strict Transport Security) según el entorno
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");  // Redirige a una página de error en caso de excepciones
    app.UseHsts();  // Forza el uso de HTTPS en entornos de producción
}

// 3.2. Configuración de HTTPS y archivos estáticos
app.UseHttpsRedirection();  // Redirige todo el tráfico HTTP hacia HTTPS
app.UseStaticFiles();  // Sirve los archivos estáticos como imágenes, CSS y JS

// 3.3. Configuración de enrutamiento
app.UseRouting();  // Habilita el enrutamiento en la aplicación

// 3.4. Agregar el middleware de sesión
app.UseSession();  // Permite el almacenamiento de datos entre solicitudes

// 3.5. Configuración de autenticación y autorización
app.UseAuthentication();  // Middleware de autenticación
app.UseAuthorization();  // Middleware de autorización

// 3.6. Definir las rutas para los controladores
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");  // Ruta predeterminada de los controladores

// 4. Creación inicial de roles y usuario administrador (esto solo ocurre una vez al iniciar la aplicación)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        // Crear roles y administrador
        await CrearRolesYAdmin(services, logger);

        // Llamar al seeder para agregar categorías y subcategorías
        var seeder = services.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedCategoriesAndSubcategoriesAsync();

        logger.LogInformation("Iniciado en puerto 7074");
    }
    catch (Exception ex)
    {
        logger.LogError($"Error al inicializar datos: {ex.Message}");
    }
}

// 5. Ejecutar la aplicación
app.Run();

// 6. Método asíncrono para crear roles y el usuario administrador por defecto si no existen
async Task CrearRolesYAdmin(IServiceProvider serviceProvider, ILogger logger)
{
    var roleManager = serviceProvider.GetRequiredService<RoleManager<Roles>>();  // Para gestionar los roles
    var userManager = serviceProvider.GetRequiredService<UserManager<Usuario>>();  // Para gestionar los usuarios

    // Roles predeterminados a crear
    string[] roles = { "Administrador", "Vendedor", "Cliente" };
    string[] descripcion = { "Administrador del sistema", "Vendedor del sistema", "Cliente del sistema" };

    for (var i = 0; i < roles.Length; i++)
    {
        bool roleExists = await roleManager.RoleExistsAsync(roles[i]);
        if (!roleExists)
        {
            var role = new Roles(roles[i], descripcion[i]);
            var result = await roleManager.CreateAsync(role);  // Crear el rol
            if (result.Succeeded)
            {
                logger.LogInformation($"Rol {roles[i]} creado con éxito.");
            }
            else
            {
                var errores = string.Join(", ", result.Errors.Select(e => e.Description));  // Si hay errores, se loguean
                logger.LogError($"Error al crear el rol {roles[i]}: {errores}");
            }
        }
        else
        {
            logger.LogInformation($"El rol {roles[i]} ya existe.");
        }
    }

    // Crear un usuario administrador por defecto si no existe
    string adminEmail = "admin@tienda.com";
    string adminPassword = "Admin123!";
    var adminRole = await roleManager.FindByNameAsync("Administrador");
    var adminRoleID = adminRole?.Id;
    var existingAdmin = await userManager.FindByEmailAsync(adminEmail);
    if (existingAdmin == null)
    {
        var adminUser = new Usuario
        {
            UserName = adminEmail,
            Email = adminEmail,
            NombreCompleto = "Administrador General",
            EmailConfirmed = true,
            RolID = adminRoleID,
            Direccion = "NAN",  // Valores predeterminados
            Telefono = "NAN",
            Referencia = "NAN",
        };

        var result = await userManager.CreateAsync(adminUser, adminPassword);  // Crear el usuario administrador
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, "Administrador");  // Asignar el rol de Administrador
            logger.LogInformation("Administrador creado con éxito.");
        }
        else
        {
            var errores = string.Join(", ", result.Errors.Select(e => e.Description));  // Log de errores
            logger.LogError($"Error al crear el administrador: {errores}");
        }
    }
}
