using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Simone.Models;
using System;

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
        public DbSet<ProductoImagen> ProductoImagenes { get; set; }
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

        // ✅ Multi-vendedor
        public DbSet<Vendedor> Vendedores { get; set; }
        public DbSet<Banco> Bancos { get; set; }
        public DbSet<CuentaBancaria> CuentasBancarias { get; set; }
        public DbSet<ContactoTienda> ContactosTiendas { get; set; }

        // ✅ NUEVO: Variantes de producto (Color+Talla)
        public DbSet<ProductoVariante> ProductoVariantes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ------------------------------------------------------------
            // 0) Usuario → configuración de cédula e índices + Asociación Tienda
            // ------------------------------------------------------------
            modelBuilder.Entity<Usuario>(entity =>
            {
                entity.Property(u => u.Cedula)
                      .HasMaxLength(10)
                      .HasColumnType("nvarchar(10)");

                entity.HasIndex(u => u.Cedula)
                      .IsUnique()
                      .HasFilter("[Cedula] IS NOT NULL");

                entity.HasOne(u => u.Vendedor)
                      .WithMany()
                      .HasForeignKey(u => u.VendedorId)
                      .OnDelete(DeleteBehavior.SetNull);
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
                .HasIndex(cd => new { cd.CarritoID, cd.ProductoID, cd.ProductoVarianteID })
                .IsUnique();

            modelBuilder.Entity<CarritoDetalle>()
                .HasOne(cd => cd.Variante)
                .WithMany(v => v.CarritoDetalles)
                .HasForeignKey(cd => cd.ProductoVarianteID)
                .OnDelete(DeleteBehavior.Restrict);

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

            modelBuilder.Entity<ProductoImagen>(e =>
            {
                e.HasKey(pi => pi.ProductoImagenID);
                e.Property(pi => pi.Path).IsRequired().HasMaxLength(300);

                e.HasOne(pi => pi.Producto)
                 .WithMany(p => p.Imagenes)
                 .HasForeignKey(pi => pi.ProductoID)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // ===== Subcategorias (multi-vendedor) =====
            modelBuilder.Entity<Subcategorias>(entity =>
            {
                entity.HasOne(s => s.Categoria)
                      .WithMany(c => c.Subcategoria)
                      .HasForeignKey(s => s.CategoriaID)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.Property(s => s.VendedorID)
                      .HasMaxLength(450);

                entity.HasOne(s => s.Usuario)
                      .WithMany()
                      .HasForeignKey(s => s.VendedorID)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(s => new { s.VendedorID, s.CategoriaID, s.NombreSubcategoria })
                      .IsUnique();
            });

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

            modelBuilder.Entity<MovimientosInventario>()
                .HasOne(mi => mi.Producto)
                .WithMany(p => p.MovimientosInventario)
                .HasForeignKey(mi => mi.ProductoID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MovimientosInventario>()
                .HasOne(mi => mi.Variante)
                .WithMany(v => v.MovimientosInventario)
                .HasForeignKey(mi => mi.ProductoVarianteID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DetalleVentas>()
                .HasOne(dv => dv.Variante)
                .WithMany(v => v.DetalleVentas)
                .HasForeignKey(dv => dv.ProductoVarianteID)
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
            // 3.b) Multi-vendedor
            // ------------------------------------------------------------
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

            modelBuilder.Entity<Banco>(entity =>
            {
                entity.HasKey(b => b.BancoId);
                entity.HasIndex(b => b.Codigo).IsUnique();
            });

            modelBuilder.Entity<CuentaBancaria>(entity =>
            {
                entity.HasKey(c => c.CuentaBancariaId);

                entity.HasOne(c => c.Banco)
                      .WithMany(b => b.Cuentas)
                      .HasForeignKey(c => c.BancoId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(c => new { c.VendedorId, c.BancoId, c.Numero })
                      .IsUnique();
            });

            modelBuilder.Entity<ContactoTienda>(entity =>
            {
                entity.HasKey(c => c.ContactoTiendaId);
            });

            // ------------------------------------------------------------
            // 3.c) Variantes de Producto
            // ------------------------------------------------------------
            modelBuilder.Entity<ProductoVariante>(entity =>
            {
                entity.HasKey(v => v.ProductoVarianteID);

                entity.Property(v => v.Color).HasMaxLength(50).IsRequired();
                entity.Property(v => v.Talla).HasMaxLength(20).IsRequired();
                entity.Property(v => v.PrecioCompra).HasColumnType("decimal(18,2)");
                entity.Property(v => v.PrecioVenta).HasColumnType("decimal(18,2)");

                entity.HasOne(v => v.Producto)
                      .WithMany(p => p.Variantes)
                      .HasForeignKey(v => v.ProductoID)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(v => new { v.ProductoID, v.Color, v.Talla })
                      .IsUnique();
            });

            // ------------------------------------------------------------
            // 4) DECIMAL(18,2)
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
                (typeof(ProductoVariante),      new[] { "PrecioCompra", "PrecioVenta" }),
            };

            foreach (var (entity, props) in decimalProps)
                foreach (var prop in props)
                    modelBuilder.Entity(entity).Property(prop).HasColumnType("decimal(18,2)");

            // ------------------------------------------------------------
            // 5) Defaults
            // ------------------------------------------------------------
            modelBuilder.Entity<Favorito>()
                .Property(f => f.FechaGuardado)
                .HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<CarritoDetalle>()
                .Property(cd => cd.FechaAgregado)
                .HasDefaultValueSql("GETUTCDATE()");

            // ------------------------------------------------------------
            // 6) Logs
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
