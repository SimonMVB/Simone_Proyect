using Microsoft.EntityFrameworkCore;
using Simone.Models;

namespace Simone.Data
{
    public class TiendaDbContext : DbContext
    {
        public TiendaDbContext(DbContextOptions<TiendaDbContext> options)
            : base(options)
        {
        }

        // ✅ Definición de todas las tablas de la base de datos
        public DbSet<AsistenciaEmpleados> AsistenciaEmpleados { get; set; }
        public DbSet<AuditoriaProductos> AuditoriaProductos { get; set; }
        public DbSet<Carrito> Carrito { get; set; }
        public DbSet<CarritoDetalle> CarritoDetalle { get; set; }
        public DbSet<CatalogoEstados> CatalogoEstados { get; set; }
        public DbSet<Categorias> Categorias { get; set; }
        public DbSet<Cliente> Clientes { get; set; }
        public DbSet<ClientesProgramas> ClientesProgramas { get; set; }
        public DbSet<Comisiones> Comisiones { get; set; }
        public DbSet<Compras> Compras { get; set; }
        public DbSet<CuponesUsados> CuponesUsados { get; set; }
        public DbSet<DetallesCompra> DetallesCompra { get; set; }
        public DbSet<DetallesPedido> DetallesPedido { get; set; }
        public DbSet<DetalleVentas> DetalleVentas { get; set; }
        public DbSet<Devoluciones> Devoluciones { get; set; }
        public DbSet<DireccionesCliente> DireccionesCliente { get; set; }
        public DbSet<Empleados> Empleados { get; set; }
        public DbSet<Gastos> Gastos { get; set; }
        public DbSet<HistorialPrecios> HistorialPrecios { get; set; }
        public DbSet<ImagenesProductos> ImagenesProductos { get; set; }
        public DbSet<LogIniciosSesion> LogIniciosSesion { get; set; }
        public DbSet<MovimientosInventario> MovimientosInventario { get; set; }
        public DbSet<Pedidos> Pedidos { get; set; }
        public DbSet<Productos> Productos { get; set; }
        public DbSet<ProgramasFidelizacion> ProgramasFidelizacion { get; set; }
        public DbSet<Promociones> Promociones { get; set; }
        public DbSet<Proveedores> Proveedores { get; set; }
        public DbSet<Reseñas> Reseñas { get; set; }
        public DbSet<Roles> Roles { get; set; }
        public DbSet<Subcategorias> Subcategorias { get; set; }
        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Ventas> Ventas { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ✅ Relación: CarritoDetalle con Carrito y Producto
            modelBuilder.Entity<CarritoDetalle>()
                .HasOne(cd => cd.Carrito)
                .WithMany(c => c.CarritoDetalles)
                .HasForeignKey(cd => cd.CarritoID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CarritoDetalle>()
                .HasOne(cd => cd.Producto)
                .WithMany(p => p.CarritoDetalles)
                .HasForeignKey(cd => cd.ProductoID)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<DetalleVentas>()
            .HasOne(dv => dv.Venta)
           .WithMany(v => v.DetalleVentas)
           .HasForeignKey(dv => dv.VentaID)
          .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DetalleVentas>()
                .HasOne(dv => dv.Producto)
                .WithMany(p => p.DetalleVentas)
                .HasForeignKey(dv => dv.ProductoID)
                .OnDelete(DeleteBehavior.Cascade);

            // ✅ Relación: DetallesCompra con Compras y Producto
            modelBuilder.Entity<DetallesCompra>()
                .HasOne(dc => dc.Compra)
                .WithMany(c => c.DetallesCompra)
                .HasForeignKey(dc => dc.CompraID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DetallesCompra>()
                .HasOne(dc => dc.Producto)
                .WithMany(p => p.DetallesCompra)
                .HasForeignKey(dc => dc.ProductoID)
                .OnDelete(DeleteBehavior.Cascade);

            // ✅ Relación: Pedidos con Clientes
            modelBuilder.Entity<Pedidos>()
                .HasOne(p => p.Cliente)
                .WithMany(c => c.Pedidos)
                .HasForeignKey(p => p.ClienteID);

            // ✅ Relación: Productos con Proveedores y Subcategorías
            modelBuilder.Entity<Productos>()
                .HasOne(p => p.Proveedor)
                .WithMany(pr => pr.Productos)
                .HasForeignKey(p => p.ProveedorID);

            modelBuilder.Entity<Productos>()
                .HasOne(p => p.Subcategoria)
                .WithMany(s => s.Productos)
                .HasForeignKey(p => p.SubcategoriaID);

            // ✅ Relación: Reseñas con Clientes y Productos
            modelBuilder.Entity<Reseñas>()
                .HasOne(r => r.Clientes)
                .WithMany(c => c.Reseñas)
                .HasForeignKey(r => r.ClienteID);

            modelBuilder.Entity<Reseñas>()
                .HasOne(r => r.Producto)
                .WithMany(p => p.Reseñas)
                .HasForeignKey(r => r.ProductoID);

            // ✅ Relación: Usuarios con Roles
            modelBuilder.Entity<Usuario>()
                .HasOne(u => u.Rol)
                .WithMany(r => r.Usuarios)
                .HasForeignKey(u => u.RolID);

modelBuilder.Entity<Comisiones>()
    .HasKey(c => c.ComisionID); // ✅ Primero definir la clave primaria

modelBuilder.Entity<Comisiones>()
    .HasOne(c => c.Empleado)
    .WithMany(e => e.Comisiones)
    .HasForeignKey(c => c.EmpleadoID)
    .OnDelete(DeleteBehavior.Cascade);


            modelBuilder.Entity<Gastos>()
                .HasOne(g => g.Empleado)
                .WithMany(e => e.Gastos)
                .HasForeignKey(g => g.EmpleadoID)
                .OnDelete(DeleteBehavior.Cascade);

            // ✅ Relación: MovimientosInventario con Productos
            modelBuilder.Entity<MovimientosInventario>()
                .HasOne(mi => mi.Producto)
                .WithMany(p => p.MovimientosInventario)
                .HasForeignKey(mi => mi.ProductoID)
                .OnDelete(DeleteBehavior.Cascade);


            modelBuilder.Entity<ClientesProgramas>()
             .HasKey(cp => new { cp.ClienteID, cp.ProgramaID });  // Definir clave compuesta

        

        // ✅ Corrección de método para obtener detalles de compra
        private static List<DetallesCompra> GetDetallesCompra(Compras c)
        {
            return c.DetallesCompra?.ToList() ?? new List<DetallesCompra>();
        }
    }
}
