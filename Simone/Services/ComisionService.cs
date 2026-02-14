using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Simone.Data;
using Simone.Models;

namespace Simone.Services
{
    /// <summary>
    /// Servicio para gestión de comisiones
    /// Soporta pedidos multi-vendedor
    /// </summary>
    public class ComisionService
    {
        private readonly TiendaDbContext _context;
        private readonly ILogger<ComisionService> _logger;

        public ComisionService(TiendaDbContext context, ILogger<ComisionService> logger)
        {
            _context = context;
            _logger = logger;
        }

        #region Configuración de Comisiones

        /// <summary>
        /// Obtener configuración de comisión activa para un vendedor
        /// Prioridad: PorVendedor > PorCategoria > Escalonado > Global
        /// </summary>
        public async Task<decimal> ObtenerPorcentajeComisionAsync(
            string vendedorId,
            int? categoriaId = null,
            decimal? montoVentas = null)
        {
            // 1. Buscar configuración específica por vendedor
            var configVendedor = await _context.Set<ConfiguracionComision>()
                .Where(c => c.Activo &&
                            c.TipoComision == TiposComision.PorVendedor &&
                            c.VendedorId == vendedorId &&
                            c.FechaInicio <= DateTime.UtcNow &&
                            (!c.FechaFin.HasValue || c.FechaFin >= DateTime.UtcNow))
                .FirstOrDefaultAsync();

            if (configVendedor != null)
                return configVendedor.Porcentaje;

            // 2. Buscar configuración por categoría
            if (categoriaId.HasValue)
            {
                var configCategoria = await _context.Set<ConfiguracionComision>()
                    .Where(c => c.Activo &&
                                c.TipoComision == TiposComision.PorCategoria &&
                                c.CategoriaId == categoriaId &&
                                c.FechaInicio <= DateTime.UtcNow &&
                                (!c.FechaFin.HasValue || c.FechaFin >= DateTime.UtcNow))
                    .FirstOrDefaultAsync();

                if (configCategoria != null)
                    return configCategoria.Porcentaje;
            }

            // 3. Buscar configuración escalonada
            if (montoVentas.HasValue)
            {
                var configEscalonada = await _context.Set<ConfiguracionComision>()
                    .Where(c => c.Activo &&
                                c.TipoComision == TiposComision.Escalonado &&
                                c.MontoMinimo <= montoVentas &&
                                (!c.MontoMaximo.HasValue || c.MontoMaximo >= montoVentas) &&
                                c.FechaInicio <= DateTime.UtcNow &&
                                (!c.FechaFin.HasValue || c.FechaFin >= DateTime.UtcNow))
                    .OrderByDescending(c => c.MontoMinimo)
                    .FirstOrDefaultAsync();

                if (configEscalonada != null)
                    return configEscalonada.Porcentaje;
            }

            // 4. Usar configuración global
            var configGlobal = await _context.Set<ConfiguracionComision>()
                .Where(c => c.Activo &&
                            c.TipoComision == TiposComision.Global &&
                            c.FechaInicio <= DateTime.UtcNow &&
                            (!c.FechaFin.HasValue || c.FechaFin >= DateTime.UtcNow))
                .FirstOrDefaultAsync();

            return configGlobal?.Porcentaje ?? 10m; // Default 10% si no hay configuración
        }

        /// <summary>
        /// Obtener todas las configuraciones de comisión
        /// </summary>
        public async Task<List<ConfiguracionComision>> ObtenerConfiguracionesAsync()
        {
            return await _context.Set<ConfiguracionComision>()
                .Include(c => c.Vendedor)
                .Include(c => c.Categoria)
                .OrderBy(c => c.TipoComision)
                .ThenBy(c => c.Porcentaje)
                .ToListAsync();
        }

        /// <summary>
        /// Crear o actualizar configuración de comisión
        /// </summary>
        public async Task<(bool Exito, string Mensaje)> GuardarConfiguracionAsync(ConfiguracionComision config)
        {
            try
            {
                if (config.ConfiguracionId == 0)
                {
                    config.CreadoUtc = DateTime.UtcNow;
                    _context.Set<ConfiguracionComision>().Add(config);
                }
                else
                {
                    config.ModificadoUtc = DateTime.UtcNow;
                    _context.Set<ConfiguracionComision>().Update(config);
                }

                await _context.SaveChangesAsync();
                return (true, "Configuración guardada correctamente");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar configuración de comisión");
                return (false, "Error al guardar la configuración");
            }
        }

