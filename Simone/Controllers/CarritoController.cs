using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Simone.Data;
using Simone.Extensions;
using Simone.Models;
using Simone.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Simone.Controllers
{
    /// <summary>
    /// Controlador de carrito de compras
    /// Versión optimizada con mejores prácticas empresariales
    /// ACTUALIZADO: Añadido CartPartial para actualización AJAX del carrito
    /// </summary>
    [Route("Carrito")]
    public class CarritoController : Controller
    {
        #region Dependencias e Inyección

        private readonly TiendaDbContext _context;
        private readonly UserManager<Usuario> _userManager;
        private readonly ICarritoService _carritoService;
        private readonly EnviosCarritoService _enviosCarrito;
        private readonly IMemoryCache _cache;
        private readonly ILogger<CarritoController> _logger;

        public CarritoController(
            TiendaDbContext context,
            UserManager<Usuario> userManager,
            ICarritoService carritoService,
            EnviosCarritoService enviosCarrito,
            IMemoryCache cache,
            ILogger<CarritoController> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _carritoService = carritoService ?? throw new ArgumentNullException(nameof(carritoService));
            _enviosCarrito = enviosCarrito ?? throw new ArgumentNullException(nameof(enviosCarrito));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #endregion

        #region Constantes

        private const string SESSION_KEY_CUPON = "Cupon";
        private const string CACHE_KEY_PROMOCIONES = "Promociones_Activas";
        private const string HEADER_AJAX = "X-Requested-With";
        private const string HEADER_AJAX_VALUE = "XMLHttpRequest";

        private const int CANTIDAD_MINIMA = 1;
        private const int CANTIDAD_MAXIMA_POR_PRODUCTO = 99;
        private const int CANTIDAD_DEFAULT = 1;

        private static readonly TimeSpan CACHE_DURATION_PROMOCIONES = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan CACHE_DURATION_VENDEDORES = TimeSpan.FromMinutes(10);

        #endregion

        #region Helpers y Utilidades

        /// <summary>
        /// Determina si la solicitud es AJAX
        /// </summary>
        private bool EsAjax()
        {
            return Request.Headers.TryGetValue(HEADER_AJAX, out var headerValue) &&
                   string.Equals(headerValue, HEADER_AJAX_VALUE, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Calcula resumen del carrito (cantidad total y precio total)
        /// </summary>
        private CarritoResumen CalcularResumen(List<CarritoDetalle> detalles)
        {
            if (detalles == null || !detalles.Any())
            {
                return new CarritoResumen(0, 0m);
            }

            var cantidad = detalles.Sum(c => c.Cantidad);
            var total = detalles.Sum(c => c.Precio * c.Cantidad);

            return new CarritoResumen(cantidad, total);
        }

        /// <summary>
        /// Obtiene el cupón de descuento de la sesión
        /// </summary>
        private Promocion? ObtenerCupon()
        {
            try
            {
                return HttpContext.Session.GetObjectFromJson<Promocion>(SESSION_KEY_CUPON);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al obtener cupón de la sesión");
                return null;
            }
        }

        /// <summary>
        /// Guarda el cupón de descuento en la sesión
        /// </summary>
        private void GuardarCupon(Promocion cupon)
        {
            if (cupon == null)
            {
                _logger.LogWarning("Intento de guardar cupón null");
                return;
            }

            try
            {
                HttpContext.Session.SetObjectAsJson(SESSION_KEY_CUPON, cupon);
                _logger.LogDebug("Cupón guardado en sesión: {Codigo}", cupon.CodigoCupon);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar cupón en sesión: {Codigo}", cupon.CodigoCupon);
            }
        }

        /// <summary>
        /// Elimina el cupón de la sesión
        /// </summary>
        private void EliminarCupon()
        {
            HttpContext.Session.Remove(SESSION_KEY_CUPON);
            _logger.LogDebug("Cupón eliminado de sesión");
        }

        /// <summary>
        /// Valida que la cantidad esté en rango permitido
        /// </summary>
        private ValidationResult ValidarCantidad(int cantidad)
        {
            if (cantidad < CANTIDAD_MINIMA)
            {
                return ValidationResult.Failure(
                    $"La cantidad mínima es {CANTIDAD_MINIMA}");
            }

            if (cantidad > CANTIDAD_MAXIMA_POR_PRODUCTO)
            {
                return ValidationResult.Failure(
                    $"La cantidad máxima por producto es {CANTIDAD_MAXIMA_POR_PRODUCTO}");
            }

            return ValidationResult.Success();
        }

        /// <summary>
        /// Obtiene nombres de vendedores con cache
        /// </summary>
        private async Task<Dictionary<string, string>> ObtenerNombresVendedoresAsync(List<string> vendedorIds)
        {
            if (!vendedorIds.Any())
            {
                return new Dictionary<string, string>();
            }

            var cacheKey = $"Vendedores_{string.Join("_", vendedorIds.OrderBy(x => x))}";

            return await _cache.GetOrCreateAsync(
                cacheKey,
                async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = CACHE_DURATION_VENDEDORES;

                    var vendedores = await _userManager.Users
                        .Where(u => vendedorIds.Contains(u.Id))
                        .Select(u => new { u.Id, u.NombreCompleto, u.Email })
                        .AsNoTracking()
                        .ToListAsync();

                    return vendedores.ToDictionary(
                        k => k.Id,
                        v => string.IsNullOrWhiteSpace(v.NombreCompleto)
                            ? (v.Email ?? v.Id)
                            : v.NombreCompleto
                    );
                }) ?? new Dictionary<string, string>();
        }

        /// <summary>
        /// Obtiene usuario actual autenticado
        /// </summary>
        private async Task<Usuario?> ObtenerUsuarioActualAsync()
        {
            try
            {
                return await _userManager.GetUserAsync(User);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener usuario actual");
                return null;
            }
        }

        #endregion

        #region Resultado de Validación

        /// <summary>
        /// Resultado de una validación
        /// </summary>
        private record ValidationResult(bool IsValid, string? ErrorMessage)
        {
            public static ValidationResult Success() => new(true, null);
            public static ValidationResult Failure(string message) => new(false, message);
        }

        /// <summary>
        /// Resumen del carrito
        /// </summary>
        private record CarritoResumen(int Cantidad, decimal Total);

        #endregion

        #region Vista Principal

        /// <summary>
        /// GET: /Carrito/VerCarrito
        /// Vista principal del carrito de compras
        /// </summary>
        [HttpGet("VerCarrito", Name = "Carrito_Ver")]
        public async Task<IActionResult> VerCarrito()
        {
            try
            {
                var usuario = await ObtenerUsuarioActualAsync();
                var detalles = new List<CarritoDetalle>();

                if (usuario != null)
                {
                    var carrito = await _carritoService.GetByUsuarioIdAsync(usuario.Id);
                    if (carrito != null)
                    {
                        detalles = await _carritoService.LoadCartDetails(carrito.CarritoID);

                        _logger.LogInformation(
                            "Carrito cargado. Usuario: {UserId}, Items: {Count}, " +
                            "Total: {Total:C}",
                            usuario.Id,
                            detalles.Count,
                            detalles.Sum(d => d.Precio * d.Cantidad));
                    }
                }

                // Cupón de descuento
                var cupon = ObtenerCupon();
                ViewBag.Descuento = cupon?.Descuento ?? 0m;
                ViewBag.CodigoCupon = cupon?.CodigoCupon;

                // Información de destino
                var provincia = usuario?.Provincia;
                var ciudad = usuario?.Ciudad;
                ViewBag.DestinoProvincia = provincia;
                ViewBag.DestinoCiudad = ciudad;

                // Vendedores en el carrito
                var vendedorIds = detalles
                    .Select(c => c.Producto?.VendedorID)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Cast<string>()
                    .Distinct()
                    .ToList();

                var vendedorNombres = await ObtenerNombresVendedoresAsync(vendedorIds);
                ViewBag.VendedorNombres = vendedorNombres;

                // Cálculo de costos de envío
                var (envioTotal, envioPorVendedor, envioMensajes) = await CalcularEnviosAsync(
                    vendedorIds,
                    provincia,
                    ciudad);

                ViewBag.EnvioTotal = envioTotal;
                ViewBag.EnvioPorVendedor = envioPorVendedor;
                ViewBag.EnvioMensajes = envioMensajes;

                // Mensajes de TempData
                ViewBag.MensajeExito = TempData["MensajeExito"];
                ViewBag.MensajeError = TempData["MensajeError"];
                ViewBag.CuponAplicado = TempData["CuponAplicado"];
                ViewBag.CuponError = TempData["CuponError"];

                return View(detalles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar vista del carrito");
                TempData["MensajeError"] = "Error al cargar el carrito. Por favor, intenta nuevamente.";
                return View(new List<CarritoDetalle>());
            }
        }

        /// <summary>
        /// Calcula los costos de envío para los vendedores
        /// </summary>
        private async Task<(decimal Total, Dictionary<string, decimal> PorVendedor, List<string> Mensajes)>
            CalcularEnviosAsync(List<string> vendedorIds, string? provincia, string? ciudad)
        {
            if (string.IsNullOrWhiteSpace(provincia) || !vendedorIds.Any())
            {
                return (0m, new Dictionary<string, decimal>(), new List<string>());
            }

            try
            {
                var resultado = await _enviosCarrito.CalcularAsync(vendedorIds, provincia!, ciudad);

                _logger.LogDebug(
                    "Envíos calculados. Vendedores: {Count}, Total: {Total:C}, " +
                    "Destino: {Provincia}/{Ciudad}",
                    vendedorIds.Count,
                    resultado.TotalEnvio,
                    provincia,
                    ciudad ?? "N/A");

                return (resultado.TotalEnvio, resultado.PorVendedor, resultado.Mensajes);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error al calcular envíos. Provincia: {Provincia}, Ciudad: {Ciudad}",
                    provincia,
                    ciudad);

                return (0m, new Dictionary<string, decimal>(),
                    new List<string> { "Error al calcular costos de envío" });
            }
        }

        #endregion

        #region API - Info del Carrito

        /// <summary>
        /// GET: /Carrito/CartInfo
        /// Mini resumen para navbar/widget (AJAX)
        /// </summary>
        [HttpGet("CartInfo", Name = "Carrito_Info")]
        [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> CartInfo()
        {
            // SEGURIDAD: Este endpoint solo debe ser llamado por AJAX
            if (!EsAjax())
            {
                _logger.LogWarning("Intento de acceder a CartInfo sin AJAX");
                return BadRequest(new { error = "Este endpoint solo acepta solicitudes AJAX" });
            }

            try
            {
                var usuario = await ObtenerUsuarioActualAsync();
                if (usuario == null)
                {
                    return Json(new { count = 0, subtotal = 0m });
                }

                var carrito = await _carritoService.GetByUsuarioIdAsync(usuario.Id);
                if (carrito == null)
                {
                    return Json(new { count = 0, subtotal = 0m });
                }

                var detalles = await _carritoService.LoadCartDetails(carrito.CarritoID);
                var resumen = CalcularResumen(detalles);

                return Json(new
                {
                    count = resumen.Cantidad,
                    subtotal = resumen.Total
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener info del carrito");
                return Json(new { count = 0, subtotal = 0m });
            }
        }

        /// <summary>
        /// GET: /Carrito/CartPartial
        /// Devuelve el partial view del contenido del carrito para AJAX
        /// NUEVO: Permite actualizar el contenido del carrito sin recargar la página
        /// </summary>
        [HttpGet("CartPartial", Name = "Carrito_Partial")]
        [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> CartPartial()
        {
            try
            {
                var usuario = await ObtenerUsuarioActualAsync();
                var detalles = new List<CarritoDetalle>();

                if (usuario != null)
                {
                    var carrito = await _carritoService.GetByUsuarioIdAsync(usuario.Id);
                    if (carrito != null)
                    {
                        detalles = await _carritoService.LoadCartDetails(carrito.CarritoID);

                        _logger.LogDebug(
                            "CartPartial cargado. Usuario: {UserId}, Items: {Count}",
                            usuario.Id,
                            detalles.Count);
                    }
                }

                return PartialView("_CartPartial", detalles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar partial del carrito");
                return PartialView("_CartPartial", new List<CarritoDetalle>());
            }
        }

        #endregion

        #region Mutaciones - Agregar al Carrito

        /// <summary>
        /// POST: /Carrito/AgregarAlCarrito
        /// Agrega un producto al carrito
        /// </summary>
        [HttpPost("AgregarAlCarrito", Name = "Carrito_Agregar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AgregarAlCarrito(
            int productoId,
            int cantidad = CANTIDAD_DEFAULT,
            int? productoVarianteId = null)
        {
            // Validar cantidad
            var validacionCantidad = ValidarCantidad(cantidad);
            if (!validacionCantidad.IsValid)
            {
                return ResponderError(validacionCantidad.ErrorMessage!);
            }

            // Validar usuario autenticado
            var usuario = await ObtenerUsuarioActualAsync();
            if (usuario == null)
            {
                _logger.LogWarning("Intento de agregar al carrito sin autenticación");

                if (EsAjax())
                {
                    return Json(new { ok = false, needLogin = true, error = "Debes iniciar sesión" });
                }

                return ResponderError("Debes iniciar sesión para agregar productos al carrito.");
            }

            try
            {
                // Obtener producto
                var producto = await _context.Productos
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.ProductoID == productoId);

                if (producto == null)
                {
                    _logger.LogWarning(
                        "Intento de agregar producto inexistente. ProductoId: {ProductoId}",
                        productoId);
                    return ResponderError("Producto no encontrado.");
                }

                // Validar stock disponible
                var validacionStock = await ValidarStockDisponibleAsync(producto, cantidad, productoVarianteId);
                if (!validacionStock.IsValid)
                {
                    return ResponderError(validacionStock.ErrorMessage!);
                }

                // Agregar al carrito
                var agregado = await _carritoService.AnadirProducto(
                    producto,
                    usuario,
                    cantidad,
                    productoVarianteId);

                if (!agregado)
                {
                    _logger.LogWarning(
                        "Falló agregar producto al carrito. ProductoId: {ProductoId}, " +
                        "UsuarioId: {UsuarioId}, Cantidad: {Cantidad}",
                        productoId,
                        usuario.Id,
                        cantidad);
                    return ResponderError("No se pudo agregar el producto al carrito.");
                }

                // Obtener resumen actualizado
                var carrito = await _carritoService.GetByUsuarioIdAsync(usuario.Id);
                var detalles = carrito != null
                    ? await _carritoService.LoadCartDetails(carrito.CarritoID)
                    : new List<CarritoDetalle>();
                var resumen = CalcularResumen(detalles);

                _logger.LogInformation(
                    "Producto agregado al carrito. ProductoId: {ProductoId}, " +
                    "UsuarioId: {UsuarioId}, Cantidad: {Cantidad}, " +
                    "Total items: {TotalItems}, Total: {Total:C}",
                    productoId,
                    usuario.Id,
                    cantidad,
                    resumen.Cantidad,
                    resumen.Total);

                if (EsAjax())
                {
                    return Json(new
                    {
                        ok = true,
                        count = resumen.Cantidad,
                        total = resumen.Total,
                        message = "Producto agregado correctamente"
                    });
                }

                TempData["MensajeExito"] = "Producto agregado al carrito correctamente.";
                return RedirectToAction(nameof(VerCarrito));
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Operación inválida al agregar producto. ProductoId: {ProductoId}",
                    productoId);
                return ResponderError(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error al agregar producto al carrito. ProductoId: {ProductoId}, " +
                    "UsuarioId: {UsuarioId}",
                    productoId,
                    usuario?.Id);
                return ResponderError("Error inesperado al agregar el producto.");
            }
        }

        /// <summary>
        /// Valida que haya stock disponible para la cantidad solicitada
        /// </summary>
        private async Task<ValidationResult> ValidarStockDisponibleAsync(
            Producto producto,
            int cantidad,
            int? productoVarianteId)
        {
            // TODO: Implementar validación de stock según tu lógica de negocio
            // Por ahora, asumimos que siempre hay stock
            // En producción, deberías verificar:
            // - Stock en Producto o ProductoVariante
            // - Stock reservado en otros carritos
            // - Stock en pedidos pendientes

            await Task.CompletedTask; // Placeholder para async

            return ValidationResult.Success();
        }

        #endregion

        #region Mutaciones - Actualizar Carrito

        /// <summary>
        /// POST: /Carrito/ActualizarCarrito
        /// Actualiza la cantidad de un producto en el carrito
        /// </summary>
        [HttpPost("ActualizarCarrito", Name = "Carrito_Actualizar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarCarrito(
            int carritoDetalleId,
            int cantidad,
            int? productoId = null)
        {
            // Validar cantidad
            var validacionCantidad = ValidarCantidad(cantidad);
            if (!validacionCantidad.IsValid)
            {
                return Json(new
                {
                    ok = false,
                    error = validacionCantidad.ErrorMessage
                });
            }

            try
            {
                var (ok, lineSubtotal, error) = await _carritoService.ActualizarCantidadAsync(
                    carritoDetalleId,
                    cantidad);

                if (!ok)
                {
                    _logger.LogWarning(
                        "Falló actualizar cantidad. CarritoDetalleId: {Id}, " +
                        "Cantidad: {Cantidad}, Error: {Error}",
                        carritoDetalleId,
                        cantidad,
                        error);
                    return Json(new { ok = false, error });
                }

                // Obtener resumen actualizado
                var usuario = await ObtenerUsuarioActualAsync();
                var carrito = usuario != null
                    ? await _carritoService.GetByUsuarioIdAsync(usuario.Id)
                    : null;
                var detalles = carrito != null
                    ? await _carritoService.LoadCartDetails(carrito.CarritoID)
                    : new List<CarritoDetalle>();
                var resumen = CalcularResumen(detalles);

                _logger.LogInformation(
                    "Cantidad actualizada. CarritoDetalleId: {Id}, " +
                    "Nueva cantidad: {Cantidad}, Subtotal línea: {Subtotal:C}, " +
                    "Total carrito: {Total:C}",
                    carritoDetalleId,
                    cantidad,
                    lineSubtotal,
                    resumen.Total);

                return Json(new
                {
                    ok = true,
                    count = resumen.Cantidad,
                    total = resumen.Total,
                    lineSubtotal
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error al actualizar cantidad. CarritoDetalleId: {Id}, Cantidad: {Cantidad}",
                    carritoDetalleId,
                    cantidad);
                return Json(new
                {
                    ok = false,
                    error = "Error al actualizar la cantidad"
                });
            }
        }

        #endregion

        #region Mutaciones - Eliminar del Carrito

        /// <summary>
        /// POST: /Carrito/EliminarDelCarrito
        /// Elimina un producto del carrito
        /// </summary>
        [HttpPost("EliminarDelCarrito", Name = "Carrito_Eliminar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarDelCarrito(
            int carritoDetalleId,
            int? productoId = null)
        {
            try
            {
                var eliminado = await _carritoService.BorrarProductoCarrito(carritoDetalleId);

                if (!eliminado)
                {
                    _logger.LogWarning(
                        "Falló eliminar producto del carrito. CarritoDetalleId: {Id}",
                        carritoDetalleId);
                }

                // Obtener resumen actualizado
                var usuario = await ObtenerUsuarioActualAsync();
                var carrito = usuario != null
                    ? await _carritoService.GetByUsuarioIdAsync(usuario.Id)
                    : null;
                var detalles = carrito != null
                    ? await _carritoService.LoadCartDetails(carrito.CarritoID)
                    : new List<CarritoDetalle>();
                var resumen = CalcularResumen(detalles);

                _logger.LogInformation(
                    "Producto eliminado del carrito. CarritoDetalleId: {Id}, " +
                    "Items restantes: {Count}, Total: {Total:C}",
                    carritoDetalleId,
                    resumen.Cantidad,
                    resumen.Total);

                if (EsAjax())
                {
                    return Json(new
                    {
                        ok = eliminado,
                        count = resumen.Cantidad,
                        total = resumen.Total,
                        message = eliminado
                            ? "Producto eliminado correctamente"
                            : "No se pudo eliminar el producto"
                    });
                }

                TempData["MensajeExito"] = eliminado
                    ? "Producto eliminado del carrito."
                    : "No se pudo eliminar el producto.";

                return RedirectToAction(nameof(VerCarrito));
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error al eliminar producto del carrito. CarritoDetalleId: {Id}",
                    carritoDetalleId);

                if (EsAjax())
                {
                    return Json(new
                    {
                        ok = false,
                        error = "Error al eliminar el producto"
                    });
                }

                TempData["MensajeError"] = "Error al eliminar el producto.";
                return RedirectToAction(nameof(VerCarrito));
            }
        }

        #endregion

        #region Cupones de Descuento

        /// <summary>
        /// POST: /Carrito/AplicarCupon
        /// Aplica un cupón de descuento al carrito
        /// </summary>
        [HttpPost("AplicarCupon", Name = "Carrito_AplicarCupon")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AplicarCupon(string codigoCupon)
        {
            if (string.IsNullOrWhiteSpace(codigoCupon))
            {
                _logger.LogWarning("Intento de aplicar cupón vacío");
                return ResponderCuponInvalido("Debes ingresar un código de cupón.");
            }

            try
            {
                var cupon = await ObtenerPromocionAsync(codigoCupon.Trim());

                if (cupon != null)
                {
                    GuardarCupon(cupon);

                    _logger.LogInformation(
                        "Cupón aplicado. Código: {Codigo}, Descuento: {Descuento:C}, " +
                        "Usuario: {UsuarioId}",
                        cupon.CodigoCupon,
                        cupon.Descuento,
                        User.Identity?.Name);

                    if (EsAjax())
                    {
                        return Json(new
                        {
                            ok = true,
                            descuento = cupon.Descuento,
                            codigo = cupon.CodigoCupon,
                            message = $"Cupón '{cupon.CodigoCupon}' aplicado correctamente"
                        });
                    }

                    TempData["CuponAplicado"] = $"Cupón '{cupon.CodigoCupon}' aplicado: {cupon.Descuento:C}.";
                    return RedirectToAction(nameof(VerCarrito));
                }
                else
                {
                    _logger.LogWarning(
                        "Intento de aplicar cupón inválido o expirado. Código: {Codigo}",
                        codigoCupon);

                    EliminarCupon();
                    return ResponderCuponInvalido("El cupón ingresado no es válido o ha expirado.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al aplicar cupón. Código: {Codigo}", codigoCupon);
                return ResponderCuponInvalido("Error al validar el cupón.");
            }
        }

        /// <summary>
        /// POST: /Carrito/QuitarCupon
        /// Elimina el cupón de descuento aplicado
        /// </summary>
        [HttpPost("QuitarCupon", Name = "Carrito_QuitarCupon")]
        [ValidateAntiForgeryToken]
        public IActionResult QuitarCupon()
        {
            try
            {
                EliminarCupon();

                _logger.LogInformation(
                    "Cupón eliminado. Usuario: {UsuarioId}",
                    User.Identity?.Name);

                if (EsAjax())
                {
                    return Json(new
                    {
                        ok = true,
                        message = "Cupón eliminado correctamente"
                    });
                }

                TempData["MensajeExito"] = "Cupón eliminado correctamente.";
                return RedirectToAction(nameof(VerCarrito));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al quitar cupón");

                if (EsAjax())
                {
                    return Json(new { ok = false, error = "Error al eliminar cupón" });
                }

                TempData["MensajeError"] = "Error al eliminar el cupón.";
                return RedirectToAction(nameof(VerCarrito));
            }
        }

        /// <summary>
        /// Obtiene una promoción activa por código con cache
        /// </summary>
        private async Task<Promocion?> ObtenerPromocionAsync(string codigoCupon)
        {
            var ahora = DateTime.UtcNow;

            // Intentar obtener de cache
            var promocionesActivas = await _cache.GetOrCreateAsync(
                CACHE_KEY_PROMOCIONES,
                async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = CACHE_DURATION_PROMOCIONES;

                    return await _context.Promociones
                        .Where(p =>
                            (p.FechaInicio == null || p.FechaInicio <= ahora) &&
                            (p.FechaFin == null || p.FechaFin >= ahora))
                        .AsNoTracking()
                        .ToListAsync();
                }) ?? new List<Promocion>();

            return promocionesActivas.FirstOrDefault(p =>
                string.Equals(p.CodigoCupon, codigoCupon, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Helper para responder cupón inválido
        /// </summary>
        private IActionResult ResponderCuponInvalido(string mensaje)
        {
            if (EsAjax())
            {
                return Json(new { ok = false, error = mensaje });
            }

            TempData["CuponError"] = mensaje;
            return RedirectToAction(nameof(VerCarrito));
        }

        #endregion

        #region Checkout - Confirmar Compra

        /// <summary>
        /// POST: /Carrito/ConfirmarCompra
        /// Procesa la compra del carrito
        /// </summary>
        [HttpPost("ConfirmarCompra", Name = "Carrito_ConfirmarCompra")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmarCompra(
            string? EnvioProvincia,
            string? EnvioCiudad,
            decimal? EnvioPrecio)
        {
            // Validar usuario autenticado
            var usuario = await ObtenerUsuarioActualAsync();
            if (usuario == null)
            {
                _logger.LogWarning("Intento de confirmar compra sin autenticación");
                TempData["MensajeError"] = "Debes iniciar sesión para confirmar tu compra.";
                return RedirectToAction(nameof(VerCarrito));
            }

            try
            {
                // Obtener carrito
                var carrito = await _carritoService.GetByUsuarioIdAsync(usuario.Id);
                if (carrito == null)
                {
                    _logger.LogWarning(
                        "Intento de confirmar compra sin carrito. UsuarioId: {UsuarioId}",
                        usuario.Id);
                    TempData["MensajeError"] = "Tu carrito está vacío.";
                    return RedirectToAction(nameof(VerCarrito));
                }

                // Validar que tenga productos
                var detalles = await _carritoService.LoadCartDetails(carrito.CarritoID);
                if (!detalles.Any())
                {
                    _logger.LogWarning(
                        "Intento de confirmar compra con carrito vacío. UsuarioId: {UsuarioId}",
                        usuario.Id);
                    TempData["MensajeError"] = "Tu carrito está vacío.";
                    return RedirectToAction(nameof(VerCarrito));
                }

                var totalCompra = detalles.Sum(d => d.Precio * d.Cantidad);

                _logger.LogInformation(
                    "Iniciando procesamiento de compra. UsuarioId: {UsuarioId}, " +
                    "Items: {Items}, Total: {Total:C}",
                    usuario.Id,
                    detalles.Count,
                    totalCompra);

                // Procesar carrito (crear venta, actualizar stock, etc.)
                var procesado = await _carritoService.ProcessCartDetails(carrito.CarritoID, usuario);

                if (!procesado)
                {
                    _logger.LogError(
                        "Falló procesar carrito. UsuarioId: {UsuarioId}, CarritoId: {CarritoId}",
                        usuario.Id,
                        carrito.CarritoID);
                    TempData["MensajeError"] = "No se pudo procesar la compra. Por favor, intenta nuevamente.";
                    return RedirectToAction(nameof(VerCarrito));
                }

                // Eliminar cupón de la sesión
                EliminarCupon();

                // Obtener la venta recién creada
                var ventaId = await _context.Ventas
                    .Where(v => v.UsuarioId == usuario.Id)
                    .OrderByDescending(v => v.FechaVenta)
                    .Select(v => v.VentaID)
                    .FirstOrDefaultAsync();

                _logger.LogInformation(
                    "Compra procesada exitosamente. UsuarioId: {UsuarioId}, " +
                    "VentaId: {VentaId}, Total: {Total:C}",
                    usuario.Id,
                    ventaId,
                    totalCompra);

                TempData["MensajeExito"] = "¡Gracias por tu compra! Tu pedido ha sido procesado exitosamente.";
                return RedirectToAction(nameof(ConfirmacionCompra), new { id = ventaId });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Operación inválida al confirmar compra. UsuarioId: {UsuarioId}",
                    usuario.Id);
                TempData["MensajeError"] = ex.Message;
                return RedirectToAction(nameof(VerCarrito));
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error al confirmar compra. UsuarioId: {UsuarioId}",
                    usuario.Id);
                TempData["MensajeError"] = "Error inesperado al procesar la compra. Por favor, contacta a soporte.";
                return RedirectToAction(nameof(VerCarrito));
            }
        }

        /// <summary>
        /// GET: /Carrito/ConfirmacionCompra/{id}
        /// Vista de confirmación de compra
        /// </summary>
        [HttpGet("ConfirmacionCompra/{id:int}", Name = "Carrito_Confirmacion")]
        public async Task<IActionResult> ConfirmacionCompra(int id)
        {
            try
            {
                // Validar que la venta exista y pertenezca al usuario
                var usuario = await ObtenerUsuarioActualAsync();
                if (usuario == null)
                {
                    return RedirectToAction("Index", "Home");
                }

                var ventaExiste = await _context.Ventas
                    .AnyAsync(v => v.VentaID == id && v.UsuarioId == usuario.Id);

                if (!ventaExiste)
                {
                    _logger.LogWarning(
                        "Intento de acceso a venta inexistente o no autorizada. " +
                        "VentaId: {VentaId}, UsuarioId: {UsuarioId}",
                        id,
                        usuario.Id);
                    return RedirectToAction("Index", "Home");
                }

                _logger.LogInformation(
                    "Vista de confirmación cargada. VentaId: {VentaId}, UsuarioId: {UsuarioId}",
                    id,
                    usuario.Id);

                return View(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar confirmación de compra. VentaId: {VentaId}", id);
                return RedirectToAction("Index", "Home");
            }
        }

        #endregion

        #region Helpers de Respuesta

        /// <summary>
        /// Helper para responder errores de forma consistente
        /// </summary>
        private IActionResult ResponderError(string mensaje)
        {
            if (EsAjax())
            {
                return Json(new { ok = false, error = mensaje });
            }

            TempData["MensajeError"] = mensaje;
            return RedirectToAction(nameof(VerCarrito));
        }

        #endregion
    }
}
