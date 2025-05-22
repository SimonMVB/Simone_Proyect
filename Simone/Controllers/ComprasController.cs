using Microsoft.AspNetCore.Mvc;
using Simone.Models;
using Simone.Data;
using Microsoft.EntityFrameworkCore;
using Simone.Extensions;
using Simone.Services;

namespace Simone.Controllers
{
    public class ComprasController : Controller
    {
        private readonly TiendaDbContext _context;
        private readonly ProductosService _productos;
        private readonly CategoriasService _categorias;
        private readonly SubcategoriasService _subcategorias;
        public ComprasController(TiendaDbContext context, ProductosService productos, CategoriasService categorias, SubcategoriasService subcategorias)
        {
            _context = context;
            _productos = productos;
            _categorias = categorias;
            _subcategorias = subcategorias;
        }

        [HttpGet]
        public async Task<IActionResult> Catalogo(int? categoriaID, int[] subcategoriaIDs, int pageNumber = 1, int pageSize = 20)
        {
            // Load categories for filter display
            var categorias = await _categorias.GetAllAsync();

            // Load subcategories for the selected category (or empty if none)
            var subcategorias = categoriaID.HasValue
                ? await _subcategorias.GetByCategoriaIdAsync(categoriaID.Value)
                : new List<Subcategorias>();

            // Prepare queryable filtered products
            IQueryable<Producto> query =  _context.Productos;

            if (categoriaID.HasValue)
            {
                query = query.Where(p => p.CategoriaID == categoriaID.Value);
            }

            if (subcategoriaIDs != null && subcategoriaIDs.Length > 0)
            {
                query = query.Where(p => subcategoriaIDs.Contains(p.SubcategoriaID));
            }

            var totalProducts = await query.CountAsync();

            var productos = await query
                .OrderBy(p => p.Nombre) // or any ordering
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var model = new CatalogoViewModel
            {
                Categorias = categorias,
                SelectedCategoriaID = categoriaID,
                Subcategorias = subcategorias,
                SelectedSubcategoriaIDs = subcategoriaIDs?.ToList() ?? new List<int>(),
                Productos = productos,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalProducts = totalProducts
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> VerProducto(int productoID)
        {
            var producto = await _productos.GetByIdAsync(productoID);
            if (producto != null)
            {
                ViewBag.Producto = producto;
                return View();
            }
            else
            {
                return View("Invalido");
            }
        }

    }
}