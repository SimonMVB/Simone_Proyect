using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Simone.Data;
using Simone.ModelBinders;
using Simone.Models;
using Simone.Services;
using System.Globalization;
using System.Linq;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ============================================================================
// 1) CONFIGURACIÓN DE BASE DE DATOS
// ============================================================================
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("La cadena de conexión 'DefaultConnection' no está configurada.");

builder.Services.AddDbContext<TiendaDbContext>(options =>
    options.UseSqlServer(connectionString)
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
    options.Cookie.SameSite = SameSiteMode.Strict;
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
    // Límites razonables para prevenir ataques DoS por valores excesivamente grandes
    options.ValueLengthLimit = 4 * 1024 * 1024;              // 4 MB para valores de formulario
    options.MultipartHeadersLengthLimit = 16 * 1024;          // 16 KB para cabeceras multipart
    options.MemoryBufferThreshold = 64 * 1024;                // 64 KB en memoria, el resto a disco
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
// Registro del tipo concreto primero; la interfaz hace forwarding al mismo scope.
// Esto permite inyectar tanto ICategoriasService como CategoriasService directamente.
builder.Services.AddScoped<CategoriasService>();
builder.Services.AddScoped<ICategoriasService>(sp => sp.GetRequiredService<CategoriasService>());
builder.Services.AddScoped<SubcategoriasService>();

// ✅ Atributos dinámicos (fusionado - usa Categorias directamente)
builder.Services.AddScoped<CategoriaAtributoService>();
builder.Services.AddScoped<ProductoAtributoService>();

// Productos — mismo patrón: tipo concreto + forwarding de interfaz
builder.Services.AddScoped<ProductosService>();
builder.Services.AddScoped<IProductosService>(sp => sp.GetRequiredService<ProductosService>());




builder.Services.AddScoped<IEnvioConsolidadoService, EnvioConsolidadoService>();

// ────────────────────────────────────────────────────────────────────────────
// CARRITO Y PAGOS
// ────────────────────────────────────────────────────────────────────────────
builder.Services.AddScoped<ICarritoService, CarritoService>();
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
// Proveedores — mismo patrón: tipo concreto + forwarding de interfaz
builder.Services.AddScoped<ProveedorService>();
builder.Services.AddScoped<IProveedorService>(sp => sp.GetRequiredService<ProveedorService>());
builder.Services.AddSingleton<IBancosConfigService, BancosConfigService>();

// ────────────────────────────────────────────────────────────────────────────
// ALMACENAMIENTO DE ARCHIVOS (imágenes, comprobantes) — fuera del proyecto
// ────────────────────────────────────────────────────────────────────────────
builder.Services.Configure<UploadsOptions>(builder.Configuration.GetSection("Uploads"));
builder.Services.AddSingleton<IFileStorageService, FileStorageService>();

// ────────────────────────────────────────────────────────────────────────────
// UTILIDADES Y LOGGING
// ────────────────────────────────────────────────────────────────────────────
builder.Services.AddScoped<DatabaseSeeder>();
builder.Services.AddScoped<LogService>();

// ────────────────────────────────────────────────────────────────────────────
// VENTAS Y COMISIONES
// ────────────────────────────────────────────────────────────────────────────
builder.Services.AddScoped<ComisionService>();
builder.Services.AddScoped<FinanzasAdminService>();
builder.Services.AddScoped<IDevolucionesService, DevolucionesService>();


// ============================================================================
// 7) JWT BEARER AUTHENTICATION (para API Mobile / POS)
// ============================================================================
var jwtKey     = builder.Configuration["Jwt:SecretKey"]  ?? "Simone-POS-Super-Secret-JWT-Key-2025-Ecuador-Mobile-App!";
var jwtIssuer  = builder.Configuration["Jwt:Issuer"]     ?? "SimoneAPI";
var jwtAudience = builder.Configuration["Jwt:Audience"]  ?? "SimoneMobileApp";

builder.Services.AddAuthentication()
    .AddJwtBearer("JwtBearer", options =>
    {
        options.RequireHttpsMetadata = false; // permitir HTTP en desarrollo
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = jwtIssuer,
            ValidAudience            = jwtAudience,
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew                = TimeSpan.FromMinutes(1)
        };
    });

// ============================================================================
// 7b) CORS — permite conexiones desde la app React Native (cualquier origen en dev)
// ============================================================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("ReactNativePolicy", policy =>
    {
        policy
            .AllowAnyOrigin()   // en producción restringe a la IP/dominio del servidor
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

// ============================================================================
// 7c) SWAGGER / OPENAPI
// ============================================================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "Simone POS API",
        Version     = "v1",
        Description = "API REST para la app móvil POS de Simone (React Native)"
    });

    // Soporte JWT en Swagger UI
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description  = "JWT Token. Formato: Bearer {token}",
        Name         = "Authorization",
        In           = ParameterLocation.Header,
        Type         = SecuritySchemeType.Http,
        Scheme       = "bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id   = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ============================================================================
// 7d) MVC Y MODEL BINDERS
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

// UseHttpsRedirection solo en producción — en desarrollo la app móvil
// usa HTTP plano y no puede seguir redirects a HTTPS con cert autofirmado.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// ── Headers de seguridad ──────────────────────────────────────────────────
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    await next();
});

