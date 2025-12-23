using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Simone.Data;
using Simone.Models;

namespace Simone.Services
{
    public interface ICarritoService
    {
        Task<bool> AddAsync(Usuario usuario);
        Task<List<Carrito>> GetAllAsync();
        Task<Carrito> GetByIdAsync(int id);
        Task<bool> UpdateAsync(Carrito carrito);
        Task<bool> DeleteAsync(int id);

        Task<Carrito> GetByUsuarioIdAsync(string usuarioId);
        Task<Carrito> GetByClienteIdAsync(string clienteID);
        Task<List<CarritoDetalle>> LoadCartDetails(int carritoID);

        Task<bool> AnadirProducto(Producto producto, Usuario usuario, int cantidad);
        Task<bool> AnadirProducto(Producto producto, Usuario usuario, int cantidad, int? productoVarianteId);
        Task<bool> BorrarProductoCarrito(int detalleId);
        Task<(bool ok, decimal lineSubtotal, string? error)> ActualizarCantidadAsync(int carritoDetalleId, int cantidad);
        Task<bool> ProcessCartDetails(int carritoID, Usuario user);
    }

    public sealed class CarritoService : ICarritoService
    {
        private readonly TiendaDbContext _context;
        private readonly ILogger<CarritoService> _logger;
        private readonly UserManager<Usuario> _userManager;

        private const string AdminEmail = "admin@tienda.com";
        private const string EstadoCerrado = "Cerrado";
        private const string EstadoEnUso = "En Uso";
        private const string EstadoVacio = "Vacio";

        public CarritoService(
            TiendaDbContext context,
            ILogger<CarritoService> logger,
            UserManager<Usuario> userManager)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        }

        #region Helpers Internos

