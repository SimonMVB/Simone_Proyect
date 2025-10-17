using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Simone.Models;

namespace Simone.Data
{
    public class TiendaDbContext : IdentityDbContext<Usuario, Roles, string>
    {
        public TiendaDbContext(DbContextOptions<TiendaDbContext> options)
            : base(options) { }

        // ===== Identity / Sistema
        public DbSet<Usuario> Usuarios { get; set; }

        // ===== Dominio
        public DbSet<Producto> Productos { get; set; }
        public DbSet<ProductoVariante> ProductoVariantes { get; set; }
        public DbSet<ProductoImagen> ProductoImagenes { get; set; }          // Esquema moderno (galería)
        public DbSet<ImagenesProductos> ImagenesProductos { get; set; }      // LEGADO (mantener si hay uso)
        public DbSet<Categorias> Categorias { get; set; }
        public DbSet<Subcategorias> Subcategorias { get; set; }
        public DbSet<Proveedores> Proveedores { get; set; }
        public DbSet<Ventas> Ventas { get; set; }
        public DbSet<DetalleVentas> DetalleVentas { get; set; }
        public DbSet<Compras> Compras { get; set; }
        public DbSet<DetallesCompra> DetallesCompra { get; set; }
        public DbSet<Pedido> Pedidos { get; set; }
        public DbSet<DetallesPedido> DetallesPedido { get; set; }
        public DbSet<Carrito> Carrito { get; set; }
        public DbSet<CarritoDetalle> CarritoDetalle { get; set; }
        public DbSet<Reseñas> Reseñas { get; set; }
        public DbSet<MovimientosInventario> MovimientosInventario { get; set; }
        public DbSet<HistorialPrecios> HistorialPrecios { get; set; }
        public DbSet<Promocion> Promociones { get; set; }
        public DbSet<ProgramasFidelizacion> ProgramasFidelizacion { get; set; }
        public DbSet<ClientesProgramas> ClientesProgramas { get; set; }
        public DbSet<CuponesUsados> CuponesUsados { get; set; }
        public DbSet<Gastos> Gastos { get; set; }
        public DbSet<Empleados> Empleados { get; set; }
        public DbSet<Devoluciones> Devoluciones { get; set; }
        public DbSet<CatalogoEstados> CatalogoEstados { get; set; }
        public DbSet<Favorito> Favoritos { get; set; }
        public DbSet<VentaReversion> VentaReversiones { get; set; }
        public DbSet<AuditoriaProductos> AuditoriaProductos { get; set; }
        public DbSet<ActividadUsuario> ActividadesUsuarios { get; set; }
        public DbSet<LogIniciosSesion> LogIniciosSesion { get; set; }
        public DbSet<LogActividad> LogsActividad { get; set; }

