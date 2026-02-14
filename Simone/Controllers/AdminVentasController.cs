using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Simone.Data;
using Simone.Models;
using Simone.Services;

namespace Simone.Controllers
{
    /// <summary>
    /// Panel de administración de ventas y comisiones
    /// Solo accesible para Administradores
    /// </summary>
    [Authorize(Roles = "Administrador")]
    public class AdminVentasController : Controller
    {
        private readonly TiendaDbContext _context;
        private readonly ComisionService _comisionService;
        private readonly UserManager<Usuario> _userManager;
        private readonly ILogger<AdminVentasController> _logger;

        public AdminVentasController(
            TiendaDbContext context,
            ComisionService comisionService,
            UserManager<Usuario> userManager,
            ILogger<AdminVentasController> logger)
        {
            _context = context;
            _comisionService = comisionService;
            _userManager = userManager;
            _logger = logger;
        }

        #region Dashboard

        /// <summary>
        /// GET: /AdminVentas
        /// Dashboard principal de ventas
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index(int? mes = null, int? anio = null)
        {
            try
            {
                // Determinar período
                var ahora = DateTime.UtcNow;
                anio ??= ahora.Year;
                mes ??= ahora.Month;

                var fechaInicio = new DateTime(anio.Value, mes.Value, 1, 0, 0, 0, DateTimeKind.Utc);
                var fechaFin = fechaInicio.AddMonths(1);

                // Estadísticas generales
                var stats = await _comisionService.ObtenerEstadisticasAsync(fechaInicio, fechaFin);

                // Top vendedores
                var topVendedores = await _comisionService.ObtenerTopVendedoresAsync(
                    fechaInicio, fechaFin, 5);

                // Últimos pedidos
                var ultimosPedidos = await _context.Pedidos
                    .Include(p => p.Usuario)
                    .Include(p => p.DetallesPedido)
                        .ThenInclude(d => d.Producto)
                    .OrderByDescending(p => p.FechaPedido)
                    .Take(10)
                    .ToListAsync();

                // Comisiones pendientes de pago
                var comisionesPendientes = await _context.Set<PagoComision>()
                    .Include(p => p.Vendedor)
                    .Where(p => p.Estado == EstadosPagoComision.Pendiente ||
                                p.Estado == EstadosPagoComision.Aprobado)
                    .OrderByDescending(p => p.CreadoUtc)
                    .Take(5)
                    .ToListAsync();

                // Ventas por día del mes (para gráfico)
                var ventasPorDia = await _context.Pedidos
                    .Where(p => p.EstadoPago == EstadosPago.Pagado &&
                                p.FechaPago >= fechaInicio &&
                                p.FechaPago < fechaFin)
                    .GroupBy(p => p.FechaPago!.Value.Day)
                    .Select(g => new { Dia = g.Key, Total = g.Sum(p => p.Total) })
                    .OrderBy(x => x.Dia)
                    .ToListAsync();

                ViewBag.Stats = stats;
                ViewBag.TopVendedores = topVendedores;
                ViewBag.UltimosPedidos = ultimosPedidos;
                ViewBag.ComisionesPendientes = comisionesPendientes;
                ViewBag.VentasPorDia = ventasPorDia;
                ViewBag.MesActual = mes;
                ViewBag.AnioActual = anio;
                ViewBag.NombreMes = ObtenerNombreMes(mes.Value);

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar dashboard de ventas");
                TempData["Error"] = "Error al cargar el dashboard";
                return View();
            }
        }

        #endregion

        #region Ventas por Vendedor

        /// <summary>
        /// GET: /AdminVentas/Vendedores
        /// Lista de ventas agrupadas por vendedor
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Vendedores(int? mes = null, int? anio = null)
        {
            try
            {
                var ahora = DateTime.UtcNow;
                anio ??= ahora.Year;
                mes ??= ahora.Month;

                var fechaInicio = new DateTime(anio.Value, mes.Value, 1, 0, 0, 0, DateTimeKind.Utc);
                var fechaFin = fechaInicio.AddMonths(1);

                // Calcular comisiones por vendedor
                var comisiones = await _comisionService.CalcularComisionesPeriodoAsync(
                    fechaInicio, fechaFin);

                // Obtener pagos existentes del período
                var pagosExistentes = await _context.Set<PagoComision>()
                    .Where(p => p.Anio == anio && p.Mes == mes)
                    .ToListAsync();

                ViewBag.Comisiones = comisiones;
                ViewBag.PagosExistentes = pagosExistentes;
                ViewBag.MesActual = mes;
                ViewBag.AnioActual = anio;
                ViewBag.NombreMes = ObtenerNombreMes(mes.Value);

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar ventas por vendedor");
                TempData["Error"] = "Error al cargar los datos";
                return View();
            }
        }