        #endregion

        #region Cálculo de Comisiones

        /// <summary>
        /// Calcular comisiones por vendedor en un período
        /// </summary>
        public async Task<List<ResumenComisionVendedor>> CalcularComisionesPeriodoAsync(
            DateTime fechaInicio,
            DateTime fechaFin)
        {
            _logger.LogInformation("Calculando comisiones del {Inicio} al {Fin}",
                fechaInicio, fechaFin);

            // Obtener todos los detalles de pedidos pagados en el período
            // Agrupados por vendedor del producto
            var detalles = await _context.DetallesPedido
                .Include(d => d.Pedido)
                .Include(d => d.Producto)
                    .ThenInclude(p => p.Usuario) // Vendedor
                .Include(d => d.Producto)
                    .ThenInclude(p => p.Categoria)
                .Where(d => d.Pedido != null &&
                            d.Pedido.EstadoPago == EstadosPago.Pagado &&
                            d.Pedido.FechaPago >= fechaInicio &&
                            d.Pedido.FechaPago < fechaFin &&
                            !d.Pedido.ComisionCalculada)
                .ToListAsync();

            // Agrupar por vendedor
            var porVendedor = detalles
                .Where(d => d.Producto?.VendedorID != null)
                .GroupBy(d => d.Producto!.VendedorID)
                .ToList();

            var resultado = new List<ResumenComisionVendedor>();

            foreach (var grupo in porVendedor)
            {
                var vendedorId = grupo.Key;
                var vendedor = grupo.First().Producto?.Usuario;

                // Calcular monto total de ventas del vendedor
                var montoVentas = grupo.Sum(d => d.Subtotal);

                // Obtener porcentaje de comisión
                var porcentaje = await ObtenerPorcentajeComisionAsync(
                    vendedorId,
                    montoVentas: montoVentas);

                var comision = Math.Round(montoVentas * (porcentaje / 100m), 2);

                resultado.Add(new ResumenComisionVendedor
                {
                    VendedorId = vendedorId,
                    NombreVendedor = vendedor?.NombreCompleto ?? "Desconocido",
                    EmailVendedor = vendedor?.Email ?? "",
                    MontoVentas = montoVentas,
                    CantidadPedidos = grupo.Select(d => d.PedidoID).Distinct().Count(),
                    CantidadProductos = grupo.Sum(d => d.Cantidad),
                    PorcentajeComision = porcentaje,
                    MontoComision = comision,
                    Detalles = grupo.Select(d => new DetalleComisionPedido
                    {
                        PedidoId = d.PedidoID ?? 0,
                        ProductoId = d.ProductoID,
                        NombreProducto = d.Producto?.Nombre ?? "",
                        Cantidad = d.Cantidad,
                        PrecioUnitario = d.PrecioUnitario,
                        Subtotal = d.Subtotal,
                        FechaPedido = d.Pedido?.FechaPedido ?? DateTime.MinValue
                    }).ToList()
                });
            }

            _logger.LogInformation("Comisiones calculadas: {Count} vendedores, Total: {Total:C}",
                resultado.Count, resultado.Sum(r => r.MontoComision));

            return resultado.OrderByDescending(r => r.MontoVentas).ToList();
        }

