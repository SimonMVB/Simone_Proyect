using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Simone.Data;
using Simone.Models;

namespace Simone.Services
{
    #region Interface

    /// <summary>
    /// Servicio de gestión de carritos de compra con operaciones thread-safe
    /// Maneja toda la lógica de negocio relacionada con carritos y sus detalles
    /// Versión optimizada con mejores prácticas empresariales
    /// </summary>
    public interface ICarritoService
    {
        #region CRUD Básico

        /// <summary>
        /// Crea un nuevo carrito para un usuario
        /// </summary>
        /// <param name="usuario">Usuario propietario del carrito</param>
        /// <param name="ct">Token de cancelación</param>
        Task<bool> AddAsync(Usuario usuario, CancellationToken ct = default);

        /// <summary>
        /// Obtiene todos los carritos del sistema
        /// </summary>
        /// <param name="ct">Token de cancelación</param>
        Task<List<Carrito>> GetAllAsync(CancellationToken ct = default);

        /// <summary>
        /// Obtiene un carrito por su ID
        /// </summary>
        /// <param name="id">ID del carrito</param>
        /// <param name="ct">Token de cancelación</param>
        Task<Carrito> GetByIdAsync(int id, CancellationToken ct = default);

        /// <summary>
        /// Actualiza un carrito existente
        /// </summary>
        /// <param name="carrito">Carrito a actualizar</param>
        /// <param name="ct">Token de cancelación</param>
        Task<bool> UpdateAsync(Carrito carrito, CancellationToken ct = default);

        /// <summary>
        /// Elimina un carrito y sus detalles
        /// </summary>
        /// <param name="id">ID del carrito</param>
        /// <param name="ct">Token de cancelación</param>
        Task<bool> DeleteAsync(int id, CancellationToken ct = default);

        #endregion

        #region Consultas

        /// <summary>
        /// Obtiene el carrito activo de un usuario
        /// </summary>
        /// <param name="usuarioId">ID del usuario</param>
        /// <param name="ct">Token de cancelación</param>
        Task<Carrito> GetByUsuarioIdAsync(string usuarioId, CancellationToken ct = default);

        /// <summary>
        /// Obtiene el carrito activo de un cliente (alias de GetByUsuarioIdAsync)
        /// </summary>
        /// <param name="clienteID">ID del cliente</param>
        /// <param name="ct">Token de cancelación</param>
        Task<Carrito> GetByClienteIdAsync(string clienteID, CancellationToken ct = default);

        /// <summary>
        /// Carga los detalles de un carrito con productos y variantes
        /// </summary>
        /// <param name="carritoID">ID del carrito</param>
        /// <param name="ct">Token de cancelación</param>
        Task<List<CarritoDetalle>> LoadCartDetails(int carritoID, CancellationToken ct = default);

        /// <summary>
        /// Obtiene el total de items en un carrito
        /// </summary>
        /// <param name="carritoID">ID del carrito</param>
        /// <param name="ct">Token de cancelación</param>
        Task<int> GetCartItemCountAsync(int carritoID, CancellationToken ct = default);

        /// <summary>
        /// Calcula el total del carrito
        /// </summary>
        /// <param name="carritoID">ID del carrito</param>
        /// <param name="ct">Token de cancelación</param>
        Task<decimal> GetCartTotalAsync(int carritoID, CancellationToken ct = default);

        #endregion

        #region Mutaciones - Productos

        /// <summary>
        /// Añade un producto al carrito (sin variante)
        /// </summary>
        /// <param name="producto">Producto a añadir</param>
        /// <param name="usuario">Usuario propietario del carrito</param>
        /// <param name="cantidad">Cantidad a añadir</param>
        /// <param name="ct">Token de cancelación</param>
        Task<bool> AnadirProducto(Producto producto, Usuario usuario, int cantidad, CancellationToken ct = default);

        /// <summary>
        /// Añade un producto al carrito (con variante opcional)
        /// </summary>
        /// <param name="producto">Producto a añadir</param>
        /// <param name="usuario">Usuario propietario del carrito</param>
        /// <param name="cantidad">Cantidad a añadir</param>
        /// <param name="productoVarianteId">ID de la variante (opcional)</param>
        /// <param name="ct">Token de cancelación</param>
        Task<bool> AnadirProducto(Producto producto, Usuario usuario, int cantidad, int? productoVarianteId, CancellationToken ct = default);

        /// <summary>
        /// Elimina un producto del carrito
        /// </summary>
        /// <param name="detalleId">ID del detalle a eliminar</param>
        /// <param name="ct">Token de cancelación</param>
        Task<bool> BorrarProductoCarrito(int detalleId, CancellationToken ct = default);

        /// <summary>
        /// Actualiza la cantidad de un producto en el carrito
        /// </summary>
        /// <param name="carritoDetalleId">ID del detalle del carrito</param>
        /// <param name="cantidad">Nueva cantidad</param>
        /// <param name="ct">Token de cancelación</param>
        /// <returns>Tupla con resultado, subtotal de línea y mensaje de error</returns>
        Task<(bool ok, decimal lineSubtotal, string? error)> ActualizarCantidadAsync(
            int carritoDetalleId,
            int cantidad,
            CancellationToken ct = default);

        /// <summary>
        /// Vacía completamente un carrito
        /// </summary>
        /// <param name="carritoID">ID del carrito</param>
        /// <param name="ct">Token de cancelación</param>
        Task<bool> VaciarCarritoAsync(int carritoID, CancellationToken ct = default);

        #endregion

        #region Procesamiento

        /// <summary>
        /// Procesa el carrito creando una venta y actualizando inventario
        /// </summary>
        /// <param name="carritoID">ID del carrito a procesar</param>
        /// <param name="user">Usuario que realiza la compra</param>
        /// <param name="ct">Token de cancelación</param>
        Task<bool> ProcessCartDetails(int carritoID, Usuario user, CancellationToken ct = default);

        /// <summary>
        /// Valida que un carrito esté listo para procesar
        /// </summary>
        /// <param name="carritoID">ID del carrito</param>
        /// <param name="ct">Token de cancelación</param>
        /// <returns>Tupla con resultado y mensaje de error si hay</returns>
        Task<(bool isValid, string? errorMessage)> ValidateCartForCheckoutAsync(
            int carritoID,
            CancellationToken ct = default);

        #endregion
    }

    #endregion

    #region Implementation

    /// <summary>
    /// Implementación del servicio de carritos con operaciones atómicas y thread-safe
    /// </summary>
    public sealed class CarritoService : ICarritoService
    {
        #region Dependencias

        private readonly TiendaDbContext _context;
        private readonly ILogger<CarritoService> _logger;
        private readonly UserManager<Usuario> _userManager;

        #endregion

        #region Constantes - Configuración

        private const string ADMIN_EMAIL = "admin@tienda.com";
        private const string ESTADO_CERRADO = "Cerrado";
        private const string ESTADO_EN_USO = "En Uso";
        private const string ESTADO_VACIO = "Vacio";
        private const string TIPO_MOVIMIENTO_SALIDA = "Salida";
        private const string METODO_PAGO_DEFAULT = "Transferencia";
        private const string ESTADO_VENTA_COMPLETADA = "Completada";

