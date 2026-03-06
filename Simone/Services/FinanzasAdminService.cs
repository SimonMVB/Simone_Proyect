using Microsoft.EntityFrameworkCore;
using Simone.Data;
using Simone.Models;

namespace Simone.Services
{
    // =========================================================================
    // DTOs — objetos de retorno (sin tocar los modelos existentes)
    // =========================================================================

    public class FiltroFinanzas
    {
        public DateTime Desde { get; set; } = DateTime.UtcNow.AddMonths(-1);
        public DateTime Hasta { get; set; } = DateTime.UtcNow;
        public string? Estado { get; set; }           // estado de venta
        public string? VendedorId { get; set; }
        public string? Busqueda { get; set; }
        public int Pagina { get; set; } = 1;
        public int PorPagina { get; set; } = 25;
    }

    public class DashboardKPIs
    {
        // Ingresos
        public decimal IngresosBrutos { get; set; }
        public decimal IngresosBrutosAnterior { get; set; }
        public decimal PorcentajeCambioIngresos =>
            IngresosBrutosAnterior == 0 ? 0
            : Math.Round((IngresosBrutos - IngresosBrutosAnterior) / IngresosBrutosAnterior * 100, 1);

        // Comisiones (ingresos netos de la plataforma)
        public decimal ComisionesTotal { get; set; }
        public decimal ComisionesAnterior { get; set; }
        public decimal PorcentajeCambioComisiones =>
            ComisionesAnterior == 0 ? 0
            : Math.Round((ComisionesTotal - ComisionesAnterior) / ComisionesAnterior * 100, 1);

        // Pedidos / Ventas
        public int TotalVentas { get; set; }
        public int TotalVentasAnterior { get; set; }
        public int VentasPendientes { get; set; }
        public int VentasCompletadas { get; set; }
        public int VentasCanceladas { get; set; }

        // Ticket promedio
        public decimal TicketPromedio { get; set; }
        public decimal TicketPromedioAnterior { get; set; }

        // Vendedores y clientes
        public int VendedoresActivos { get; set; }
        public int ClientesTotales { get; set; }
        public int ClientesNuevos { get; set; }
        public int ClientesRecurrentes { get; set; }

        // Productos
        public int ProductosVendidos { get; set; }  // unidades
        public int ProductosDistintos { get; set; }  // SKUs únicos

        // Gráfico de ingresos diarios (últimos 30 días)
        public List<PuntoGrafico> IngresosDiarios { get; set; } = new();

        // Métodos de pago breakdown
        public List<MetodoPagoResumen> MetodosPago { get; set; } = new();
    }

    public class PuntoGrafico
    {
        public string Label { get; set; } = "";
        public decimal Valor { get; set; }
        public int Cantidad { get; set; }
    }

    public class MetodoPagoResumen
    {
        public string Metodo { get; set; } = "";
        public int Cantidad { get; set; }
        public decimal Total { get; set; }
        public decimal Porcentaje { get; set; }
    }

    public class VendedorResumen
    {
        public string VendedorId { get; set; } = "";
        public string NombreVendedor { get; set; } = "";
        public string Email { get; set; } = "";
        public int TotalVentas { get; set; }
        public int UnidadesVendidas { get; set; }
        public decimal IngresosBrutos { get; set; }
        public decimal Comisiones { get; set; }   // lo que la plataforma gana de este vendedor
        public decimal IngresoNeto { get; set; }   // lo que el vendedor se lleva
        public int ProductosActivos { get; set; }
        public DateTime? UltimaVenta { get; set; }
        public string Estado { get; set; } = "Activo";
    }

    public class ProductoVentaResumen
    {
        public int ProductoId { get; set; }
        public string Nombre { get; set; } = "";
        public string Categoria { get; set; } = "";
        public string NombreVendedor { get; set; } = "";
        public int UnidadesVendidas { get; set; }
        public decimal IngresoTotal { get; set; }
        public decimal PrecioPromedio { get; set; }
        public string? ImagenPath { get; set; }
    }