        /// <summary>
        /// GET: /AdminVentas/DetalleVendedor/{vendedorId}
        /// Detalle de ventas de un vendedor específico
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> DetalleVendedor(string vendedorId, int? mes = null, int? anio = null)
        {
            try
            {
                if (string.IsNullOrEmpty(vendedorId))
                    return RedirectToAction(nameof(Vendedores));

                var vendedor = await _userManager.FindByIdAsync(vendedorId);
                if (vendedor == null)
                {
                    TempData["Error"] = "Vendedor no encontrado";
                    return RedirectToAction(nameof(Vendedores));
                }

                var ahora = DateTime.UtcNow;
                anio ??= ahora.Year;
                mes ??= ahora.Month;

                var fechaInicio = new DateTime(anio.Value, mes.Value, 1, 0, 0, 0, DateTimeKind.Utc);
                var fechaFin = fechaInicio.AddMonths(1);

                // Obtener pedidos del vendedor (a través de sus productos)
                var detalles = await _context.DetallesPedido
                    .Include(d => d.Pedido)
                        .ThenInclude(p => p.Usuario)
                    .Include(d => d.Producto)
                    .Where(d => d.Producto != null &&
                                d.Producto.VendedorID == vendedorId &&
                                d.Pedido != null &&
                                d.Pedido.FechaPedido >= fechaInicio &&
                                d.Pedido.FechaPedido < fechaFin)
                    .OrderByDescending(d => d.Pedido!.FechaPedido)
                    .ToListAsync();

                // Agrupar por pedido
                var pedidosAgrupados = detalles
                    .GroupBy(d => d.PedidoID)
                    .Select(g => new
                    {
                        Pedido = g.First().Pedido,
                        Productos = g.ToList(),
                        SubtotalVendedor = g.Sum(d => d.Subtotal)
                    })
                    .ToList();

                // Historial de pagos de comisión
                var pagos = await _context.Set<PagoComision>()
                    .Where(p => p.VendedorId == vendedorId)
                    .OrderByDescending(p => p.Anio)
                    .ThenByDescending(p => p.Mes)
                    .ThenByDescending(p => p.NumeroQuincena)
                    .Take(12)
                    .ToListAsync();

                // Obtener configuración de comisión del vendedor
                var configComision = await _context.Set<ConfiguracionComision>()
                    .Where(c => c.VendedorId == vendedorId && c.Activo)
                    .FirstOrDefaultAsync();

                var porcentaje = await _comisionService.ObtenerPorcentajeComisionAsync(vendedorId);

                ViewBag.Vendedor = vendedor;
                ViewBag.PedidosAgrupados = pedidosAgrupados;
                ViewBag.Pagos = pagos;
                ViewBag.PorcentajeComision = porcentaje;
                ViewBag.ConfigComision = configComision;
                ViewBag.MesActual = mes;
                ViewBag.AnioActual = anio;
                ViewBag.NombreMes = ObtenerNombreMes(mes.Value);

                // Totales
                ViewBag.TotalVentas = detalles.Sum(d => d.Subtotal);
                ViewBag.TotalComision = Math.Round(detalles.Sum(d => d.Subtotal) * (porcentaje / 100m), 2);
                ViewBag.TotalPedidos = pedidosAgrupados.Count;
                ViewBag.TotalProductos = detalles.Sum(d => d.Cantidad);

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar detalle de vendedor {VendedorId}", vendedorId);
                TempData["Error"] = "Error al cargar los datos";
                return RedirectToAction(nameof(Vendedores));
            }
        }

        #endregion

        #region Pedidos

