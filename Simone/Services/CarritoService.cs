using Simone.Models;
using Simone.Data;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
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

        // Agregar un nuevo elemento a un carrito
        public async Task<bool> AddAsync(Usuario usuario)
        {
            try
            {
                Carrito carrito = new Carrito
                {
                    ClienteID = usuario.Id,
                    FechaCreacion = DateTime.Now,
                };

                await _context.Carrito.AddAsync(carrito); // Usamos AddAsync
                await _context.SaveChangesAsync(); // Guardar los cambios de manera asíncrona
                return true; // Retorna true si la categoría se ha agregado correctamente
            }
            catch
            {
                return false; // Retorna false si hubo algún error
            }
        }

        // Obtener todos los carritos de manera asíncrona
        public async Task<List<Carrito>> GetAllAsync()
        {
            return await _context.Carrito.ToListAsync(); // Usamos ToListAsync
        }

        // Obtener una categoría por su ID de manera asíncrona
        public async Task<Carrito> GetByIdAsync(int id)
        {
            return await _context.Carrito.FindAsync(id); // Usamos FindAsync
        }

        // Actualizar un carrito de manera asíncrona
        public async Task<bool> UpdateAsync(Carrito carrito)
        {
            try
            {
                _context.Carrito.Update(carrito); // Actualizar categoría
                await _context.SaveChangesAsync(); // Guardar los cambios de manera asíncrona
                return true; // Retorna true si la categoría se actualiza correctamente
            }
            catch
            {
                return false; // Retorna false si hubo algún error
            }
        }

        // Eliminar una categoría de manera asíncrona
        public async Task<bool> DeleteAsync(int id)
        {
            try
            {
                var carrito = await _context.Carrito.FindAsync(id); // Buscar la categoría de manera asíncrona
                if (carrito != null)
                {
                    _context.Carrito.Remove(carrito); // Eliminar categoría
                    await _context.SaveChangesAsync(); // Guardar cambios de manera asíncrona
                    return true; // Retorna true si la categoría se elimina correctamente
                }
                return false; // Retorna false si no se encuentra la categoría
            }
            catch
            {
                return false; // Retorna false si hubo algún error
            }
        }

        public async Task<Carrito> GetByClienteIdAsync(string clienteID)
        {
            return await _context.Carrito
                                 .Where(s => s.ClienteID == clienteID)
                                 .Where(s => s.EstadoCarrito != "Cerrado")
                                 .FirstOrDefaultAsync();
        }

        public async Task<List<CarritoDetalle>> LoadCartDetails(int carritoID)
        {
            return await _context.CarritoDetalle
                                 .Where(c => c.CarritoID == carritoID)
                                 .ToListAsync();
        }

        public async Task<bool> AnadirProducto(Producto producto, Usuario usuario, int cantidad)
        {
            try
            {
                // Find the user's cart that is not "Cerrado"
                var userCarrito = await _context.Carrito
                    .FirstOrDefaultAsync(c => c.ClienteID == usuario.Id && c.EstadoCarrito != "Cerrado");

                if (userCarrito == null)
                {
                    await AddAsync(usuario);
                }

                // Check if the product already exists in the cart
                var userCarritoDetalle = await _context.CarritoDetalle
                    .Where(cd => cd.ProductoID == producto.ProductoID && cd.CarritoID == userCarrito.CarritoID)
                    .FirstOrDefaultAsync();

                if (userCarritoDetalle != null)
                {
                    // If the product already exists, update the quantity
                    userCarritoDetalle.Cantidad += cantidad; // Add the new quantity to the existing one
                    _context.CarritoDetalle.Update(userCarritoDetalle); // Update the entry in the database
                }
                else
                {
                    // If the product does not exist, create a new entry in the cart
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

                    await _context.CarritoDetalle.AddAsync(detalle); // Add the new product to the cart
                }

                // Update Estado carrito to "En Uso"
                if (userCarrito.EstadoCarrito == "Vacio")
                {
                    userCarrito.EstadoCarrito = "En Uso";
                    _context.Carrito.Update(userCarrito);
                }

                // Save changes asynchronously
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                // Handle the error and log if necessary
                return false; // Return false if any exception occurs
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
                else
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }

        }

        public async Task<bool> ProcessCartDetails(int carritoID, Usuario user)
        {
            try
            {
                // Step 1: Retrieve all cart details for the given carritoID
                var carritoDetalles = await _context.CarritoDetalle
                    .Where(cd => cd.CarritoID == carritoID)
                    .Include(cd => cd.Producto) // Ensure the product data is loaded
                    .ToListAsync();

                if (carritoDetalles == null || !carritoDetalles.Any())
                {
                    // No products in the cart, return false
                    return false;
                }

                // Step 2: Decrease stock for each product in the cart
                foreach (var detalle in carritoDetalles)
                {
                    var producto = detalle.Producto;
                    if (producto != null && producto.Stock >= detalle.Cantidad)
                    {
                        // Update the product stock
                        producto.Stock -= detalle.Cantidad;

                        // Step 3: Log the inventory movement
                        var movimientoInventario = new MovimientosInventario
                        {
                            ProductoID = producto.ProductoID,
                            Cantidad = detalle.Cantidad,
                            TipoMovimiento = "Salida", // Stock decrement
                            FechaMovimiento = DateTime.Now,
                            Descripcion = "Venta - Compra realizada en carrito"
                        };
                        await _context.MovimientosInventario.AddAsync(movimientoInventario);
                    }
                    else
                    {
                        // If there's not enough stock, return false
                        return false;
                    }
                }

                // Step 4: Create the Venta (Sale) record

                var adminUser = await _userManager.Users
                    .FirstOrDefaultAsync(u => u.UserName == "admin@tienda.com");

                if (adminUser == null)
                {
                    throw new Exception("Admin user not found");
                }

                var venta = new Ventas
                {
                    EmpleadoID = adminUser.Id, // Default to admin user
                    Estado = "Completada", // Sale status
                    ClienteID = user.Id,
                    FechaVenta = DateTime.Now,
                    MetodoPago = "Tarjeta de Débito", // Default payment method
                    Total = carritoDetalles.Sum(cd => cd.Total) // Calculate the total of the sale
                };

                await _context.Ventas.AddAsync(venta);

                // Step 5: Optionally, you can mark the cart as completed, if necessary
                var carrito = await _context.Carrito.FindAsync(carritoID);
                if (carrito != null)
                {
                    carrito.EstadoCarrito = "Cerrado"; // Mark the cart as closed
                    _context.Carrito.Update(carrito);
                }

                await _context.SaveChangesAsync();

                // Return true if everything was successful
                return true;
            }
            catch (Exception ex)
            {
                // Log any errors for debugging
                _logger.LogError($"Error processing cart details: {ex.Message}");
                return false;
            }
        }


    }
}