        #endregion

        #region Constantes - Mensajes de Log

        // Información
        private const string LOG_INFO_CARRITO_CREADO = "Carrito creado. CarritoId: {CarritoId}, UsuarioId: {UsuarioId}";
        private const string LOG_INFO_CARRITO_ACTUALIZADO = "Carrito actualizado. CarritoId: {CarritoId}";
        private const string LOG_INFO_CARRITO_ELIMINADO = "Carrito eliminado. CarritoId: {CarritoId}, Detalles: {DetallesCount}";
        private const string LOG_INFO_PRODUCTO_ANADIDO = "Producto añadido al carrito. ProductoId: {ProductoId}, CarritoId: {CarritoId}, Cantidad: {Cantidad}, VarianteId: {VarianteId}";
        private const string LOG_INFO_DETALLE_ELIMINADO = "Detalle eliminado del carrito. DetalleId: {DetalleId}, CarritoId: {CarritoId}";
        private const string LOG_INFO_CANTIDAD_ACTUALIZADA = "Cantidad actualizada. DetalleId: {DetalleId}, CantidadAnterior: {CantidadAnterior}, CantidadNueva: {CantidadNueva}";
        private const string LOG_INFO_CARRITO_PROCESADO = "Carrito procesado exitosamente. CarritoId: {CarritoId}, VentaId: {VentaId}, Total: {Total:C}, Items: {ItemsCount}";
        private const string LOG_INFO_STOCK_DESCONTADO = "Stock descontado. ProductoId: {ProductoId}, VarianteId: {VarianteId}, Cantidad: {Cantidad}, StockRestante: {StockRestante}";
        private const string LOG_INFO_CARRITO_VACIADO = "Carrito vaciado. CarritoId: {CarritoId}, ItemsEliminados: {ItemsCount}";

        // Debug
        private const string LOG_DEBUG_CARRITO_ENCONTRADO = "Carrito encontrado. CarritoId: {CarritoId}, Estado: {Estado}";
        private const string LOG_DEBUG_CARRITO_NO_ENCONTRADO = "No se encontró carrito activo. UsuarioId: {UsuarioId}, creando nuevo carrito";
        private const string LOG_DEBUG_DETALLE_NUEVO = "Creando nuevo detalle en carrito. CarritoId: {CarritoId}, ProductoId: {ProductoId}";
        private const string LOG_DEBUG_DETALLE_EXISTENTE = "Actualizando detalle existente. DetalleId: {DetalleId}, CantidadAnterior: {CantidadAnterior}";
        private const string LOG_DEBUG_CARRITO_MARCADO_VACIO = "Carrito marcado como vacío. CarritoId: {CarritoId}";
        private const string LOG_DEBUG_CARRITO_MARCADO_EN_USO = "Carrito marcado como 'En Uso'. CarritoId: {CarritoId}";
        private const string LOG_DEBUG_ELIMINANDO_DETALLES = "Eliminando detalles del carrito. CarritoId: {CarritoId}, Count: {Count}";
        private const string LOG_DEBUG_VALIDACION_STOCK = "Validando stock. ProductoId: {ProductoId}, VarianteId: {VarianteId}, Disponible: {Disponible}, Solicitado: {Solicitado}";
        private const string LOG_DEBUG_PRECIO_CALCULADO = "Precio calculado. ProductoId: {ProductoId}, VarianteId: {VarianteId}, Precio: {Precio:C}";
        private const string LOG_DEBUG_TRANSACCION_INICIADA = "Transacción iniciada. Operación: {Operacion}";
        private const string LOG_DEBUG_TRANSACCION_COMMIT = "Transacción confirmada. Operación: {Operacion}";
        private const string LOG_DEBUG_TRANSACCION_ROLLBACK = "Transacción revertida. Operación: {Operacion}";

        // Advertencias
        private const string LOG_WARN_CARRITO_NO_ENCONTRADO = "Carrito no encontrado. CarritoId: {CarritoId}";
        private const string LOG_WARN_DETALLE_NO_ENCONTRADO = "Detalle no encontrado. DetalleId: {DetalleId}";
        private const string LOG_WARN_CARRITO_VACIO = "Carrito vacío. CarritoId: {CarritoId}";
        private const string LOG_WARN_STOCK_INSUFICIENTE = "Stock insuficiente. ProductoId: {ProductoId}, VarianteId: {VarianteId}, Disponible: {Disponible}, Solicitado: {Solicitado}";
        private const string LOG_WARN_VARIANTE_REQUERIDA = "Producto requiere variante. ProductoId: {ProductoId}, Nombre: {Nombre}";
        private const string LOG_WARN_VARIANTE_INVALIDA = "Variante inválida para producto. VarianteId: {VarianteId}, ProductoId: {ProductoId}";
        private const string LOG_WARN_USUARIO_INACTIVO = "Usuario inactivo intentando procesar carrito. UsuarioId: {UsuarioId}";
        private const string LOG_WARN_ADMIN_NO_ENCONTRADO = "Usuario administrador no encontrado. Email: {Email}";
        private const string LOG_WARN_PRODUCTO_INEXISTENTE = "Producto inexistente en detalle. ProductoId: {ProductoId}, DetalleId: {DetalleId}";

        // Errores
        private const string LOG_ERROR_CREAR_CARRITO = "Error al crear carrito. UsuarioId: {UsuarioId}";
        private const string LOG_ERROR_ACTUALIZAR_CARRITO = "Error al actualizar carrito. CarritoId: {CarritoId}";
        private const string LOG_ERROR_ELIMINAR_CARRITO = "Error al eliminar carrito. CarritoId: {CarritoId}";
        private const string LOG_ERROR_ANADIR_PRODUCTO = "Error al añadir producto. ProductoId: {ProductoId}, UsuarioId: {UsuarioId}";
        private const string LOG_ERROR_ELIMINAR_DETALLE = "Error al eliminar detalle. DetalleId: {DetalleId}";
        private const string LOG_ERROR_ACTUALIZAR_CANTIDAD = "Error al actualizar cantidad. DetalleId: {DetalleId}, Cantidad: {Cantidad}";
        private const string LOG_ERROR_PROCESAR_CARRITO = "Error al procesar carrito. CarritoId: {CarritoId}";
        private const string LOG_ERROR_RELOAD_PRODUCTO = "Error al recargar producto. ProductoId: {ProductoId}";
        private const string LOG_ERROR_CREAR_VENTA = "Error al crear venta desde carrito. CarritoId: {CarritoId}";
        private const string LOG_ERROR_DB_UPDATE = "DbUpdateException en operación de carrito. Operación: {Operacion}";
        private const string LOG_ERROR_VACIAR_CARRITO = "Error al vaciar carrito. CarritoId: {CarritoId}";

        #endregion

