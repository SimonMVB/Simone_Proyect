using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Simone.Data;
using Simone.Models;
using Simone.ViewModels.Devoluciones;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;

namespace Simone.Controllers
{
    [Authorize]
    public class DevolucionesController : Controller
    {
        private readonly TiendaDbContext _context;
        private readonly UserManager<Usuario> _userManager;

        public DevolucionesController(TiendaDbContext context, UserManager<Usuario> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: /Devoluciones/Crear?ventaId=123
        [HttpGet]
        public async Task<IActionResult> Crear(int ventaId, string? returnUrl)
        {
            var venta = await _context.Ventas
                .Include(v => v.Usuario)
                .Include(v => v.DetalleVentas).ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(v => v.VentaID == ventaId);

            if (venta == null) return NotFound();

            // Permitir dueño de la venta o Admin
            var user = await _userManager.GetUserAsync(User);
            var esPropietario = venta.UsuarioId == user?.Id;
            var esAdmin = User.IsInRole("Administrador");
            if (!esPropietario && !esAdmin) return Forbid();

            // Devueltas acumuladas por detalle
            var devAcum = await _context.Devoluciones
                .Where(x => x.DetalleVentaID != 0 && x.DetalleVenta.VentaID == ventaId && x.Aprobada)
                .GroupBy(x => x.DetalleVentaID)
                .Select(g => new { DetalleVentaID = g.Key, Cant = g.Sum(x => x.CantidadDevuelta) })
                .ToDictionaryAsync(t => t.DetalleVentaID, t => t.Cant);

            var vm = new CrearDevolucionVM
            {
                VentaId = venta.VentaID,
                ReturnUrl = returnUrl,
                Motivo = "devolucion",
                Lineas = venta.DetalleVentas.Select(d => new CrearDevolucionVM.LineaVM
                {
                    DetalleVentaID = d.DetalleVentaID,
                    Producto = d.Producto?.Nombre ?? $"Producto #{d.ProductoID}",
                    CantidadVendida = d.Cantidad,
                    CantidadDevueltaAcumulada = devAcum.TryGetValue(d.DetalleVentaID, out var c) ? c : 0,
                    CantidadADevolver = 0 // por defecto 0 para evitar errores
                }).ToList()
            };

            return View(vm);
        }

        // POST: /Devoluciones/Crear
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(CrearDevolucionVM vm)
        {
            if (!ModelState.IsValid)
                return View(await Recargar(vm));

            var venta = await _context.Ventas
                .Include(v => v.Usuario)
                .Include(v => v.DetalleVentas).ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(v => v.VentaID == vm.VentaId);

            if (venta == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            var esPropietario = venta.UsuarioId == user?.Id;
            var esAdmin = User.IsInRole("Administrador");
            if (!esPropietario && !esAdmin) return Forbid();

            // Diccionario con devueltas acumuladas vigentes
            var devAcum = await _context.Devoluciones
                .Where(x => x.DetalleVenta.VentaID == vm.VentaId && x.Aprobada)
                .GroupBy(x => x.DetalleVentaID)
                .Select(g => new { g.Key, Cant = g.Sum(x => x.CantidadDevuelta) })
                .ToDictionaryAsync(t => t.Key, t => t.Cant);

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                foreach (var linea in vm.Lineas)
                {
                    if (linea.CantidadADevolver <= 0) continue;

                    var det = venta.DetalleVentas.FirstOrDefault(d => d.DetalleVentaID == linea.DetalleVentaID);
                    if (det == null) { continue; }

                    var yaDev = devAcum.TryGetValue(det.DetalleVentaID, out var c) ? c : 0;
                    var max = det.Cantidad - yaDev;

                    if (linea.CantidadADevolver > max)
                    {
                        ModelState.AddModelError(string.Empty,
                            $"La línea {det.DetalleVentaID} supera el máximo permitido ({max}).");
                        await tx.RollbackAsync();
                        return View(await Recargar(vm));
                    }

                    // Crear registro de devolución
                    var dev = new Devoluciones
                    {
                        DetalleVentaID = det.DetalleVentaID,
                        FechaDevolucion = DateTime.UtcNow,
                        Motivo = vm.Motivo, // "devolucion" | "deposito_falso" | "otro"
                        CantidadDevuelta = linea.CantidadADevolver,
                        Aprobada = true
                    };
                    await _context.Devoluciones.AddAsync(dev);

                    // Reponer stock
                    if (det.Producto != null)
                    {
                        det.Producto.Stock += linea.CantidadADevolver;
                        _context.Productos.Update(det.Producto);

                        // (Opcional) registrar movimiento
                        await _context.MovimientosInventario.AddAsync(new MovimientosInventario
                        {
                            ProductoID = det.ProductoID,
                            Cantidad = linea.CantidadADevolver,
                            TipoMovimiento = "Entrada",
                            FechaMovimiento = DateTime.UtcNow,
                            Descripcion = $"Devolución Venta #{venta.VentaID} (Detalle #{det.DetalleVentaID})"
                        });
                    }
                }

                await _context.SaveChangesAsync();

                // ¿La devolución resultó total?
                var totalVendidas = venta.DetalleVentas.Sum(d => d.Cantidad);
                var totalDevueltas = await _context.Devoluciones
                    .Where(x => x.DetalleVenta.VentaID == venta.VentaID && x.Aprobada)
                    .SumAsync(x => x.CantidadDevuelta);

                if (totalDevueltas >= totalVendidas)
                {
                    // Mantén consistencia con vistas que esperan "cancelado"
                    venta.Estado = "cancelado";
                    _context.Ventas.Update(venta);
                    await _context.SaveChangesAsync();
                }

                await tx.CommitAsync();
                TempData["MensajeExito"] = "Devolución registrada correctamente.";
            }
            catch
            {
                await tx.RollbackAsync();
                TempData["MensajeError"] = "Ocurrió un error al registrar la devolución.";
                return View(await Recargar(vm));
            }

            // Redirección segura
            if (!string.IsNullOrWhiteSpace(vm.ReturnUrl) && Url.IsLocalUrl(vm.ReturnUrl))
                return Redirect(vm.ReturnUrl);

            return RedirectToAction("Index", "MisCompras");
        }

        private async Task<CrearDevolucionVM> Recargar(CrearDevolucionVM vm)
        {
            // reconstruye límites para re-render con errores
            var venta = await _context.Ventas
                .Include(v => v.DetalleVentas).ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(v => v.VentaID == vm.VentaId);

            var devAcum = await _context.Devoluciones
                .Where(x => x.DetalleVenta.VentaID == vm.VentaId && x.Aprobada)
                .GroupBy(x => x.DetalleVentaID)
                .Select(g => new { g.Key, Cant = g.Sum(x => x.CantidadDevuelta) })
                .ToDictionaryAsync(t => t.Key, t => t.Cant);

            vm.Lineas = venta?.DetalleVentas.Select(d => new CrearDevolucionVM.LineaVM
            {
                DetalleVentaID = d.DetalleVentaID,
                Producto = d.Producto?.Nombre ?? $"Producto #{d.ProductoID}",
                CantidadVendida = d.Cantidad,
                CantidadDevueltaAcumulada = devAcum.TryGetValue(d.DetalleVentaID, out var c) ? c : 0,
                // mantiene el valor ingresado por el usuario en CantidadADevolver
                CantidadADevolver = vm.Lineas.FirstOrDefault(l => l.DetalleVentaID == d.DetalleVentaID)?.CantidadADevolver ?? 0
            }).ToList() ?? new List<CrearDevolucionVM.LineaVM>();

            return vm;
        }
    }
}
