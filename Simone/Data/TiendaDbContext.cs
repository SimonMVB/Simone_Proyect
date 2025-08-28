using System; // Necesario para Type en mapeos y DateTime
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

        // ✅ Tablas del sistema / Identity
        public DbSet<Usuario> Usuarios { get; set; }

        // ✅ Tablas del dominio
        public DbSet<ActividadUsuario> ActividadesUsuarios { get; set; }
        public DbSet<LogIniciosSesion> LogIniciosSesion { get; set; }
        public DbSet<AsistenciaEmpleados> AsistenciaEmpleados { get; set; }
        public DbSet<AuditoriaProductos> AuditoriaProductos { get; set; }
        public DbSet<LogActividad> LogsActividad { get; set; }
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
        public DbSet<MovimientosInventario> MovimientosInventario { get; set; }
        public DbSet<Pedido> Pedidos { get; set; }
        public DbSet<Producto> Productos { get; set; }
        public DbSet<ProgramasFidelizacion> ProgramasFidelizacion { get; set; }
        public DbSet<Proveedores> Proveedores { get; set; }
        public DbSet<Reseñas> Reseñas { get; set; } // Mantiene el nombre del modelo con ñ
        public DbSet<Subcategorias> Subcategorias { get; set; }
        public DbSet<Ventas> Ventas { get; set; }
        public DbSet<Favorito> Favoritos { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ------------------------------------------------------------
            // 1) Claves primarias compuestas (N:M)
            // ------------------------------------------------------------
            modelBuilder.Entity<ClientesProgramas>()
                .HasKey(cp => new { cp.ClienteID, cp.ProgramaID });

            modelBuilder.Entity<CuponesUsados>()
                .HasKey(cu => new { cu.ClienteID, cu.PromocionID });

            // ------------------------------------------------------------
            // 2) Reglas de negocio: evitar duplicados con índices únicos
            //    - Un producto solo una vez por Carrito
            //    - Un producto solo una vez por Usuario en Favoritos
            // ------------------------------------------------------------
            modelBuilder.Entity<CarritoDetalle>()
                .HasIndex(cd => new { cd.CarritoID, cd.ProductoID })
                .IsUnique();

            modelBuilder.Entity<Favorito>()
                .HasIndex(f => new { f.UsuarioId, f.ProductoId })
                .IsUnique();

            // ------------------------------------------------------------
            // 3) Relaciones y DeleteBehavior explícitos
            //    (evitar cascadas peligrosas, mantener históricos)
            // ------------------------------------------------------------

            // CarritoDetalle → Producto (Restrict)
            modelBuilder.Entity<CarritoDetalle>()
                .HasOne(cd => cd.Producto)
                .WithMany(p => p.CarritoDetalles)
                .HasForeignKey(cd => cd.ProductoID)
                .OnDelete(DeleteBehavior.Restrict);

            // CarritoDetalle → Carrito (Restrict)
            modelBuilder.Entity<CarritoDetalle>()
                .HasOne(cd => cd.Carrito)
                .WithMany(c => c.CarritoDetalles)
                .HasForeignKey(cd => cd.CarritoID)
                .OnDelete(DeleteBehavior.Restrict);

            // Producto → Proveedor/Subcategoria/Categoria (Restrict)
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

            modelBuilder.Entity<Subcategorias>()
                .HasOne(s => s.Categoria)
                .WithMany(c => c.Subcategoria)
                .HasForeignKey(s => s.CategoriaID)
                .OnDelete(DeleteBehavior.Restrict);

            // Reseñas → Cliente/Producto (Restrict)
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

            // DetalleVentas → Venta/Producto (Restrict)
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

            // Favorito → Usuario (Restrict)
            modelBuilder.Entity<Favorito>()
                .HasOne(f => f.Usuario)
                .WithMany() // sin navegación inversa en Usuario
                .HasForeignKey(f => f.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);

            // Ventas → Cliente (Restrict) (sin navegación en modelo Ventas)
            modelBuilder.Entity<Ventas>()
                .HasOne<Cliente>()          // especifica el tipo relacionado
                .WithMany()                 // sin navegación inversa en Cliente
                .HasForeignKey(v => v.ClienteID)
                .OnDelete(DeleteBehavior.Restrict);

            // ------------------------------------------------------------
            // 4) Estandarizar DECIMAL(18,2) en campos financieros
            //    (añadimos Ventas.Total a la lista)
            // ------------------------------------------------------------
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
                (typeof(Ventas), new[] { "Total" }), // ← añadido
            };

            foreach (var (entity, props) in decimalProps)
            {
                foreach (var prop in props)
                {
                    modelBuilder.Entity(entity)
                                .Property(prop)
                                .HasColumnType("decimal(18,2)");
                }
            }

            // ------------------------------------------------------------
            // 5) Defaults en BD para timestamps (robustez fuera de la app)
            // ------------------------------------------------------------
            modelBuilder.Entity<Favorito>()
                .Property(f => f.FechaGuardado)
                .HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<CarritoDetalle>()
                .Property(cd => cd.FechaAgregado)
                .HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<Cliente>()
                .Property(c => c.FechaRegistro)
                .HasDefaultValueSql("GETDATE()"); // Usa GETUTCDATE() si quieres todo en UTC

            // ------------------------------------------------------------
            // 6) Configuración específica de logs/auditoría
            // ------------------------------------------------------------
            modelBuilder.Entity<LogIniciosSesion>(entity =>
            {
                entity.HasKey(l => l.LogID);
                entity.Property(l => l.Usuario).IsRequired().HasMaxLength(150);
                entity.Property(l => l.FechaInicio).HasColumnType("datetime");
                entity.Property(l => l.Exitoso).IsRequired(false);
                // (Opcional futuro) FK nullable a UsuarioId si decides vincularlo
            });

            modelBuilder.Entity<ActividadUsuario>()
                .HasOne(a => a.Usuario)
                .WithMany(u => u.Actividades) // navegación definida en Usuario
                .HasForeignKey(a => a.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
