using Simone.Models;
using Simone.Data;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity;

namespace Simone.Services
{
    public interface ICarritoService
    {
        Task<bool> AddAsync(Usuario usuario);
        Task<List<Carrito>> GetAllAsync();
        Task<Carrito> GetByIdAsync(int id);
        Task<bool> UpdateAsync(Carrito carrito);
        Task<bool> DeleteAsync(int id);
        Task<Carrito> GetByClienteIdAsync(string clienteID);
        Task<List<CarritoDetalle>> LoadCartDetails(int carritoID);
        Task<bool> AnadirProducto(Producto producto, Usuario usuario, int cantidad);
        Task<bool> BorrarProductoCarrito(int id);
        Task<bool> ProcessCartDetails(int carritoID, Usuario user);
    }

    public class CarritoService : ICarritoService
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

        public async Task<bool> AddAsync(Usuario usuario)
        {
            if (usuario == null) throw new ArgumentNullException(nameof(usuario));

            try
            {
                var carrito = new Carrito
                {
                    ClienteID = usuario.Id,
                    FechaCreacion = DateTime.UtcNow,
                    EstadoCarrito = EstadoVacio
                };

                await _context.Carrito.AddAsync(carrito);
                return await _context.SaveChangesAsync() > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al agregar carrito para el usuario {UsuarioId}", usuario.Id);
                return false;
            }
        }

        public async Task<List<Carrito>> GetAllAsync() => 
            await _context.Carrito.AsNoTracking().ToListAsync();

        public async Task<Carrito> GetByIdAsync(int id) => 
            await _context.Carrito.AsNoTracking().FirstOrDefaultAsync(c => c.CarritoID == id);

        public async Task<bool> UpdateAsync(Carrito carrito)
        {
            if (carrito == null) throw new ArgumentNullException(nameof(carrito));

            try
            {
                _context.Carrito.Update(carrito);
                return await _context.SaveChangesAsync() > 0;
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
                var carrito = await _context.Carrito.FindAsync(id);
                if (carrito == null) return false;

                _context.Carrito.Remove(carrito);
                return await _context.SaveChangesAsync() > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar carrito {CarritoId}", id);
                return false;
            }
        }

