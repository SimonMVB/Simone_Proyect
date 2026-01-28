using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Simone.Models;
using Simone.Services;
using Simone.Extensions;

namespace Simone.Controllers
{
    /// <summary>
    /// API Controller para estimación de costos de envío del carrito
    /// Calcula tarifas basadas en reglas por vendedor y ubicación del comprador
    /// No modifica base de datos, solo realiza cálculos y retorna JSON
    /// Versión optimizada con mejores prácticas empresariales
    /// </summary>
    [Authorize(Roles = "Administrador,Vendedor,Cliente")]
    [Route("api/envios")]
    [ApiController]
    public class EnviosApiController : ControllerBase
    {
        #region Dependencias

        private readonly ILogger<EnviosApiController> _logger;
        private readonly UserManager<Usuario> _userManager;
        private readonly IEnviosResolver _enviosResolver;

        #endregion

        #region Constantes - Configuración

        private const string SESSION_CART_KEY = "Carrito";
        private const int MAX_VENDEDORES_ESPERADOS = 50;

        #endregion

        #region Constantes - Mensajes de Log

        // Información
        private const string LOG_INFO_ESTIMACION_CALCULADA = "Estimación de envío calculada. UserId: {UserId}, TotalEnvio: {Total:C}, Vendedores: {Count}";
        private const string LOG_INFO_CARRITO_VACIO = "Estimación solicitada con carrito vacío. UserId: {UserId}";
        private const string LOG_INFO_SIN_UBICACION = "Estimación solicitada sin ubicación configurada. UserId: {UserId}";

        // Debug
        private const string LOG_DEBUG_INICIANDO_ESTIMACION = "Iniciando estimación de envío. UserId: {UserId}";
        private const string LOG_DEBUG_CARRITO_LEIDO = "Carrito leído de sesión. Items: {Count}";
        private const string LOG_DEBUG_UBICACION_OBTENIDA = "Ubicación obtenida. Provincia: {Provincia}, Ciudad: {Ciudad}";
        private const string LOG_DEBUG_VENDEDORES_AGRUPADOS = "Items agrupados por vendedor. Vendedores: {Count}";
        private const string LOG_DEBUG_TARIFA_CALCULADA = "Tarifa calculada. VendedorId: {VendedorId}, Precio: {Precio:C}";

        // Advertencias
        private const string LOG_WARN_USUARIO_NO_AUTENTICADO = "Intento de estimación sin usuario autenticado";
        private const string LOG_WARN_USUARIO_SIN_PROVINCIA = "Usuario sin provincia configurada. UserId: {UserId}";
        private const string LOG_WARN_MUCHOS_VENDEDORES = "Número alto de vendedores en carrito. Count: {Count}, Máximo esperado: {Max}";
        private const string LOG_WARN_ERROR_LEER_CARRITO = "Error al leer carrito de sesión. UserId: {UserId}";

        // Errores
        private const string LOG_ERROR_ESTIMACION = "Error al calcular estimación de envío. UserId: {UserId}";
        private const string LOG_ERROR_OBTENER_UBICACION = "Error al obtener ubicación del usuario. UserId: {UserId}";

        #endregion

        #region Constantes - Mensajes de Respuesta

        private const string MSG_SIN_PROVINCIA = "El usuario no tiene provincia configurada en su perfil";
        private const string MSG_ERROR_CALCULO = "Error al calcular los costos de envío";
        private const string MSG_CARRITO_VACIO = "El carrito está vacío";

        #endregion

        #region Constructor

        /// <summary>
        /// Constructor del API Controller de envíos
        /// </summary>
        /// <param name="logger">Logger para registro de eventos</param>
        /// <param name="userManager">Administrador de usuarios</param>
        /// <param name="enviosResolver">Servicio de resolución de tarifas de envío</param>
        /// <exception cref="ArgumentNullException">Si alguna dependencia es null</exception>
        public EnviosApiController(
            ILogger<EnviosApiController> logger,
            UserManager<Usuario> userManager,
            IEnviosResolver enviosResolver)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _enviosResolver = enviosResolver ?? throw new ArgumentNullException(nameof(enviosResolver));
        }

        #endregion

        #region API Endpoints

