using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Simone.Data;
using Simone.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Simone.Controllers
{
    /// <summary>
    /// Perfil público de tienda/vendedor.
    /// Ruta: /tienda/{slug}
    /// </summary>
    public class TiendaController : Controller
    {
        private readonly TiendaDbContext _context;

        public TiendaController(TiendaDbContext context)
        {
            _context = context;
        }

        // GET /tienda/{slug}?cat=3&orden=precio_asc
        [HttpGet("/tienda/{slug}")]
        public async Task<IActionResult> Ver(
            string slug,
            int? cat = null,
            string? orden = null)
        {
            if (string.IsNullOrWhiteSpace(slug))
                return NotFound();

            // ── 1. Cargar vendedor por slug ────────────────────────────────
            var vendedor = await _context.Vendedores
                .Include(v => v.Contactos)
                .FirstOrDefaultAsync(v =>
                    v.Slug != null &&
                    v.Slug.ToLower() == slug.ToLower() &&
                    v.Activo);

            if (vendedor == null)
                return NotFound();

            // ── 2. Encontrar el Usuario vinculado a este Vendedor ──────────
            var usuario = await _context.Users
                .Where(u => u.VendedorId == vendedor.VendedorId && u.Activo)
                .FirstOrDefaultAsync();

            if (usuario == null)
                return NotFound();

            // ── 3. Cargar productos del vendedor ───────────────────────────
            var query = _context.Productos
                .Include(p => p.Variantes)
                .Include(p => p.Categoria)
                .Where(p => p.VendedorID == usuario.Id);

            // Filtrar por categoría si se seleccionó una
            if (cat.HasValue && cat > 0)
                query = query.Where(p => p.CategoriaID == cat.Value);

            // Ordenar
            query = orden switch
            {
                "precio_asc"  => query.OrderBy(p => p.PrecioVenta),
                "precio_desc" => query.OrderByDescending(p => p.PrecioVenta),
                "nombre_asc"  => query.OrderBy(p => p.Nombre),
                "antiguos"    => query.OrderBy(p => p.FechaAgregado),
                _             => query.OrderByDescending(p => p.FechaAgregado) // recientes primero
            };

            var productos = await query.ToListAsync();

            // ── 4. Categorías que tiene este vendedor (para los filtros) ───
            // Obtener IDs únicos de categorías, luego cargar las entidades
            var catIds = await _context.Productos
                .Where(p => p.VendedorID == usuario.Id && p.CategoriaID != 0)
                .Select(p => p.CategoriaID)
                .Distinct()
                .ToListAsync();

            var categorias = await _context.Categorias
                .Where(c => catIds.Contains(c.CategoriaID))
                .OrderBy(c => c.Nombre)
                .ToListAsync();

            // ── 5. Estadísticas públicas ───────────────────────────────────
            var totalProductos = await _context.Productos
                .CountAsync(p => p.VendedorID == usuario.Id);

            var totalVentas = await _context.DetallesCompra
                .Where(d => d.Producto != null && d.Producto.VendedorID == usuario.Id)
                .SumAsync(d => (int?)d.Cantidad) ?? 0;

            var rating = await _context.Reseñas
                .Where(r => r.Producto != null && r.Producto.VendedorID == usuario.Id)
                .AverageAsync(r => (double?)r.Calificacion) ?? 0.0;

            var totalResenas = await _context.Reseñas
                .Where(r => r.Producto != null && r.Producto.VendedorID == usuario.Id)
                .CountAsync();

            // Contacto principal de WhatsApp
            var whatsapp = vendedor.Contactos
                .FirstOrDefault(c => c.Tipo == "whatsapp" && c.Principal)?.Valor
                ?? vendedor.Contactos.FirstOrDefault(c => c.Tipo == "whatsapp")?.Valor;

            var vm = new TiendaViewModel
            {
                Vendedor      = vendedor,
                Usuario       = usuario,
                Productos     = productos,
                Categorias    = categorias,
                CatSeleccionada = cat,
                OrdenSeleccionado = orden ?? "recientes",
                TotalProductos = totalProductos,
                TotalVentas   = totalVentas,
                RatingPromedio = Math.Round(rating, 1),
                TotalResenas  = totalResenas,
                WhatsApp      = whatsapp,
            };

            return View(vm);
        }
    }

    // ── ViewModel ────────────────────────────────────────────────────────────
    public class TiendaViewModel
    {
        public Vendedor           Vendedor          { get; set; } = null!;
        public Usuario            Usuario           { get; set; } = null!;
        public List<Producto>     Productos         { get; set; } = new();
        public List<Categorias>   Categorias        { get; set; } = new();
        public int?               CatSeleccionada   { get; set; }
        public string             OrdenSeleccionado { get; set; } = "recientes";
        public int                TotalProductos    { get; set; }
        public int                TotalVentas       { get; set; }
        public double             RatingPromedio    { get; set; }
        public int                TotalResenas      { get; set; }
        public string?            WhatsApp          { get; set; }
    }
}