app.UseStaticFiles(); // sirve wwwroot (CSS, JS, imágenes estáticas del proyecto)

// ── Servir archivos subidos por usuarios desde la carpeta externa ─────────
{
    var fileStorage = app.Services.GetRequiredService<IFileStorageService>();
    var uploadsPath = fileStorage.RutaBase;
    Directory.CreateDirectory(uploadsPath);

    // Las URLs /images/... y /uploads/... se sirven desde la carpeta externa
    // en lugar de desde wwwroot. Las rutas guardadas en BD siguen siendo las mismas.
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(uploadsPath),
        RequestPath  = string.Empty,   // sin prefijo: /images/... mapea directo
        ServeUnknownFileTypes = false,
        OnPrepareResponse = ctx =>
        {
            // Cache de 7 días para imágenes de producto
            ctx.Context.Response.Headers["Cache-Control"] = "public,max-age=604800";
        }
    });
}

// ── Swagger (solo en desarrollo, accesible en /swagger) ──────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Simone POS API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseCors("ReactNativePolicy"); // CORS antes de Auth

app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.UseRequestLocalization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// ── API REST (controllers con [ApiController] + rutas /api/v1/...) ────────
app.MapControllers();

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
        // PASO 2b: Verificar columnas críticas (por si alguna migración
        //          incompleta no fue reconocida por EF Core)
        // ────────────────────────────────────────────────────────────────
        logger.LogInformation("🔍 Verificando esquema de columnas críticas...");
        await VerificarEsquemaAsync(db, logger);

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
/// Verifica y agrega columnas críticas que pueden faltar si alguna migración
/// incompleta (sin .Designer.cs) no fue reconocida por EF Core.
/// Todas las operaciones son idempotentes.
/// </summary>
static async Task VerificarEsquemaAsync(TiendaDbContext db, ILogger logger)
{
    // Lista de (tabla, columna, definición SQL) que deben existir
    var columnas = new (string Tabla, string Columna, string Definicion)[]
    {
        // SubPedidos
        ("SubPedidos",          "FechaEntrega",  "datetime2 NULL"),
        // EnviosConsolidados
        ("EnviosConsolidados",  "FechaEntrega",  "datetime2 NULL"),
        // Vendedores — perfil de tienda
        ("Vendedores",  "Slug",         "nvarchar(100) NULL"),
        ("Vendedores",  "Bio",          "nvarchar(500) NULL"),
        ("Vendedores",  "BannerPath",   "nvarchar(300) NULL"),
        ("Vendedores",  "Verificado",   "bit NOT NULL DEFAULT 0"),
        ("Vendedores",  "InstagramUrl", "nvarchar(200) NULL"),
        ("Vendedores",  "TikTokUrl",    "nvarchar(200) NULL"),
        ("Vendedores",  "FacebookUrl",  "nvarchar(200) NULL"),
    };

    foreach (var (tabla, columna, def) in columnas)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync($@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID(N'{tabla}') AND name = N'{columna}'
                )
                BEGIN
                    ALTER TABLE [{tabla}] ADD [{columna}] {def};
                END
            ");
            logger.LogDebug("  ✔ {Tabla}.{Columna} verificada", tabla, columna);
        }
        catch (Exception ex)
        {
            logger.LogWarning("  ⚠️ No se pudo verificar {Tabla}.{Columna}: {Msg}", tabla, columna, ex.Message);
        }
    }

    // Tabla ReservasStock — crear si no existe
    try
    {
        await db.Database.ExecuteSqlRawAsync(@"
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'ReservasStock')
            BEGIN
                CREATE TABLE ReservasStock (
                    ReservaStockId      int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    ProductoID          int NOT NULL,
                    ProductoVarianteID  int NULL,
                    Cantidad            int NOT NULL,
                    UsuarioId           nvarchar(450) NULL,
                    Canal               nvarchar(20)  NOT NULL,
                    SesionPosId         nvarchar(64)  NULL,
                    FechaCreacion       datetime2     NOT NULL,
                    Expiracion          datetime2     NOT NULL,
                    Confirmada          bit           NOT NULL DEFAULT 0,
                    CONSTRAINT FK_ReservasStock_Productos
                        FOREIGN KEY (ProductoID) REFERENCES Productos(ProductoID) ON DELETE CASCADE,
                    CONSTRAINT FK_ReservasStock_ProductoVariantes
                        FOREIGN KEY (ProductoVarianteID) REFERENCES ProductoVariantes(ProductoVarianteID)
                );
                CREATE INDEX IX_ReservasStock_ProductoID         ON ReservasStock(ProductoID);
                CREATE INDEX IX_ReservasStock_ProductoVarianteID ON ReservasStock(ProductoVarianteID);
            END
        ");
        logger.LogDebug("  ✔ Tabla ReservasStock verificada");
    }
    catch (Exception ex)
    {
        logger.LogWarning("  ⚠️ No se pudo verificar tabla ReservasStock: {Msg}", ex.Message);
    }

    logger.LogInformation("✅ Verificación de esquema completada");
}

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