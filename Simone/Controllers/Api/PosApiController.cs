using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Simone.Data;
using Simone.Models;
using System.Security.Claims;

namespace Simone.Controllers.Api
{
    /// <summary>
    /// API POS para la app móvil React Native.
    /// Todas las rutas requieren JWT válido con rol Vendedor o Administrador.
    /// Base: /api/v1/pos
    /// </summary>
    [ApiController]
    [Route("api/v1/pos")]
    [Authorize(AuthenticationSchemes = "JwtBearer")]
    public class PosApiController : ControllerBase
    {
        private readonly TiendaDbContext _ctx;

        public PosApiController(TiendaDbContext ctx)
        {
            _ctx = ctx;
        }

        // ════════════════════════════════════════════════════════════════════
        //  PRODUCTOS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Buscar productos del vendedor autenticado.
        /// GET /api/v1/pos/productos?q=vestido&catId=3&page=1&pageSize=20
        /// </summary>
        [HttpGet("productos")]
        public async Task<IActionResult> BuscarProductos(
            string? q        = null,
            int?    catId    = null,
            int     page     = 1,
            int     pageSize = 20)
        {
            var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (usuarioId == null) return Unauthorized();

            pageSize = Math.Clamp(pageSize, 1, 100);
            page     = Math.Max(1, page);

            var query = _ctx.Productos
                .Include(p => p.Categoria)
                .Include(p => p.Variantes)
                .Include(p => p.Imagenes)
                .Where(p => p.VendedorID == usuarioId);

            // Búsqueda por texto
            if (!string.IsNullOrWhiteSpace(q))
            {
                var q2 = q.ToLower();
                query = query.Where(p =>
                    p.Nombre.ToLower().Contains(q2) ||
                    (p.Descripcion != null && p.Descripcion.ToLower().Contains(q2)) ||
                    (p.Marca      != null && p.Marca.ToLower().Contains(q2)));
            }

            // Filtrar por categoría
            if (catId.HasValue && catId > 0)
                query = query.Where(p => p.CategoriaID == catId.Value);

            var total = await query.CountAsync();

            var productos = await query
                .OrderByDescending(p => p.FechaAgregado)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var resultado = productos.Select(p => new
            {
                p.ProductoID,
                p.Nombre,
                p.Descripcion,
                p.PrecioVenta,
                p.PrecioCompra,
                StockTotal    = p.StockDisponible,
                TieneVariantes = p.TieneVariantes,
                Imagen        = p.ImagenPrincipalOrPlaceholder,
                Categoria     = p.Categoria?.Nombre,
                Variantes     = p.Variantes.Select(v => new
                {
                    v.ProductoVarianteID,
                    v.Color,
                    v.Talla,
                    v.Stock,
                    Precio = v.PrecioVenta ?? p.PrecioVenta,
                    v.SKU,
                    v.ImagenPath
                })
            });

            return Ok(new
            {
                Total    = total,
                Pagina   = page,
                PageSize = pageSize,
                Paginas  = (int)Math.Ceiling((double)total / pageSize),
                Items    = resultado
            });
        }

        /// <summary>
        /// Detalle completo de un producto.
        /// GET /api/v1/pos/productos/{id}
        /// </summary>
        [HttpGet("productos/{id:int}")]
        public async Task<IActionResult> DetalleProducto(int id)
        {
            var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (usuarioId == null) return Unauthorized();

            var p = await _ctx.Productos
                .Include(x => x.Categoria)
                .Include(x => x.Variantes)
                .Include(x => x.Imagenes)
                .FirstOrDefaultAsync(x => x.ProductoID == id && x.VendedorID == usuarioId);

            if (p == null) return NotFound();

            return Ok(new
            {
                p.ProductoID,
                p.Nombre,
                p.Descripcion,
                p.PrecioVenta,
                p.PrecioCompra,
                p.Stock,
                StockTotal     = p.StockDisponible,
                TieneVariantes = p.TieneVariantes,
                p.Marca,
                p.Talla,
                p.Color,
                Imagenes   = p.Imagenes.OrderByDescending(i => i.Principal).ThenBy(i => i.Orden).Select(i => i.Path),
                Categoria  = p.Categoria != null ? new { p.Categoria.CategoriaID, p.Categoria.Nombre } : null,
                Variantes  = p.Variantes.Select(v => new
                {
                    v.ProductoVarianteID,
                    v.Color,
                    v.Talla,
                    v.Stock,
                    Precio = v.PrecioVenta ?? p.PrecioVenta,
                    v.SKU,
                    v.ImagenPath
                })
            });
        }

        // ════════════════════════════════════════════════════════════════════
        //  VENTAS POS
        // ════════════════════════════════════════════════════════════════════