        /// <summary>
        /// GET: /AdminVentas/Pedidos
        /// Lista de todos los pedidos
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Pedidos(
            string? estado = null,
            string? estadoPago = null,
            string? vendedorId = null,
            string? busqueda = null,
            int pagina = 1)
        {
            try
            {
                const int porPagina = 20;

                var query = _context.Pedidos
                    .Include(p => p.Usuario)
                    .Include(p => p.DetallesPedido)
                        .ThenInclude(d => d.Producto)
                    .AsQueryable();

                // Filtros
                if (!string.IsNullOrEmpty(estado))
                    query = query.Where(p => p.EstadoPedido == estado);

                if (!string.IsNullOrEmpty(estadoPago))
                    query = query.Where(p => p.EstadoPago == estadoPago);

                if (!string.IsNullOrEmpty(vendedorId))
                    query = query.Where(p => p.DetallesPedido.Any(d => d.Producto != null && d.Producto.VendedorID == vendedorId));

                if (!string.IsNullOrEmpty(busqueda))
                {
                    busqueda = busqueda.ToLower();
                    query = query.Where(p =>
                        (p.NumeroOrden != null && p.NumeroOrden.ToLower().Contains(busqueda)) ||
                        (p.NombreCliente != null && p.NombreCliente.ToLower().Contains(busqueda)) ||
                        (p.EmailCliente != null && p.EmailCliente.ToLower().Contains(busqueda)));
                }

                var total = await query.CountAsync();
                var pedidos = await query
                    .OrderByDescending(p => p.FechaPedido)
                    .Skip((pagina - 1) * porPagina)
                    .Take(porPagina)
                    .ToListAsync();

                // Lista de vendedores para filtro
                var vendedores = await _userManager.GetUsersInRoleAsync("Vendedor");

                ViewBag.Pedidos = pedidos;
                ViewBag.Vendedores = vendedores;
                ViewBag.Total = total;
                ViewBag.Pagina = pagina;
                ViewBag.TotalPaginas = (int)Math.Ceiling(total / (double)porPagina);
                ViewBag.FiltroEstado = estado;
                ViewBag.FiltroEstadoPago = estadoPago;
                ViewBag.FiltroVendedor = vendedorId;
                ViewBag.FiltroBusqueda = busqueda;

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar lista de pedidos");
                TempData["Error"] = "Error al cargar los pedidos";
                return View();
            }
        }

        /// <summary>
        /// GET: /AdminVentas/DetallePedido/{id}
        /// Detalle completo de un pedido
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> DetallePedido(int id)
        {
            try
            {
                var pedido = await _context.Pedidos
                    .Include(p => p.Usuario)
                    .Include(p => p.DetallesPedido)
                        .ThenInclude(d => d.Producto)
                            .ThenInclude(pr => pr.Usuario)
                    .Include(p => p.DetallesPedido)
                        .ThenInclude(d => d.Producto)
                            .ThenInclude(pr => pr.Categoria)
                    .Include(p => p.Historial)
                        .ThenInclude(h => h.Usuario)
                    .FirstOrDefaultAsync(p => p.PedidoID == id);

                if (pedido == null)
                {
                    TempData["Error"] = "Pedido no encontrado";
                    return RedirectToAction(nameof(Pedidos));
                }

                // Agrupar productos por vendedor
                var productosPorVendedor = pedido.DetallesPedido
                    .Where(d => d.Producto != null)
                    .GroupBy(d => d.Producto!.VendedorID)
                    .Select(g => new
                    {
                        VendedorId = g.Key,
                        NombreVendedor = g.First().Producto?.Usuario?.NombreCompleto ?? "Desconocido",
                        Productos = g.ToList(),
                        Subtotal = g.Sum(d => d.Subtotal)
                    })
                    .ToList();

                ViewBag.Pedido = pedido;
                ViewBag.ProductosPorVendedor = productosPorVendedor;

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar detalle del pedido {PedidoId}", id);
                TempData["Error"] = "Error al cargar el pedido";
                return RedirectToAction(nameof(Pedidos));
            }
        }