    public class ClienteResumen
    {
        public string UsuarioId { get; set; } = "";
        public string Nombre { get; set; } = "";
        public string Email { get; set; } = "";
        public int TotalCompras { get; set; }
        public decimal TotalGastado { get; set; }
        public decimal TicketPromedio { get; set; }
        public DateTime PrimeraCompra { get; set; }
        public DateTime UltimaCompra { get; set; }
        public bool EsRecurrente { get; set; }  // más de 1 compra
        public int DiasDesdeUltimaCompra { get; set; }
    }

    public class VentaResumen
    {
        public int VentaId { get; set; }
        public string NombreCliente { get; set; } = "";
        public string EmailCliente { get; set; } = "";
        public DateTime FechaVenta { get; set; }
        public string Estado { get; set; } = "";
        public string MetodoPago { get; set; } = "";
        public decimal Total { get; set; }
        public int CantidadItems { get; set; }
        public string? ComprobanteUrl { get; set; }
        public List<string> Vendedores { get; set; } = new();
    }

    public class IngresosPeriodo
    {
        public string Label { get; set; } = "";
        public DateTime Desde { get; set; }
        public DateTime Hasta { get; set; }
        public decimal IngresosBrutos { get; set; }
        public decimal Comisiones { get; set; }
        public int TotalVentas { get; set; }
        public decimal TicketPromedio { get; set; }
    }