        #region Constantes - Mensajes de Error

        private const string ERR_CANTIDAD_INVALIDA = "La cantidad debe ser mayor que cero";
        private const string ERR_PRODUCTO_NULL = "El producto no puede ser nulo";
        private const string ERR_USUARIO_NULL = "El usuario no puede ser nulo";
        private const string ERR_CARRITO_NULL = "El carrito no puede ser nulo";
        private const string ERR_USUARIO_ID_VACIO = "El ID de usuario no puede estar vacío";
        private const string ERR_USUARIO_INACTIVO = "Tu cuenta está desactivada";
        private const string ERR_PRODUCTO_INEXISTENTE = "El producto no existe";
        private const string ERR_VARIANTE_NO_CORRESPONDE = "La variante seleccionada no corresponde a este producto";
        private const string ERR_VARIANTE_REQUERIDA = "Debes seleccionar Color y Talla para este producto";
        private const string ERR_STOCK_INSUFICIENTE_VARIANTE = "No hay stock suficiente para la combinación seleccionada. Disponible: {0}, solicitado: {1}";
        private const string ERR_STOCK_INSUFICIENTE_PRODUCTO = "No hay stock suficiente de '{0}'. Disponible: {1}, solicitado: {2}";
        private const string ERR_DETALLE_NO_ENCONTRADO = "No se encontró el ítem del carrito";
        private const string ERR_PRODUCTO_EN_DETALLE_INEXISTENTE = "Producto inexistente en el carrito";
        private const string ERR_PRODUCTO_REQUIERE_VARIANTE = "El producto '{0}' requiere Color/Talla. Elimina y vuelve a agregar con variante";
        private const string ERR_STOCK_INSUFICIENTE_DETALLE_VARIANTE = "Stock insuficiente para la combinación seleccionada. Disponible: {0}";
        private const string ERR_STOCK_INSUFICIENTE_DETALLE_PRODUCTO = "Stock insuficiente. Disponible: {0}";
        private const string ERR_ACTUALIZAR_CANTIDAD_FALLIDO = "No se pudo actualizar la cantidad";
        private const string ERR_CARRITO_SIN_PRODUCTOS = "El carrito {0} no tiene productos";
        private const string ERR_VARIANTE_INVALIDA_DETALLE = "La variante del carrito no corresponde al producto";
        private const string ERR_STOCK_INSUFICIENTE_PROCESO_VARIANTE = "Stock insuficiente para la combinación seleccionada de '{0}': disponible {1}, solicitado {2}";
        private const string ERR_VARIANTE_REQUERIDA_PROCESO = "El producto '{0}' requiere Color/Talla. Elimina el ítem y vuelve a agregarlo seleccionando una variante";
        private const string ERR_STOCK_INSUFICIENTE_PROCESO_PRODUCTO = "Stock insuficiente para '{0}': disponible {1}, solicitado {2}";
        private const string ERR_ADMIN_NO_ENCONTRADO = "No se encontró el usuario administrador '{0}'";

        #endregion

        #region Constructor

        /// <summary>
        /// Constructor del servicio de carritos
        /// </summary>
        /// <param name="context">Contexto de base de datos</param>
        /// <param name="logger">Logger para registro de eventos</param>
        /// <param name="userManager">Gestor de usuarios</param>
        /// <exception cref="ArgumentNullException">Si alguna dependencia es null</exception>
        public CarritoService(
            TiendaDbContext context,
            ILogger<CarritoService> logger,
            UserManager<Usuario> userManager)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        }

        #endregion

        #region Helpers - Validación de Argumentos

        /// <summary>
        /// Valida que el usuario no sea nulo
        /// </summary>
        private static void ValidateUsuario(Usuario usuario)
        {
            if (usuario == null)
            {
                throw new ArgumentNullException(nameof(usuario), ERR_USUARIO_NULL);
            }
        }

        /// <summary>
        /// Valida que el producto no sea nulo
        /// </summary>
        private static void ValidateProducto(Producto producto)
        {
            if (producto == null)
            {
                throw new ArgumentNullException(nameof(producto), ERR_PRODUCTO_NULL);
            }
        }

        /// <summary>
        /// Valida que el carrito no sea nulo
        /// </summary>
        private static void ValidateCarrito(Carrito carrito)
        {
            if (carrito == null)
            {
                throw new ArgumentNullException(nameof(carrito), ERR_CARRITO_NULL);
            }
        }

        /// <summary>
        /// Valida que la cantidad sea mayor que cero
        /// </summary>
        private static void ValidateCantidad(int cantidad)
        {
            if (cantidad <= 0)
            {
                throw new ArgumentException(ERR_CANTIDAD_INVALIDA, nameof(cantidad));
            }
        }

        /// <summary>
        /// Valida que el ID de usuario no esté vacío
        /// </summary>
        private static void ValidateUsuarioId(string usuarioId)
        {
            if (string.IsNullOrWhiteSpace(usuarioId))
            {
                throw new ArgumentException(ERR_USUARIO_ID_VACIO, nameof(usuarioId));
            }
        }

        /// <summary>
        /// Valida que el usuario esté activo
        /// </summary>
        private void ValidateUsuarioActivo(Usuario usuario)
        {
            ValidateUsuario(usuario);

            if (!usuario.Activo)
            {
                _logger.LogWarning(LOG_WARN_USUARIO_INACTIVO, usuario.Id);
                throw new InvalidOperationException(ERR_USUARIO_INACTIVO);
            }
        }

        #endregion

        #region Helpers - Obtención de Entidades

        /// <summary>
        /// Obtiene o crea un carrito abierto para un usuario
        /// </summary>
        private async Task<Carrito> GetOrCreateOpenCartAsync(Usuario usuario, CancellationToken ct = default)
        {
            ValidateUsuario(usuario);

            var current = await _context.Carrito
                .FirstOrDefaultAsync(c => c.UsuarioId == usuario.Id && c.EstadoCarrito != ESTADO_CERRADO, ct)
                .ConfigureAwait(false);

            if (current != null)
            {
                _logger.LogDebug(LOG_DEBUG_CARRITO_ENCONTRADO, current.CarritoID, current.EstadoCarrito);
                return current;
            }

            _logger.LogDebug(LOG_DEBUG_CARRITO_NO_ENCONTRADO, usuario.Id);

            await using var trx = await _context.Database
                .BeginTransactionAsync(IsolationLevel.Serializable, ct)
                .ConfigureAwait(false);

            try
            {
                _logger.LogDebug(LOG_DEBUG_TRANSACCION_INICIADA, "GetOrCreateOpenCart");

                // Double-check en transacción
                var again = await _context.Carrito
                    .FirstOrDefaultAsync(c => c.UsuarioId == usuario.Id && c.EstadoCarrito != ESTADO_CERRADO, ct)
                    .ConfigureAwait(false);

                if (again != null)
                {
                    await trx.CommitAsync(ct).ConfigureAwait(false);
                    _logger.LogDebug(LOG_DEBUG_TRANSACCION_COMMIT, "GetOrCreateOpenCart");
                    return again;
                }

                var nuevo = new Carrito
                {
                    UsuarioId = usuario.Id,
                    FechaCreacion = DateTime.UtcNow,
                    EstadoCarrito = ESTADO_VACIO
                };

                await _context.Carrito.AddAsync(nuevo, ct).ConfigureAwait(false);
                await _context.SaveChangesAsync(ct).ConfigureAwait(false);
                await trx.CommitAsync(ct).ConfigureAwait(false);

                _logger.LogInformation(LOG_INFO_CARRITO_CREADO, nuevo.CarritoID, usuario.Id);
                _logger.LogDebug(LOG_DEBUG_TRANSACCION_COMMIT, "GetOrCreateOpenCart");

                return nuevo;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await trx.RollbackAsync(ct).ConfigureAwait(false);
                _logger.LogDebug(LOG_DEBUG_TRANSACCION_ROLLBACK, "GetOrCreateOpenCart");
                _logger.LogError(ex, LOG_ERROR_CREAR_CARRITO, usuario.Id);
                throw;
            }
        }

