using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Simone.Data;
using Simone.Models;

namespace Simone.Services
{
    #region Interface

    /// <summary>
    /// Servicio de gestión de devoluciones
    /// Maneja la lógica de negocio para crear y procesar devoluciones
    /// </summary>
    public interface IDevolucionesService
    {
        /// <summary>
        /// Obtiene las cantidades devueltas acumuladas por detalle de venta
        /// </summary>
        Task<Dictionary<int, int>> GetDevueltasAcumuladasAsync(int ventaId, CancellationToken ct = default);

        /// <summary>
        /// Procesa una devolución completa con validaciones y actualización de stock
        /// </summary>
        Task<DevolucionResult> ProcesarDevolucionAsync(
            int ventaId,
            string motivo,
            List<LineaDevolucion> lineas,
            CancellationToken ct = default);

        /// <summary>
        /// Verifica si una venta tiene devoluciones registradas
        /// </summary>
        Task<bool> TieneDevolucionesAsync(int ventaId, CancellationToken ct = default);

        /// <summary>
        /// Obtiene el total de unidades devueltas para una venta
        /// </summary>
        Task<int> GetTotalDevueltasAsync(int ventaId, CancellationToken ct = default);
    }

    #endregion

    #region Implementation

    /// <summary>
    /// Implementación del servicio de devoluciones
    /// Versión optimizada con mejores prácticas empresariales
    /// </summary>
    public class DevolucionesService : IDevolucionesService
    {
        #region Dependencias

        private readonly TiendaDbContext _context;
        private readonly ILogger<DevolucionesService> _logger;

        #endregion

        #region Constantes - Configuración

        private const string ESTADO_CANCELADO = "cancelado";
        private const string TIPO_MOVIMIENTO_ENTRADA = "Entrada";

        #endregion

        #region Constantes - Mensajes de Log

        // Información
        private const string LOG_INFO_DEVOLUCION_PROCESADA = "Devolución procesada. VentaId: {VentaId}, Líneas: {Count}, VentaCancelada: {Cancelada}";
        private const string LOG_INFO_VENTA_CANCELADA = "Venta cancelada por devolución total. VentaId: {VentaId}";
        private const string LOG_INFO_STOCK_REPUESTO = "Stock repuesto. ProductoId: {ProductoId}, Cantidad: {Cantidad}, StockNuevo: {Stock}";

        // Debug
        private const string LOG_DEBUG_OBTENIENDO_DEVUELTAS = "Obteniendo devoluciones acumuladas. VentaId: {VentaId}";
        private const string LOG_DEBUG_PROCESANDO_DEVOLUCION = "Iniciando procesamiento de devolución. VentaId: {VentaId}, Líneas: {Count}";
        private const string LOG_DEBUG_VALIDANDO_LINEA = "Validando línea. DetalleVentaId: {DetalleVentaId}, Cantidad: {Cantidad}";
        private const string LOG_DEBUG_CREANDO_REGISTRO = "Creando registro devolución. DetalleVentaId: {DetalleVentaId}, Cantidad: {Cantidad}";
        private const string LOG_DEBUG_VERIFICANDO_CANCELACION = "Verificando cancelación total. VentaId: {VentaId}, Vendidas: {Vendidas}, Devueltas: {Devueltas}";

        // Advertencias
        private const string LOG_WARN_VENTA_NO_ENCONTRADA = "Venta no encontrada. VentaId: {VentaId}";
        private const string LOG_WARN_DETALLE_NO_ENCONTRADO = "Detalle de venta no encontrado. DetalleVentaId: {DetalleVentaId}";
        private const string LOG_WARN_CANTIDAD_EXCEDIDA = "Cantidad a devolver excede el máximo. DetalleVentaId: {DetalleVentaId}, Solicitada: {Solicitada}, Máximo: {Maximo}";
        private const string LOG_WARN_SIN_LINEAS = "Intento de procesar devolución sin líneas. VentaId: {VentaId}";

        // Errores
        private const string LOG_ERROR_PROCESAR_DEVOLUCION = "Error al procesar devolución. VentaId: {VentaId}";
        private const string LOG_ERROR_OBTENER_DEVUELTAS = "Error al obtener devoluciones acumuladas. VentaId: {VentaId}";

        #endregion

        #region Constantes - Mensajes de Resultado

        private const string MSG_DEVOLUCION_EXITOSA = "Devolución registrada correctamente";
        private const string MSG_VENTA_NO_ENCONTRADA = "La venta no existe";
        private const string MSG_SIN_LINEAS = "No se especificaron líneas para devolver";
        private const string MSG_CANTIDAD_EXCEDIDA = "La línea {0} supera el máximo permitido ({1})";
        private const string MSG_ERROR_PROCESAR = "Ocurrió un error al procesar la devolución";

        #endregion

