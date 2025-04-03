using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Simone.Models;

namespace Simone.Data
{
    public class TiendaDbContext : IdentityDbContext<Usuario, IdentityRole, string>
    {
        public TiendaDbContext(DbContextOptions<TiendaDbContext> options)
            : base(options)
        {
        }

        // ✅ DbSet: Definición de tablas
        public DbSet<AsistenciaEmpleados> AsistenciaEmpleados { get; set; }
        public DbSet<AuditoriaProductos> AuditoriaProductos { get; set; }
        public DbSet<Carrito> Carrito { get; set; }
        public DbSet<CarritoDetalle> CarritoDetalle { get; set; }
        public DbSet<CatalogoEstados> CatalogoEstados { get; set; }
        public DbSet<Categorias> Categorias { get; set; }
        public DbSet<Cliente> Clientes { get; set; }
        public DbSet<Promocion> Promociones { get; set; }
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
        public DbSet<Pedido> Pedidos { get; set; }
        public DbSet<Producto> Productos { get; set; }
        public DbSet<ProgramasFidelizacion> ProgramasFidelizacion { get; set; }
        public DbSet<Proveedores> Proveedores { get; set; }
        public DbSet<Reseñas> Reseñas { get; set; }
        public DbSet<Subcategorias> Subcategorias { get; set; }
        public DbSet<Ventas> Ventas { get; set; }
        public DbSet<IdentityRole> IdentityRoles { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ✅ Configuración de claves y relaciones
            modelBuilder.Entity<IdentityRole>().HasKey(r => r.Id);

            modelBuilder.Entity<Compras>().HasKey(c => c.CompraID);
            modelBuilder.Entity<Comisiones>().HasKey(c => c.ComisionID);
            modelBuilder.Entity<ClientesProgramas>().HasKey(cp => new { cp.ClienteID, cp.ProgramaID });

            modelBuilder.Entity<Producto>()
                .HasOne(p => p.Proveedor)
                .WithMany(pr => pr.Productos)
                .HasForeignKey(p => p.ProveedorID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Producto>()
                .HasOne(p => p.Subcategoria)
                .WithMany(s => s.Productos)
                .HasForeignKey(p => p.SubcategoriaID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Subcategorias>()
                .HasKey(s => s.SubcategoriaID);

            modelBuilder.Entity<Subcategorias>()
                .HasOne(s => s.Categoria)
                .WithMany(c => c.Subcategoria)
                .HasForeignKey(s => s.CategoriaID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Reseñas>()
                .HasKey(r => r.ReseñaID);

            modelBuilder.Entity<Reseñas>()
                .HasOne(r => r.Cliente)
                .WithMany(c => c.Reseñas)
                .HasForeignKey(r => r.ClienteID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Reseñas>()
                .HasOne(r => r.Producto)
                .WithMany(p => p.Reseñas)
                .HasForeignKey(r => r.ProductoID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Usuario>()
                .HasOne(u => u.Rol)
                .WithMany(r => r.Usuarios)
                .HasForeignKey(u => u.RolID);

            modelBuilder.Entity<CuponesUsados>()
                .HasKey(cu => new { cu.ClienteID, cu.PromocionID });

            modelBuilder.Entity<CuponesUsados>()
                .HasOne(cu => cu.Cliente)
                .WithMany(c => c.CuponesUsados)
                .HasForeignKey(cu => cu.ClienteID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CuponesUsados>()
                .HasOne(cu => cu.Promocion)
                .WithMany(p => p.CuponesUsados)
                .HasForeignKey(cu => cu.PromocionID)
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

            // ✅ Tipos de datos financieros
            modelBuilder.Entity<Comisiones>().Property(c => c.MontoComision).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Comisiones>().Property(c => c.PorcentajeComision).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Compras>().Property(c => c.Total).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<DetalleVentas>().Property(d => d.Descuento).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<DetalleVentas>().Property(d => d.PrecioUnitario).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<DetalleVentas>().Property(d => d.Subtotal).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<DetallesCompra>().Property(d => d.PrecioUnitario).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<DetallesCompra>().Property(d => d.Subtotal).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<DetallesPedido>().Property(d => d.PrecioUnitario).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<DetallesPedido>().Property(d => d.Subtotal).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Empleados>().Property(e => e.Salario).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Gastos>().Property(g => g.Monto).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<HistorialPrecios>().Property(h => h.PrecioAnterior).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<HistorialPrecios>().Property(h => h.PrecioNuevo).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Pedido>().Property(p => p.Total).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Producto>().Property(p => p.PrecioCompra).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Producto>().Property(p => p.PrecioVenta).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<ProgramasFidelizacion>().Property(p => p.Descuento).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Promocion>().Property(p => p.Descuento).HasColumnType("decimal(18,2)");

            // ✅ Configuración para LogIniciosSesion
            modelBuilder.Entity<LogIniciosSesion>(entity =>
            {
                entity.HasKey(l => l.LogID);
                entity.Property(l => l.Usuario)
                      .IsRequired()
                      .HasMaxLength(150);
                entity.Property(l => l.FechaInicio)
                      .HasColumnType("datetime");
                entity.Property(l => l.Exitoso)
                      .IsRequired(false);
            });
        }
    }
}
