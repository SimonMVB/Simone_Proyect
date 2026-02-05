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
        public DbSet<Reseñas> Reseñas { get; set; }
        public DbSet<Subcategorias> Subcategorias { get; set; }
        public DbSet<Ventas> Ventas { get; set; }
        public DbSet<Favorito> Favoritos { get; set; }
        public DbSet<VentaReversion> VentaReversiones { get; set; }

        // ✅ Multi-vendedor
        public DbSet<Vendedor> Vendedores { get; set; }
        public DbSet<Banco> Bancos { get; set; }
        public DbSet<CuentaBancaria> CuentasBancarias { get; set; }
        public DbSet<ContactoTienda> ContactosTiendas { get; set; }

        // ✅ Variantes de producto (Color+Talla)
        public DbSet<ProductoVariante> ProductoVariantes { get; set; }

        // ==================== ATRIBUTOS DINÁMICOS (Fusionado) ====================
        // ❌ ELIMINADO: DbSet<Categoria> CategoriasEnterprise (ahora todo está en Categorias)

        /// <summary>
        /// Atributos personalizados por categoría (Material, Talla, Color, etc.)
        /// </summary>
        public DbSet<CategoriaAtributo> CategoriaAtributos { get; set; } = null!;

        /// <summary>
        /// Valores de atributos en productos
        /// </summary>
        public DbSet<ProductoAtributoValor> ProductoAtributoValores { get; set; } = null!;

        // ==================== FIN DbSets ====================

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

            // ------------------------------------------------------------
            // ⭐ CATEGORÍAS (Fusionado - con jerarquía y atributos)
            // ------------------------------------------------------------
            modelBuilder.Entity<Categorias>(entity =>
            {
                entity.ToTable("Categorias");

                entity.HasKey(c => c.CategoriaID);

                // Jerarquía auto-referencial (opcional)
                entity.HasOne(c => c.CategoriaPadre)
                      .WithMany(c => c.CategoriasHijas)
                      .HasForeignKey(c => c.CategoriaPadreID)
                      .OnDelete(DeleteBehavior.Restrict);

                // Propiedades
                entity.Property(c => c.Nombre)
                      .IsRequired()
                      .HasMaxLength(100);

                entity.Property(c => c.Slug)
                      .HasMaxLength(150);

                entity.Property(c => c.Path)
                      .HasMaxLength(500);

                entity.Property(c => c.Descripcion)
                      .HasMaxLength(2000);

                entity.Property(c => c.MetaDescripcion)
                      .HasMaxLength(300);

                entity.Property(c => c.MetaKeywords)
                      .HasMaxLength(500);

                entity.Property(c => c.IconoClass)
                      .HasMaxLength(100);

                entity.Property(c => c.ImagenPath)
                      .HasMaxLength(300);

                entity.Property(c => c.ImagenThumbnail)
                      .HasMaxLength(300);

                // Analytics
                entity.Property(c => c.ConversionRate)
                      .HasColumnType("decimal(5,4)");

                entity.Property(c => c.TrendingScore)
                      .HasColumnType("decimal(10,2)");

                // Índices para performance
                entity.HasIndex(c => c.Slug)
                      .HasDatabaseName("IX_Categorias_Slug");

                entity.HasIndex(c => c.Activo)
                      .HasDatabaseName("IX_Categorias_Activo");

                entity.HasIndex(c => c.MostrarEnMenu)
                      .HasDatabaseName("IX_Categorias_MostrarEnMenu");

                entity.HasIndex(c => c.Destacada)
                      .HasDatabaseName("IX_Categorias_Destacada");

                entity.HasIndex(c => new { c.CategoriaPadreID, c.Orden })
                      .HasDatabaseName("IX_Categorias_Padre_Orden");

                // Ignorar propiedades calculadas
                entity.Ignore(c => c.NombreCompleto);
                entity.Ignore(c => c.Breadcrumbs);
                entity.Ignore(c => c.Url);
                entity.Ignore(c => c.TieneCategoriasHijas);
                entity.Ignore(c => c.TieneSubcategorias);
                entity.Ignore(c => c.EsRaiz);
                entity.Ignore(c => c.EsHoja);
                entity.Ignore(c => c.TotalSubcategorias);
                entity.Ignore(c => c.TotalProductosDirectos);
                entity.Ignore(c => c.TotalProductos);
                entity.Ignore(c => c.TotalAtributos);
            });

            // ------------------------------------------------------------
            // ⭐ SUBCATEGORÍAS (Mejorado)
            // ------------------------------------------------------------
            modelBuilder.Entity<Subcategorias>(entity =>
            {
                entity.ToTable("Subcategorias");

                entity.HasKey(s => s.SubcategoriaID);

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

                // Propiedades
                entity.Property(s => s.NombreSubcategoria)
                      .IsRequired()
                      .HasMaxLength(100);

                entity.Property(s => s.Slug)
                      .HasMaxLength(150);

                entity.Property(s => s.Descripcion)
                      .HasMaxLength(500);

                entity.Property(s => s.IconoClass)
                      .HasMaxLength(100);

                entity.Property(s => s.ImagenPath)
                      .HasMaxLength(300);

                entity.Property(s => s.MetaDescripcion)
                      .HasMaxLength(300);

                entity.Property(s => s.MetaKeywords)
                      .HasMaxLength(500);

                // Índices
                entity.HasIndex(s => new { s.VendedorID, s.CategoriaID, s.NombreSubcategoria })
                      .IsUnique()
                      .HasDatabaseName("IX_Subcategorias_Vendedor_Categoria_Nombre");

                entity.HasIndex(s => s.Slug)
                      .HasDatabaseName("IX_Subcategorias_Slug");

                entity.HasIndex(s => s.Activo)
                      .HasDatabaseName("IX_Subcategorias_Activo");

                // Ignorar propiedades calculadas
                entity.Ignore(s => s.NombreCompleto);
                entity.Ignore(s => s.TotalProductos);
                entity.Ignore(s => s.TotalProductosActivos);
                entity.Ignore(s => s.TieneProductos);
                entity.Ignore(s => s.Url);
                entity.Ignore(s => s.EsDeVendedor);
                entity.Ignore(s => s.EsGlobal);
                entity.Ignore(s => s.NombreVendedor);
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

            // ===== ProductoVariante =====
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

            // ===== ProductoImagen =====
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

            // ------------------------------------------------------------
            // ⭐ CATEGORIA ATRIBUTO (Ahora apunta a Categorias fusionado)
            // ------------------------------------------------------------
            modelBuilder.Entity<CategoriaAtributo>(entity =>
            {
                entity.ToTable("CategoriaAtributos");

                entity.HasKey(e => e.AtributoID);

                // ✅ CAMBIO: Ahora apunta a Categorias (no a Categoria enterprise)
                entity.HasOne(e => e.Categoria)
                      .WithMany(c => c.Atributos)
                      .HasForeignKey(e => e.CategoriaID)
                      .OnDelete(DeleteBehavior.Cascade);

                // Propiedades
                entity.Property(e => e.Nombre)
                      .IsRequired()
                      .HasMaxLength(100);

                entity.Property(e => e.NombreTecnico)
                      .IsRequired()
                      .HasMaxLength(100);

                entity.Property(e => e.Descripcion)
                      .HasMaxLength(300);

                entity.Property(e => e.TipoCampo)
                      .IsRequired()
                      .HasMaxLength(50);

                entity.Property(e => e.OpcionesJson)
                      .HasMaxLength(2000);

                entity.Property(e => e.Unidad)
                      .HasMaxLength(20);

                entity.Property(e => e.IconoClass)
                      .HasMaxLength(100);

                entity.Property(e => e.Grupo)
                      .HasMaxLength(100);

                entity.Property(e => e.PatronValidacion)
                      .HasMaxLength(200);

                entity.Property(e => e.MensajeError)
                      .HasMaxLength(200);

                entity.Property(e => e.ValorMinimo)
                      .HasColumnType("decimal(18,2)");

                entity.Property(e => e.ValorMaximo)
                      .HasColumnType("decimal(18,2)");

                // Índices
                entity.HasIndex(e => new { e.CategoriaID, e.Orden })
                      .HasDatabaseName("IX_CategoriaAtributo_Categoria_Orden");

                entity.HasIndex(e => new { e.CategoriaID, e.NombreTecnico })
                      .IsUnique()
                      .HasDatabaseName("IX_CategoriaAtributo_Categoria_NombreTecnico_Unique");

                entity.HasIndex(e => e.Filtrable)
                      .HasDatabaseName("IX_CategoriaAtributo_Filtrable");

                entity.HasIndex(e => e.Activo)
                      .HasDatabaseName("IX_CategoriaAtributo_Activo");

                // Ignorar propiedades calculadas
                entity.Ignore(e => e.OpcionesLista);
                entity.Ignore(e => e.TieneOpciones);
                entity.Ignore(e => e.RequiereValidacionNumerica);
                entity.Ignore(e => e.EsSeleccion);
                entity.Ignore(e => e.NombreCategoria);
                entity.Ignore(e => e.TotalProductos);
                entity.Ignore(e => e.TieneRango);
                entity.Ignore(e => e.DescripcionRango);
            });

            // ------------------------------------------------------------
            // ⭐ PRODUCTO ATRIBUTO VALOR
            // ------------------------------------------------------------
            modelBuilder.Entity<ProductoAtributoValor>(entity =>
            {
                entity.ToTable("ProductoAtributoValores");

                entity.HasKey(e => e.ValorID);

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

                // Propiedades
                entity.Property(e => e.Valor)
                      .IsRequired()
                      .HasColumnType("nvarchar(max)");

                entity.Property(e => e.ValorMostrable)
                      .HasColumnType("nvarchar(500)");

                // Índice único: Un producto solo puede tener UN valor por atributo
                entity.HasIndex(e => new { e.ProductoID, e.AtributoID })
                      .IsUnique()
                      .HasDatabaseName("IX_ProductoAtributoValores_Producto_Atributo_Unique");

                entity.HasIndex(e => e.AtributoID)
                      .HasDatabaseName("IX_ProductoAtributoValores_Atributo");

                // Ignorar propiedades calculadas
                entity.Ignore(e => e.ValorFormateado);
                entity.Ignore(e => e.ValorNumerico);
                entity.Ignore(e => e.ValorComoLista);
                entity.Ignore(e => e.ValorComoNumero);
                entity.Ignore(e => e.ValorComoBooleano);
                entity.Ignore(e => e.ValorComoFecha);
                entity.Ignore(e => e.NombreAtributo);
                entity.Ignore(e => e.TipoCampo);
                entity.Ignore(e => e.Unidad);
                entity.Ignore(e => e.EstaVacio);
                entity.Ignore(e => e.EsObligatorio);
            });

            // ==================== FIN CONFIGURACIÓN ====================
        }
    }
}
