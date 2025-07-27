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
    public class CarritoService
    {
        private readonly TiendaDbContext _context;
        private readonly ILogger<CarritoService> _logger;
        private readonly UserManager<Usuario> _userManager;

        public CarritoService(TiendaDbContext context, ILogger<CarritoService> logger, UserManager<Usuario> user)
        {
            _context = context;
            _logger = logger;
            _userManager = user;
        }

        public async Task<bool> AddAsync(Usuario usuario)
        {
            try
            {
                Carrito carrito = new Carrito
                {
                    ClienteID = usuario.Id,
                    FechaCreacion = DateTime.Now,
                };

                await _context.Carrito.AddAsync(carrito);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error adding cart: {ex.Message}");
                return false;
            }
        }

        public async Task<List<Carrito>> GetAllAsync() =>
            await _context.Carrito.ToListAsync();

        public async Task<Carrito> GetByIdAsync(int id) =>
            await _context.Carrito.FindAsync(id);

        public async Task<bool> UpdateAsync(Carrito carrito)
        {
            try
            {
                _context.Carrito.Update(carrito);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating cart: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteAsync(int id)
        {
            try
            {
                var carrito = await _context.Carrito.FindAsync(id);
                if (carrito != null)
                {
                    _context.Carrito.Remove(carrito);
                    await _context.SaveChangesAsync();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting cart: {ex.Message}");
                return false;
            }
        }

        public async Task<Carrito> GetByClienteIdAsync(string clienteID) =>
            await _context.Carrito
                .Where(s => s.ClienteID == clienteID && s.EstadoCarrito != "Cerrado")
                .FirstOrDefaultAsync();

        public async Task<List<CarritoDetalle>> LoadCartDetails(int carritoID) =>
            await _context.CarritoDetalle
                .Where(c => c.CarritoID == carritoID)
                .ToListAsync();

        public async Task<bool> AnadirProducto(Producto producto, Usuario usuario, int cantidad)
        {
            try
            {
                var userCarrito = await _context.Carrito
                    .FirstOrDefaultAsync(c => c.ClienteID == usuario.Id && c.EstadoCarrito != "Cerrado");

                if (userCarrito == null)
                {
                    await AddAsync(usuario);
                }

                var userCarritoDetalle = await _context.CarritoDetalle
                    .Where(cd => cd.ProductoID == producto.ProductoID && cd.CarritoID == userCarrito.CarritoID)
                    .FirstOrDefaultAsync();

                if (userCarritoDetalle != null)
                {
                    userCarritoDetalle.Cantidad += cantidad;
                    _context.CarritoDetalle.Update(userCarritoDetalle);
                }
                else
                {
                    CarritoDetalle detalle = new CarritoDetalle
                    {
                        CarritoID = userCarrito.CarritoID,
                        Carrito = userCarrito,
                        ProductoID = producto.ProductoID,
                        Producto = producto,
                        Cantidad = cantidad,
                        Precio = producto.PrecioVenta,
                        FechaAgregado = DateTime.Now,
                    };

                    await _context.CarritoDetalle.AddAsync(detalle);
                }

                if (userCarrito.EstadoCarrito == "Vacio")
                {
                    userCarrito.EstadoCarrito = "En Uso";
                    _context.Carrito.Update(userCarrito);
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error adding product to cart: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> BorrarProductoCarrito(int id)
        {
            try
            {
                var detalle = await _context.CarritoDetalle.FindAsync(id);

                if (detalle != null)
                {
                    _context.CarritoDetalle.Remove(detalle);
                    await _context.SaveChangesAsync();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting product from cart: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ProcessCartDetails(int carritoID, Usuario user)
        {
            try
            {
                var carritoDetalles = await _context.CarritoDetalle
                    .Where(cd => cd.CarritoID == carritoID)
                    .Include(cd => cd.Producto)
                    .ToListAsync();

                if (!carritoDetalles.Any())
                {
                    return false;
                }

                foreach (var detalle in carritoDetalles)
                {
                    var producto = detalle.Producto;
                    if (producto.Stock >= detalle.Cantidad)
                    {
                        producto.Stock -= detalle.Cantidad;

                        var movimientoInventario = new MovimientosInventario
                        {
                            ProductoID = producto.ProductoID,
                            Cantidad = detalle.Cantidad,
                            TipoMovimiento = "Salida",
                            FechaMovimiento = DateTime.Now,
                            Descripcion = "Venta - Compra realizada en carrito"
                        };

                        await _context.MovimientosInventario.AddAsync(movimientoInventario);
                    }
                    else
                    {
                        return false;
                    }
                }

                var venta = new Ventas
                {
                    EmpleadoID = (await _userManager.Users.FirstOrDefaultAsync(u => u.UserName == "admin@tienda.com"))?.Id,
                    Estado = "Completada",
                    ClienteID = user.Id,
                    FechaVenta = DateTime.Now,
                    MetodoPago = "Transferencia",
                    Total = carritoDetalles.Sum(cd => cd.Total)
                };

                await _context.Ventas.AddAsync(venta);
                var carrito = await _context.Carrito.FindAsync(carritoID);
                if (carrito != null)
                {
                    carrito.EstadoCarrito = "Cerrado";
                    _context.Carrito.Update(carrito);
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing cart details: {ex.Message}");
                return false;
            }
        }
    }
}
