using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Simone.Data;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Simone.Controllers
{
    [Authorize(Roles = "Administrador")] // Solo administradores pueden ver los reportes
    public class ReportesController : Controller
    {
        private readonly TiendaDbContext _context;

        public ReportesController(TiendaDbContext context)
        {
            _context = context;
        }

        // GET: /Reportes
        public async Task<IActionResult> Index()
        {
            var totalVentas = await _context.Ventas.CountAsync();
            var totalIngresos = await _context.Ventas.SumAsync(v => (decimal?)v.Total) ?? 0;
            var productosVendidos = await _context.DetalleVentas.SumAsync(dv => (int?)dv.Cantidad) ?? 0;
            var clientesNuevos = await _context.Clientes.CountAsync(c => c.FechaRegistro >= DateTime.Now.AddMonths(-1));

            ViewBag.TotalVentas = totalVentas;
            ViewBag.TotalIngresos = totalIngresos;
            ViewBag.ProductosVendidos = productosVendidos;
            ViewBag.ClientesNuevos = clientesNuevos;

            var ventasRecientes = await _context.Ventas
                .Include(v => v.Clientes)
                .OrderByDescending(v => v.FechaVenta)
                .Take(10)
                .ToListAsync();

            return View(ventasRecientes); // Se asume vista en Views/Reportes/Index.cshtml
        }
    }
}