        /// <summary>
        /// Generar liquidación quincenal para un vendedor
        /// </summary>
        public async Task<(bool Exito, string Mensaje, PagoComision? Pago)> GenerarLiquidacionAsync(
            string vendedorId,
            int anio,
            int mes,
            int quincena,
            string creadoPor)
        {
            try
            {
                // Determinar fechas del período
                DateTime fechaInicio, fechaFin;
                if (quincena == 1)
                {
                    fechaInicio = new DateTime(anio, mes, 1, 0, 0, 0, DateTimeKind.Utc);
                    fechaFin = new DateTime(anio, mes, 16, 0, 0, 0, DateTimeKind.Utc);
                }
                else
                {
                    fechaInicio = new DateTime(anio, mes, 16, 0, 0, 0, DateTimeKind.Utc);
                    fechaFin = new DateTime(anio, mes, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1);
                }

                // Verificar si ya existe liquidación para este período
                var existente = await _context.Set<PagoComision>()
                    .AnyAsync(p => p.VendedorId == vendedorId &&
                                   p.Anio == anio &&
                                   p.Mes == mes &&
                                   p.NumeroQuincena == quincena &&
                                   p.Estado != EstadosPagoComision.Cancelado);

                if (existente)
                    return (false, "Ya existe una liquidación para este período", null);

                // Calcular comisiones del período
                var comisiones = await CalcularComisionesPeriodoAsync(fechaInicio, fechaFin);
                var comisionVendedor = comisiones.FirstOrDefault(c => c.VendedorId == vendedorId);

                if (comisionVendedor == null || comisionVendedor.MontoVentas == 0)
                    return (false, "No hay ventas para liquidar en este período", null);

                // Crear pago de comisión
                var pago = new PagoComision
                {
                    VendedorId = vendedorId,
                    PeriodoInicio = fechaInicio,
                    PeriodoFin = fechaFin.AddDays(-1),
                    NumeroQuincena = quincena,
                    Anio = anio,
                    Mes = mes,
                    MontoVentas = comisionVendedor.MontoVentas,
                    CantidadPedidos = comisionVendedor.CantidadPedidos,
                    CantidadProductos = comisionVendedor.CantidadProductos,
                    PorcentajeAplicado = comisionVendedor.PorcentajeComision,
                    MontoComision = comisionVendedor.MontoComision,
                    MontoFinal = comisionVendedor.MontoComision,
                    Estado = EstadosPagoComision.Pendiente,
                    CreadoUtc = DateTime.UtcNow,
                    CreadoPor = creadoPor
                };

                // Agregar detalles
                foreach (var detalle in comisionVendedor.Detalles)
                {
                    pago.Detalles.Add(new PagoComisionDetalle
                    {
                        PedidoId = detalle.PedidoId,
                        MontoPedido = detalle.Subtotal,
                        ComisionPedido = Math.Round(detalle.Subtotal * (comisionVendedor.PorcentajeComision / 100m), 2)
                    });
                }

                _context.Set<PagoComision>().Add(pago);

                // Marcar pedidos como procesados
                var pedidoIds = comisionVendedor.Detalles.Select(d => d.PedidoId).Distinct().ToList();
                var pedidos = await _context.Pedidos
                    .Where(p => pedidoIds.Contains(p.PedidoID))
                    .ToListAsync();

                foreach (var pedido in pedidos)
                {
                    pedido.ComisionCalculada = true;
                    pedido.PagoComisionId = pago.PagoId;
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Liquidación generada. VendedorId: {VendedorId}, Período: Q{Q} {Mes}/{Anio}, Monto: {Monto:C}",
                    vendedorId, quincena, mes, anio, pago.MontoComision);

                return (true, "Liquidación generada correctamente", pago);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar liquidación para vendedor {VendedorId}", vendedorId);
                return (false, "Error al generar la liquidación", null);
            }
        }

        #endregion

        #region Pagos de Comisiones

        /// <summary>
        /// Obtener pagos de comisión con filtros
        /// </summary>
        public async Task<List<PagoComision>> ObtenerPagosAsync(
            string? vendedorId = null,
            string? estado = null,
            int? anio = null,
            int? mes = null)
        {
            var query = _context.Set<PagoComision>()
                .Include(p => p.Vendedor)
                .AsQueryable();

            if (!string.IsNullOrEmpty(vendedorId))
                query = query.Where(p => p.VendedorId == vendedorId);

            if (!string.IsNullOrEmpty(estado))
                query = query.Where(p => p.Estado == estado);

            if (anio.HasValue)
                query = query.Where(p => p.Anio == anio);

            if (mes.HasValue)
                query = query.Where(p => p.Mes == mes);

            return await query
                .OrderByDescending(p => p.Anio)
                .ThenByDescending(p => p.Mes)
                .ThenByDescending(p => p.NumeroQuincena)
                .ToListAsync();
        }