        #region Constantes - Excepciones

        private const string EXC_VENTA_ID_INVALIDO = "El ID de venta debe ser mayor a 0";
        private const string EXC_MOTIVO_VACIO = "El motivo no puede estar vacío";
        private const string EXC_LINEAS_NULL = "Las líneas no pueden ser nulas";

        #endregion

        #region Constructor

        /// <summary>
        /// Constructor del servicio de devoluciones
        /// </summary>
        public DevolucionesService(TiendaDbContext context, ILogger<DevolucionesService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #endregion

        #region Helpers - Validación

        private static void ValidateVentaId(int ventaId)
        {
            if (ventaId <= 0)
            {
                throw new ArgumentException(EXC_VENTA_ID_INVALIDO, nameof(ventaId));
            }
        }

        private static void ValidateMotivo(string motivo)
        {
            if (string.IsNullOrWhiteSpace(motivo))
            {
                throw new ArgumentException(EXC_MOTIVO_VACIO, nameof(motivo));
            }
        }

        private static void ValidateLineas(List<LineaDevolucion> lineas)
        {
            if (lineas == null)
            {
                throw new ArgumentNullException(nameof(lineas), EXC_LINEAS_NULL);
            }
        }

        #endregion

        #region Public Methods

        /// <inheritdoc />
        public async Task<Dictionary<int, int>> GetDevueltasAcumuladasAsync(int ventaId, CancellationToken ct = default)
        {
            ValidateVentaId(ventaId);

            try
            {
                _logger.LogDebug(LOG_DEBUG_OBTENIENDO_DEVUELTAS, ventaId);

                var devueltas = await _context.Devoluciones
                    .AsNoTracking()
                    .Where(x => x.DetalleVenta.VentaID == ventaId && x.Aprobada)
                    .GroupBy(x => x.DetalleVentaID)
                    .Select(g => new { DetalleVentaID = g.Key, Cantidad = g.Sum(x => x.CantidadDevuelta) })
                    .ToDictionaryAsync(t => t.DetalleVentaID, t => t.Cantidad, ct)
                    .ConfigureAwait(false);

                return devueltas;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_OBTENER_DEVUELTAS, ventaId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<DevolucionResult> ProcesarDevolucionAsync(
            int ventaId,
            string motivo,
            List<LineaDevolucion> lineas,
            CancellationToken ct = default)
        {
            ValidateVentaId(ventaId);
            ValidateMotivo(motivo);
            ValidateLineas(lineas);

            _logger.LogDebug(LOG_DEBUG_PROCESANDO_DEVOLUCION, ventaId, lineas.Count);

            using var transaction = await _context.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

            try
            {
                var lineasValidas = lineas.Where(l => l.CantidadADevolver > 0).ToList();

                if (lineasValidas.Count == 0)
                {
                    _logger.LogWarning(LOG_WARN_SIN_LINEAS, ventaId);
                    return new DevolucionResult
                    {
                        Success = false,
                        Message = MSG_SIN_LINEAS
                    };
                }

                var venta = await _context.Ventas
                    .Include(v => v.DetalleVentas)
                        .ThenInclude(d => d.Producto)
                    .FirstOrDefaultAsync(v => v.VentaID == ventaId, ct)
                    .ConfigureAwait(false);

                if (venta == null)
                {
                    _logger.LogWarning(LOG_WARN_VENTA_NO_ENCONTRADA, ventaId);
                    return new DevolucionResult
                    {
                        Success = false,
                        Message = MSG_VENTA_NO_ENCONTRADA
                    };
                }

                var devueltasAcum = await GetDevueltasAcumuladasAsync(ventaId, ct).ConfigureAwait(false);

                var result = new DevolucionResult { Success = true };

                foreach (var linea in lineasValidas)
                {
                    _logger.LogDebug(LOG_DEBUG_VALIDANDO_LINEA, linea.DetalleVentaID, linea.CantidadADevolver);

                    var detalle = venta.DetalleVentas.FirstOrDefault(d => d.DetalleVentaID == linea.DetalleVentaID);

                    if (detalle == null)
                    {
                        _logger.LogWarning(LOG_WARN_DETALLE_NO_ENCONTRADO, linea.DetalleVentaID);
                        continue;
                    }

                    var yaDevueltas = devueltasAcum.TryGetValue(detalle.DetalleVentaID, out var devPrev) ? devPrev : 0;
                    var maxPermitido = detalle.Cantidad - yaDevueltas;

                    if (linea.CantidadADevolver > maxPermitido)
                    {
                        _logger.LogWarning(LOG_WARN_CANTIDAD_EXCEDIDA,
                            linea.DetalleVentaID, linea.CantidadADevolver, maxPermitido);

                        var error = string.Format(MSG_CANTIDAD_EXCEDIDA, detalle.DetalleVentaID, maxPermitido);
                        result.Errors.Add(error);
                        result.Success = false;
                        continue;
                    }

                    _logger.LogDebug(LOG_DEBUG_CREANDO_REGISTRO, linea.DetalleVentaID, linea.CantidadADevolver);

                    var devolucion = new Devoluciones
                    {
                        DetalleVentaID = detalle.DetalleVentaID,
                        FechaDevolucion = DateTime.UtcNow,
                        Motivo = motivo,
                        CantidadDevuelta = linea.CantidadADevolver,
                        Aprobada = true
                    };

                    await _context.Devoluciones.AddAsync(devolucion, ct).ConfigureAwait(false);

                    if (detalle.Producto != null)
                    {
                        detalle.Producto.Stock += linea.CantidadADevolver;
                        _context.Productos.Update(detalle.Producto);

                        _logger.LogInformation(LOG_INFO_STOCK_REPUESTO,
                            detalle.ProductoID, linea.CantidadADevolver, detalle.Producto.Stock);

                        var movimiento = new MovimientosInventario
                        {
                            ProductoID = detalle.ProductoID,
                            Cantidad = linea.CantidadADevolver,
                            TipoMovimiento = TIPO_MOVIMIENTO_ENTRADA,
                            FechaMovimiento = DateTime.UtcNow,
                            Descripcion = $"Devolución Venta #{venta.VentaID} (Detalle #{detalle.DetalleVentaID})"
                        };

                        await _context.MovimientosInventario.AddAsync(movimiento, ct).ConfigureAwait(false);
                    }
                }

                if (!result.Success)
                {
                    await transaction.RollbackAsync(ct).ConfigureAwait(false);
                    result.Message = MSG_ERROR_PROCESAR;
                    return result;
                }

                await _context.SaveChangesAsync(ct).ConfigureAwait(false);

                var totalVendidas = venta.DetalleVentas.Sum(d => d.Cantidad);
                var totalDevueltas = await GetTotalDevueltasAsync(ventaId, ct).ConfigureAwait(false);

                _logger.LogDebug(LOG_DEBUG_VERIFICANDO_CANCELACION, ventaId, totalVendidas, totalDevueltas);

                if (totalDevueltas >= totalVendidas)
                {
                    venta.Estado = ESTADO_CANCELADO;
                    _context.Ventas.Update(venta);
                    await _context.SaveChangesAsync(ct).ConfigureAwait(false);

                    result.VentaCancelada = true;
                    _logger.LogInformation(LOG_INFO_VENTA_CANCELADA, ventaId);
                }

                await transaction.CommitAsync(ct).ConfigureAwait(false);

                result.Message = MSG_DEVOLUCION_EXITOSA;
                _logger.LogInformation(LOG_INFO_DEVOLUCION_PROCESADA, ventaId, lineasValidas.Count, result.VentaCancelada);

                return result;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await transaction.RollbackAsync(ct).ConfigureAwait(false);
                _logger.LogError(ex, LOG_ERROR_PROCESAR_DEVOLUCION, ventaId);

                return new DevolucionResult
                {
                    Success = false,
                    Message = MSG_ERROR_PROCESAR,
                    Errors = new List<string> { ex.Message }
                };
            }
        }

        /// <inheritdoc />
        public async Task<bool> TieneDevolucionesAsync(int ventaId, CancellationToken ct = default)
        {
            ValidateVentaId(ventaId);

            return await _context.Devoluciones
                .AsNoTracking()
                .AnyAsync(d => d.DetalleVenta.VentaID == ventaId, ct)
                .ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<int> GetTotalDevueltasAsync(int ventaId, CancellationToken ct = default)
        {
            ValidateVentaId(ventaId);

            var total = await _context.Devoluciones
                .AsNoTracking()
                .Where(x => x.DetalleVenta.VentaID == ventaId && x.Aprobada)
                .SumAsync(x => x.CantidadDevuelta, ct)
                .ConfigureAwait(false);

            return total;
        }

        #endregion
    }

    #endregion

    #region DTOs

    /// <summary>
    /// Resultado del procesamiento de devolución
    /// </summary>
    public sealed class DevolucionResult
    {
        /// <summary>
        /// Indica si el procesamiento fue exitoso
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Mensaje descriptivo del resultado
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Indica si la devolución resultó en cancelación total de la venta
        /// </summary>
        public bool VentaCancelada { get; set; }

        /// <summary>
        /// Errores de validación si los hay
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();
    }

    /// <summary>
    /// Línea de devolución a procesar
    /// </summary>
    public sealed class LineaDevolucion
    {
        /// <summary>
        /// ID del detalle de venta
        /// </summary>
        public int DetalleVentaID { get; set; }

        /// <summary>
        /// Cantidad a devolver
        /// </summary>
        public int CantidadADevolver { get; set; }
    }

    #endregion
}