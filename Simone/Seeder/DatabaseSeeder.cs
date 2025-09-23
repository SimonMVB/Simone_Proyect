using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Simone.Data;
using Simone.Models;

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
        await SeedCategoriesAsync();
        await SeedSubcategoriesAsync();
        await SeedAdminCarritoAsync();
    }

    private async Task SeedCategoriesAsync()
    {
        var categorias = new[]
        {
            "Blusas","Tops","Body's","Trajes de Baño","Conjuntos",
            "Vestidos","Faldas","Pantalones","Jeans","Bolsas"
        };

        foreach (var nombre in categorias)
        {
            var existente = await _context.Categorias.FirstOrDefaultAsync(c => c.Nombre == nombre);
            if (existente == null)
            {
                await _context.Categorias.AddAsync(new Categorias { Nombre = nombre });
            }
            else
            {
                // Si algún día cambias visualmente nombres (tildes/espacios), lo sincroniza
                existente.Nombre = nombre;
                _context.Categorias.Update(existente);
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task SeedSubcategoriesAsync()
    {
        // Mapea subcategorías a la categoría por NOMBRE (no asumimos IDs fijos)
        var subcategoriasPorCategoria = new Dictionary<string, List<string>>
        {
            { "Blusas", new List<string> { "Manga larga", "Manga corta" } },
            // Agrega aquí las demás subcategorías por categoría...
            // { "Vestidos", new List<string> { "Casual", "Fiesta" } },
        };

        foreach (var kvp in subcategoriasPorCategoria)
        {
            var categoriaNombre = kvp.Key;
            var subcats = kvp.Value;

            var categoriaId = await _context.Categorias
                .Where(c => c.Nombre == categoriaNombre)
                .Select(c => c.CategoriaID)
                .FirstOrDefaultAsync();

            // Si no existe la categoría (algo falló arriba), continúa con la siguiente
            if (categoriaId == 0) continue;

            foreach (var subcatNombre in subcats)
            {
                var existente = await _context.Subcategorias
                    .FirstOrDefaultAsync(sc => sc.NombreSubcategoria == subcatNombre &&
                                               sc.CategoriaID == categoriaId);

                if (existente == null)
                {
                    await _context.Subcategorias.AddAsync(new Subcategorias
                    {
                        CategoriaID = categoriaId,
                        NombreSubcategoria = subcatNombre
                    });
                }
                else
                {
                    existente.NombreSubcategoria = subcatNombre;
                    _context.Subcategorias.Update(existente);
                }
            }
        }

        await _context.SaveChangesAsync();
    }

    // Crea un carrito para el admin solo si no tiene uno "abierto" (no cerrado)
    private async Task SeedAdminCarritoAsync()
    {
        var adminUser = await _userManager.Users.FirstOrDefaultAsync(u => u.UserName == "admin@tienda.com");
        if (adminUser == null)
        {
            // Si no existe el admin, no hacemos nada aquí.
            // (Opcional) Crear admin por seed inicial en otro método.
            return;
        }

        var carritoExistente = await _context.Carrito
            .FirstOrDefaultAsync(c => c.UsuarioId == adminUser.Id && c.EstadoCarrito != "Cerrado");

        if (carritoExistente != null) return; // ya tiene carrito activo

        var adminCarrito = new Carrito
        {
            UsuarioId = adminUser.Id,      // ← centralizado en Usuario
            FechaCreacion = DateTime.UtcNow,
            EstadoCarrito = "Vacio"        // usa el estado de tu dominio
        };

        _context.Carrito.Add(adminCarrito);
        await _context.SaveChangesAsync();
    }
}