        /// <summary>
        /// POST: /AdminVentas/CambiarEstadoPedido
        /// Cambiar estado de un pedido
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarEstadoPedido(int pedidoId, string nuevoEstado, string? comentario)
        {
            try
            {
                var pedido = await _context.Pedidos
                    .Include(p => p.Historial)
                    .FirstOrDefaultAsync(p => p.PedidoID == pedidoId);

                if (pedido == null)
                    return Json(new { success = false, message = "Pedido no encontrado" });

                var estadoAnterior = pedido.EstadoPedido;
                var userId = _userManager.GetUserId(User);

                // Cambiar estado según el nuevo
                switch (nuevoEstado)
                {
                    case "Confirmado":
                        pedido.Confirmar();
                        break;
                    case "EnProceso":
                        pedido.EstadoPedido = EstadosPedido.EnProceso;
                        break;
                    case "Enviado":
                        pedido.MarcarEnviado();
                        break;
                    case "Entregado":
                        pedido.MarcarEntregado();
                        break;
                    case "Cancelado":
                        pedido.Cancelar(comentario ?? "Cancelado por administrador");
                        break;
                    default:
                        return Json(new { success = false, message = "Estado no válido" });
                }

                // Registrar en historial
                pedido.Historial.Add(new PedidoHistorial
                {
                    PedidoID = pedido.PedidoID,
                    EstadoAnterior = estadoAnterior,
                    EstadoNuevo = nuevoEstado,
                    Comentario = comentario,
                    UsuarioId = userId,
                    FechaCambio = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();

                _logger.LogInformation("Estado de pedido {PedidoId} cambiado de {Anterior} a {Nuevo} por {Usuario}",
                    pedidoId, estadoAnterior, nuevoEstado, userId);

                return Json(new { success = true, message = "Estado actualizado correctamente" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cambiar estado del pedido {PedidoId}", pedidoId);
                return Json(new { success = false, message = "Error al actualizar el estado" });
            }
        }

        #endregion

        #region Comisiones

        /// <summary>
        /// GET: /AdminVentas/Comisiones
        /// Gestión de pagos de comisiones
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Comisiones(string? estado = null, int? anio = null)
        {
            try
            {
                anio ??= DateTime.UtcNow.Year;

                var pagos = await _comisionService.ObtenerPagosAsync(
                    estado: estado,
                    anio: anio);

                // Totales por estado
                var totales = pagos.GroupBy(p => p.Estado)
                    .ToDictionary(g => g.Key, g => g.Sum(p => p.MontoFinal));

                ViewBag.Pagos = pagos;
                ViewBag.Totales = totales;
                ViewBag.FiltroEstado = estado;
                ViewBag.FiltroAnio = anio;

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar comisiones");
                TempData["Error"] = "Error al cargar las comisiones";
                return View();
            }
        }

        /// <summary>
        /// POST: /AdminVentas/GenerarLiquidacion
        /// Generar liquidación quincenal para un vendedor
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerarLiquidacion(
            string vendedorId,
            int anio,
            int mes,
            int quincena)
        {
            try
            {
                var userId = _userManager.GetUserId(User)!;
                var (exito, mensaje, pago) = await _comisionService.GenerarLiquidacionAsync(
                    vendedorId, anio, mes, quincena, userId);

                if (exito)
                    TempData["Success"] = mensaje;
                else
                    TempData["Error"] = mensaje;

                return RedirectToAction(nameof(Vendedores), new { mes, anio });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar liquidación");
                TempData["Error"] = "Error al generar la liquidación";
                return RedirectToAction(nameof(Vendedores));
            }
        }

        /// <summary>
        /// POST: /AdminVentas/AprobarPago
        /// Aprobar un pago de comisión
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AprobarPago(int pagoId)
        {
            try
            {
                var userId = _userManager.GetUserId(User)!;
                var (exito, mensaje) = await _comisionService.AprobarPagoAsync(pagoId, userId);

                return Json(new { success = exito, message = mensaje });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al aprobar pago {PagoId}", pagoId);
                return Json(new { success = false, message = "Error al aprobar el pago" });
            }
        }

        /// <summary>
        /// POST: /AdminVentas/MarcarPagado
        /// Marcar un pago como realizado
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarcarPagado(
            int pagoId,
            string metodoPago,
            string? comprobante,
            string? banco)
        {
            try
            {
                var userId = _userManager.GetUserId(User)!;
                var (exito, mensaje) = await _comisionService.MarcarPagadoAsync(
                    pagoId, userId, metodoPago, comprobante, banco);

                return Json(new { success = exito, message = mensaje });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al marcar pago {PagoId}", pagoId);
                return Json(new { success = false, message = "Error al registrar el pago" });
            }
        }

        /// <summary>
        /// POST: /AdminVentas/CancelarPago
        /// Cancelar una liquidación
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelarPago(int pagoId, string motivo)
        {
            try
            {
                var (exito, mensaje) = await _comisionService.CancelarPagoAsync(pagoId, motivo);
                return Json(new { success = exito, message = mensaje });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cancelar pago {PagoId}", pagoId);
                return Json(new { success = false, message = "Error al cancelar la liquidación" });
            }
        }

        #endregion

        #region Configuración de Comisiones

        /// <summary>
        /// GET: /AdminVentas/ConfiguracionComisiones
        /// Configurar porcentajes de comisión
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ConfiguracionComisiones()
        {
            try
            {
                var configuraciones = await _comisionService.ObtenerConfiguracionesAsync();
                var vendedores = await _userManager.GetUsersInRoleAsync("Vendedor");
                var categorias = await _context.Categorias.OrderBy(c => c.Nombre).ToListAsync();

                ViewBag.Configuraciones = configuraciones;
                ViewBag.Vendedores = vendedores;
                ViewBag.Categorias = categorias;

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar configuración de comisiones");
                TempData["Error"] = "Error al cargar la configuración";
                return View();
            }
        }

        /// <summary>
        /// POST: /AdminVentas/GuardarConfiguracionComision
        /// Guardar configuración de comisión
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarConfiguracionComision(ConfiguracionComision config)
        {
            try
            {
                config.CreadoPor = _userManager.GetUserId(User);
                var (exito, mensaje) = await _comisionService.GuardarConfiguracionAsync(config);

                if (exito)
                    TempData["Success"] = mensaje;
                else
                    TempData["Error"] = mensaje;

                return RedirectToAction(nameof(ConfiguracionComisiones));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar configuración de comisión");
                TempData["Error"] = "Error al guardar la configuración";
                return RedirectToAction(nameof(ConfiguracionComisiones));
            }
        }

        /// <summary>
        /// POST: /AdminVentas/EliminarConfiguracionComision
        /// Eliminar (desactivar) configuración de comisión
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarConfiguracionComision(int id)
        {
            try
            {
                var config = await _context.Set<ConfiguracionComision>().FindAsync(id);
                if (config != null)
                {
                    config.Activo = false;
                    config.ModificadoUtc = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Configuración eliminada";
                }

                return RedirectToAction(nameof(ConfiguracionComisiones));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar configuración {Id}", id);
                TempData["Error"] = "Error al eliminar la configuración";
                return RedirectToAction(nameof(ConfiguracionComisiones));
            }
        }

        #endregion

        #region Exportación

        /// <summary>
        /// GET: /AdminVentas/ExportarVentas
        /// Exportar ventas a CSV/Excel
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ExportarVentas(int? mes = null, int? anio = null)
        {
            try
            {
                var ahora = DateTime.UtcNow;
                anio ??= ahora.Year;
                mes ??= ahora.Month;

                var fechaInicio = new DateTime(anio.Value, mes.Value, 1, 0, 0, 0, DateTimeKind.Utc);
                var fechaFin = fechaInicio.AddMonths(1);

                var detalles = await _context.DetallesPedido
                    .Include(d => d.Pedido)
                        .ThenInclude(p => p.Usuario)
                    .Include(d => d.Producto)
                        .ThenInclude(p => p.Usuario)
                    .Include(d => d.Producto)
                        .ThenInclude(p => p.Categoria)
                    .Where(d => d.Pedido != null &&
                                d.Pedido.FechaPedido >= fechaInicio &&
                                d.Pedido.FechaPedido < fechaFin)
                    .OrderBy(d => d.Pedido!.FechaPedido)
                    .ToListAsync();

                var sb = new StringBuilder();
                sb.AppendLine("Fecha,NumeroOrden,Cliente,Email,Vendedor,Categoria,Producto,Cantidad,PrecioUnit,Subtotal,EstadoPedido,EstadoPago");

                foreach (var d in detalles)
                {
                    var fecha = d.Pedido?.FechaPedido.ToString("yyyy-MM-dd HH:mm") ?? "";
                    var orden = d.Pedido?.NumeroOrden ?? d.PedidoID.ToString();
                    var cliente = d.Pedido?.NombreCliente ?? d.Pedido?.Usuario?.NombreCompleto ?? "";
                    var email = d.Pedido?.EmailCliente ?? d.Pedido?.Usuario?.Email ?? "";
                    var vendedor = d.Producto?.Usuario?.NombreCompleto ?? "";
                    var categoria = d.Producto?.Categoria?.Nombre ?? "";
                    var producto = d.Producto?.Nombre ?? "";
                    var cantidad = d.Cantidad;
                    var precio = d.PrecioUnitario.ToString("F2", CultureInfo.InvariantCulture);
                    var subtotal = d.Subtotal.ToString("F2", CultureInfo.InvariantCulture);
                    var estadoPedido = d.Pedido?.EstadoPedido ?? "";
                    var estadoPago = d.Pedido?.EstadoPago ?? "";

                    sb.AppendLine($"\"{fecha}\",\"{orden}\",\"{EscapeCsv(cliente)}\",\"{EscapeCsv(email)}\",\"{EscapeCsv(vendedor)}\",\"{EscapeCsv(categoria)}\",\"{EscapeCsv(producto)}\",{cantidad},{precio},{subtotal},\"{estadoPedido}\",\"{estadoPago}\"");
                }

                var bytes = Encoding.UTF8.GetBytes(sb.ToString());
                var nombreArchivo = $"ventas_{anio}_{mes:D2}.csv";

                return File(bytes, "text/csv; charset=utf-8", nombreArchivo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al exportar ventas");
                TempData["Error"] = "Error al exportar los datos";
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// GET: /AdminVentas/ExportarComisiones
        /// Exportar comisiones a CSV
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ExportarComisiones(int? anio = null)
        {
            try
            {
                anio ??= DateTime.UtcNow.Year;

                var pagos = await _context.Set<PagoComision>()
                    .Include(p => p.Vendedor)
                    .Where(p => p.Anio == anio)
                    .OrderBy(p => p.Mes)
                    .ThenBy(p => p.NumeroQuincena)
                    .ToListAsync();

                var sb = new StringBuilder();
                sb.AppendLine("Periodo,Vendedor,Email,MontoVentas,Pedidos,Productos,Porcentaje,Comision,Deducciones,Bonificaciones,MontoFinal,Estado,FechaPago,MetodoPago");

                foreach (var p in pagos)
                {
                    var periodo = p.PeriodoNombre;
                    var vendedor = p.Vendedor?.NombreCompleto ?? "";
                    var email = p.Vendedor?.Email ?? "";
                    var ventas = p.MontoVentas.ToString("F2", CultureInfo.InvariantCulture);
                    var pedidos = p.CantidadPedidos;
                    var productos = p.CantidadProductos;
                    var porcentaje = p.PorcentajeAplicado.ToString("F2", CultureInfo.InvariantCulture);
                    var comision = p.MontoComision.ToString("F2", CultureInfo.InvariantCulture);
                    var deducciones = p.Deducciones.ToString("F2", CultureInfo.InvariantCulture);
                    var bonificaciones = p.Bonificaciones.ToString("F2", CultureInfo.InvariantCulture);
                    var final = p.MontoFinal.ToString("F2", CultureInfo.InvariantCulture);
                    var estado = p.Estado;
                    var fechaPago = p.FechaPago?.ToString("yyyy-MM-dd") ?? "";
                    var metodo = p.MetodoPago ?? "";

                    sb.AppendLine($"\"{periodo}\",\"{EscapeCsv(vendedor)}\",\"{EscapeCsv(email)}\",{ventas},{pedidos},{productos},{porcentaje},{comision},{deducciones},{bonificaciones},{final},\"{estado}\",\"{fechaPago}\",\"{EscapeCsv(metodo)}\"");
                }

                var bytes = Encoding.UTF8.GetBytes(sb.ToString());
                var nombreArchivo = $"comisiones_{anio}.csv";

                return File(bytes, "text/csv; charset=utf-8", nombreArchivo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al exportar comisiones");
                TempData["Error"] = "Error al exportar los datos";
                return RedirectToAction(nameof(Comisiones));
            }
        }

        #endregion

        #region Helpers

        private static string ObtenerNombreMes(int mes) => mes switch
        {
            1 => "Enero",
            2 => "Febrero",
            3 => "Marzo",
            4 => "Abril",
            5 => "Mayo",
            6 => "Junio",
            7 => "Julio",
            8 => "Agosto",
            9 => "Septiembre",
            10 => "Octubre",
            11 => "Noviembre",
            12 => "Diciembre",
            _ => "Desconocido"
        };

        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Replace("\"", "\"\"");
        }

        #endregion
    }
}