        public async Task<Carrito> GetByClienteIdAsync(string clienteID)
        {
            if (string.IsNullOrWhiteSpace(clienteID)) 
                throw new ArgumentException("ClienteID no puede estar vacío", nameof(clienteID));

            return await _context.Carrito
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.ClienteID == clienteID && c.EstadoCarrito != EstadoCerrado);
        }

        public async Task<List<CarritoDetalle>> LoadCartDetails(int carritoID) =>
            await _context.CarritoDetalle
                .AsNoTracking()
                .Where(c => c.CarritoID == carritoID)
                .Include(cd => cd.Producto)
                .ToListAsync();

        public async Task<bool> AnadirProducto(Producto producto, Usuario usuario, int cantidad)
        {
            if (producto == null) throw new ArgumentNullException(nameof(producto));
            if (usuario == null) throw new ArgumentNullException(nameof(usuario));
            if (cantidad <= 0) throw new ArgumentException("La cantidad debe ser mayor que cero", nameof(cantidad));

            try
            {
                var userCarrito = await _context.Carrito
                    .FirstOrDefaultAsync(c => c.ClienteID == usuario.Id && c.EstadoCarrito != EstadoCerrado);

                if (userCarrito == null)
                {
                    if (!await AddAsync(usuario)) return false;
                    userCarrito = await GetByClienteIdAsync(usuario.Id);
                }

                var userCarritoDetalle = await _context.CarritoDetalle
                    .FirstOrDefaultAsync(cd => cd.ProductoID == producto.ProductoID && cd.CarritoID == userCarrito.CarritoID);

                if (userCarritoDetalle != null)
                {
                    userCarritoDetalle.Cantidad += cantidad;
                    _context.CarritoDetalle.Update(userCarritoDetalle);
                }
                else
                {
                    var detalle = new CarritoDetalle
                    {
                        CarritoID = userCarrito.CarritoID,
                        ProductoID = producto.ProductoID,
                        Cantidad = cantidad,
                        Precio = producto.PrecioVenta,
                        FechaAgregado = DateTime.UtcNow,
                    };

                    await _context.CarritoDetalle.AddAsync(detalle);
                }

                if (userCarrito.EstadoCarrito == EstadoVacio)
                {
                    userCarrito.EstadoCarrito = EstadoEnUso;
                    _context.Carrito.Update(userCarrito);
                }

                return await _context.SaveChangesAsync() > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al añadir producto {ProductoId} al carrito del usuario {UsuarioId}", 
                    producto.ProductoID, usuario.Id);
                return false;
            }
        }

        public async Task<bool> BorrarProductoCarrito(int id)
        {
            try
            {
                var detalle = await _context.CarritoDetalle.FindAsync(id);
                if (detalle == null) return false;

                _context.CarritoDetalle.Remove(detalle);
                return await _context.SaveChangesAsync() > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al borrar producto del carrito {DetalleId}", id);
                return false;
            }
        }

        public async Task<bool> ProcessCartDetails(int carritoID, Usuario user)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var carritoDetalles = await _context.CarritoDetalle
                    .Where(cd => cd.CarritoID == carritoID)
                    .Include(cd => cd.Producto)
                    .ToListAsync();

                if (!carritoDetalles.Any())
                {
                    _logger.LogWarning("No hay productos en el carrito {CarritoId} para procesar", carritoID);
                    return false;
                }

                // ✅ Asegurar que el cliente exista (autocreación si no existe)
                var cliente = await _context.Clientes.FirstOrDefaultAsync(c => c.Email == user.Email);
                if (cliente == null)
                {
                    _logger.LogWarning("No se encontró cliente con email {Email}. Se creará automáticamente.", user.Email);

                    cliente = new Cliente
                    {
                        Nombre = string.IsNullOrWhiteSpace(user.NombreCompleto) ? user.Email : user.NombreCompleto,
                        Email = user.Email,
                        Telefono = string.IsNullOrWhiteSpace(user.PhoneNumber) ? user.Telefono : user.PhoneNumber,
                        Direccion = user.Direccion ?? string.Empty,
                        FechaRegistro = DateTime.UtcNow
                    };

                    await _context.Clientes.AddAsync(cliente);
                    await _context.SaveChangesAsync(); // genera ClienteID
                    _logger.LogInformation("Cliente creado: {ClienteId} para {Email}", cliente.ClienteID, user.Email);
                }

                // Validar stock antes de hacer cualquier cambio
                foreach (var detalle in carritoDetalles)
                {
                    if (detalle.Producto.Stock < detalle.Cantidad)
                    {
                        _logger.LogWarning("Stock insuficiente para el producto {ProductoId}", detalle.Producto.ProductoID);
                        return false;
                    }
                }

                // Descontar stock + registrar movimiento
                foreach (var detalle in carritoDetalles)
                {
                    detalle.Producto.Stock -= detalle.Cantidad;

                    await _context.MovimientosInventario.AddAsync(new MovimientosInventario
                    {
                        ProductoID = detalle.ProductoID,
                        Cantidad = detalle.Cantidad,
                        TipoMovimiento = "Salida",
                        FechaMovimiento = DateTime.UtcNow,
                        Descripcion = "Venta - Compra realizada en carrito"
                    });
                }

                // Vendedor responsable (admin por defecto)
                var admin = await _userManager.Users.FirstOrDefaultAsync(u => u.UserName == AdminEmail);

                // Crear venta
                var venta = new Ventas
                {
                    EmpleadoID = admin?.Id,
                    Estado = "Completada",
                    ClienteID = cliente.ClienteID,
                    FechaVenta = DateTime.UtcNow,
                    MetodoPago = "Transferencia", // si tu UI dice "Depósito", unifica el texto luego
                    Total = carritoDetalles.Sum(cd => cd.Cantidad * cd.Precio)
                };
                await _context.Ventas.AddAsync(venta);

                // Detalles de venta
                var detallesVenta = carritoDetalles.Select(detalle => new DetalleVentas
                {
                    VentaID = venta.VentaID, // EF seteará al guardar; con Venta = venta también vale
                    Venta = venta,
                    ProductoID = detalle.ProductoID,
                    Cantidad = detalle.Cantidad,
                    PrecioUnitario = detalle.Precio,
                    Descuento = 0,
                    Subtotal = detalle.Cantidad * detalle.Precio,
                    FechaCreacion = DateTime.UtcNow
                }).ToList();
                await _context.DetalleVentas.AddRangeAsync(detallesVenta);

                // Cerrar carrito y limpiar detalles
                var carrito = await _context.Carrito.FindAsync(carritoID);
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
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error al procesar el carrito {CarritoId}", carritoID);
                return false;
            }
        }

    }
}