        private async Task<Carrito> GetOrCreateOpenCartAsync(Usuario usuario)
        {
            if (usuario == null) throw new ArgumentNullException(nameof(usuario));

            var current = await _context.Carrito
                .FirstOrDefaultAsync(c => c.UsuarioId == usuario.Id && c.EstadoCarrito != EstadoCerrado);

            if (current != null) return current;

            await using var trx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                var again = await _context.Carrito
                    .FirstOrDefaultAsync(c => c.UsuarioId == usuario.Id && c.EstadoCarrito != EstadoCerrado);

                if (again != null)
                {
                    await trx.CommitAsync();
                    return again;
                }

                var nuevo = new Carrito
                {
                    UsuarioId = usuario.Id,
                    FechaCreacion = DateTime.UtcNow,
                    EstadoCarrito = EstadoVacio
                };
                await _context.Carrito.AddAsync(nuevo);
                await _context.SaveChangesAsync();
                await trx.CommitAsync();

                _logger.LogInformation("Carrito {CarritoId} creado para usuario {UsuarioId}", nuevo.CarritoID, usuario.Id);
                return nuevo;
            }
            catch (Exception ex)
            {
                await trx.RollbackAsync();
                _logger.LogError(ex, "Error creando carrito para {UsuarioId}", usuario.Id);
                throw;
            }
        }

        private async Task<Producto> ReloadProductoAsync(int productoId)
        {
            var p = await _context.Productos.FirstOrDefaultAsync(x => x.ProductoID == productoId);
            if (p == null) throw new InvalidOperationException($"Producto {productoId} inexistente.");
            return p;
        }

        private async Task<Producto> ReloadProductoConVariantesAsync(int productoId)
        {
            var p = await _context.Productos
                .Include(x => x.Variantes)
                .FirstOrDefaultAsync(x => x.ProductoID == productoId);
            if (p == null) throw new InvalidOperationException($"Producto {productoId} inexistente.");
            return p;
        }

        #endregion

        #region CRUD Básico

        public async Task<bool> AddAsync(Usuario usuario)
        {
            if (usuario == null) throw new ArgumentNullException(nameof(usuario));
            try
            {
                _logger.LogInformation("Creando carrito para usuario {UsuarioId}", usuario.Id);

                var carrito = new Carrito
                {
                    UsuarioId = usuario.Id,
                    FechaCreacion = DateTime.UtcNow,
                    EstadoCarrito = EstadoVacio
                };
                await _context.Carrito.AddAsync(carrito);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Carrito {CarritoId} creado exitosamente", carrito.CarritoID);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al agregar carrito para {UsuarioId}", usuario.Id);
                return false;
            }
        }

        public Task<List<Carrito>> GetAllAsync() =>
            _context.Carrito.AsNoTracking().ToListAsync();

        public Task<Carrito> GetByIdAsync(int id) =>
            _context.Carrito.AsNoTracking().FirstOrDefaultAsync(c => c.CarritoID == id);

        public async Task<bool> UpdateAsync(Carrito carrito)
        {
            if (carrito == null) throw new ArgumentNullException(nameof(carrito));
            try
            {
                _logger.LogInformation("Actualizando carrito {CarritoId}", carrito.CarritoID);

                _context.Carrito.Update(carrito);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Carrito {CarritoId} actualizado exitosamente", carrito.CarritoID);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar carrito {CarritoId}", carrito.CarritoID);
                return false;
            }
        }

        public async Task<bool> DeleteAsync(int id)
        {
            // ✅ MEJORA: Ahora usa transacción
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _logger.LogInformation("Eliminando carrito {CarritoId}", id);

                var carrito = await _context.Carrito
                    .Include(c => c.CarritoDetalles)
                    .FirstOrDefaultAsync(c => c.CarritoID == id);

                if (carrito == null)
                {
                    _logger.LogWarning("Carrito {CarritoId} no encontrado", id);
                    return false;
                }

                if (carrito.CarritoDetalles?.Count > 0)
                {
                    _logger.LogDebug("Eliminando {Count} detalles del carrito {CarritoId}",
                        carrito.CarritoDetalles.Count, id);
                    _context.CarritoDetalle.RemoveRange(carrito.CarritoDetalles);
                }

                _context.Carrito.Remove(carrito);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Carrito {CarritoId} eliminado exitosamente", id);
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error al eliminar carrito {CarritoId}. Transacción revertida", id);
                return false;
            }
        }

        #endregion

        #region Lecturas

        public Task<Carrito> GetByUsuarioIdAsync(string usuarioId)
        {
            if (string.IsNullOrWhiteSpace(usuarioId))
                throw new ArgumentException("UsuarioId no puede estar vacío", nameof(usuarioId));

            return _context.Carrito
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.UsuarioId == usuarioId && c.EstadoCarrito != EstadoCerrado);
        }

        public Task<Carrito> GetByClienteIdAsync(string clienteID) =>
            GetByUsuarioIdAsync(clienteID);

        public Task<List<CarritoDetalle>> LoadCartDetails(int carritoID) =>
            _context.CarritoDetalle
                .AsNoTracking()
                .Where(c => c.CarritoID == carritoID)
                .Include(cd => cd.Producto)
                .Include(cd => cd.Variante)
                .OrderByDescending(cd => cd.CarritoDetalleID)
                .ToListAsync();

        #endregion

        #region Mutaciones - Agregar Productos

        public Task<bool> AnadirProducto(Producto producto, Usuario usuario, int cantidad) =>
            AnadirProducto(producto, usuario, cantidad, null);

        public async Task<bool> AnadirProducto(Producto producto, Usuario usuario, int cantidad, int? productoVarianteId)
        {
            if (producto == null) throw new ArgumentNullException(nameof(producto));
            if (usuario == null) throw new ArgumentNullException(nameof(usuario));
            if (cantidad <= 0) throw new ArgumentException("La cantidad debe ser mayor que cero", nameof(cantidad));

            await using var trx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                _logger.LogInformation("Añadiendo producto {ProductoId} (variante: {VarianteId}) al carrito del usuario {UsuarioId}",
                    producto.ProductoID, productoVarianteId, usuario.Id);

                var carrito = await GetOrCreateOpenCartAsync(usuario);
                var prod = await ReloadProductoConVariantesAsync(producto.ProductoID);

                var tieneVariantes = prod.Variantes != null && prod.Variantes.Any();

                ProductoVariante? variante = null;
                if (productoVarianteId.HasValue)
                {
                    variante = await _context.ProductoVariantes
                        .FirstOrDefaultAsync(v => v.ProductoVarianteID == productoVarianteId.Value);

                    if (variante == null || variante.ProductoID != prod.ProductoID)
                        throw new InvalidOperationException("La variante seleccionada no corresponde a este producto.");
                }
                else if (tieneVariantes)
                {
                    throw new InvalidOperationException("Debes seleccionar Color y Talla para este producto.");
                }

                var existente = await _context.CarritoDetalle.FirstOrDefaultAsync(cd =>
                    cd.CarritoID == carrito.CarritoID &&
                    cd.ProductoID == prod.ProductoID &&
                    cd.ProductoVarianteID == (productoVarianteId.HasValue ? productoVarianteId.Value : (int?)null));

                var nuevoTotal = (existente?.Cantidad ?? 0) + cantidad;

                if (variante != null)
                {
                    if (variante.Stock < nuevoTotal)
                        throw new InvalidOperationException(
                            $"No hay stock suficiente para la combinación seleccionada. Disponible: {variante.Stock}, solicitado: {nuevoTotal}.");
                }
                else
                {
                    if (prod.Stock < nuevoTotal)
                        throw new InvalidOperationException(
                            $"No hay stock suficiente de '{prod.Nombre}'. Disponible: {prod.Stock}, solicitado: {nuevoTotal}.");
                }

                var precioUnit = variante?.PrecioVenta ?? prod.PrecioVenta;

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
                    await _context.CarritoDetalle.AddAsync(detalle);
                    _logger.LogDebug("Nuevo detalle agregado al carrito {CarritoId}", carrito.CarritoID);
                }
                else
                {
                    existente.Cantidad = nuevoTotal;
                    existente.Precio = precioUnit;
                    _context.CarritoDetalle.Update(existente);
                    _logger.LogDebug("Detalle actualizado en carrito {CarritoId}", carrito.CarritoID);
                }

                if (carrito.EstadoCarrito == EstadoVacio)
                {
                    carrito.EstadoCarrito = EstadoEnUso;
                    _context.Carrito.Update(carrito);
                }

                await _context.SaveChangesAsync();
                await trx.CommitAsync();

                _logger.LogInformation("Producto {ProductoId} añadido exitosamente al carrito", producto.ProductoID);
                return true;
            }
            catch (DbUpdateException ex)
            {
                await trx.RollbackAsync();
                _logger.LogError(ex, "DbUpdateException al añadir producto {ProductoId} al carrito", producto.ProductoID);
                return false;
            }
            catch (Exception ex)
            {
                await trx.RollbackAsync();
                _logger.LogWarning(ex, "Error al añadir producto {ProductoId} al carrito", producto.ProductoID);
                throw;
            }
        }

        #endregion

        #region Mutaciones - Eliminar/Actualizar

        public async Task<bool> BorrarProductoCarrito(int detalleId)
        {
            // ✅ MEJORA: Ahora usa transacción para garantizar atomicidad
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _logger.LogInformation("Eliminando detalle {DetalleId} del carrito", detalleId);

                var detalle = await _context.CarritoDetalle.FirstOrDefaultAsync(d => d.CarritoDetalleID == detalleId);
                if (detalle == null)
                {
                    _logger.LogWarning("Detalle {DetalleId} no encontrado", detalleId);
                    return false;
                }

                var carritoId = detalle.CarritoID;

                _context.CarritoDetalle.Remove(detalle);

                var restantes = await _context.CarritoDetalle
                    .AnyAsync(d => d.CarritoID == carritoId && d.CarritoDetalleID != detalleId);

                if (!restantes)
                {
                    var carrito = await _context.Carrito.FirstOrDefaultAsync(c => c.CarritoID == carritoId);
                    if (carrito != null && carrito.EstadoCarrito != EstadoCerrado && carrito.EstadoCarrito != EstadoVacio)
                    {
                        carrito.EstadoCarrito = EstadoVacio;
                        _context.Carrito.Update(carrito);
                        _logger.LogDebug("Carrito {CarritoId} marcado como vacío", carritoId);
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Detalle {DetalleId} eliminado exitosamente", detalleId);
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error al borrar detalle {DetalleId}. Transacción revertida", detalleId);
                return false;
            }
        }

        public async Task<(bool ok, decimal lineSubtotal, string? error)> ActualizarCantidadAsync(int carritoDetalleId, int cantidad)
        {
            if (cantidad <= 0) return (false, 0m, "La cantidad debe ser mayor que cero.");

            await using var trx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                _logger.LogInformation("Actualizando cantidad del detalle {DetalleId} a {Cantidad}",
                    carritoDetalleId, cantidad);

                var detalle = await _context.CarritoDetalle
                    .Include(d => d.Producto)
                    .Include(d => d.Variante)
                    .FirstOrDefaultAsync(d => d.CarritoDetalleID == carritoDetalleId);

                if (detalle == null)
                    return (false, 0m, "No se encontró el ítem del carrito.");

                if (detalle.Producto == null)
                    return (false, 0m, "Producto inexistente.");

                if (detalle.Variante != null)
                {
                    if (detalle.Variante.Stock < cantidad)
                        return (false, 0m, $"Stock insuficiente para la combinación seleccionada. Disponible: {detalle.Variante.Stock}.");

                    detalle.Precio = detalle.Variante.PrecioVenta ?? detalle.Producto.PrecioVenta;
                }
                else
                {
                    var tieneVariantes = await _context.ProductoVariantes.AnyAsync(v => v.ProductoID == detalle.ProductoID);
                    if (tieneVariantes)
                        return (false, 0m, $"El producto '{detalle.Producto.Nombre}' requiere Color/Talla. Elimina y vuelve a agregar con variante.");

                    if (detalle.Producto.Stock < cantidad)
                        return (false, 0m, $"Stock insuficiente. Disponible: {detalle.Producto.Stock}.");

                    detalle.Precio = detalle.Producto.PrecioVenta;
                }

                detalle.Cantidad = cantidad;
                _context.CarritoDetalle.Update(detalle);

                await _context.SaveChangesAsync();
                await trx.CommitAsync();

                var lineSub = detalle.Cantidad * detalle.Precio;

                _logger.LogInformation("Cantidad del detalle {DetalleId} actualizada exitosamente", carritoDetalleId);
                return (true, lineSub, null);
            }
            catch (Exception ex)
            {
                await trx.RollbackAsync();
                _logger.LogError(ex, "Error al actualizar cantidad del detalle {DetalleId}. Transacción revertida",
                    carritoDetalleId);
                return (false, 0m, "No se pudo actualizar la cantidad.");
            }
        }

        #endregion

        #region Procesamiento

        public async Task<bool> ProcessCartDetails(int carritoID, Usuario user)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            if (!user.Activo) throw new InvalidOperationException("Tu cuenta está desactivada.");

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            try
            {
                _logger.LogInformation("Procesando carrito {CarritoId} para usuario {UsuarioId}", carritoID, user.Id);

                var carritoDetalles = await _context.CarritoDetalle
                    .Where(cd => cd.CarritoID == carritoID)
                    .Include(cd => cd.Producto)
                    .Include(cd => cd.Variante)
                    .ToListAsync();

                if (!carritoDetalles.Any())
                {
                    var msg = $"El carrito {carritoID} no tiene productos.";
                    _logger.LogWarning(msg);
                    throw new InvalidOperationException(msg);
                }

                // Validaciones de stock
                foreach (var d in carritoDetalles)
                {
                    if (d.Producto == null)
                        throw new InvalidOperationException($"Producto {d.ProductoID} inexistente.");

                    var productoConVariantes = await _context.ProductoVariantes
                        .AnyAsync(v => v.ProductoID == d.ProductoID);

                    if (d.Variante != null)
                    {
                        if (d.Variante.ProductoID != d.ProductoID)
                            throw new InvalidOperationException("La variante del carrito no corresponde al producto.");

                        if (d.Variante.Stock < d.Cantidad)
                            throw new InvalidOperationException(
                                $"Stock insuficiente para la combinación seleccionada de '{d.Producto.Nombre}': disp {d.Variante.Stock}, solicitado {d.Cantidad}.");
                    }
                    else
                    {
                        if (productoConVariantes)
                            throw new InvalidOperationException(
                                $"El producto '{d.Producto.Nombre}' requiere Color/Talla. Elimina el ítem y vuelve a agregarlo seleccionando una variante.");
                        if (d.Producto.Stock < d.Cantidad)
                            throw new InvalidOperationException(
                                $"Stock insuficiente para '{d.Producto.Nombre}': disp {d.Producto.Stock}, solicitado {d.Cantidad}.");
                    }
                }

                // Descuento de stock + movimientos
                foreach (var d in carritoDetalles)
                {
                    if (d.Variante != null)
                    {
                        d.Variante.Stock -= d.Cantidad;

                        await _context.MovimientosInventario.AddAsync(new MovimientosInventario
                        {
                            ProductoID = d.ProductoID,
                            ProductoVarianteID = d.ProductoVarianteID,
                            Cantidad = d.Cantidad,
                            TipoMovimiento = "Salida",
                            FechaMovimiento = DateTime.UtcNow,
                            Descripcion = $"Venta - Carrito #{carritoID} (variante: {d.Variante.Color ?? "-"} / {d.Variante.Talla ?? "-"})"
                        });
                    }
                    else
                    {
                        d.Producto.Stock -= d.Cantidad;

                        await _context.MovimientosInventario.AddAsync(new MovimientosInventario
                        {
                            ProductoID = d.ProductoID,
                            ProductoVarianteID = null,
                            Cantidad = d.Cantidad,
                            TipoMovimiento = "Salida",
                            FechaMovimiento = DateTime.UtcNow,
                            Descripcion = $"Venta - Carrito #{carritoID}"
                        });
                    }
                }

                var admin = await _userManager.Users.FirstOrDefaultAsync(u => u.Email == AdminEmail);
                if (admin == null)
                    throw new InvalidOperationException($"No se encontró el usuario administrador '{AdminEmail}'.");

                var total = carritoDetalles.Sum(cd => cd.Cantidad * cd.Precio);
                var venta = new Ventas
                {
                    EmpleadoID = admin.Id,
                    Estado = "Completada",
                    UsuarioId = user.Id,
                    Usuario = user,
                    FechaVenta = DateTime.UtcNow,
                    MetodoPago = "Transferencia",
                    Total = total
                };
                await _context.Ventas.AddAsync(venta);

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

                await _context.DetalleVentas.AddRangeAsync(dv);

                var carrito = await _context.Carrito.FirstOrDefaultAsync(c => c.CarritoID == carritoID);
                if (carrito != null)
                {
                    carrito.EstadoCarrito = EstadoCerrado;
                    _context.Carrito.Update(carrito);
                }
                _context.CarritoDetalle.RemoveRange(carritoDetalles);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Carrito {CarritoId} procesado exitosamente. Venta creada", carritoID);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                await transaction.RollbackAsync();
                _logger.LogWarning(ex, "Operación inválida al procesar carrito {CarritoId}", carritoID);
                throw;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error inesperado al procesar el carrito {CarritoId}. Transacción revertida", carritoID);
                return false;
            }
        }

        #endregion
    }
}
