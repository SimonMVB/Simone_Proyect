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
    /// Servicio de resolución de vendedores en carrito para gestión de pagos
    /// Determina si un carrito tiene productos de múltiples vendedores
    /// Versión optimizada con mejores prácticas empresariales
    /// </summary>
    public interface IPagosResolver
    {
        #region Resolución Principal

        /// <summary>
        /// Resuelve la información de vendedores en el carrito del usuario
        /// </summary>
        /// <param name="usuarioId">ID del usuario</param>
        /// <param name="carritoId">ID del carrito específico (opcional, si null analiza todos los carritos del usuario)</param>
        /// <param name="ct">Token de cancelación</param>
        /// <returns>Información sobre vendedores en el carrito</returns>
        Task<CarritoVendedorInfo> ResolverAsync(string usuarioId, int? carritoId = null, CancellationToken ct = default);

        #endregion

        #region Consultas

        /// <summary>
        /// Obtiene el ID del vendedor único si el carrito es de un solo vendedor
        /// </summary>
        /// <param name="usuarioId">ID del usuario</param>
        /// <param name="carritoId">ID del carrito</param>
        /// <param name="ct">Token de cancelación</param>
        /// <returns>ID del vendedor único o null si es multi-vendedor</returns>
        Task<string?> GetVendedorUnicoAsync(string usuarioId, int? carritoId = null, CancellationToken ct = default);

        /// <summary>
        /// Verifica si el carrito tiene productos de múltiples vendedores
        /// </summary>
        /// <param name="usuarioId">ID del usuario</param>
        /// <param name="carritoId">ID del carrito</param>
        /// <param name="ct">Token de cancelación</param>
        Task<bool> IsMultiVendedorAsync(string usuarioId, int? carritoId = null, CancellationToken ct = default);

        /// <summary>
        /// Obtiene la lista de todos los vendedores distintos en el carrito
        /// </summary>
        /// <param name="usuarioId">ID del usuario</param>
        /// <param name="carritoId">ID del carrito</param>
        /// <param name="ct">Token de cancelación</param>
        Task<IReadOnlyList<string>> GetVendedoresAsync(string usuarioId, int? carritoId = null, CancellationToken ct = default);

        #endregion

        #region Estadísticas

        /// <summary>
        /// Obtiene estadísticas detalladas de vendedores en el carrito
        /// </summary>
        /// <param name="usuarioId">ID del usuario</param>
        /// <param name="carritoId">ID del carrito</param>
        /// <param name="ct">Token de cancelación</param>
        Task<CarritoEstadisticas> GetEstadisticasAsync(string usuarioId, int? carritoId = null, CancellationToken ct = default);

        /// <summary>
        /// Obtiene vendedores agrupados con conteo de productos
        /// </summary>
        /// <param name="usuarioId">ID del usuario</param>
        /// <param name="carritoId">ID del carrito</param>
        /// <param name="ct">Token de cancelación</param>
        Task<Dictionary<string, int>> GetVendedoresPorCarritoAsync(string usuarioId, int? carritoId = null, CancellationToken ct = default);

        #endregion
    }

    #endregion

    #region Implementation

    /// <summary>
    /// Implementación del resolver de pagos con logging y validación robusta
    /// </summary>
    public sealed class PagosResolver : IPagosResolver
    {
        #region Dependencias

        private readonly TiendaDbContext _db;
        private readonly ILogger<PagosResolver> _logger;

        #endregion

        #region Constantes - Configuración

        private const int MIN_CANTIDAD_VALIDA = 1;
        private const int MAX_VENDEDORES_ESPERADOS = 100;

        #endregion

        #region Constantes - Mensajes de Log

        // Información
        private const string LOG_INFO_RESOLUCION_COMPLETADA = "Resolución de vendedores completada. UsuarioId: {UsuarioId}, CarritoId: {CarritoId}, VendedoresCount: {Count}, EsMultiVendedor: {EsMulti}";
        private const string LOG_INFO_VENDEDOR_UNICO_ENCONTRADO = "Vendedor único encontrado. UsuarioId: {UsuarioId}, VendedorId: {VendedorId}";
        private const string LOG_INFO_MULTI_VENDEDOR_DETECTADO = "Carrito multi-vendedor detectado. UsuarioId: {UsuarioId}, VendedoresCount: {Count}";
        private const string LOG_INFO_CARRITO_VACIO = "Carrito sin vendedores (vacío o sin productos válidos). UsuarioId: {UsuarioId}, CarritoId: {CarritoId}";

        // Debug
        private const string LOG_DEBUG_INICIANDO_RESOLUCION = "Iniciando resolución de vendedores. UsuarioId: {UsuarioId}, CarritoId: {CarritoId}";
        private const string LOG_DEBUG_QUERY_CONSTRUIDA = "Query construida. UsuarioId: {UsuarioId}, CarritoId: {CarritoId}, FiltraCarrito: {FiltraCarrito}";
        private const string LOG_DEBUG_VENDEDORES_CRUDOS = "Vendedores sin normalizar obtenidos. Count: {Count}";
        private const string LOG_DEBUG_VENDEDORES_NORMALIZADOS = "Vendedores normalizados. Original: {Original}, Normalizados: {Normalizados}";
        private const string LOG_DEBUG_VENDEDOR_NORMALIZADO = "Vendedor normalizado. Original: '{Original}', Normalizado: '{Normalizado}'";
        private const string LOG_DEBUG_VENDEDORES_DISTINTOS = "Vendedores distintos. Count: {Count}, Lista: {Lista}";
        private const string LOG_DEBUG_OBTENIENDO_VENDEDOR_UNICO = "Obteniendo vendedor único. UsuarioId: {UsuarioId}, CarritoId: {CarritoId}";
        private const string LOG_DEBUG_VERIFICANDO_MULTI_VENDEDOR = "Verificando si es multi-vendedor. UsuarioId: {UsuarioId}, CarritoId: {CarritoId}";
        private const string LOG_DEBUG_OBTENIENDO_LISTA_VENDEDORES = "Obteniendo lista de vendedores. UsuarioId: {UsuarioId}, CarritoId: {CarritoId}";
        private const string LOG_DEBUG_CALCULANDO_ESTADISTICAS = "Calculando estadísticas. UsuarioId: {UsuarioId}, CarritoId: {CarritoId}";
        private const string LOG_DEBUG_AGRUPANDO_POR_VENDEDOR = "Agrupando productos por vendedor. UsuarioId: {UsuarioId}, CarritoId: {CarritoId}";

        // Advertencias
        private const string LOG_WARN_USUARIO_ID_VACIO = "UsuarioId vacío o nulo en ResolverAsync";
        private const string LOG_WARN_MUCHOS_VENDEDORES = "Número alto de vendedores en un carrito. Count: {Count}, Máximo esperado: {Max}";
        private const string LOG_WARN_VENDEDOR_VACIO_FILTRADO = "Vendedor vacío filtrado después de normalización. Original: '{Original}'";
        private const string LOG_WARN_SIN_VENDEDORES = "No se encontraron vendedores en el carrito. UsuarioId: {UsuarioId}, CarritoId: {CarritoId}";

        // Errores
        private const string LOG_ERROR_RESOLVER = "Error al resolver vendedores. UsuarioId: {UsuarioId}, CarritoId: {CarritoId}";
        private const string LOG_ERROR_OBTENER_VENDEDOR_UNICO = "Error al obtener vendedor único. UsuarioId: {UsuarioId}, CarritoId: {CarritoId}";
        private const string LOG_ERROR_VERIFICAR_MULTI = "Error al verificar multi-vendedor. UsuarioId: {UsuarioId}, CarritoId: {CarritoId}";
        private const string LOG_ERROR_OBTENER_LISTA = "Error al obtener lista de vendedores. UsuarioId: {UsuarioId}, CarritoId: {CarritoId}";
        private const string LOG_ERROR_ESTADISTICAS = "Error al calcular estadísticas. UsuarioId: {UsuarioId}, CarritoId: {CarritoId}";
        private const string LOG_ERROR_AGRUPAR_VENDEDORES = "Error al agrupar vendedores. UsuarioId: {UsuarioId}, CarritoId: {CarritoId}";

        #endregion

        #region Constantes - Mensajes de Excepción

        private const string EXC_USUARIO_ID_NULL = "El ID del usuario no puede ser nulo o vacío";

        #endregion

        #region Constructor

        /// <summary>
        /// Constructor del resolver de pagos
        /// </summary>
        /// <param name="db">Contexto de base de datos</param>
        /// <param name="logger">Logger para registro de eventos</param>
        /// <exception cref="ArgumentNullException">Si alguna dependencia es null</exception>
        public PagosResolver(TiendaDbContext db, ILogger<PagosResolver> logger)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #endregion

        #region Helpers - Validación

        /// <summary>
        /// Valida que el usuarioId no esté vacío
        /// </summary>
        private void ValidateUsuarioId(string usuarioId)
        {
            if (string.IsNullOrWhiteSpace(usuarioId))
            {
                _logger.LogWarning(LOG_WARN_USUARIO_ID_VACIO);
                throw new ArgumentException(EXC_USUARIO_ID_NULL, nameof(usuarioId));
            }
        }

        #endregion

        #region Helpers - Query Building

        /// <summary>
        /// Construye la query base de carrito detalle para el usuario
        /// </summary>
        private IQueryable<CarritoDetalle> BuildBaseQuery(string usuarioId, int? carritoId)
        {
            _logger.LogDebug(LOG_DEBUG_QUERY_CONSTRUIDA, usuarioId, carritoId, carritoId.HasValue);

            IQueryable<CarritoDetalle> query = _db.CarritoDetalle
                .AsNoTracking()
                .Where(cd => cd.Carrito != null && cd.Carrito.UsuarioId == usuarioId);

            if (carritoId.HasValue)
            {
                query = query.Where(cd => cd.CarritoID == carritoId.Value);
            }

            // Solo cantidades válidas
            query = query.Where(cd => cd.Cantidad >= MIN_CANTIDAD_VALIDA);

            return query;
        }

        #endregion

        #region Helpers - Normalización

        /// <summary>
        /// Normaliza y filtra una lista de vendedores
        /// </summary>
        private List<string> NormalizarVendedores(List<string> vendedoresCrudos)
        {
            _logger.LogDebug(LOG_DEBUG_VENDEDORES_CRUDOS, vendedoresCrudos.Count);

            var vendedoresNormalizados = new List<string>();

            foreach (var vendedor in vendedoresCrudos)
            {
                var original = vendedor;
                var normalizado = vendedor.Trim();

                if (original != normalizado)
                {
                    _logger.LogDebug(LOG_DEBUG_VENDEDOR_NORMALIZADO, original, normalizado);
                }

                if (normalizado.Length > 0)
                {
                    vendedoresNormalizados.Add(normalizado);
                }
                else
                {
                    _logger.LogWarning(LOG_WARN_VENDEDOR_VACIO_FILTRADO, original);
                }
            }

            var vendedoresDistintos = vendedoresNormalizados
                .Distinct(StringComparer.Ordinal)
                .ToList();

            _logger.LogDebug(LOG_DEBUG_VENDEDORES_NORMALIZADOS,
                vendedoresCrudos.Count, vendedoresDistintos.Count);

            if (vendedoresDistintos.Count > 0)
            {
                _logger.LogDebug(LOG_DEBUG_VENDEDORES_DISTINTOS,
                    vendedoresDistintos.Count, string.Join(", ", vendedoresDistintos));
            }

            if (vendedoresDistintos.Count > MAX_VENDEDORES_ESPERADOS)
            {
                _logger.LogWarning(LOG_WARN_MUCHOS_VENDEDORES,
                    vendedoresDistintos.Count, MAX_VENDEDORES_ESPERADOS);
            }

            return vendedoresDistintos;
        }

        #endregion

        #region Helpers - Construcción de Resultado

        /// <summary>
        /// Construye el resultado de CarritoVendedorInfo
        /// </summary>
        private CarritoVendedorInfo BuildCarritoVendedorInfo(List<string> vendedores)
        {
            if (vendedores.Count == 0)
            {
                return new CarritoVendedorInfo
                {
                    EsMultiVendedor = false,
                    VendedorIdUnico = null,
                    VendedoresIds = Array.Empty<string>()
                };
            }

            if (vendedores.Count == 1)
            {
                return new CarritoVendedorInfo
                {
                    EsMultiVendedor = false,
                    VendedorIdUnico = vendedores[0],
                    VendedoresIds = vendedores
                };
            }

            return new CarritoVendedorInfo
            {
                EsMultiVendedor = true,
                VendedorIdUnico = null,
                VendedoresIds = vendedores
            };
        }

        #endregion

        #region Resolución Principal

        /// <inheritdoc />
        public async Task<CarritoVendedorInfo> ResolverAsync(
            string usuarioId,
            int? carritoId = null,
            CancellationToken ct = default)
        {
            ValidateUsuarioId(usuarioId);

            try
            {
                _logger.LogDebug(LOG_DEBUG_INICIANDO_RESOLUCION, usuarioId, carritoId);

                // Construir query base
                var query = BuildBaseQuery(usuarioId, carritoId);

                // Proyección directa y obtención
                var vendedoresCrudos = await query
                    .Where(cd => cd.Producto != null &&
                                cd.Producto.VendedorID != null &&
                                cd.Producto.VendedorID != "")
                    .Select(cd => cd.Producto!.VendedorID!)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);

                // Normalizar vendedores
                var vendedoresNorm = NormalizarVendedores(vendedoresCrudos);

                // Construir resultado
                var resultado = BuildCarritoVendedorInfo(vendedoresNorm);

                // Logging del resultado
                if (vendedoresNorm.Count == 0)
                {
                    _logger.LogInformation(LOG_INFO_CARRITO_VACIO, usuarioId, carritoId);
                }
                else if (vendedoresNorm.Count == 1)
                {
                    _logger.LogInformation(LOG_INFO_VENDEDOR_UNICO_ENCONTRADO,
                        usuarioId, vendedoresNorm[0]);
                }
                else
                {
                    _logger.LogInformation(LOG_INFO_MULTI_VENDEDOR_DETECTADO,
                        usuarioId, vendedoresNorm.Count);
                }

                _logger.LogInformation(LOG_INFO_RESOLUCION_COMPLETADA,
                    usuarioId, carritoId, vendedoresNorm.Count, resultado.EsMultiVendedor);

                return resultado;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_RESOLVER, usuarioId, carritoId);
                throw;
            }
        }

        #endregion

        #region Consultas

        /// <inheritdoc />
        public async Task<string?> GetVendedorUnicoAsync(
            string usuarioId,
            int? carritoId = null,
            CancellationToken ct = default)
        {
            ValidateUsuarioId(usuarioId);

            try
            {
                _logger.LogDebug(LOG_DEBUG_OBTENIENDO_VENDEDOR_UNICO, usuarioId, carritoId);

                var info = await ResolverAsync(usuarioId, carritoId, ct).ConfigureAwait(false);

                return info.VendedorIdUnico;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_OBTENER_VENDEDOR_UNICO, usuarioId, carritoId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> IsMultiVendedorAsync(
            string usuarioId,
            int? carritoId = null,
            CancellationToken ct = default)
        {
            ValidateUsuarioId(usuarioId);

            try
            {
                _logger.LogDebug(LOG_DEBUG_VERIFICANDO_MULTI_VENDEDOR, usuarioId, carritoId);

                var info = await ResolverAsync(usuarioId, carritoId, ct).ConfigureAwait(false);

                return info.EsMultiVendedor;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_VERIFICAR_MULTI, usuarioId, carritoId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<string>> GetVendedoresAsync(
            string usuarioId,
            int? carritoId = null,
            CancellationToken ct = default)
        {
            ValidateUsuarioId(usuarioId);

            try
            {
                _logger.LogDebug(LOG_DEBUG_OBTENIENDO_LISTA_VENDEDORES, usuarioId, carritoId);

                var info = await ResolverAsync(usuarioId, carritoId, ct).ConfigureAwait(false);

                return info.VendedoresIds;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_OBTENER_LISTA, usuarioId, carritoId);
                throw;
            }
        }

        #endregion

        #region Estadísticas

        /// <inheritdoc />
        public async Task<CarritoEstadisticas> GetEstadisticasAsync(
            string usuarioId,
            int? carritoId = null,
            CancellationToken ct = default)
        {
            ValidateUsuarioId(usuarioId);

            try
            {
                _logger.LogDebug(LOG_DEBUG_CALCULANDO_ESTADISTICAS, usuarioId, carritoId);

                var query = BuildBaseQuery(usuarioId, carritoId);

                // Obtener detalles completos
                var detalles = await query
                    .Where(cd => cd.Producto != null &&
                                cd.Producto.VendedorID != null &&
                                cd.Producto.VendedorID != "")
                    .Select(cd => new
                    {
                        VendedorId = cd.Producto!.VendedorID!,
                        Cantidad = cd.Cantidad
                    })
                    .ToListAsync(ct)
                    .ConfigureAwait(false);

                // Normalizar vendedores
                var vendedoresNormalizados = detalles
                    .Select(d => d.VendedorId.Trim())
                    .Where(v => v.Length > 0)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

                // Agrupar por vendedor
                var agrupados = detalles
                    .GroupBy(d => d.VendedorId.Trim(), StringComparer.Ordinal)
                    .Where(g => g.Key.Length > 0)
                    .Select(g => new
                    {
                        VendedorId = g.Key,
                        ProductosCount = g.Count(),
                        CantidadTotal = g.Sum(x => x.Cantidad)
                    })
                    .ToList();

                var estadisticas = new CarritoEstadisticas
                {
                    TotalVendedores = vendedoresNormalizados.Count,
                    EsMultiVendedor = vendedoresNormalizados.Count > 1,
                    VendedorUnico = vendedoresNormalizados.Count == 1 ? vendedoresNormalizados[0] : null,
                    TotalProductosDistintos = detalles.Count,
                    CantidadTotalItems = detalles.Sum(d => d.Cantidad)
                };

                if (agrupados.Count > 0)
                {
                    estadisticas.ProductosPorVendedorPromedio = (decimal)agrupados.Average(a => a.ProductosCount);
                    estadisticas.ItemsPorVendedorPromedio = (decimal)agrupados.Average(a => a.CantidadTotal);

                    var vendedorConMasProductos = agrupados.OrderByDescending(a => a.ProductosCount).First();
                    estadisticas.VendedorConMasProductos = vendedorConMasProductos.VendedorId;
                    estadisticas.MaxProductosPorVendedor = vendedorConMasProductos.ProductosCount;
                }

                return estadisticas;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_ESTADISTICAS, usuarioId, carritoId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<Dictionary<string, int>> GetVendedoresPorCarritoAsync(
            string usuarioId,
            int? carritoId = null,
            CancellationToken ct = default)
        {
            ValidateUsuarioId(usuarioId);

            try
            {
                _logger.LogDebug(LOG_DEBUG_AGRUPANDO_POR_VENDEDOR, usuarioId, carritoId);

                var query = BuildBaseQuery(usuarioId, carritoId);

                // Agrupar por vendedor
                var agrupados = await query
                    .Where(cd => cd.Producto != null &&
                                cd.Producto.VendedorID != null &&
                                cd.Producto.VendedorID != "")
                    .GroupBy(cd => cd.Producto!.VendedorID!)
                    .Select(g => new
                    {
                        VendedorId = g.Key,
                        Count = g.Count()
                    })
                    .ToListAsync(ct)
                    .ConfigureAwait(false);

                // Normalizar y convertir a diccionario
                var resultado = agrupados
                    .Select(a => new
                    {
                        VendedorId = a.VendedorId.Trim(),
                        a.Count
                    })
                    .Where(a => a.VendedorId.Length > 0)
                    .GroupBy(a => a.VendedorId, StringComparer.Ordinal)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Sum(x => x.Count),
                        StringComparer.Ordinal
                    );

                return resultado;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_AGRUPAR_VENDEDORES, usuarioId, carritoId);
                throw;
            }
        }

        #endregion
    }

    #endregion

    #region DTOs

    /// <summary>
    /// Información de vendedores en el carrito para decisiones de pago
    /// </summary>
    public sealed class CarritoVendedorInfo
    {
        /// <summary>
        /// Indica si el carrito tiene productos de múltiples vendedores
        /// </summary>
        public bool EsMultiVendedor { get; init; }

        /// <summary>
        /// ID del vendedor único si el carrito tiene productos de un solo vendedor
        /// Null si es multi-vendedor o carrito vacío
        /// </summary>
        public string? VendedorIdUnico { get; init; }

        /// <summary>
        /// Lista de IDs de vendedores distintos en el carrito
        /// </summary>
        public IReadOnlyList<string> VendedoresIds { get; init; } = Array.Empty<string>();

        /// <summary>
        /// Cantidad de tiendas (vendedores) distintas en el carrito
        /// </summary>
        public int TiendasCount => VendedoresIds?.Count ?? 0;

        /// <summary>
        /// Indica si el carrito está vacío (sin vendedores)
        /// </summary>
        public bool EstaVacio => TiendasCount == 0;

        /// <summary>
        /// Indica si el carrito tiene exactamente un vendedor
        /// </summary>
        public bool TieneVendedorUnico => TiendasCount == 1;

        /// <summary>
        /// Indica si el carrito tiene más de un vendedor
        /// </summary>
        public bool TieneMultiplesVendedores => TiendasCount > 1;
    }

    /// <summary>
    /// Estadísticas detalladas de vendedores en el carrito
    /// </summary>
    public sealed class CarritoEstadisticas
    {
        /// <summary>
        /// Total de vendedores distintos
        /// </summary>
        public int TotalVendedores { get; set; }

        /// <summary>
        /// Indica si es multi-vendedor
        /// </summary>
        public bool EsMultiVendedor { get; set; }

        /// <summary>
        /// Vendedor único (si aplica)
        /// </summary>
        public string? VendedorUnico { get; set; }

        /// <summary>
        /// Total de productos distintos (sin contar cantidades)
        /// </summary>
        public int TotalProductosDistintos { get; set; }

        /// <summary>
        /// Cantidad total de items (sumando cantidades)
        /// </summary>
        public int CantidadTotalItems { get; set; }

        /// <summary>
        /// Promedio de productos por vendedor
        /// </summary>
        public decimal ProductosPorVendedorPromedio { get; set; }

        /// <summary>
        /// Promedio de items por vendedor (considerando cantidades)
        /// </summary>
        public decimal ItemsPorVendedorPromedio { get; set; }

        /// <summary>
        /// Vendedor con más productos
        /// </summary>
        public string? VendedorConMasProductos { get; set; }

        /// <summary>
        /// Cantidad máxima de productos en un vendedor
        /// </summary>
        public int MaxProductosPorVendedor { get; set; }
    }

    #endregion
}