        /// <summary>
        /// Aprobar pago de comisión
        /// </summary>
        public async Task<(bool Exito, string Mensaje)> AprobarPagoAsync(int pagoId, string aprobadoPor)
        {
            try
            {
                var pago = await _context.Set<PagoComision>().FindAsync(pagoId);
                if (pago == null)
                    return (false, "Pago no encontrado");

                if (pago.Estado != EstadosPagoComision.Pendiente)
                    return (false, "Solo se pueden aprobar pagos pendientes");

                pago.Aprobar(aprobadoPor);
                await _context.SaveChangesAsync();

                return (true, "Pago aprobado correctamente");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al aprobar pago {PagoId}", pagoId);
                return (false, "Error al aprobar el pago");
            }
        }

        /// <summary>
        /// Marcar pago como realizado
        /// </summary>
        public async Task<(bool Exito, string Mensaje)> MarcarPagadoAsync(
            int pagoId,
            string pagadoPor,
            string metodoPago,
            string? comprobante = null,
            string? banco = null)
        {
            try
            {
                var pago = await _context.Set<PagoComision>().FindAsync(pagoId);
                if (pago == null)
                    return (false, "Pago no encontrado");

                if (pago.Estado == EstadosPagoComision.Pagado)
                    return (false, "Este pago ya fue realizado");

                if (pago.Estado == EstadosPagoComision.Cancelado)
                    return (false, "No se puede pagar una liquidación cancelada");

                pago.MarcarPagado(pagadoPor, metodoPago, comprobante);
                pago.BancoEntidad = banco;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Pago marcado como realizado. PagoId: {PagoId}, Método: {Metodo}",
                    pagoId, metodoPago);

                return (true, "Pago registrado correctamente");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al marcar pago {PagoId}", pagoId);
                return (false, "Error al registrar el pago");
            }
        }

        /// <summary>
        /// Cancelar pago de comisión
        /// </summary>
        public async Task<(bool Exito, string Mensaje)> CancelarPagoAsync(int pagoId, string motivo)
        {
            try
            {
                var pago = await _context.Set<PagoComision>()
                    .Include(p => p.Detalles)
                    .FirstOrDefaultAsync(p => p.PagoId == pagoId);

                if (pago == null)
                    return (false, "Pago no encontrado");

                if (pago.Estado == EstadosPagoComision.Pagado)
                    return (false, "No se puede cancelar un pago ya realizado");

                // Liberar los pedidos para que puedan ser incluidos en otra liquidación
                var pedidoIds = pago.Detalles.Select(d => d.PedidoId).ToList();
                var pedidos = await _context.Pedidos
                    .Where(p => pedidoIds.Contains(p.PedidoID))
                    .ToListAsync();

                foreach (var pedido in pedidos)
                {
                    pedido.ComisionCalculada = false;
                    pedido.PagoComisionId = null;
                }

                pago.Cancelar(motivo);
                await _context.SaveChangesAsync();

                return (true, "Liquidación cancelada correctamente");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cancelar pago {PagoId}", pagoId);
                return (false, "Error al cancelar la liquidación");
            }
        }

        #endregion

        #region Estadísticas

        /// <summary>
        /// Obtener estadísticas generales de ventas
        /// </summary>
        public async Task<EstadisticasVentas> ObtenerEstadisticasAsync(
            DateTime? fechaInicio = null,
            DateTime? fechaFin = null)
        {
            fechaInicio ??= new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            fechaFin ??= DateTime.UtcNow;

            var pedidosPagados = await _context.Pedidos
                .Where(p => p.EstadoPago == EstadosPago.Pagado &&
                            p.FechaPago >= fechaInicio &&
                            p.FechaPago <= fechaFin)
                .ToListAsync();

            var totalVentas = pedidosPagados.Sum(p => p.Total);
            var cantidadPedidos = pedidosPagados.Count;
            var ticketPromedio = cantidadPedidos > 0 ? totalVentas / cantidadPedidos : 0;

            // Comisiones del período
            var comisionesPendientes = await _context.Set<PagoComision>()
                .Where(p => p.Estado == EstadosPagoComision.Pendiente)
                .SumAsync(p => p.MontoFinal);

            var comisionesPagadas = await _context.Set<PagoComision>()
                .Where(p => p.Estado == EstadosPagoComision.Pagado &&
                            p.FechaPago >= fechaInicio &&
                            p.FechaPago <= fechaFin)
                .SumAsync(p => p.MontoFinal);

            // Vendedores activos
            var vendedoresActivos = await _context.DetallesPedido
                .Include(d => d.Pedido)
                .Include(d => d.Producto)
                .Where(d => d.Pedido != null &&
                            d.Pedido.EstadoPago == EstadosPago.Pagado &&
                            d.Pedido.FechaPago >= fechaInicio &&
                            d.Pedido.FechaPago <= fechaFin)
                .Select(d => d.Producto!.VendedorID)
                .Distinct()
                .CountAsync();

            return new EstadisticasVentas
            {
                FechaInicio = fechaInicio.Value,
                FechaFin = fechaFin.Value,
                TotalVentas = totalVentas,
                CantidadPedidos = cantidadPedidos,
                TicketPromedio = ticketPromedio,
                ComisionesPendientes = comisionesPendientes,
                ComisionesPagadas = comisionesPagadas,
                VendedoresActivos = vendedoresActivos
            };
        }