        /// <summary>
        /// Recarga un producto desde la base de datos
        /// </summary>
        private async Task<Producto> ReloadProductoAsync(int productoId, CancellationToken ct = default)
        {
            var p = await _context.Productos
                .FirstOrDefaultAsync(x => x.ProductoID == productoId, ct)
                .ConfigureAwait(false);

            if (p == null)
            {
                _logger.LogError(LOG_ERROR_RELOAD_PRODUCTO, productoId);
                throw new InvalidOperationException(ERR_PRODUCTO_INEXISTENTE);
            }

            return p;
        }

        /// <summary>
        /// Recarga un producto con sus variantes desde la base de datos
        /// </summary>
        private async Task<Producto> ReloadProductoConVariantesAsync(int productoId, CancellationToken ct = default)
        {
            var p = await _context.Productos
                .Include(x => x.Variantes)
                .FirstOrDefaultAsync(x => x.ProductoID == productoId, ct)
                .ConfigureAwait(false);

            if (p == null)
            {
                _logger.LogError(LOG_ERROR_RELOAD_PRODUCTO, productoId);
                throw new InvalidOperationException(ERR_PRODUCTO_INEXISTENTE);
            }

            return p;
        }

        /// <summary>
        /// Obtiene el usuario administrador
        /// </summary>
        private async Task<Usuario> GetAdminUsuarioAsync(CancellationToken ct = default)
        {
            var admin = await _userManager.Users
                .FirstOrDefaultAsync(u => u.Email == ADMIN_EMAIL, ct)
                .ConfigureAwait(false);

            if (admin == null)
            {
                _logger.LogWarning(LOG_WARN_ADMIN_NO_ENCONTRADO, ADMIN_EMAIL);
                throw new InvalidOperationException(string.Format(ERR_ADMIN_NO_ENCONTRADO, ADMIN_EMAIL));
            }

            return admin;
        }

        #endregion

        #region Helpers - Validación de Stock

        /// <summary>
        /// Valida el stock disponible para una variante
        /// </summary>
        private void ValidateStockVariante(ProductoVariante variante, Producto producto, int cantidadRequerida)
        {
            _logger.LogDebug(LOG_DEBUG_VALIDACION_STOCK,
                producto.ProductoID, variante.ProductoVarianteID, variante.Stock, cantidadRequerida);

            if (variante.Stock < cantidadRequerida)
            {
                _logger.LogWarning(LOG_WARN_STOCK_INSUFICIENTE,
                    producto.ProductoID, variante.ProductoVarianteID, variante.Stock, cantidadRequerida);

                throw new InvalidOperationException(
                    string.Format(ERR_STOCK_INSUFICIENTE_VARIANTE, variante.Stock, cantidadRequerida));
            }
        }

        /// <summary>
        /// Valida el stock disponible para un producto sin variante
        /// </summary>
        private void ValidateStockProducto(Producto producto, int cantidadRequerida)
        {
            _logger.LogDebug(LOG_DEBUG_VALIDACION_STOCK,
                producto.ProductoID, null, producto.Stock, cantidadRequerida);

            if (producto.Stock < cantidadRequerida)
            {
                _logger.LogWarning(LOG_WARN_STOCK_INSUFICIENTE,
                    producto.ProductoID, null, producto.Stock, cantidadRequerida);

                throw new InvalidOperationException(
                    string.Format(ERR_STOCK_INSUFICIENTE_PRODUCTO, producto.Nombre, producto.Stock, cantidadRequerida));
            }
        }

        #endregion

        #region Helpers - Cálculo de Precios

        /// <summary>
        /// Obtiene el precio de venta para un producto o variante
        /// </summary>
        private decimal GetPrecioVenta(Producto producto, ProductoVariante? variante)
        {
            var precio = variante?.PrecioVenta ?? producto.PrecioVenta;

            _logger.LogDebug(LOG_DEBUG_PRECIO_CALCULADO,
                producto.ProductoID, variante?.ProductoVarianteID, precio);

            return precio;
        }

        #endregion

        #region CRUD Básico