        /// <summary>
        /// Estima el costo total de envío del carrito actual
        /// </summary>
        /// <param name="ct">Token de cancelación</param>
        /// <returns>Respuesta con costo total y detalle por vendedor</returns>
        /// <response code="200">Estimación calculada exitosamente</response>
        /// <response code="400">Error en los datos de entrada</response>
        /// <response code="401">Usuario no autenticado</response>
        /// <response code="500">Error interno del servidor</response>
        [HttpGet("estimar")]
        [ProducesResponseType(typeof(EstimacionEnvioResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Estimar(CancellationToken ct = default)
        {
            try
            {
                // Obtener usuario actual
                var userId = _userManager.GetUserId(User);

                if (string.IsNullOrWhiteSpace(userId))
                {
                    _logger.LogWarning(LOG_WARN_USUARIO_NO_AUTENTICADO);
                    return Unauthorized();
                }

                _logger.LogDebug(LOG_DEBUG_INICIANDO_ESTIMACION, userId);

                // Obtener ubicación del usuario
                var (provincia, ciudad) = await GetBuyerLocationAsync(userId, ct).ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(provincia))
                {
                    _logger.LogInformation(LOG_INFO_SIN_UBICACION, userId);
                    return Ok(new EstimacionEnvioResponse
                    {
                        TotalEnvio = 0m,
                        Detalle = new List<DetalleEnvioVendedor>(),
                        Warning = MSG_SIN_PROVINCIA
                    });
                }

                // Leer carrito de sesión
                var cartItems = ReadCartFromSession();
                _logger.LogDebug(LOG_DEBUG_CARRITO_LEIDO, cartItems.Count);

                if (cartItems.Count == 0)
                {
                    _logger.LogInformation(LOG_INFO_CARRITO_VACIO, userId);
                    return Ok(new EstimacionEnvioResponse
                    {
                        TotalEnvio = 0m,
                        Detalle = new List<DetalleEnvioVendedor>(),
                        Info = MSG_CARRITO_VACIO
                    });
                }

                // Agrupar por vendedor
                var porVendedor = AgruparPorVendedor(cartItems);
                _logger.LogDebug(LOG_DEBUG_VENDEDORES_AGRUPADOS, porVendedor.Count);

                if (porVendedor.Count > MAX_VENDEDORES_ESPERADOS)
                {
                    _logger.LogWarning(LOG_WARN_MUCHOS_VENDEDORES, porVendedor.Count, MAX_VENDEDORES_ESPERADOS);
                }

                // Calcular tarifas
                var detalle = new List<DetalleEnvioVendedor>();
                decimal totalEnvio = 0m;

                // ✅ CORRECTO
                foreach (var grupo in porVendedor)
                {
                    var tarifaNullable = await _enviosResolver.GetTarifaAsync(
                        grupo.VendedorId,
                        provincia,
                        ciudad,
                        ct).ConfigureAwait(false);

                    var tarifa = tarifaNullable ?? 0m; 

                    _logger.LogDebug(LOG_DEBUG_TARIFA_CALCULADA, grupo.VendedorId, tarifa);

                    totalEnvio += tarifa; 

                    detalle.Add(new DetalleEnvioVendedor
                    {
                        VendedorId = grupo.VendedorId,
                        Provincia = provincia,
                        Ciudad = ciudad ?? string.Empty,
                        Precio = tarifa,  
                        ItemsCount = grupo.ItemsCount,
                        TieneReglaDefinida = tarifa > 0
                    });
                }

                _logger.LogInformation(LOG_INFO_ESTIMACION_CALCULADA, userId, totalEnvio, porVendedor.Count);

                return Ok(new EstimacionEnvioResponse
                {
                    TotalEnvio = totalEnvio,
                    Detalle = detalle
                });
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var userId = _userManager.GetUserId(User);
                _logger.LogError(ex, LOG_ERROR_ESTIMACION, userId);

                return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
                {
                    Error = MSG_ERROR_CALCULO,
                    Details = ex.Message
                });
            }
        }

        #endregion

        #region Helpers - Session

        /// <summary>
        /// Lee el carrito desde la sesión
        /// </summary>
        private List<SessionCartItem> ReadCartFromSession()
        {
            try
            {
                if (HttpContext?.Session == null)
                {
                    return new List<SessionCartItem>();
                }

                // Usar SessionExtensions mejorado
                if (HttpContext.Session.TryGetObjectFromJson<List<SessionCartItem>>(SESSION_CART_KEY, out var cart))
                {
                    return cart ?? new List<SessionCartItem>();
                }

                return new List<SessionCartItem>();
            }
            catch (Exception ex)
            {
                var userId = _userManager.GetUserId(User);
                _logger.LogWarning(ex, LOG_WARN_ERROR_LEER_CARRITO, userId);
                return new List<SessionCartItem>();
            }
        }

        #endregion

        #region Helpers - User Location

        /// <summary>
        /// Obtiene la ubicación del comprador (provincia y ciudad)
        /// </summary>
        private async Task<(string? Provincia, string? Ciudad)> GetBuyerLocationAsync(
            string userId,
            CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return (null, null);
                }

                var user = await _userManager.FindByIdAsync(userId).ConfigureAwait(false);

                if (user == null)
                {
                    return (null, null);
                }

                var provincia = user.Provincia;
                var ciudad = user.Ciudad;

                _logger.LogDebug(LOG_DEBUG_UBICACION_OBTENIDA, provincia, ciudad);

                return (provincia, ciudad);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_OBTENER_UBICACION, userId);
                return (null, null);
            }
        }

        #endregion

        #region Helpers - Grouping

        /// <summary>
        /// Agrupa items del carrito por vendedor
        /// </summary>
        private static List<VendedorGroup> AgruparPorVendedor(List<SessionCartItem> items)
        {
            return items
                .Where(i => !string.IsNullOrWhiteSpace(i.VendedorID))
                .GroupBy(i => i.VendedorID.Trim())
                .Select(g => new VendedorGroup
                {
                    VendedorId = g.Key,
                    ItemsCount = g.Sum(x => x.Cantidad)
                })
                .ToList();
        }

        #endregion
    }

    #region DTOs

    /// <summary>
    /// Item del carrito almacenado en sesión
    /// </summary>
    public sealed class SessionCartItem
    {
        /// <summary>
        /// ID del producto
        /// </summary>
        public int ProductoID { get; set; }

        /// <summary>
        /// Cantidad de items
        /// </summary>
        public int Cantidad { get; set; }

        /// <summary>
        /// Precio unitario del producto
        /// </summary>
        public decimal PrecioUnitario { get; set; }

        /// <summary>
        /// ID del vendedor
        /// </summary>
        public string VendedorID { get; set; } = string.Empty;
    }

    /// <summary>
    /// Respuesta de estimación de envío
    /// </summary>
    public sealed class EstimacionEnvioResponse
    {
        /// <summary>
        /// Costo total de envío
        /// </summary>
        public decimal TotalEnvio { get; set; }

        /// <summary>
        /// Detalle por vendedor
        /// </summary>
        public List<DetalleEnvioVendedor> Detalle { get; set; } = new List<DetalleEnvioVendedor>();

        /// <summary>
        /// Mensaje de advertencia (opcional)
        /// </summary>
        public string? Warning { get; set; }

        /// <summary>
        /// Mensaje informativo (opcional)
        /// </summary>
        public string? Info { get; set; }
    }

    /// <summary>
    /// Detalle de envío por vendedor
    /// </summary>
    public sealed class DetalleEnvioVendedor
    {
        /// <summary>
        /// ID del vendedor
        /// </summary>
        public string VendedorId { get; set; } = string.Empty;

        /// <summary>
        /// Provincia de destino
        /// </summary>
        public string Provincia { get; set; } = string.Empty;

        /// <summary>
        /// Ciudad de destino
        /// </summary>
        public string Ciudad { get; set; } = string.Empty;

        /// <summary>
        /// Precio de envío
        /// </summary>
        public decimal Precio { get; set; }

        /// <summary>
        /// Cantidad de items del vendedor
        /// </summary>
        public int ItemsCount { get; set; }

        /// <summary>
        /// Indica si el vendedor tiene regla de envío definida
        /// </summary>
        public bool TieneReglaDefinida { get; set; }
    }

    /// <summary>
    /// Respuesta de error estándar
    /// </summary>
    public sealed class ErrorResponse
    {
        /// <summary>
        /// Mensaje de error
        /// </summary>
        public string Error { get; set; } = string.Empty;

        /// <summary>
        /// Detalles del error (opcional)
        /// </summary>
        public string? Details { get; set; }
    }

    /// <summary>
    /// Agrupación de items por vendedor
    /// </summary>
    internal sealed class VendedorGroup
    {
        /// <summary>
        /// ID del vendedor
        /// </summary>
        public string VendedorId { get; set; } = string.Empty;

        /// <summary>
        /// Cantidad total de items
        /// </summary>
        public int ItemsCount { get; set; }
    }

    #endregion
}