        /// <summary>
        /// Top vendedores por ventas
        /// </summary>
        public async Task<List<TopVendedor>> ObtenerTopVendedoresAsync(
            DateTime fechaInicio,
            DateTime fechaFin,
            int top = 10)
        {
            var detalles = await _context.DetallesPedido
                .Include(d => d.Pedido)
                .Include(d => d.Producto)
                    .ThenInclude(p => p.Usuario)
                .Where(d => d.Pedido != null &&
                            d.Pedido.EstadoPago == EstadosPago.Pagado &&
                            d.Pedido.FechaPago >= fechaInicio &&
                            d.Pedido.FechaPago <= fechaFin)
                .ToListAsync();

            return detalles
                .Where(d => d.Producto?.VendedorID != null)
                .GroupBy(d => d.Producto!.VendedorID)
                .Select(g => new TopVendedor
                {
                    VendedorId = g.Key,
                    NombreVendedor = g.First().Producto?.Usuario?.NombreCompleto ?? "Desconocido",
                    TotalVentas = g.Sum(d => d.Subtotal),
                    CantidadPedidos = g.Select(d => d.PedidoID).Distinct().Count(),
                    CantidadProductos = g.Sum(d => d.Cantidad)
                })
                .OrderByDescending(v => v.TotalVentas)
                .Take(top)
                .ToList();
        }

        #endregion
    }

    #region DTOs

    /// <summary>
    /// Resumen de comisión por vendedor
    /// </summary>
    public class ResumenComisionVendedor
    {
        public string VendedorId { get; set; } = "";
        public string NombreVendedor { get; set; } = "";
        public string EmailVendedor { get; set; } = "";
        public decimal MontoVentas { get; set; }
        public int CantidadPedidos { get; set; }
        public int CantidadProductos { get; set; }
        public decimal PorcentajeComision { get; set; }
        public decimal MontoComision { get; set; }
        public List<DetalleComisionPedido> Detalles { get; set; } = new();
    }

    /// <summary>
    /// Detalle de comisión por pedido
    /// </summary>
    public class DetalleComisionPedido
    {
        public int PedidoId { get; set; }
        public int ProductoId { get; set; }
        public string NombreProducto { get; set; } = "";
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Subtotal { get; set; }
        public DateTime FechaPedido { get; set; }
    }

    /// <summary>
    /// Estadísticas generales de ventas
    /// </summary>
    public class EstadisticasVentas
    {
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }
        public decimal TotalVentas { get; set; }
        public int CantidadPedidos { get; set; }
        public decimal TicketPromedio { get; set; }
        public decimal ComisionesPendientes { get; set; }
        public decimal ComisionesPagadas { get; set; }
        public int VendedoresActivos { get; set; }

        public decimal TotalComisiones => ComisionesPendientes + ComisionesPagadas;
    }

    /// <summary>
    /// Top vendedor
    /// </summary>
    public class TopVendedor
    {
        public string VendedorId { get; set; } = "";
        public string NombreVendedor { get; set; } = "";
        public decimal TotalVentas { get; set; }
        public int CantidadPedidos { get; set; }
        public int CantidadProductos { get; set; }
    }

    #endregion
}