        // ── DTOs ─────────────────────────────────────────────────────────────

        public record ItemVentaDto(
            int  ProductoId,
            int? VarianteId,
            int  Cantidad,
            decimal? PrecioOverride   // null = usar precio del producto/variante
        );

        public record NuevaVentaPosDto(
            IList<ItemVentaDto> Items,
            string MetodoPago,         // "efectivo", "tarjeta", "transferencia", etc.
            string? NombreCliente,
            string? NotasExtra
        );

        /// <summary>
        /// Registra una venta física (POS).
        /// Descuenta stock, crea Venta + DetalleVentas + MovimientosInventario.
        /// POST /api/v1/pos/ventas
        /// </summary>
        [HttpPost("ventas")]
        public async Task<IActionResult> RegistrarVenta([FromBody] NuevaVentaPosDto dto)
        {
            if (dto.Items == null || dto.Items.Count == 0)
                return BadRequest(new { error = "Debe incluir al menos un producto." });

            var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (usuarioId == null) return Unauthorized();

            // ── Validación: todos los productos pertenecen a este vendedor ──
            var productoIds = dto.Items.Select(i => i.ProductoId).Distinct().ToList();

            var productos = await _ctx.Productos
                .Include(p => p.Variantes)
                .Where(p => productoIds.Contains(p.ProductoID) && p.VendedorID == usuarioId)
                .ToListAsync();

            if (productos.Count != productoIds.Count)
                return BadRequest(new { error = "Algunos productos no se encontraron o no te pertenecen." });

            // ── Verificar stock ────────────────────────────────────────────
            foreach (var item in dto.Items)
            {
                var prod = productos.First(p => p.ProductoID == item.ProductoId);

                if (item.VarianteId.HasValue)
                {
                    var variante = prod.Variantes.FirstOrDefault(v => v.ProductoVarianteID == item.VarianteId.Value);
                    if (variante == null)
                        return BadRequest(new { error = $"Variante {item.VarianteId} no encontrada en producto {prod.Nombre}." });
                    if (variante.Stock < item.Cantidad)
                        return BadRequest(new { error = $"Stock insuficiente para {prod.Nombre} ({variante.Color}/{variante.Talla}). Disponible: {variante.Stock}." });
                }
                else
                {
                    var stockDisp = prod.TieneVariantes ? prod.Variantes.Sum(v => v.Stock) : prod.Stock;
                    if (stockDisp < item.Cantidad)
                        return BadRequest(new { error = $"Stock insuficiente para {prod.Nombre}. Disponible: {stockDisp}." });
                }
            }

            // ── Transacción: crear venta + descontar stock ─────────────────
            await using var tx = await _ctx.Database.BeginTransactionAsync();
            try
            {
                // 1. Crear la venta
                var venta = new Ventas
                {
                    UsuarioId  = usuarioId,   // vendedor = el que hace la venta POS
                    EmpleadoID = usuarioId,
                    Estado     = "Completada",
                    MetodoPago = dto.MetodoPago ?? "efectivo",
                    FechaVenta = DateTime.UtcNow,
                    Total      = 0m,
                    Depositante = dto.NombreCliente
                };
                _ctx.Ventas.Add(venta);
                await _ctx.SaveChangesAsync(); // necesitamos el VentaID

                decimal totalVenta = 0m;

                // 2. Crear detalles y descontar stock
                foreach (var item in dto.Items)
                {
                    var prod = productos.First(p => p.ProductoID == item.ProductoId);
                    decimal precioUnit;
                    int?    varianteId = null;

                    if (item.VarianteId.HasValue)
                    {
                        var v = prod.Variantes.First(x => x.ProductoVarianteID == item.VarianteId.Value);
                        precioUnit = item.PrecioOverride ?? v.PrecioVenta ?? prod.PrecioVenta;
                        varianteId = v.ProductoVarianteID;

                        // Descontar stock en variante
                        v.Stock -= item.Cantidad;
                    }
                    else
                    {
                        precioUnit = item.PrecioOverride ?? prod.PrecioVenta;

                        // Descontar stock en producto base
                        prod.Stock -= item.Cantidad;
                    }

                    var subtotal = precioUnit * item.Cantidad;
                    totalVenta  += subtotal;

                    // Detalle venta
                    _ctx.DetalleVentas.Add(new DetalleVentas
                    {
                        VentaID             = venta.VentaID,
                        ProductoID          = prod.ProductoID,
                        ProductoVarianteID  = varianteId,
                        Cantidad            = item.Cantidad,
                        PrecioUnitario      = precioUnit,
                        Subtotal            = subtotal,
                        FechaCreacion       = DateTime.UtcNow
                    });

                    // Movimiento inventario (auditoría)
                    _ctx.MovimientosInventario.Add(new MovimientosInventario
                    {
                        ProductoID          = prod.ProductoID,
                        ProductoVarianteID  = varianteId,
                        Cantidad            = -item.Cantidad,   // salida
                        TipoMovimiento      = "Salida",
                        FechaMovimiento     = DateTime.UtcNow,
                        Descripcion         = $"Venta POS #{venta.VentaID} - {dto.MetodoPago}"
                    });
                }

                // 3. Actualizar total
                venta.Total = totalVenta;
                await _ctx.SaveChangesAsync();

                await tx.CommitAsync();

                return Ok(new
                {
                    VentaId     = venta.VentaID,
                    Total       = venta.Total,
                    FechaVenta  = venta.FechaVenta,
                    MetodoPago  = venta.MetodoPago,
                    Estado      = venta.Estado,
                    mensaje     = "Venta registrada correctamente."
                });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return StatusCode(500, new { error = "Error al procesar la venta.", detalle = ex.Message });
            }
        }

