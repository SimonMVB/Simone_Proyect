using System; // Necesario para Type en mapeos y DateTime
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Simone.Models;

namespace Simone.Data
{
    public class TiendaDbContext : IdentityDbContext<Usuario, Roles, string>
    {
        public TiendaDbContext(DbContextOptions<TiendaDbContext> options)
            : base(options)
        {
        }

        // ✅ Tablas del sistema / Identity
        public DbSet<Usuario> Usuarios { get; set; }

        // ✅ Tablas del dominio (existentes)
        public DbSet<ActividadUsuario> ActividadesUsuarios { get; set; }
        public DbSet<LogIniciosSesion> LogIniciosSesion { get; set; }
        public DbSet<AsistenciaEmpleados> AsistenciaEmpleados { get; set; }
        public DbSet<AuditoriaProductos> AuditoriaProductos { get; set; }
        public DbSet<LogActividad> LogsActividad { get; set; }
        public DbSet<Carrito> Carrito { get; set; }
        public DbSet<CarritoDetalle> CarritoDetalle { get; set; }
        public DbSet<CatalogoEstados> CatalogoEstados { get; set; }
        public DbSet<Categorias> Categorias { get; set; }
        public DbSet<Promocion> Promociones { get; set; }
        public DbSet<ClientesProgramas> ClientesProgramas { get; set; }
        public DbSet<Comisiones> Comisiones { get; set; }
        public DbSet<Compras> Compras { get; set; }
        public DbSet<CuponesUsados> CuponesUsados { get; set; }
        public DbSet<DetallesCompra> DetallesCompra { get; set; }
        public DbSet<DetallesPedido> DetallesPedido { get; set; }
        public DbSet<DetalleVentas> DetalleVentas { get; set; }
        public DbSet<Devoluciones> Devoluciones { get; set; } = default!;
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
        public DbSet<VentaReversion> VentaReversiones { get; set; }

