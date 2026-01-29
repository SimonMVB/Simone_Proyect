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

        // ==================== ⭐ ENTERPRISE: NUEVOS DbSets ====================
        /// <summary>
        /// Nueva tabla de Categorías (sistema jerárquico enterprise)
        /// </summary>
        public DbSet<Categoria> CategoriasEnterprise { get; set; } = null!;

        /// <summary>
        /// Atributos personalizados por categoría
        /// </summary>
        public DbSet<CategoriaAtributo> CategoriaAtributos { get; set; } = null!;

        /// <summary>
        /// Valores de atributos en productos
        /// </summary>
        public DbSet<ProductoAtributoValor> ProductoAtributoValores { get; set; } = null!;
        // ==================== FIN NUEVOS DbSets ====================

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

                entity.HasOne(cu => cu.Promocion)
                      .WithMany()
                      .HasForeignKey(cu => cu.PromocionID)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ------------------------------------------------------------
            // 2) Producto → índices básicos + navegación ampliada
            // ------------------------------------------------------------
            modelBuilder.Entity<Producto>(entity =>
            {
                entity.HasIndex(p => new { p.CategoriaID, p.SubcategoriaID });
                entity.HasIndex(p => p.VendedorID);

                entity.HasOne(p => p.Categoria)
                      .WithMany(c => c.Productos)
                      .HasForeignKey(p => p.CategoriaID)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(p => p.Subcategoria)
                      .WithMany(s => s.Productos)
                      .HasForeignKey(p => p.SubcategoriaID)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(p => p.Usuario)
                      .WithMany()
                      .HasForeignKey(p => p.VendedorID)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(p => p.Proveedor)
                      .WithMany()
                      .HasForeignKey(p => p.ProveedorID)
                      .OnDelete(DeleteBehavior.Restrict);
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

            modelBuilder.Entity<DetalleVentas>()
                .HasOne(dv => dv.Producto)
                .WithMany(p => p.DetalleVentas)
                .HasForeignKey(dv => dv.ProductoID)
                .OnDelete(DeleteBehavior.Restrict);

            // ===== ProductoVariante (nuevo) =====
            modelBuilder.Entity<ProductoVariante>(entity =>
            {
                entity.HasKey(v => v.ProductoVarianteID);

                entity.HasOne(v => v.Producto)
                      .WithMany(p => p.Variantes)
                      .HasForeignKey(v => v.ProductoID)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(v => new { v.ProductoID, v.Color, v.Talla })
                      .IsUnique()
                      .HasDatabaseName("IX_ProductoVariante_Producto_Color_Talla_Unique");

                entity.Property(v => v.Color).HasMaxLength(30);
                entity.Property(v => v.Talla).HasMaxLength(20);
                entity.Property(v => v.SKU).HasMaxLength(50);
                entity.Property(v => v.ImagenPath).HasMaxLength(300);

                entity.Property(v => v.PrecioCompra).HasColumnType("decimal(18,2)");
                entity.Property(v => v.PrecioVenta).HasColumnType("decimal(18,2)");
                entity.Property(v => v.Stock).HasDefaultValue(0);
            });

            // ===== ProductoImagen (galería moderna) =====
            modelBuilder.Entity<ProductoImagen>(entity =>
            {
                entity.HasKey(i => i.ProductoImagenID);

                entity.HasOne(i => i.Producto)
                      .WithMany(p => p.Imagenes)
                      .HasForeignKey(i => i.ProductoID)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(i => new { i.ProductoID, i.Principal })
                      .HasDatabaseName("IX_ProductoImagen_Producto_Principal");

                entity.Property(i => i.Path).IsRequired().HasMaxLength(300);
                entity.Property(i => i.Orden).HasDefaultValue(0);
            });

            // ------------------------------------------------------------
            // 3) Ventas + Detalles
            // ------------------------------------------------------------
            modelBuilder.Entity<Ventas>()
                .HasOne(v => v.Usuario)
                .WithMany()
                .HasForeignKey(v => v.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DetalleVentas>()
                .HasOne(dv => dv.Venta)
                .WithMany(v => v.DetalleVentas)
                .HasForeignKey(dv => dv.VentaID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DetalleVentas>()
                .HasOne(dv => dv.Producto)
                .WithMany(p => p.DetalleVentas)
                .HasForeignKey(dv => dv.ProductoID)
                .OnDelete(DeleteBehavior.Restrict);

            // ------------------------------------------------------------
            // 4) Compras + Detalles + Proveedores
            // ------------------------------------------------------------
            modelBuilder.Entity<Compras>()
                .HasOne(c => c.Proveedor)
                .WithMany()
                .HasForeignKey(c => c.ProveedorID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DetallesCompra>()
                .HasOne(dc => dc.Compra)
                .WithMany(c => c.DetallesCompra)
                .HasForeignKey(dc => dc.CompraID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DetallesCompra>()
                .HasOne(dc => dc.Producto)
                .WithMany(p => p.DetallesCompra)
                .HasForeignKey(dc => dc.ProductoID)
                .OnDelete(DeleteBehavior.Restrict);

            // ------------------------------------------------------------
            // 5) Carrito
            // ------------------------------------------------------------
            modelBuilder.Entity<Carrito>()
                .HasOne(c => c.Usuario)
                .WithMany()
                .HasForeignKey(c => c.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CarritoDetalle>()
                .HasOne(cd => cd.Carrito)
                .WithMany(c => c.CarritoDetalles)
                .HasForeignKey(cd => cd.CarritoID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CarritoDetalle>()
                .HasOne(cd => cd.Producto)
                .WithMany(p => p.CarritoDetalles)
                .HasForeignKey(cd => cd.ProductoID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Carrito>()
                .Property(c => c.FechaCreacion)
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

            // ==================== ⭐ ENTERPRISE: CONFIGURACIÓN CATEGORÍAS ====================

            // ------------------------------------------------------------
            // CATEGORIA (Auto-referencial - Jerarquía infinita)
            // ------------------------------------------------------------
            modelBuilder.Entity<Categoria>(entity =>
            {
                entity.ToTable("Categorias_Enterprise");

                entity.HasKey(e => e.CategoriaID);

                // Auto-referencial: Una categoría puede tener padre y muchas hijas
                entity.HasOne(e => e.CategoriaPadre)
                    .WithMany(e => e.CategoriasHijas)
                    .HasForeignKey(e => e.CategoriaPadreID)
                    .OnDelete(DeleteBehavior.Restrict);

                // Índices para performance
                entity.HasIndex(e => e.Slug)
                    .HasDatabaseName("IX_Categoria_Slug");

                entity.HasIndex(e => e.Path)
                    .IsUnique()
                    .HasDatabaseName("IX_Categoria_Path_Unique");

                entity.HasIndex(e => new { e.CategoriaPadreID, e.Nivel, e.Orden })
                    .HasDatabaseName("IX_Categoria_Padre_Nivel_Orden");

                entity.HasIndex(e => e.Activa)
                    .HasDatabaseName("IX_Categoria_Activa");

                entity.HasIndex(e => e.Destacada)
                    .HasDatabaseName("IX_Categoria_Destacada");

                entity.HasIndex(e => e.Trending)
                    .HasDatabaseName("IX_Categoria_Trending");

                entity.HasIndex(e => e.TrendingScore)
                    .HasDatabaseName("IX_Categoria_TrendingScore");

                // Validación: Slug único dentro del mismo padre
                entity.HasIndex(e => new { e.Slug, e.CategoriaPadreID })
                    .IsUnique()
                    .HasDatabaseName("IX_Categoria_Slug_Padre_Unique");
            });

            // ------------------------------------------------------------
            // CATEGORIA ATRIBUTO
            // ------------------------------------------------------------
            modelBuilder.Entity<CategoriaAtributo>(entity =>
            {
                entity.ToTable("CategoriaAtributos");

                entity.HasKey(e => e.AtributoID);

                entity.HasOne(e => e.Categoria)
                    .WithMany(e => e.Atributos)
                    .HasForeignKey(e => e.CategoriaID)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => new { e.CategoriaID, e.Orden })
                    .HasDatabaseName("IX_CategoriaAtributo_Categoria_Orden");

                entity.HasIndex(e => new { e.CategoriaID, e.NombreTecnico })
                    .IsUnique()
                    .HasDatabaseName("IX_CategoriaAtributo_Categoria_NombreTecnico_Unique");

                entity.HasIndex(e => e.Filtrable)
                    .HasDatabaseName("IX_CategoriaAtributo_Filtrable");

                entity.HasIndex(e => e.Activo)
                    .HasDatabaseName("IX_CategoriaAtributo_Activo");
            });

            // ------------------------------------------------------------
            // PRODUCTO ATRIBUTO VALOR
            // ------------------------------------------------------------
            modelBuilder.Entity<ProductoAtributoValor>(entity =>
            {
                // Ignorar propiedades calculadas (no son columnas de BD)
                entity.Ignore(e => e.ValorFormateado);
                entity.Ignore(e => e.ValorNumerico);
                entity.Ignore(e => e.ValorComoLista);
                entity.Ignore(e => e.ValorComoNumero);
                entity.Ignore(e => e.ValorComoBooleano);
                entity.Ignore(e => e.ValorComoFecha);

                // Índice único: Un producto solo puede tener UN valor por atributo
                entity.HasIndex(e => new { e.ProductoID, e.AtributoID })
                    .IsUnique()
                    .HasDatabaseName("IX_ProductoAtributoValores_ProductoID_AtributoID");

                // Relación con Producto (CASCADE delete)
                entity.HasOne(e => e.Producto)
                    .WithMany(p => p.AtributosValores)
                    .HasForeignKey(e => e.ProductoID)
                    .OnDelete(DeleteBehavior.Cascade);

                // Relación con CategoriaAtributo (RESTRICT delete)
                entity.HasOne(e => e.Atributo)
                    .WithMany(a => a.ValoresProductos)
                    .HasForeignKey(e => e.AtributoID)
                    .OnDelete(DeleteBehavior.Restrict);
            });


            // ==================== FIN CONFIGURACIÓN ENTERPRISE ====================
        }
    }
}