        /// <summary>
        /// Historial de ventas POS del vendedor.
        /// GET /api/v1/pos/ventas?page=1&pageSize=30
        /// </summary>
        [HttpGet("ventas")]
        public async Task<IActionResult> HistorialVentas(int page = 1, int pageSize = 30)
        {
            var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (usuarioId == null) return Unauthorized();

            pageSize = Math.Clamp(pageSize, 1, 100);
            page     = Math.Max(1, page);

            var query = _ctx.Ventas
                .Include(v => v.DetalleVentas)
                    .ThenInclude(d => d.Producto)
                .Where(v => v.EmpleadoID == usuarioId)
                .OrderByDescending(v => v.FechaVenta);

            var total  = await query.CountAsync();
            var ventas = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var resultado = ventas.Select(v => new
            {
                v.VentaID,
                v.Total,
                v.MetodoPago,
                v.Estado,
                v.FechaVenta,
                v.Depositante,
                Lineas = v.DetalleVentas.Select(d => new
                {
                    d.ProductoID,
                    NombreProducto = d.Producto?.Nombre,
                    d.Cantidad,
                    d.PrecioUnitario,
                    d.Subtotal
                })
            });

            return Ok(new
            {
                Total    = total,
                Pagina   = page,
                PageSize = pageSize,
                Paginas  = (int)Math.Ceiling((double)total / pageSize),
                Items    = resultado
            });
        }

        // ════════════════════════════════════════════════════════════════════
        //  RESUMEN / DASHBOARD POS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Resumen rápido para el dashboard de la app.
        /// GET /api/v1/pos/dashboard
        /// </summary>
        [HttpGet("dashboard")]
        public async Task<IActionResult> Dashboard()
        {
            var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (usuarioId == null) return Unauthorized();

            var hoy     = DateTime.UtcNow.Date;
            var semana  = hoy.AddDays(-7);
            var mes     = new DateTime(hoy.Year, hoy.Month, 1);

            // Ventas del día (POS)
            var ventasHoy = await _ctx.Ventas
                .Where(v => v.EmpleadoID == usuarioId && v.FechaVenta.Date == hoy)
                .SumAsync(v => (decimal?)v.Total) ?? 0m;

            // Ventas de la semana
            var ventasSemana = await _ctx.Ventas
                .Where(v => v.EmpleadoID == usuarioId && v.FechaVenta >= semana)
                .SumAsync(v => (decimal?)v.Total) ?? 0m;

            // Ventas del mes
            var ventasMes = await _ctx.Ventas
                .Where(v => v.EmpleadoID == usuarioId && v.FechaVenta >= mes)
                .SumAsync(v => (decimal?)v.Total) ?? 0m;

            // Total transacciones hoy
            var txHoy = await _ctx.Ventas
                .CountAsync(v => v.EmpleadoID == usuarioId && v.FechaVenta.Date == hoy);

            // Productos con stock bajo (≤5)
            var stockBajo = await _ctx.Productos
                .Where(p => p.VendedorID == usuarioId && p.Stock <= 5)
                .CountAsync();

            // Total productos activos
            var totalProductos = await _ctx.Productos
                .CountAsync(p => p.VendedorID == usuarioId);

            return Ok(new
            {
                VentasHoy    = ventasHoy,
                VentasSemana = ventasSemana,
                VentasMes    = ventasMes,
                TxHoy        = txHoy,
                StockBajo    = stockBajo,
                TotalProductos = totalProductos,
                FechaServidor  = DateTime.UtcNow
            });
        }
    }
}