        /// <inheritdoc />
        public async Task<bool> AddAsync(Usuario usuario, CancellationToken ct = default)
        {
            ValidateUsuario(usuario);

            try
            {
                _logger.LogInformation("Creando carrito para usuario. UsuarioId: {UsuarioId}", usuario.Id);

                var carrito = new Carrito
                {
                    UsuarioId = usuario.Id,
                    FechaCreacion = DateTime.UtcNow,
                    EstadoCarrito = ESTADO_VACIO
                };

                await _context.Carrito.AddAsync(carrito, ct).ConfigureAwait(false);
                await _context.SaveChangesAsync(ct).ConfigureAwait(false);

                _logger.LogInformation(LOG_INFO_CARRITO_CREADO, carrito.CarritoID, usuario.Id);
                return true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_CREAR_CARRITO, usuario.Id);
                return false;
            }
        }

        /// <inheritdoc />
        public Task<List<Carrito>> GetAllAsync(CancellationToken ct = default)
        {
            return _context.Carrito
                .AsNoTracking()
                .ToListAsync(ct);
        }

        /// <inheritdoc />
        public Task<Carrito> GetByIdAsync(int id, CancellationToken ct = default)
        {
            return _context.Carrito
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CarritoID == id, ct);
        }

        /// <inheritdoc />
        public async Task<bool> UpdateAsync(Carrito carrito, CancellationToken ct = default)
        {
            ValidateCarrito(carrito);

            try
            {
                _logger.LogInformation("Actualizando carrito. CarritoId: {CarritoId}", carrito.CarritoID);

                _context.Carrito.Update(carrito);
                await _context.SaveChangesAsync(ct).ConfigureAwait(false);

                _logger.LogInformation(LOG_INFO_CARRITO_ACTUALIZADO, carrito.CarritoID);
                return true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_ACTUALIZAR_CARRITO, carrito.CarritoID);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
        {
            await using var transaction = await _context.Database
                .BeginTransactionAsync(ct)
                .ConfigureAwait(false);

            try
            {
                _logger.LogInformation("Eliminando carrito. CarritoId: {CarritoId}", id);
                _logger.LogDebug(LOG_DEBUG_TRANSACCION_INICIADA, "DeleteCarrito");

                var carrito = await _context.Carrito
                    .Include(c => c.CarritoDetalles)
                    .FirstOrDefaultAsync(c => c.CarritoID == id, ct)
                    .ConfigureAwait(false);

                if (carrito == null)
                {
                    _logger.LogWarning(LOG_WARN_CARRITO_NO_ENCONTRADO, id);
                    return false;
                }

                var detallesCount = carrito.CarritoDetalles?.Count ?? 0;

                if (detallesCount > 0)
                {
                    _logger.LogDebug(LOG_DEBUG_ELIMINANDO_DETALLES, id, detallesCount);
                    _context.CarritoDetalle.RemoveRange(carrito.CarritoDetalles);
                }

                _context.Carrito.Remove(carrito);
                await _context.SaveChangesAsync(ct).ConfigureAwait(false);
                await transaction.CommitAsync(ct).ConfigureAwait(false);

                _logger.LogInformation(LOG_INFO_CARRITO_ELIMINADO, id, detallesCount);
                _logger.LogDebug(LOG_DEBUG_TRANSACCION_COMMIT, "DeleteCarrito");

                return true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await transaction.RollbackAsync(ct).ConfigureAwait(false);
                _logger.LogDebug(LOG_DEBUG_TRANSACCION_ROLLBACK, "DeleteCarrito");
                _logger.LogError(ex, LOG_ERROR_ELIMINAR_CARRITO, id);
                return false;
            }
        }

        #endregion

        #region Consultas

        /// <inheritdoc />
        public Task<Carrito> GetByUsuarioIdAsync(string usuarioId, CancellationToken ct = default)
        {
            ValidateUsuarioId(usuarioId);

            return _context.Carrito
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.UsuarioId == usuarioId && c.EstadoCarrito != ESTADO_CERRADO, ct);
        }

        /// <inheritdoc />
        public Task<Carrito> GetByClienteIdAsync(string clienteID, CancellationToken ct = default)
        {
            return GetByUsuarioIdAsync(clienteID, ct);
        }

        /// <inheritdoc />
        public Task<List<CarritoDetalle>> LoadCartDetails(int carritoID, CancellationToken ct = default)
        {
            return _context.CarritoDetalle
                .AsNoTracking()
                .Where(c => c.CarritoID == carritoID)
                .Include(cd => cd.Producto)
                .Include(cd => cd.Variante)
                .OrderByDescending(cd => cd.CarritoDetalleID)
                .ToListAsync(ct);
        }

        /// <inheritdoc />
        public Task<int> GetCartItemCountAsync(int carritoID, CancellationToken ct = default)
        {
            return _context.CarritoDetalle
                .AsNoTracking()
                .Where(cd => cd.CarritoID == carritoID)
                .SumAsync(cd => cd.Cantidad, ct);
        }

        /// <inheritdoc />
        public async Task<decimal> GetCartTotalAsync(int carritoID, CancellationToken ct = default)
        {
            var detalles = await _context.CarritoDetalle
                .AsNoTracking()
                .Where(cd => cd.CarritoID == carritoID)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            return detalles.Sum(cd => cd.Cantidad * cd.Precio);
        }

        #endregion

        #region Mutaciones - Agregar Productos

        /// <inheritdoc />
        public Task<bool> AnadirProducto(Producto producto, Usuario usuario, int cantidad, CancellationToken ct = default)
        {
            return AnadirProducto(producto, usuario, cantidad, null, ct);
        }

        /// <inheritdoc />
        public async Task<bool> AnadirProducto(
            Producto producto,
            Usuario usuario,
            int cantidad,
            int? productoVarianteId,
            CancellationToken ct = default)
        {
            ValidateProducto(producto);
            ValidateUsuario(usuario);
            ValidateCantidad(cantidad);

            await using var trx = await _context.Database
                .BeginTransactionAsync(IsolationLevel.Serializable, ct)
                .ConfigureAwait(false);

            try
            {
                _logger.LogInformation("Añadiendo producto al carrito. ProductoId: {ProductoId}, UsuarioId: {UsuarioId}, Cantidad: {Cantidad}, VarianteId: {VarianteId}",
                    producto.ProductoID, usuario.Id, cantidad, productoVarianteId);
                _logger.LogDebug(LOG_DEBUG_TRANSACCION_INICIADA, "AnadirProducto");

                var carrito = await GetOrCreateOpenCartAsync(usuario, ct).ConfigureAwait(false);
                var prod = await ReloadProductoConVariantesAsync(producto.ProductoID, ct).ConfigureAwait(false);

                var tieneVariantes = prod.Variantes != null && prod.Variantes.Any();

                ProductoVariante? variante = null;
                if (productoVarianteId.HasValue)
                {
                    variante = await _context.ProductoVariantes
                        .FirstOrDefaultAsync(v => v.ProductoVarianteID == productoVarianteId.Value, ct)
                        .ConfigureAwait(false);

                    if (variante == null || variante.ProductoID != prod.ProductoID)
                    {
                        _logger.LogWarning(LOG_WARN_VARIANTE_INVALIDA, productoVarianteId.Value, prod.ProductoID);
                        throw new InvalidOperationException(ERR_VARIANTE_NO_CORRESPONDE);
                    }
                }
                else if (tieneVariantes)
                {
                    _logger.LogWarning(LOG_WARN_VARIANTE_REQUERIDA, prod.ProductoID, prod.Nombre);
                    throw new InvalidOperationException(ERR_VARIANTE_REQUERIDA);
                }

                var existente = await _context.CarritoDetalle
                    .FirstOrDefaultAsync(cd =>
                        cd.CarritoID == carrito.CarritoID &&
                        cd.ProductoID == prod.ProductoID &&
                        cd.ProductoVarianteID == (productoVarianteId.HasValue ? productoVarianteId.Value : (int?)null),
                        ct)
                    .ConfigureAwait(false);

                var cantidadAnterior = existente?.Cantidad ?? 0;
                var nuevoTotal = cantidadAnterior + cantidad;

                // Validar stock
                if (variante != null)
                {
                    ValidateStockVariante(variante, prod, nuevoTotal);
                }
                else
                {
                    ValidateStockProducto(prod, nuevoTotal);
                }

                var precioUnit = GetPrecioVenta(prod, variante);

                if (existente == null)
                {
                    var detalle = new CarritoDetalle
                    {
                        CarritoID = carrito.CarritoID,
                        ProductoID = prod.ProductoID,
                        ProductoVarianteID = productoVarianteId,
                        Cantidad = cantidad,
                        Precio = precioUnit
                    };

                    await _context.CarritoDetalle.AddAsync(detalle, ct).ConfigureAwait(false);
                    _logger.LogDebug(LOG_DEBUG_DETALLE_NUEVO, carrito.CarritoID, prod.ProductoID);
                }
                else
                {
                    existente.Cantidad = nuevoTotal;
                    existente.Precio = precioUnit;
                    _context.CarritoDetalle.Update(existente);
                    _logger.LogDebug(LOG_DEBUG_DETALLE_EXISTENTE, existente.CarritoDetalleID, cantidadAnterior);
                }

                if (carrito.EstadoCarrito == ESTADO_VACIO)
                {
                    carrito.EstadoCarrito = ESTADO_EN_USO;
                    _context.Carrito.Update(carrito);
                    _logger.LogDebug(LOG_DEBUG_CARRITO_MARCADO_EN_USO, carrito.CarritoID);
                }

                await _context.SaveChangesAsync(ct).ConfigureAwait(false);
                await trx.CommitAsync(ct).ConfigureAwait(false);

                _logger.LogInformation(LOG_INFO_PRODUCTO_ANADIDO,
                    producto.ProductoID, carrito.CarritoID, cantidad, productoVarianteId);
                _logger.LogDebug(LOG_DEBUG_TRANSACCION_COMMIT, "AnadirProducto");

                return true;
            }
            catch (DbUpdateException ex)
            {
                await trx.RollbackAsync(ct).ConfigureAwait(false);
                _logger.LogDebug(LOG_DEBUG_TRANSACCION_ROLLBACK, "AnadirProducto");
                _logger.LogError(ex, LOG_ERROR_DB_UPDATE, "AnadirProducto");
                return false;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await trx.RollbackAsync(ct).ConfigureAwait(false);
                _logger.LogDebug(LOG_DEBUG_TRANSACCION_ROLLBACK, "AnadirProducto");
                _logger.LogError(ex, LOG_ERROR_ANADIR_PRODUCTO, producto.ProductoID, usuario.Id);
                throw;
            }
        }

        #endregion

        #region Mutaciones - Eliminar/Actualizar

        /// <inheritdoc />
        public async Task<bool> BorrarProductoCarrito(int detalleId, CancellationToken ct = default)
        {
            await using var transaction = await _context.Database
                .BeginTransactionAsync(ct)
                .ConfigureAwait(false);

            try
            {
                _logger.LogInformation("Eliminando detalle del carrito. DetalleId: {DetalleId}", detalleId);
                _logger.LogDebug(LOG_DEBUG_TRANSACCION_INICIADA, "BorrarProductoCarrito");

                var detalle = await _context.CarritoDetalle
                    .FirstOrDefaultAsync(d => d.CarritoDetalleID == detalleId, ct)
                    .ConfigureAwait(false);

                if (detalle == null)
                {
                    _logger.LogWarning(LOG_WARN_DETALLE_NO_ENCONTRADO, detalleId);
                    return false;
                }

                var carritoId = detalle.CarritoID;

                _context.CarritoDetalle.Remove(detalle);

                var restantes = await _context.CarritoDetalle
                    .AnyAsync(d => d.CarritoID == carritoId && d.CarritoDetalleID != detalleId, ct)
                    .ConfigureAwait(false);

                if (!restantes)
                {
                    var carrito = await _context.Carrito
                        .FirstOrDefaultAsync(c => c.CarritoID == carritoId, ct)
                        .ConfigureAwait(false);

                    if (carrito != null &&
                        carrito.EstadoCarrito != ESTADO_CERRADO &&
                        carrito.EstadoCarrito != ESTADO_VACIO)
                    {
                        carrito.EstadoCarrito = ESTADO_VACIO;
                        _context.Carrito.Update(carrito);
                        _logger.LogDebug(LOG_DEBUG_CARRITO_MARCADO_VACIO, carritoId);
                    }
                }

                await _context.SaveChangesAsync(ct).ConfigureAwait(false);
                await transaction.CommitAsync(ct).ConfigureAwait(false);

                _logger.LogInformation(LOG_INFO_DETALLE_ELIMINADO, detalleId, carritoId);
                _logger.LogDebug(LOG_DEBUG_TRANSACCION_COMMIT, "BorrarProductoCarrito");

                return true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await transaction.RollbackAsync(ct).ConfigureAwait(false);
                _logger.LogDebug(LOG_DEBUG_TRANSACCION_ROLLBACK, "BorrarProductoCarrito");
                _logger.LogError(ex, LOG_ERROR_ELIMINAR_DETALLE, detalleId);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<(bool ok, decimal lineSubtotal, string? error)> ActualizarCantidadAsync(
            int carritoDetalleId,
            int cantidad,
            CancellationToken ct = default)
        {
            if (cantidad <= 0)
            {
                return (false, 0m, ERR_CANTIDAD_INVALIDA);
            }

            await using var trx = await _context.Database
                .BeginTransactionAsync(IsolationLevel.Serializable, ct)
                .ConfigureAwait(false);

            try
            {
                _logger.LogInformation("Actualizando cantidad de detalle. DetalleId: {DetalleId}, Cantidad: {Cantidad}",
                    carritoDetalleId, cantidad);
                _logger.LogDebug(LOG_DEBUG_TRANSACCION_INICIADA, "ActualizarCantidad");

                var detalle = await _context.CarritoDetalle
                    .Include(d => d.Producto)
                    .Include(d => d.Variante)
                    .FirstOrDefaultAsync(d => d.CarritoDetalleID == carritoDetalleId, ct)
                    .ConfigureAwait(false);

                if (detalle == null)
                {
                    return (false, 0m, ERR_DETALLE_NO_ENCONTRADO);
                }

                if (detalle.Producto == null)
                {
                    _logger.LogWarning(LOG_WARN_PRODUCTO_INEXISTENTE, detalle.ProductoID, carritoDetalleId);
                    return (false, 0m, ERR_PRODUCTO_EN_DETALLE_INEXISTENTE);
                }

                var cantidadAnterior = detalle.Cantidad;

                if (detalle.Variante != null)
                {
                    if (detalle.Variante.Stock < cantidad)
                    {
                        _logger.LogWarning(LOG_WARN_STOCK_INSUFICIENTE,
                            detalle.ProductoID, detalle.ProductoVarianteID, detalle.Variante.Stock, cantidad);

                        return (false, 0m,
                            string.Format(ERR_STOCK_INSUFICIENTE_DETALLE_VARIANTE, detalle.Variante.Stock));
                    }

                    detalle.Precio = GetPrecioVenta(detalle.Producto, detalle.Variante);
                }
                else
                {
                    var tieneVariantes = await _context.ProductoVariantes
                        .AnyAsync(v => v.ProductoID == detalle.ProductoID, ct)
                        .ConfigureAwait(false);

                    if (tieneVariantes)
                    {
                        _logger.LogWarning(LOG_WARN_VARIANTE_REQUERIDA, detalle.ProductoID, detalle.Producto.Nombre);
                        return (false, 0m, string.Format(ERR_PRODUCTO_REQUIERE_VARIANTE, detalle.Producto.Nombre));
                    }

                    if (detalle.Producto.Stock < cantidad)
                    {
                        _logger.LogWarning(LOG_WARN_STOCK_INSUFICIENTE,
                            detalle.ProductoID, null, detalle.Producto.Stock, cantidad);

                        return (false, 0m,
                            string.Format(ERR_STOCK_INSUFICIENTE_DETALLE_PRODUCTO, detalle.Producto.Stock));
                    }

                    detalle.Precio = GetPrecioVenta(detalle.Producto, null);
                }

                detalle.Cantidad = cantidad;
                _context.CarritoDetalle.Update(detalle);

                await _context.SaveChangesAsync(ct).ConfigureAwait(false);
                await trx.CommitAsync(ct).ConfigureAwait(false);

                var lineSub = detalle.Cantidad * detalle.Precio;

                _logger.LogInformation(LOG_INFO_CANTIDAD_ACTUALIZADA,
                    carritoDetalleId, cantidadAnterior, cantidad);
                _logger.LogDebug(LOG_DEBUG_TRANSACCION_COMMIT, "ActualizarCantidad");

                return (true, lineSub, null);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await trx.RollbackAsync(ct).ConfigureAwait(false);
                _logger.LogDebug(LOG_DEBUG_TRANSACCION_ROLLBACK, "ActualizarCantidad");
                _logger.LogError(ex, LOG_ERROR_ACTUALIZAR_CANTIDAD, carritoDetalleId, cantidad);
                return (false, 0m, ERR_ACTUALIZAR_CANTIDAD_FALLIDO);
            }
        }

        /// <inheritdoc />
        public async Task<bool> VaciarCarritoAsync(int carritoID, CancellationToken ct = default)
        {
            await using var transaction = await _context.Database
                .BeginTransactionAsync(ct)
                .ConfigureAwait(false);

            try
            {
                _logger.LogInformation("Vaciando carrito. CarritoId: {CarritoId}", carritoID);
                _logger.LogDebug(LOG_DEBUG_TRANSACCION_INICIADA, "VaciarCarrito");

                var detalles = await _context.CarritoDetalle
                    .Where(cd => cd.CarritoID == carritoID)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);

                var itemsCount = detalles.Count;

                if (itemsCount > 0)
                {
                    _context.CarritoDetalle.RemoveRange(detalles);
                }

                var carrito = await _context.Carrito
                    .FirstOrDefaultAsync(c => c.CarritoID == carritoID, ct)
                    .ConfigureAwait(false);

                if (carrito != null && carrito.EstadoCarrito != ESTADO_CERRADO)
                {
                    carrito.EstadoCarrito = ESTADO_VACIO;
                    _context.Carrito.Update(carrito);
                }

                await _context.SaveChangesAsync(ct).ConfigureAwait(false);
                await transaction.CommitAsync(ct).ConfigureAwait(false);

                _logger.LogInformation(LOG_INFO_CARRITO_VACIADO, carritoID, itemsCount);
                _logger.LogDebug(LOG_DEBUG_TRANSACCION_COMMIT, "VaciarCarrito");

                return true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await transaction.RollbackAsync(ct).ConfigureAwait(false);
                _logger.LogDebug(LOG_DEBUG_TRANSACCION_ROLLBACK, "VaciarCarrito");
                _logger.LogError(ex, LOG_ERROR_VACIAR_CARRITO, carritoID);
                return false;
            }
        }

        #endregion

        #region Procesamiento

        /// <inheritdoc />
        public async Task<(bool isValid, string? errorMessage)> ValidateCartForCheckoutAsync(
            int carritoID,
            CancellationToken ct = default)
        {
            try
            {
                var carritoDetalles = await _context.CarritoDetalle
                    .AsNoTracking()
                    .Where(cd => cd.CarritoID == carritoID)
                    .Include(cd => cd.Producto)
                    .Include(cd => cd.Variante)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);

                if (!carritoDetalles.Any())
                {
                    _logger.LogWarning(LOG_WARN_CARRITO_VACIO, carritoID);
                    return (false, string.Format(ERR_CARRITO_SIN_PRODUCTOS, carritoID));
                }

                foreach (var d in carritoDetalles)
                {
                    if (d.Producto == null)
                    {
                        _logger.LogWarning(LOG_WARN_PRODUCTO_INEXISTENTE, d.ProductoID, d.CarritoDetalleID);
                        return (false, ERR_PRODUCTO_EN_DETALLE_INEXISTENTE);
                    }

                    var productoConVariantes = await _context.ProductoVariantes
                        .AnyAsync(v => v.ProductoID == d.ProductoID, ct)
                        .ConfigureAwait(false);

                    if (d.Variante != null)
                    {
                        if (d.Variante.ProductoID != d.ProductoID)
                        {
                            return (false, ERR_VARIANTE_INVALIDA_DETALLE);
                        }

                        if (d.Variante.Stock < d.Cantidad)
                        {
                            _logger.LogWarning(LOG_WARN_STOCK_INSUFICIENTE,
                                d.ProductoID, d.ProductoVarianteID, d.Variante.Stock, d.Cantidad);

                            return (false, string.Format(ERR_STOCK_INSUFICIENTE_PROCESO_VARIANTE,
                                d.Producto.Nombre, d.Variante.Stock, d.Cantidad));
                        }
                    }
                    else
                    {
                        if (productoConVariantes)
                        {
                            _logger.LogWarning(LOG_WARN_VARIANTE_REQUERIDA, d.ProductoID, d.Producto.Nombre);
                            return (false, string.Format(ERR_VARIANTE_REQUERIDA_PROCESO, d.Producto.Nombre));
                        }

                        if (d.Producto.Stock < d.Cantidad)
                        {
                            _logger.LogWarning(LOG_WARN_STOCK_INSUFICIENTE,
                                d.ProductoID, null, d.Producto.Stock, d.Cantidad);

                            return (false, string.Format(ERR_STOCK_INSUFICIENTE_PROCESO_PRODUCTO,
                                d.Producto.Nombre, d.Producto.Stock, d.Cantidad));
                        }
                    }
                }

                return (true, null);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error al validar carrito para checkout. CarritoId: {CarritoId}", carritoID);
                return (false, "Error al validar el carrito");
            }
        }

        /// <inheritdoc />
        public async Task<bool> ProcessCartDetails(int carritoID, Usuario user, CancellationToken ct = default)
        {
            ValidateUsuarioActivo(user);

            await using var transaction = await _context.Database
                .BeginTransactionAsync(IsolationLevel.Serializable, ct)
                .ConfigureAwait(false);

            try
            {
                _logger.LogInformation("Procesando carrito. CarritoId: {CarritoId}, UsuarioId: {UsuarioId}",
                    carritoID, user.Id);
                _logger.LogDebug(LOG_DEBUG_TRANSACCION_INICIADA, "ProcessCartDetails");

                var carritoDetalles = await _context.CarritoDetalle
                    .Where(cd => cd.CarritoID == carritoID)
                    .Include(cd => cd.Producto)
                    .Include(cd => cd.Variante)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);

                if (!carritoDetalles.Any())
                {
                    var msg = string.Format(ERR_CARRITO_SIN_PRODUCTOS, carritoID);
                    _logger.LogWarning(LOG_WARN_CARRITO_VACIO, carritoID);
                    throw new InvalidOperationException(msg);
                }

                // Validaciones de stock
                foreach (var d in carritoDetalles)
                {
                    if (d.Producto == null)
                    {
                        _logger.LogWarning(LOG_WARN_PRODUCTO_INEXISTENTE, d.ProductoID, d.CarritoDetalleID);
                        throw new InvalidOperationException(ERR_PRODUCTO_EN_DETALLE_INEXISTENTE);
                    }

                    var productoConVariantes = await _context.ProductoVariantes
                        .AnyAsync(v => v.ProductoID == d.ProductoID, ct)
                        .ConfigureAwait(false);

                    if (d.Variante != null)
                    {
                        if (d.Variante.ProductoID != d.ProductoID)
                        {
                            throw new InvalidOperationException(ERR_VARIANTE_INVALIDA_DETALLE);
                        }

                        if (d.Variante.Stock < d.Cantidad)
                        {
                            _logger.LogWarning(LOG_WARN_STOCK_INSUFICIENTE,
                                d.ProductoID, d.ProductoVarianteID, d.Variante.Stock, d.Cantidad);

                            throw new InvalidOperationException(
                                string.Format(ERR_STOCK_INSUFICIENTE_PROCESO_VARIANTE,
                                    d.Producto.Nombre, d.Variante.Stock, d.Cantidad));
                        }
                    }
                    else
                    {
                        if (productoConVariantes)
                        {
                            _logger.LogWarning(LOG_WARN_VARIANTE_REQUERIDA, d.ProductoID, d.Producto.Nombre);
                            throw new InvalidOperationException(
                                string.Format(ERR_VARIANTE_REQUERIDA_PROCESO, d.Producto.Nombre));
                        }

                        if (d.Producto.Stock < d.Cantidad)
                        {
                            _logger.LogWarning(LOG_WARN_STOCK_INSUFICIENTE,
                                d.ProductoID, null, d.Producto.Stock, d.Cantidad);

                            throw new InvalidOperationException(
                                string.Format(ERR_STOCK_INSUFICIENTE_PROCESO_PRODUCTO,
                                    d.Producto.Nombre, d.Producto.Stock, d.Cantidad));
                        }
                    }
                }

                // Descuento de stock + movimientos
                foreach (var d in carritoDetalles)
                {
                    if (d.Variante != null)
                    {
                        var stockAnterior = d.Variante.Stock;
                        d.Variante.Stock -= d.Cantidad;

                        _logger.LogInformation(LOG_INFO_STOCK_DESCONTADO,
                            d.ProductoID, d.ProductoVarianteID, d.Cantidad, d.Variante.Stock);

                        await _context.MovimientosInventario.AddAsync(new MovimientosInventario
                        {
                            ProductoID = d.ProductoID,
                            ProductoVarianteID = d.ProductoVarianteID,
                            Cantidad = d.Cantidad,
                            TipoMovimiento = TIPO_MOVIMIENTO_SALIDA,
                            FechaMovimiento = DateTime.UtcNow,
                            Descripcion = $"Venta - Carrito #{carritoID} (variante: {d.Variante.Color ?? "-"} / {d.Variante.Talla ?? "-"})"
                        }, ct).ConfigureAwait(false);
                    }
                    else
                    {
                        var stockAnterior = d.Producto.Stock;
                        d.Producto.Stock -= d.Cantidad;

                        _logger.LogInformation(LOG_INFO_STOCK_DESCONTADO,
                            d.ProductoID, null, d.Cantidad, d.Producto.Stock);

                        await _context.MovimientosInventario.AddAsync(new MovimientosInventario
                        {
                            ProductoID = d.ProductoID,
                            ProductoVarianteID = null,
                            Cantidad = d.Cantidad,
                            TipoMovimiento = TIPO_MOVIMIENTO_SALIDA,
                            FechaMovimiento = DateTime.UtcNow,
                            Descripcion = $"Venta - Carrito #{carritoID}"
                        }, ct).ConfigureAwait(false);
                    }
                }

                var admin = await GetAdminUsuarioAsync(ct).ConfigureAwait(false);

                var total = carritoDetalles.Sum(cd => cd.Cantidad * cd.Precio);
                var venta = new Ventas
                {
                    EmpleadoID = admin.Id,
                    Estado = ESTADO_VENTA_COMPLETADA,
                    UsuarioId = user.Id,
                    Usuario = user,
                    FechaVenta = DateTime.UtcNow,
                    MetodoPago = METODO_PAGO_DEFAULT,
                    Total = total
                };

                await _context.Ventas.AddAsync(venta, ct).ConfigureAwait(false);

                var dv = carritoDetalles.Select(d => new DetalleVentas
                {
                    Venta = venta,
                    ProductoID = d.ProductoID,
                    ProductoVarianteID = d.ProductoVarianteID,
                    Cantidad = d.Cantidad,
                    PrecioUnitario = d.Precio,
                    Descuento = 0,
                    Subtotal = d.Cantidad * d.Precio,
                    FechaCreacion = DateTime.UtcNow
                }).ToList();

                await _context.DetalleVentas.AddRangeAsync(dv, ct).ConfigureAwait(false);

                var carrito = await _context.Carrito
                    .FirstOrDefaultAsync(c => c.CarritoID == carritoID, ct)
                    .ConfigureAwait(false);

                if (carrito != null)
                {
                    carrito.EstadoCarrito = ESTADO_CERRADO;
                    _context.Carrito.Update(carrito);
                }

                _context.CarritoDetalle.RemoveRange(carritoDetalles);

                await _context.SaveChangesAsync(ct).ConfigureAwait(false);
                await transaction.CommitAsync(ct).ConfigureAwait(false);

                _logger.LogInformation(LOG_INFO_CARRITO_PROCESADO,
                    carritoID, venta.VentaID, total, carritoDetalles.Count);
                _logger.LogDebug(LOG_DEBUG_TRANSACCION_COMMIT, "ProcessCartDetails");

                return true;
            }
            catch (InvalidOperationException ex)
            {
                await transaction.RollbackAsync(ct).ConfigureAwait(false);
                _logger.LogDebug(LOG_DEBUG_TRANSACCION_ROLLBACK, "ProcessCartDetails");
                _logger.LogWarning(ex, "Operación inválida al procesar carrito. CarritoId: {CarritoId}", carritoID);
                throw;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await transaction.RollbackAsync(ct).ConfigureAwait(false);
                _logger.LogDebug(LOG_DEBUG_TRANSACCION_ROLLBACK, "ProcessCartDetails");
                _logger.LogError(ex, LOG_ERROR_PROCESAR_CARRITO, carritoID);
                return false;
            }
        }

        #endregion
    }

    #endregion
}