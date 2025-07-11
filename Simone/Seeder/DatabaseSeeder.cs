using Microsoft.EntityFrameworkCore;
using Simone.Data;
using Simone.Models;
using Microsoft.AspNetCore.Identity;

public class DatabaseSeeder
{
    private readonly TiendaDbContext _context;
    private readonly UserManager<Usuario> _userManager;

    public DatabaseSeeder(TiendaDbContext context, UserManager<Usuario> user)
    {
        _context = context;
        _userManager = user;
    }

    public async Task SeedCategoriesAndSubcategoriesAsync()
    {
        // Seed Categories and update if necessary
        await SeedCategoriesAsync();

        // Seed Subcategories and update if necessary
        await SeedSubcategoriesAsync();

        // Seed the admin user, carrito and carritodetalles
        await SeedAdminCarritoAsync();
    }

    private async Task SeedCategoriesAsync()
    {
        var categorias = new List<Categorias>
        {
            new Categorias { Nombre = "Blusas" },
            new Categorias { Nombre = "Tops" },
            new Categorias { Nombre = "Body's" },
            new Categorias { Nombre = "Trajes de Baño" },
            new Categorias { Nombre = "Conjuntos" },
            new Categorias { Nombre = "Vestidos" },
            new Categorias { Nombre = "Faldas" },
            new Categorias { Nombre = "Pantalones" },
            new Categorias { Nombre = "Jeans" },
            new Categorias { Nombre = "Bolsas" }
        };

        foreach (var categoria in categorias)
        {
            var existingCategory = await _context.Categorias
                .FirstOrDefaultAsync(c => c.Nombre == categoria.Nombre);

            if (existingCategory == null)
            {
                await _context.Categorias.AddAsync(categoria);
            }
            else
            {
                existingCategory.Nombre = categoria.Nombre;
                _context.Categorias.Update(existingCategory);
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task SeedSubcategoriesAsync()
    {
        var subcategorias = new List<Subcategorias>
        {
            new Subcategorias { NombreSubcategoria = "Manga larga", CategoriaID = 1 },
            new Subcategorias { NombreSubcategoria = "Manga corta", CategoriaID = 1 },
            // Add all other subcategories here...
        };

        foreach (var subcategoria in subcategorias)
        {
            var existingSubcategory = await _context.Subcategorias
                .FirstOrDefaultAsync(sc => sc.NombreSubcategoria == subcategoria.NombreSubcategoria
                                            && sc.CategoriaID == subcategoria.CategoriaID);

            if (existingSubcategory == null)
            {
                await _context.Subcategorias.AddAsync(subcategoria);
            }
            else
            {
                existingSubcategory.NombreSubcategoria = subcategoria.NombreSubcategoria;
                _context.Subcategorias.Update(existingSubcategory);
            }
        }

        await _context.SaveChangesAsync();
    }

    // This method will create the admin carrito and carrito detalles
    private async Task SeedAdminCarritoAsync()
    {

        var adminUser = await _userManager.Users.FirstOrDefaultAsync(u => u.UserName == "admin@tienda.com");

        // Create the carrito (shopping cart) for the admin user
        var adminCarrito = new Carrito
        {
            ClienteID = adminUser.Id,  // Link to the admin user
            FechaCreacion = DateTime.Now,
            EstadoCarrito = "Abierto"
        };

        _context.Carrito.Add(adminCarrito);
        await _context.SaveChangesAsync();

    }
}