    public class FinanzasPagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int Total { get; set; }
        public int Pagina { get; set; }
        public int PorPagina { get; set; }
        public int TotalPaginas => (int)Math.Ceiling((double)Total / PorPagina);
    }

    // =========================================================================
    // SERVICIO PRINCIPAL
    // =========================================================================

    public class FinanzasAdminService
    {
        private readonly TiendaDbContext _db;
        private readonly ILogger<FinanzasAdminService> _logger;

        // Porcentaje de comisión que la plataforma cobra a los vendedores
        // En el futuro esto puede venir de ConfiguracionComision
        private const decimal COMISION_PLATAFORMA = 10m;

        public FinanzasAdminService(TiendaDbContext db, ILogger<FinanzasAdminService> logger)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // ── Periodo anterior (para comparativas) ──────────────────────────────
        private static (DateTime, DateTime) PeriodoAnterior(DateTime desde, DateTime hasta)
        {
            var duracion = hasta - desde;
            return (desde - duracion, desde.AddTicks(-1));
        }

        // ─────────────────────────────────────────────────────────────────────
        // 1. DASHBOARD KPIs
        // ─────────────────────────────────────────────────────────────────────
        public async Task<DashboardKPIs> GetDashboardKPIsAsync(DateTime desde, DateTime hasta)
        {
            var (antDesde, antHasta) = PeriodoAnterior(desde, hasta);

            // Ventas del período actual
            var ventasQ = _db.Ventas
                .Where(v => v.FechaVenta >= desde && v.FechaVenta <= hasta);

            var ventasAntQ = _db.Ventas
                .Where(v => v.FechaVenta >= antDesde && v.FechaVenta <= antHasta);

            // Ventas activas (excluir canceladas para ingresos)
            var ventasActivas = ventasQ.Where(v => v.Estado != "Cancelado");
            var ventasActAnt = ventasAntQ.Where(v => v.Estado != "Cancelado");

            var ingresosBrutos = await ventasActivas.SumAsync(v => (decimal?)v.Total) ?? 0;
            var ingresosAnt = await ventasActAnt.SumAsync(v => (decimal?)v.Total) ?? 0;
            var comisiones = Math.Round(ingresosBrutos * COMISION_PLATAFORMA / 100, 2);
            var comisionesAnt = Math.Round(ingresosAnt * COMISION_PLATAFORMA / 100, 2);

            var totalVentas = await ventasQ.CountAsync();
            var totalVentasAnt = await ventasAntQ.CountAsync();
            var pendientes = await ventasQ.CountAsync(v => v.Estado == "Pendiente");
            var completadas = await ventasQ.CountAsync(v => v.Estado == "Completado" || v.Estado == "Entregado");
            var canceladas = await ventasQ.CountAsync(v => v.Estado == "Cancelado");

            var ticketPromedio = totalVentas > 0
                ? Math.Round(ingresosBrutos / totalVentas, 2) : 0;
            var ticketAnt = totalVentasAnt > 0
                ? Math.Round(ingresosAnt / totalVentasAnt, 2) : 0;

            // Clientes
            var clientesIds = await ventasQ.Select(v => v.UsuarioId).Distinct().ToListAsync();
            var clientesNuevosIds = await ventasQ
                .Where(v => !_db.Ventas.Any(vv => vv.UsuarioId == v.UsuarioId && vv.FechaVenta < desde))
                .Select(v => v.UsuarioId).Distinct().ToListAsync();

            // Vendedores activos (con al menos 1 venta en el período)
            var vendedoresActivos = await _db.DetalleVentas
                .Include(d => d.Venta)
                .Include(d => d.Producto)
                .Where(d => d.Venta.FechaVenta >= desde && d.Venta.FechaVenta <= hasta
                         && d.Venta.Estado != "Cancelado")
                .Select(d => d.Producto.VendedorID)
                .Distinct()
                .CountAsync();

            // Productos
            var detallesQ = _db.DetalleVentas
                .Include(d => d.Venta)
                .Where(d => d.Venta.FechaVenta >= desde && d.Venta.FechaVenta <= hasta
                         && d.Venta.Estado != "Cancelado");

            var unidades = await detallesQ.SumAsync(d => (int?)d.Cantidad) ?? 0;
            var productosDistinct = await detallesQ.Select(d => d.ProductoID).Distinct().CountAsync();

            // Gráfico diario (últimos N días del período)
            var dias = Math.Min((hasta - desde).Days + 1, 30);
            var inicio = hasta.AddDays(-(dias - 1)).Date;

            var ventasPorDia = await _db.Ventas
                .Where(v => v.FechaVenta >= inicio && v.FechaVenta <= hasta
                         && v.Estado != "Cancelado")
                .GroupBy(v => v.FechaVenta.Date)
                .Select(g => new { Fecha = g.Key, Total = g.Sum(v => v.Total), Count = g.Count() })
                .ToListAsync();

            var ingresosDiarios = Enumerable.Range(0, dias)
                .Select(i => inicio.AddDays(i))
                .Select(d => {
                    var found = ventasPorDia.FirstOrDefault(x => x.Fecha == d);
                    return new PuntoGrafico
                    {
                        Label = d.ToString("dd/MM"),
                        Valor = found?.Total ?? 0,
                        Cantidad = found?.Count ?? 0
                    };
                }).ToList();

            // Métodos de pago
            var metodos = await ventasQ
                .Where(v => v.Estado != "Cancelado")
                .GroupBy(v => v.MetodoPago)
                .Select(g => new { Metodo = g.Key, Count = g.Count(), Total = g.Sum(v => v.Total) })
                .ToListAsync();

            var metodosPago = metodos.Select(m => new MetodoPagoResumen
            {
                Metodo = m.Metodo,
                Cantidad = m.Count,
                Total = m.Total,
                Porcentaje = ingresosBrutos > 0
                    ? Math.Round(m.Total / ingresosBrutos * 100, 1) : 0
            }).OrderByDescending(m => m.Total).ToList();

            return new DashboardKPIs
            {
                IngresosBrutos = ingresosBrutos,
                IngresosBrutosAnterior = ingresosAnt,
                ComisionesTotal = comisiones,
                ComisionesAnterior = comisionesAnt,
                TotalVentas = totalVentas,
                TotalVentasAnterior = totalVentasAnt,
                VentasPendientes = pendientes,
                VentasCompletadas = completadas,
                VentasCanceladas = canceladas,
                TicketPromedio = ticketPromedio,
                TicketPromedioAnterior = ticketAnt,
                VendedoresActivos = vendedoresActivos,
                ClientesTotales = clientesIds.Count,
                ClientesNuevos = clientesNuevosIds.Count,
                ClientesRecurrentes = clientesIds.Count - clientesNuevosIds.Count,
                ProductosVendidos = unidades,
                ProductosDistintos = productosDistinct,
                IngresosDiarios = ingresosDiarios,
                MetodosPago = metodosPago
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // 2. INGRESOS POR PERÍODO (semana / mes / año)
        // ─────────────────────────────────────────────────────────────────────
        public async Task<List<IngresosPeriodo>> GetIngresosPorMesAsync(int anio)
        {
            var ventas = await _db.Ventas
                .Where(v => v.FechaVenta.Year == anio && v.Estado != "Cancelado")
                .GroupBy(v => v.FechaVenta.Month)
                .Select(g => new {
                    Mes = g.Key,
                    Total = g.Sum(v => v.Total),
                    Count = g.Count()
                })
                .ToListAsync();

            return Enumerable.Range(1, 12).Select(mes => {
                var found = ventas.FirstOrDefault(v => v.Mes == mes);
                var total = found?.Total ?? 0;
                var count = found?.Count ?? 0;
                return new IngresosPeriodo
                {
                    Label = new DateTime(anio, mes, 1).ToString("MMM"),
                    Desde = new DateTime(anio, mes, 1),
                    Hasta = new DateTime(anio, mes, DateTime.DaysInMonth(anio, mes)),
                    IngresosBrutos = total,
                    Comisiones = Math.Round(total * COMISION_PLATAFORMA / 100, 2),
                    TotalVentas = count,
                    TicketPromedio = count > 0 ? Math.Round(total / count, 2) : 0
                };
            }).ToList();
        }

        // ─────────────────────────────────────────────────────────────────────
        // 3. RENDIMIENTO POR VENDEDOR
        // ─────────────────────────────────────────────────────────────────────
        public async Task<List<VendedorResumen>> GetVendedoresAsync(FiltroFinanzas filtro)
        {
            var query = _db.DetalleVentas
                .Include(d => d.Venta)
                .Include(d => d.Producto)
                    .ThenInclude(p => p.Usuario)
                .Where(d => d.Venta.FechaVenta >= filtro.Desde
                         && d.Venta.FechaVenta <= filtro.Hasta
                         && d.Venta.Estado != "Cancelado");

            if (!string.IsNullOrEmpty(filtro.VendedorId))
                query = query.Where(d => d.Producto.VendedorID == filtro.VendedorId);

            var detalles = await query.ToListAsync();

            var agrupados = detalles
                .GroupBy(d => d.Producto?.VendedorID ?? "desconocido")
                .Select(g => {
                    var vendedor = g.First().Producto?.Usuario;
                    var ingresos = g.Sum(d => d.Subtotal ?? d.SubtotalCalculado);
                    return new VendedorResumen
                    {
                        VendedorId = g.Key,
                        NombreVendedor = vendedor?.NombreCompleto ?? "Vendedor desconocido",
                        Email = vendedor?.Email ?? "",
                        TotalVentas = g.Select(d => d.VentaID).Distinct().Count(),
                        UnidadesVendidas = g.Sum(d => d.Cantidad),
                        IngresosBrutos = ingresos,
                        Comisiones = Math.Round(ingresos * COMISION_PLATAFORMA / 100, 2),
                        IngresoNeto = Math.Round(ingresos * (100 - COMISION_PLATAFORMA) / 100, 2),
                        UltimaVenta = g.Max(d => d.Venta?.FechaVenta)
                    };
                })
                .OrderByDescending(v => v.IngresosBrutos)
                .ToList();

            // Contar productos activos por vendedor
            var vendedorIds = agrupados.Select(v => v.VendedorId).ToList();
            var productosActivos = await _db.Productos
                .Where(p => vendedorIds.Contains(p.VendedorID) && p.Stock > 0)
                .GroupBy(p => p.VendedorID)
                .Select(g => new { VendedorId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.VendedorId, x => x.Count);

            foreach (var v in agrupados)
                v.ProductosActivos = productosActivos.GetValueOrDefault(v.VendedorId, 0);

            return agrupados;
        }

        // ─────────────────────────────────────────────────────────────────────
        // 4. PRODUCTOS MÁS VENDIDOS
        // ─────────────────────────────────────────────────────────────────────
        public async Task<List<ProductoVentaResumen>> GetProductosMasVendidosAsync(
            FiltroFinanzas filtro, int top = 20)
        {
            var query = _db.DetalleVentas
                .Include(d => d.Venta)
                .Include(d => d.Producto)
                    .ThenInclude(p => p.Categoria)
                .Include(d => d.Producto)
                    .ThenInclude(p => p.Usuario)
                .Where(d => d.Venta.FechaVenta >= filtro.Desde
                         && d.Venta.FechaVenta <= filtro.Hasta
                         && d.Venta.Estado != "Cancelado");

            if (!string.IsNullOrEmpty(filtro.VendedorId))
                query = query.Where(d => d.Producto.VendedorID == filtro.VendedorId);

            if (!string.IsNullOrEmpty(filtro.Busqueda))
                query = query.Where(d => d.Producto.Nombre.Contains(filtro.Busqueda));

            var detalles = await query.ToListAsync();

            return detalles
                .GroupBy(d => d.ProductoID)
                .Select(g => {
                    var prod = g.First().Producto;
                    var ingresos = g.Sum(d => d.Subtotal ?? d.SubtotalCalculado);
                    var unidades = g.Sum(d => d.Cantidad);
                    return new ProductoVentaResumen
                    {
                        ProductoId = g.Key,
                        Nombre = prod?.Nombre ?? $"Producto #{g.Key}",
                        Categoria = prod?.Categoria?.Nombre ?? "Sin categoría",
                        NombreVendedor = prod?.Usuario?.NombreCompleto ?? "Desconocido",
                        UnidadesVendidas = unidades,
                        IngresoTotal = ingresos,
                        PrecioPromedio = unidades > 0
                            ? Math.Round(ingresos / unidades, 2) : 0,
                        ImagenPath = prod?.ImagenPath
                    };
                })
                .OrderByDescending(p => p.UnidadesVendidas)
                .Take(top)
                .ToList();
        }

        // ─────────────────────────────────────────────────────────────────────
        // 5. ANÁLISIS DE CLIENTES
        // ─────────────────────────────────────────────────────────────────────
        public async Task<List<ClienteResumen>> GetClientesAsync(FiltroFinanzas filtro)
        {
            var ventasClientes = await _db.Ventas
                .Include(v => v.Usuario)
                .Where(v => v.FechaVenta >= filtro.Desde
                         && v.FechaVenta <= filtro.Hasta
                         && v.Estado != "Cancelado")
                .ToListAsync();

            if (!string.IsNullOrEmpty(filtro.Busqueda))
                ventasClientes = ventasClientes
                    .Where(v => (v.Usuario?.NombreCompleto?.Contains(filtro.Busqueda, StringComparison.OrdinalIgnoreCase) == true)
                             || (v.Usuario?.Email?.Contains(filtro.Busqueda, StringComparison.OrdinalIgnoreCase) == true))
                    .ToList();

            // Todas las compras históricas para determinar si es recurrente
            var todosLosUsuarioIds = ventasClientes.Select(v => v.UsuarioId).Distinct().ToList();
            var comprasHistoricas = await _db.Ventas
                .Where(v => todosLosUsuarioIds.Contains(v.UsuarioId) && v.Estado != "Cancelado")
                .GroupBy(v => v.UsuarioId)
                .Select(g => new {
                    UsuarioId = g.Key,
                    TotalCompras = g.Count(),
                    PrimeraCompra = g.Min(v => v.FechaVenta),
                    UltimaCompra = g.Max(v => v.FechaVenta),
                    TotalGastado = g.Sum(v => v.Total)
                })
                .ToDictionaryAsync(x => x.UsuarioId);

            return ventasClientes
                .GroupBy(v => v.UsuarioId)
                .Select(g => {
                    var usuario = g.First().Usuario;
                    var hist = comprasHistoricas.GetValueOrDefault(g.Key);
                    var totalCompras = hist?.TotalCompras ?? g.Count();
                    var totalGastado = hist?.TotalGastado ?? g.Sum(v => v.Total);
                    var ultimaCompra = hist?.UltimaCompra ?? g.Max(v => v.FechaVenta);
                    return new ClienteResumen
                    {
                        UsuarioId = g.Key,
                        Nombre = usuario?.NombreCompleto ?? "Cliente desconocido",
                        Email = usuario?.Email ?? "",
                        TotalCompras = totalCompras,
                        TotalGastado = totalGastado,
                        TicketPromedio = totalCompras > 0
                            ? Math.Round(totalGastado / totalCompras, 2) : 0,
                        PrimeraCompra = hist?.PrimeraCompra ?? g.Min(v => v.FechaVenta),
                        UltimaCompra = ultimaCompra,
                        EsRecurrente = totalCompras > 1,
                        DiasDesdeUltimaCompra = (DateTime.UtcNow - ultimaCompra).Days
                    };
                })
                .OrderByDescending(c => c.TotalGastado)
                .ToList();
        }

        // ─────────────────────────────────────────────────────────────────────
        // 6. VENTAS (tabla principal con paginación)
        // ─────────────────────────────────────────────────────────────────────
        public async Task<FinanzasPagedResult<VentaResumen>> GetVentasAsync(FiltroFinanzas filtro)
        {
            var query = _db.Ventas
                .Include(v => v.Usuario)
                .Include(v => v.DetalleVentas)
                    .ThenInclude(d => d.Producto)
                .AsQueryable();

            // Filtros
            query = query.Where(v => v.FechaVenta >= filtro.Desde && v.FechaVenta <= filtro.Hasta);

            if (!string.IsNullOrEmpty(filtro.Estado))
                query = query.Where(v => v.Estado == filtro.Estado);

            if (!string.IsNullOrEmpty(filtro.Busqueda))
                query = query.Where(v =>
                    v.Usuario.NombreCompleto.Contains(filtro.Busqueda) ||
                    v.Usuario.Email.Contains(filtro.Busqueda) ||
                    v.VentaID.ToString().Contains(filtro.Busqueda));

            if (!string.IsNullOrEmpty(filtro.VendedorId))
                query = query.Where(v =>
                    v.DetalleVentas.Any(d => d.Producto.VendedorID == filtro.VendedorId));

            var total = await query.CountAsync();

            var ventas = await query
                .OrderByDescending(v => v.FechaVenta)
                .Skip((filtro.Pagina - 1) * filtro.PorPagina)
                .Take(filtro.PorPagina)
                .ToListAsync();

            var items = ventas.Select(v => new VentaResumen
            {
                VentaId = v.VentaID,
                NombreCliente = v.Usuario?.NombreCompleto ?? "Desconocido",
                EmailCliente = v.Usuario?.Email ?? "",
                FechaVenta = v.FechaVenta,
                Estado = v.Estado,
                MetodoPago = v.MetodoPago,
                Total = v.Total,
                CantidadItems = v.DetalleVentas.Sum(d => d.Cantidad),
                ComprobanteUrl = v.ComprobanteUrl,
                Vendedores = v.DetalleVentas
                    .Select(d => d.Producto?.Usuario?.NombreCompleto ?? "")
                    .Where(n => !string.IsNullOrEmpty(n))
                    .Distinct()
                    .ToList()
            }).ToList();

            return new FinanzasPagedResult<VentaResumen>
            {
                Items = items,
                Total = total,
                Pagina = filtro.Pagina,
                PorPagina = filtro.PorPagina
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // 7. DETALLE DE UNA VENTA
        // ─────────────────────────────────────────────────────────────────────
        public async Task<Ventas?> GetDetalleVentaAsync(int ventaId)
        {
            return await _db.Ventas
                .Include(v => v.Usuario)
                .Include(v => v.DetalleVentas)
                    .ThenInclude(d => d.Producto)
                        .ThenInclude(p => p.Usuario)
                .Include(v => v.DetalleVentas)
                    .ThenInclude(d => d.Variante)
                .FirstOrDefaultAsync(v => v.VentaID == ventaId);
        }

        // ─────────────────────────────────────────────────────────────────────
        // 8. RESUMEN RÁPIDO PARA ALERTAS
        // ─────────────────────────────────────────────────────────────────────
        public async Task<(int Pendientes, int SinComprobante, decimal TotalPendiente)>
            GetAlertasAsync()
        {
            var pendientes = await _db.Ventas
                .CountAsync(v => v.Estado == "Pendiente");

            var sinComprobante = await _db.Ventas
                .CountAsync(v => v.Estado == "Pendiente"
                              && v.MetodoPago != "Efectivo"
                              && string.IsNullOrEmpty(v.ComprobanteUrl));

            var totalPendiente = await _db.Ventas
                .Where(v => v.Estado == "Pendiente")
                .SumAsync(v => (decimal?)v.Total) ?? 0;

            return (pendientes, sinComprobante, totalPendiente);
        }
    }
}
