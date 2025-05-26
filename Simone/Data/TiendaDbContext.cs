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

        // ✅ Tablas del sistema
        public DbSet<Usuario> Usuarios { get; set; }

        // ✅ Tablas del dominio
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
        public DbSet<Favorito> Favoritos { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Claves primarias compuestas
            modelBuilder.Entity<ClientesProgramas>().HasKey(cp => new { cp.ClienteID, cp.ProgramaID });
            modelBuilder.Entity<CuponesUsados>().HasKey(cu => new { cu.ClienteID, cu.PromocionID });

            // Relaciones de Producto
            modelBuilder.Entity<Producto>()
                .HasOne(p => p.Proveedor)
                .WithMany(pr => pr.Productos)
                .HasForeignKey(p => p.ProveedorID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Producto>()
                .HasOne(p => p.Subcategoria)
                .WithMany(s => s.Productos)
                .HasForeignKey(p => p.SubcategoriaID)
                .OnDelete(DeleteBehavior.Restrict);

            // Subcategoría con categoría
            modelBuilder.Entity<Subcategorias>()
                .HasOne(s => s.Categoria)
                .WithMany(c => c.Subcategoria)
                .HasForeignKey(s => s.CategoriaID)
                .OnDelete(DeleteBehavior.Restrict);

            // Reseñas con cliente y producto
            modelBuilder.Entity<Reseñas>()
                .HasOne(r => r.Cliente)
                .WithMany(c => c.Reseñas)
                .HasForeignKey(r => r.ClienteID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Reseñas>()
                .HasOne(r => r.Producto)
                .WithMany(p => p.Reseñas)
                .HasForeignKey(r => r.ProductoID)
                .OnDelete(DeleteBehavior.Restrict);

            //Relación Usuario → Rol(solo si tienes clase Rol)
            //  modelBuilder.Entity<Usuario>()
            //      .HasOne(u => u.Rol)
            //      .WithMany(r => r.Usuarios)
            //      .HasForeignKey(u => u.RolID);

            // Cupones usados
            modelBuilder.Entity<CuponesUsados>()
                .HasOne(cu => cu.Cliente)
                .WithMany(c => c.CuponesUsados)
                .HasForeignKey(cu => cu.ClienteID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CuponesUsados>()
                .HasOne(cu => cu.Promocion)
                .WithMany(p => p.CuponesUsados)
                .HasForeignKey(cu => cu.PromocionID)
                .OnDelete(DeleteBehavior.Restrict);

            // DetalleVentas
            modelBuilder.Entity<DetalleVentas>()
                .HasOne(dv => dv.Venta)
                .WithMany(v => v.DetalleVentas)
                .HasForeignKey(dv => dv.VentaID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DetalleVentas>()
                .HasOne(dv => dv.Producto)
                .WithMany(p => p.DetalleVentas)
                .HasForeignKey(dv => dv.ProductoID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Favorito>()
            .HasOne(f => f.Usuario)
            .WithMany()
            .HasForeignKey(f => f.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict); 

            // Tipos DECIMAL para campos financieros
            var decimalProps = new (Type entity, string[] props)[]
            {
                (typeof(Comisiones), new[] { "MontoComision", "PorcentajeComision" }),
                (typeof(Compras), new[] { "Total" }),
                (typeof(DetalleVentas), new[] { "Descuento", "PrecioUnitario", "Subtotal" }),
                (typeof(DetallesCompra), new[] { "PrecioUnitario", "Subtotal" }),
                (typeof(DetallesPedido), new[] { "PrecioUnitario", "Subtotal" }),
                (typeof(Empleados), new[] { "Salario" }),
                (typeof(Gastos), new[] { "Monto" }),
                (typeof(HistorialPrecios), new[] { "PrecioAnterior", "PrecioNuevo" }),
                (typeof(Pedido), new[] { "Total" }),
                (typeof(Producto), new[] { "PrecioCompra", "PrecioVenta" }),
                (typeof(ProgramasFidelizacion), new[] { "Descuento" }),
                (typeof(Promocion), new[] { "Descuento" }),
            };

            foreach (var (entity, props) in decimalProps)
            {
                foreach (var prop in props)
                {
                    modelBuilder.Entity(entity).Property(prop).HasColumnType("decimal(18,2)");
                }
            }

            // Configuración especial: LogIniciosSesion
            modelBuilder.Entity<LogIniciosSesion>(entity =>
            {
                entity.HasKey(l => l.LogID);
                entity.Property(l => l.Usuario).IsRequired().HasMaxLength(150);
                entity.Property(l => l.FechaInicio).HasColumnType("datetime");
                entity.Property(l => l.Exitoso).IsRequired(false);
            });
        }
    }
}