        // ✅ NUEVO: Multi-vendedor (según Models/Vendedor.cs)
        public DbSet<Vendedor> Vendedores { get; set; }
        public DbSet<Banco> Bancos { get; set; }
        public DbSet<CuentaBancaria> CuentasBancarias { get; set; }
        public DbSet<ContactoTienda> ContactosTiendas { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ------------------------------------------------------------
            // 0) Usuario.Cedula → longitud/columna e índice único (opcional)
            // ------------------------------------------------------------
            modelBuilder.Entity<Usuario>(entity =>
            {
                entity.Property(u => u.Cedula)
                      .HasMaxLength(10)
                      .HasColumnType("nvarchar(10)");

                // Única si no es null (SQL Server)
                entity.HasIndex(u => u.Cedula)
                      .IsUnique()
                      .HasFilter("[Cedula] IS NOT NULL");
            });

            // ------------------------------------------------------------
            // 1) Claves primarias compuestas (N:M) + FKs
            // ------------------------------------------------------------
            modelBuilder.Entity<Devoluciones>()
               .HasOne(d => d.DetalleVenta)
               .WithMany(v => v.Devoluciones)
               .HasForeignKey(d => d.DetalleVentaID)
               .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ClientesProgramas>(entity =>
            {
                entity.HasKey(cp => new { cp.UsuarioId, cp.ProgramaID });

                entity.HasOne(cp => cp.Usuario)
                      .WithMany()
                      .HasForeignKey(cp => cp.UsuarioId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(cp => cp.Programa)
                      .WithMany()
                      .HasForeignKey(cp => cp.ProgramaID)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<CuponesUsados>(entity =>
            {
                entity.HasKey(cu => new { cu.UsuarioId, cu.PromocionID });

                entity.HasOne(cu => cu.Usuario)
                      .WithMany()
                      .HasForeignKey(cu => cu.UsuarioId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ------------------------------------------------------------
            // 2) Índices únicos de negocio
            // ------------------------------------------------------------
            modelBuilder.Entity<CarritoDetalle>()
                .HasIndex(cd => new { cd.CarritoID, cd.ProductoID })
                .IsUnique();

            modelBuilder.Entity<Favorito>()
                .HasIndex(f => new { f.UsuarioId, f.ProductoId })
                .IsUnique();

            // ------------------------------------------------------------
            // 3) Relaciones + DeleteBehavior
            // ------------------------------------------------------------
            modelBuilder.Entity<Carrito>(entity =>
            {
                entity.HasOne(c => c.Usuario)
                      .WithMany()
                      .HasForeignKey(c => c.UsuarioId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<CarritoDetalle>()
                .HasOne(cd => cd.Producto)
                .WithMany(p => p.CarritoDetalles)
                .HasForeignKey(cd => cd.ProductoID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CarritoDetalle>()
                .HasOne(cd => cd.Carrito)
                .WithMany(c => c.CarritoDetalles)
                .HasForeignKey(cd => cd.CarritoID)
                .OnDelete(DeleteBehavior.Restrict);

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

            modelBuilder.Entity<Reseñas>()
                .HasOne(r => r.Usuario)
                .WithMany()
                .HasForeignKey(r => r.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Reseñas>()
                .HasOne(r => r.Producto)
                .WithMany(p => p.Reseñas)
                .HasForeignKey(r => r.ProductoID)
                .OnDelete(DeleteBehavior.Restrict);

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

            modelBuilder.Entity<Ventas>()
                .HasOne(v => v.Usuario)
                .WithMany()
                .HasForeignKey(v => v.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Pedido>()
                .HasOne(p => p.Usuario)
                .WithMany()
                .HasForeignKey(p => p.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);

            // ------------------------------------------------------------
            // 3.b) NUEVO — Multi-vendedor: Vendedor/Banco/Cuenta/Contacto
            // ------------------------------------------------------------
            // Vendedor
            modelBuilder.Entity<Vendedor>(entity =>
            {
                entity.HasKey(v => v.VendedorId);

                entity.HasMany(v => v.Cuentas)
                      .WithOne(c => c.Vendedor)
                      .HasForeignKey(c => c.VendedorId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(v => v.Contactos)
                      .WithOne(c => c.Vendedor)
                      .HasForeignKey(c => c.VendedorId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Banco
            modelBuilder.Entity<Banco>(entity =>
            {
                entity.HasKey(b => b.BancoId);

                // Código único (ej.: "pichincha", "guayaquil")
                entity.HasIndex(b => b.Codigo)
                      .IsUnique();
            });

            // CuentaBancaria
            modelBuilder.Entity<CuentaBancaria>(entity =>
            {
                entity.HasKey(c => c.CuentaBancariaId);

                entity.HasOne(c => c.Banco)
                      .WithMany(b => b.Cuentas)
                      .HasForeignKey(c => c.BancoId)
                      .OnDelete(DeleteBehavior.Restrict);

                // Evita duplicados de misma cuenta por vendedor/banco
                entity.HasIndex(c => new { c.VendedorId, c.BancoId, c.Numero })
                      .IsUnique();
            });

            // ContactoTienda
            modelBuilder.Entity<ContactoTienda>(entity =>
            {
                entity.HasKey(c => c.ContactoTiendaId);
            });

            // ------------------------------------------------------------
            // 4) Estandarizar DECIMAL(18,2) en campos financieros
            // ------------------------------------------------------------
            var decimalProps = new (Type entity, string[] props)[]
            {
                (typeof(Comisiones),            new[] { "MontoComision", "PorcentajeComision" }),
                (typeof(Compras),               new[] { "Total" }),
                (typeof(DetalleVentas),         new[] { "Descuento", "PrecioUnitario", "Subtotal" }),
                (typeof(DetallesCompra),        new[] { "PrecioUnitario", "Subtotal" }),
                (typeof(DetallesPedido),        new[] { "PrecioUnitario", "Subtotal" }),
                (typeof(Empleados),             new[] { "Salario" }),
                (typeof(Gastos),                new[] { "Monto" }),
                (typeof(HistorialPrecios),      new[] { "PrecioAnterior", "PrecioNuevo" }),
                (typeof(Pedido),                new[] { "Total" }),
                (typeof(Producto),              new[] { "PrecioCompra", "PrecioVenta" }),
                (typeof(ProgramasFidelizacion), new[] { "Descuento" }),
                (typeof(Promocion),             new[] { "Descuento" }),
                (typeof(Ventas),                new[] { "Total" }),
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
            // 5) Defaults en BD para timestamps
            // ------------------------------------------------------------
            modelBuilder.Entity<Favorito>()
                .Property(f => f.FechaGuardado)
                .HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<CarritoDetalle>()
                .Property(cd => cd.FechaAgregado)
                .HasDefaultValueSql("GETUTCDATE()");

            // ------------------------------------------------------------
            // 6) Logs / auditoría
            // ------------------------------------------------------------
            modelBuilder.Entity<LogIniciosSesion>(entity =>
            {
                entity.HasKey(l => l.LogID);
                entity.Property(l => l.Usuario).IsRequired().HasMaxLength(150);
                entity.Property(l => l.FechaInicio).HasColumnType("datetime");
                entity.Property(l => l.Exitoso).IsRequired(false);
            });

            modelBuilder.Entity<ActividadUsuario>()
                .HasOne(a => a.Usuario)
                .WithMany(u => u.Actividades)
                .HasForeignKey(a => a.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