        // ===== Multi-vendedor
        public DbSet<Vendedor> Vendedores { get; set; }
        public DbSet<Banco> Bancos { get; set; }
        public DbSet<CuentaBancaria> CuentasBancarias { get; set; }
        public DbSet<ContactoTienda> ContactosTiendas { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ====================== USUARIO ======================
            modelBuilder.Entity<Usuario>(entity =>
            {
                entity.Property(u => u.Cedula).HasMaxLength(10).HasColumnType("nvarchar(10)");
                entity.HasIndex(u => u.Cedula).IsUnique().HasFilter("[Cedula] IS NOT NULL");

                entity.HasOne(u => u.Vendedor)
                      .WithMany()
                      .HasForeignKey(u => u.VendedorId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // ====================== CARRITO DETALLE ======================
            modelBuilder.Entity<CarritoDetalle>()
                .HasIndex(cd => new { cd.CarritoID, cd.ProductoID })
                .HasFilter("[ProductoVarianteID] IS NULL")
                .IsUnique();

            modelBuilder.Entity<CarritoDetalle>()
                .HasIndex(cd => new { cd.CarritoID, cd.ProductoVarianteID })
                .HasFilter("[ProductoVarianteID] IS NOT NULL")
                .IsUnique();

            modelBuilder.Entity<CarritoDetalle>()
                .HasOne(cd => cd.Variante)
                .WithMany(v => v.CarritoDetalles)
                .HasForeignKey(cd => cd.ProductoVarianteID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CarritoDetalle>()
                .HasOne(cd => cd.Producto)
                .WithMany(p => p.CarritoDetalles)
                .HasForeignKey(cd => cd.ProductoID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Carrito>(entity =>
            {
                entity.HasOne(c => c.Usuario)
                      .WithMany()
                      .HasForeignKey(c => c.UsuarioId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ====================== PRODUCTO ======================
            modelBuilder.Entity<Producto>(entity =>
            {
                // Longitudes alineadas con el modelo
                entity.Property(p => p.Nombre).HasMaxLength(200).IsRequired();
                entity.Property(p => p.Marca).HasMaxLength(120);
                entity.Property(p => p.Color).HasMaxLength(30);
                entity.Property(p => p.Talla).HasMaxLength(30);
                entity.Property(p => p.ImagenPath).HasMaxLength(300);

                // Defaults/precision
                entity.Property(p => p.FechaAgregado).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(p => p.PrecioCompra).HasPrecision(18, 2);
                entity.Property(p => p.PrecioVenta).HasPrecision(18, 2);

                // Índices útiles
                entity.HasIndex(p => new { p.VendedorID, p.Nombre });
                entity.HasIndex(p => new { p.CategoriaID, p.SubcategoriaID });

                // Relaciones
                entity.HasOne(p => p.Proveedor)
                      .WithMany(pr => pr.Productos)
                      .HasForeignKey(p => p.ProveedorID)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(p => p.Subcategoria)
                      .WithMany(s => s.Productos)
                      .HasForeignKey(p => p.SubcategoriaID)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(p => p.Usuario)
                      .WithMany()
                      .HasForeignKey(p => p.VendedorID)
                      .OnDelete(DeleteBehavior.Restrict);

                // Checks
                entity.ToTable(tb =>
                    tb.HasCheckConstraint("CK_Producto_Precios",
                        "[PrecioCompra] >= 0 AND [PrecioVenta] >= 0 AND [Stock] >= 0"));

                // (OPCIONAL) Concurrency token sombra
                // entity.Property<byte[]>("RowVersion").IsRowVersion();
            });

            // ====================== PRODUCTO VARIANTE ======================
            modelBuilder.Entity<ProductoVariante>(entity =>
            {
                entity.HasKey(v => v.ProductoVarianteID);

                entity.Property(v => v.Color).HasMaxLength(50).IsRequired();
                entity.Property(v => v.Talla).HasMaxLength(20).IsRequired();
                entity.Property(v => v.SKU).HasMaxLength(64);
                entity.Property(v => v.ImagenPath).HasMaxLength(300);

                entity.Property(v => v.PrecioCompra).HasPrecision(18, 2);
                entity.Property(v => v.PrecioVenta).HasPrecision(18, 2);

                entity.HasOne(v => v.Producto)
                      .WithMany(p => p.Variantes)
                      .HasForeignKey(v => v.ProductoID)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(v => new { v.ProductoID, v.Color, v.Talla }).IsUnique();
                entity.HasIndex(v => v.SKU).IsUnique().HasFilter("[SKU] IS NOT NULL");

                entity.ToTable(tb =>
                    tb.HasCheckConstraint("CK_ProductoVariante_Valores",
                        "([PrecioCompra] IS NULL OR [PrecioCompra] >= 0) " +
                        "AND ([PrecioVenta] IS NULL OR [PrecioVenta] > 0) " +
                        "AND [Stock] >= 0"));

                // (OPCIONAL) Concurrency token sombra
                // entity.Property<byte[]>("RowVersion").IsRowVersion();
            });

            // ====================== IMÁGENES DE PRODUCTO (nuevo) ======================
            modelBuilder.Entity<ProductoImagen>(e =>
            {
                e.HasKey(pi => pi.ProductoImagenID);
                e.Property(pi => pi.Path).IsRequired().HasMaxLength(300);
                e.Property(pi => pi.Orden).HasDefaultValue(0);
                e.Property(pi => pi.Principal).HasDefaultValue(false);

                e.HasOne(pi => pi.Producto)
                 .WithMany(p => p.Imagenes)
                 .HasForeignKey(pi => pi.ProductoID)
                 .OnDelete(DeleteBehavior.Cascade);

                // Evita duplicados del mismo archivo por producto
                e.HasIndex(pi => new { pi.ProductoID, pi.Path }).IsUnique();
            });

            // ====================== IMÁGENES DE PRODUCTO (LEGADO) ======================
            modelBuilder.Entity<ImagenesProductos>(e =>
            {
                e.HasKey(ip => ip.ImagenID);

                e.Property(ip => ip.RutaImagen)
                    .IsRequired()
                    .HasMaxLength(300)
                    .IsUnicode(false);

                e.HasOne(ip => ip.Producto)
                    .WithMany(p => p.ImagenesProductos)
                    .HasForeignKey(ip => ip.ProductoID)
                    .OnDelete(DeleteBehavior.Cascade);

                e.HasIndex(ip => new { ip.ProductoID, ip.RutaImagen })
                    .IsUnique()
                    .HasDatabaseName("IX_ImagenesProductos_Producto_Ruta");
            });

            // ====================== SUBCATEGORÍAS ======================
            modelBuilder.Entity<Subcategorias>(entity =>
            {
                entity.HasOne(s => s.Categoria)
                      .WithMany(c => c.Subcategoria)
                      .HasForeignKey(s => s.CategoriaID)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.Property(s => s.VendedorID).HasMaxLength(450);

                entity.HasOne(s => s.Usuario)
                      .WithMany()
                      .HasForeignKey(s => s.VendedorID)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(s => new { s.VendedorID, s.CategoriaID, s.NombreSubcategoria })
                      .IsUnique();
            });

            // ====================== VENTAS / COMPRAS / PEDIDOS ======================
            modelBuilder.Entity<DetalleVentas>(e =>
            {
                e.Property(x => x.Descuento).HasPrecision(18, 2);
                e.Property(x => x.PrecioUnitario).HasPrecision(18, 2);
                e.Property(x => x.Subtotal).HasPrecision(18, 2);

                e.HasOne(dv => dv.Variante)
                 .WithMany(v => v.DetalleVentas)
                 .HasForeignKey(dv => dv.ProductoVarianteID)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(dv => dv.Producto)
                 .WithMany(p => p.DetalleVentas)
                 .HasForeignKey(dv => dv.ProductoID)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<DetallesCompra>(e =>
            {
                e.Property(x => x.PrecioUnitario).HasPrecision(18, 2);
                e.Property(x => x.Subtotal).HasPrecision(18, 2);
            });

            modelBuilder.Entity<DetallesPedido>(e =>
            {
                e.Property(x => x.PrecioUnitario).HasPrecision(18, 2);
                e.Property(x => x.Subtotal).HasPrecision(18, 2);
            });

            modelBuilder.Entity<Compras>(e => e.Property(x => x.Total).HasPrecision(18, 2));
            modelBuilder.Entity<Ventas>(e => e.Property(x => x.Total).HasPrecision(18, 2));
            modelBuilder.Entity<Gastos>(e => e.Property(x => x.Monto).HasPrecision(18, 2));

            modelBuilder.Entity<HistorialPrecios>(e =>
            {
                e.Property(x => x.PrecioAnterior).HasPrecision(18, 2);
                e.Property(x => x.PrecioNuevo).HasPrecision(18, 2);
            });

            modelBuilder.Entity<ProgramasFidelizacion>(e => e.Property(x => x.Descuento).HasPrecision(18, 2));
            modelBuilder.Entity<Promocion>(e => e.Property(x => x.Descuento).HasPrecision(18, 2));

            // ====================== INVENTARIO / FAVORITOS / LOGS ======================
            modelBuilder.Entity<MovimientosInventario>(e =>
            {
                e.HasOne(mi => mi.Producto)
                 .WithMany(p => p.MovimientosInventario)
                 .HasForeignKey(mi => mi.ProductoID)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(mi => mi.Variante)
                 .WithMany(v => v.MovimientosInventario)
                 .HasForeignKey(mi => mi.ProductoVarianteID)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Favorito>()
                .Property(f => f.FechaGuardado)
                .HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<CarritoDetalle>()
                .Property(cd => cd.FechaAgregado)
                .HasDefaultValueSql("GETUTCDATE()");

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

            // ====================== MULTI-VENDEDOR ======================
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

                entity.HasIndex(c => new { c.VendedorId, c.BancoId, c.Numero }).IsUnique();
            });

            modelBuilder.Entity<ContactoTienda>(entity =>
            {
                entity.HasKey(c => c.ContactoTiendaId);
            });

            // ====================== LOGS ======================
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

            // (OPCIONAL) AutoInclude si quieres que Producto siempre cargue Imagenes/Variantes
            // modelBuilder.Entity<Producto>().Navigation(p => p.Imagenes).AutoInclude();
            // modelBuilder.Entity<Producto>().Navigation(p => p.Variantes).AutoInclude();
        }
    }
}
