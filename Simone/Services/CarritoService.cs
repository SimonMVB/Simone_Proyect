using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Data; // IsolationLevel
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

        // Correcto (UsuarioId)
        Task<Carrito> GetByUsuarioIdAsync(string usuarioId);

        // Compatibilidad (legacy)
        Task<Carrito> GetByClienteIdAsync(string clienteID);

        Task<List<CarritoDetalle>> LoadCartDetails(int carritoID);
        Task<bool> AnadirProducto(Producto producto, Usuario usuario, int cantidad);
        Task<bool> BorrarProductoCarrito(int detalleId);
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

        /* ===================== Helpers internos ===================== */

        /// <summary>
        /// Obtiene el carrito abierto del usuario o lo crea si no existe.
        /// Transacción SERIALIZABLE para evitar duplicados bajo concurrencia.
        /// </summary>
        private async Task<Carrito> GetOrCreateOpenCartAsync(Usuario usuario)
        {
            if (usuario == null) throw new ArgumentNullException(nameof(usuario));

            // 1) Intentar leer sin transacción (rápido)
            var current = await _context.Carrito
                .FirstOrDefaultAsync(c => c.UsuarioId == usuario.Id && c.EstadoCarrito != EstadoCerrado);

            if (current != null) return current;

            // 2) Crear con transacción de mayor aislamiento para evitar "doble creación"
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
                return nuevo;
            }
            catch (Exception ex)
            {
                await trx.RollbackAsync();
                _logger.LogError(ex, "Error creando carrito para {UsuarioId}", usuario.Id);
                throw;
            }
        }

        /// <summary>
        /// Vuelve a cargar el producto desde la BD para obtener el stock más reciente.
        /// </summary>
        private async Task<Producto> ReloadProductoAsync(int productoId)
        {
            var p = await _context.Productos.FirstOrDefaultAsync(x => x.ProductoID == productoId);
            if (p == null) throw new InvalidOperationException($"Producto {productoId} inexistente.");
            return p;
        }

        /* ===================== CRUD básico ===================== */

        public async Task<bool> AddAsync(Usuario usuario)
        {
            if (usuario == null) throw new ArgumentNullException(nameof(usuario));
            try
            {
                var carrito = new Carrito
                {
                    UsuarioId = usuario.Id,
                    FechaCreacion = DateTime.UtcNow,
                    EstadoCarrito = EstadoVacio
                };
                await _context.Carrito.AddAsync(carrito);
                return (await _context.SaveChangesAsync()) > 0;
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
                _context.Carrito.Update(carrito);
                return (await _context.SaveChangesAsync()) > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar carrito {CarritoId}", carrito.CarritoID);
                return false;
            }
        }

        public async Task<bool> DeleteAsync(int id)
        {
            try
            {
                var carrito = await _context.Carrito
                    .Include(c => c.CarritoDetalles)
                    .FirstOrDefaultAsync(c => c.CarritoID == id);

                if (carrito == null) return false;

                if (carrito.CarritoDetalles?.Count > 0)
                    _context.CarritoDetalle.RemoveRange(carrito.CarritoDetalles);

                _context.Carrito.Remove(carrito);
                return (await _context.SaveChangesAsync()) > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar carrito {CarritoId}", id);
                return false;
            }
        }

        /* ===================== Lecturas de carrito ===================== */

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
                .OrderByDescending(cd => cd.CarritoDetalleID) // orden estable para UI (si existe ID)
                .ToListAsync();

        /* ===================== Mutaciones ===================== */

        public async Task<bool> AnadirProducto(Producto producto, Usuario usuario, int cantidad)
        {
            if (producto == null) throw new ArgumentNullException(nameof(producto));
            if (usuario == null) throw new ArgumentNullException(nameof(usuario));
            if (cantidad <= 0) throw new ArgumentException("La cantidad debe ser mayor que cero", nameof(cantidad));

            // Transacción SERIALIZABLE para evitar duplicados (mismo producto) bajo concurrencia
            await using var trx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                var carrito = await GetOrCreateOpenCartAsync(usuario); // ya manejado con serializable
                // Recargar producto (stock fresco)
                var prod = await ReloadProductoAsync(producto.ProductoID);

                // Buscar detalle existente con tracking
                var existente = await _context.CarritoDetalle
                    .FirstOrDefaultAsync(cd => cd.CarritoID == carrito.CarritoID && cd.ProductoID == prod.ProductoID);

                var nuevoTotal = (existente?.Cantidad ?? 0) + cantidad;
                if (prod.Stock < nuevoTotal)
                    throw new InvalidOperationException(
                        $"No hay stock suficiente de '{prod.Nombre}'. Disponible: {prod.Stock}, solicitado: {nuevoTotal}.");

                if (existente == null)
                {
                    var detalle = new CarritoDetalle
                    {
                        CarritoID = carrito.CarritoID,
                        ProductoID = prod.ProductoID,
                        Cantidad = cantidad,
                        Precio = prod.PrecioVenta
                    };
                    await _context.CarritoDetalle.AddAsync(detalle);
                }
                else
                {
                    existente.Cantidad = nuevoTotal;
                    _context.CarritoDetalle.Update(existente);
                }

                if (carrito.EstadoCarrito == EstadoVacio)
                {
                    carrito.EstadoCarrito = EstadoEnUso;
                    _context.Carrito.Update(carrito);
                }

                await _context.SaveChangesAsync();
                await trx.CommitAsync();
                return true;
            }
            catch (DbUpdateException ex)
            {
                await trx.RollbackAsync();
                _logger.LogError(ex, "DbUpdateException al añadir {ProductoId} al carrito de {UsuarioId}", producto.ProductoID, usuario.Id);
                return false; // dejamos que la UI reintente si desea
            }
            catch (Exception ex)
            {
                await trx.RollbackAsync();
                _logger.LogWarning(ex, "Error al añadir producto {ProductoId} al carrito de {UsuarioId}", producto.ProductoID, usuario.Id);
                throw; // para que la capa superior pueda mostrar mensaje preciso
            }
        }

        public async Task<bool> BorrarProductoCarrito(int detalleId)
        {
            try
            {
                var detalle = await _context.CarritoDetalle.FirstOrDefaultAsync(d => d.CarritoDetalleID == detalleId);
                if (detalle == null) return false;

                // Guardar CarritoID antes de eliminar
                var carritoId = detalle.CarritoID;

                _context.CarritoDetalle.Remove(detalle);
                var ok = (await _context.SaveChangesAsync()) > 0;

                // Si quedó vacío, marcar carrito como "Vacio"
                var restantes = await _context.CarritoDetalle.AnyAsync(d => d.CarritoID == carritoId);
                if (!restantes)
                {
                    var carrito = await _context.Carrito.FirstOrDefaultAsync(c => c.CarritoID == carritoId);
                    if (carrito != null && carrito.EstadoCarrito != EstadoCerrado && carrito.EstadoCarrito != EstadoVacio)
                    {
                        carrito.EstadoCarrito = EstadoVacio;
                        _context.Carrito.Update(carrito);
                        await _context.SaveChangesAsync();
                    }
                }

                return ok;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al borrar detalle {DetalleId} del carrito", detalleId);
                return false;
            }
        }

        public async Task<bool> ProcessCartDetails(int carritoID, Usuario user)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            if (!user.Activo) throw new InvalidOperationException("Tu cuenta está desactivada.");

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            try
            {
                // Cargar detalles con tracking + producto
                var carritoDetalles = await _context.CarritoDetalle
                    .Where(cd => cd.CarritoID == carritoID)
                    .Include(cd => cd.Producto)
                    .ToListAsync();

                if (!carritoDetalles.Any())
                {
                    var msg = $"El carrito {carritoID} no tiene productos.";
                    _logger.LogWarning(msg);
                    throw new InvalidOperationException(msg);
                }

                // Validar stock disponible
                foreach (var d in carritoDetalles)
                {
                    if (d.Producto == null)
                        throw new InvalidOperationException($"Producto {d.ProductoID} inexistente.");
                    if (d.Producto.Stock < d.Cantidad)
                        throw new InvalidOperationException($"Stock insuficiente para '{d.Producto.Nombre}': disp {d.Producto.Stock}, solicitado {d.Cantidad}");
                }

                // Descontar stock + registrar movimiento
                foreach (var d in carritoDetalles)
                {
                    d.Producto.Stock -= d.Cantidad;

                    await _context.MovimientosInventario.AddAsync(new MovimientosInventario
                    {
                        ProductoID = d.ProductoID,
                        Cantidad = d.Cantidad,
                        TipoMovimiento = "Salida",
                        FechaMovimiento = DateTime.UtcNow,
                        Descripcion = $"Venta - Carrito #{carritoID}"
                    });
                }

                // Vendedor/empleado responsable (admin por defecto)
                var admin = await _userManager.Users.FirstOrDefaultAsync(u => u.Email == AdminEmail);
                if (admin == null)
                    throw new InvalidOperationException($"No se encontró el usuario administrador '{AdminEmail}'.");

                // Crear venta
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

                // Detalles de venta
                var dv = carritoDetalles.Select(d => new DetalleVentas
                {
                    Venta = venta,
                    ProductoID = d.ProductoID,
                    Cantidad = d.Cantidad,
                    PrecioUnitario = d.Precio,
                    Descuento = 0,
                    Subtotal = d.Cantidad * d.Precio,
                    FechaCreacion = DateTime.UtcNow
                }).ToList();

                await _context.DetalleVentas.AddRangeAsync(dv);

                // Cerrar carrito + limpiar detalles
                var carrito = await _context.Carrito.FirstOrDefaultAsync(c => c.CarritoID == carritoID);
                if (carrito != null)
                {
                    carrito.EstadoCarrito = EstadoCerrado;
                    _context.Carrito.Update(carrito);
                }
                _context.CarritoDetalle.RemoveRange(carritoDetalles);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
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
                _logger.LogError(ex, "Error inesperado al procesar el carrito {CarritoId}", carritoID);
                return false;
            }
        }
    }
}
