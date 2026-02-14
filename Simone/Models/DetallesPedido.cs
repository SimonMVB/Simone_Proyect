using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Simone.Models
{
    /// <summary>
    /// Detalle/línea de un pedido
    /// Cada línea representa un producto con su cantidad y precio
    /// Para comisiones multi-vendedor, el vendedor se obtiene del Producto
    /// </summary>
    public class DetallesPedido
    {
        [Key]
        public int DetalleID { get; set; }

        // ==================== RELACIONES ====================

        /// <summary>
        /// ID del pedido al que pertenece
        /// </summary>
        public int? PedidoID { get; set; }

        /// <summary>
        /// ID del producto
        /// </summary>
        public int ProductoID { get; set; }

        /// <summary>
        /// ID de la variante (si aplica)
        /// </summary>
        public int? VarianteID { get; set; }

        // ==================== DATOS DEL PRODUCTO (Desnormalizados) ====================
        // Se guardan para mantener historial aunque el producto cambie

        /// <summary>
        /// Nombre del producto al momento de la compra
        /// </summary>
        [StringLength(200)]
        public string? NombreProducto { get; set; }

        /// <summary>
        /// SKU del producto/variante
        /// </summary>
        [StringLength(100)]
        public string? SKU { get; set; }

        /// <summary>
        /// Color (si aplica)
        /// </summary>
        [StringLength(50)]
        public string? Color { get; set; }

        /// <summary>
        /// Talla (si aplica)
        /// </summary>
        [StringLength(50)]
        public string? Talla { get; set; }

        /// <summary>
        /// Imagen del producto
        /// </summary>
        [StringLength(500)]
        public string? ImagenProducto { get; set; }

        // ==================== VENDEDOR (Para comisiones) ====================

        /// <summary>
        /// ID del vendedor del producto (desnormalizado para reportes)
        /// </summary>
        [StringLength(450)]
        public string? VendedorID { get; set; }

        /// <summary>
        /// Nombre del vendedor al momento de la compra
        /// </summary>
        [StringLength(200)]
        public string? NombreVendedor { get; set; }

        // ==================== CANTIDADES Y PRECIOS ====================

        /// <summary>
        /// Cantidad de unidades
        /// </summary>
        [Range(1, int.MaxValue)]
        public int Cantidad { get; set; } = 1;

        /// <summary>
        /// Precio unitario al momento de la compra
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal PrecioUnitario { get; set; }

        /// <summary>
        /// Precio original (antes de descuento)
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal? PrecioOriginal { get; set; }

        /// <summary>
        /// Descuento aplicado a esta línea
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal Descuento { get; set; } = 0;

        /// <summary>
        /// Subtotal = (PrecioUnitario * Cantidad) - Descuento
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal Subtotal { get; set; }

        // ==================== COMISIONES ====================

        /// <summary>
        /// Porcentaje de comisión aplicado a esta línea
        /// </summary>
        [Column(TypeName = "decimal(5,2)")]
        public decimal? PorcentajeComision { get; set; }

        /// <summary>
        /// Monto de comisión calculado
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal? MontoComision { get; set; }

        /// <summary>
        /// ¿Ya se incluyó en una liquidación de comisión?
        /// </summary>
        public bool ComisionLiquidada { get; set; } = false;

        // ==================== ESTADO ====================

        /// <summary>
        /// Estado de esta línea: Normal, Devuelto, Cancelado
        /// </summary>
        [StringLength(50)]
        public string Estado { get; set; } = "Normal";

        /// <summary>
        /// Motivo de devolución/cancelación
        /// </summary>
        [StringLength(500)]
        public string? MotivoDevolucion { get; set; }

        /// <summary>
        /// Fecha de devolución (si aplica)
        /// </summary>
        public DateTime? FechaDevolucion { get; set; }

        // ==================== NOTAS ====================

        /// <summary>
        /// Notas o personalizaciones del producto
        /// </summary>
        [StringLength(1000)]
        public string? Notas { get; set; }

        // ==================== NAVEGACIÓN ====================

        [ForeignKey(nameof(PedidoID))]
        public virtual Pedido? Pedido { get; set; }

        [ForeignKey(nameof(ProductoID))]
        public virtual Producto? Producto { get; set; }

        [ForeignKey(nameof(VarianteID))]
        public virtual ProductoVariante? Variante { get; set; }

        // ==================== PROPIEDADES CALCULADAS ====================

        /// <summary>
        /// ¿Tiene descuento aplicado?
        /// </summary>
        [NotMapped]
        public bool TieneDescuento => Descuento > 0;

        /// <summary>
        /// Porcentaje de descuento
        /// </summary>
        [NotMapped]
        public decimal PorcentajeDescuento =>
            PrecioOriginal > 0 ? Math.Round((1 - (PrecioUnitario / PrecioOriginal.Value)) * 100, 0) : 0;

        /// <summary>
        /// ¿Es una devolución?
        /// </summary>
        [NotMapped]
        public bool EsDevolucion => Estado == "Devuelto";

        /// <summary>
        /// ¿Está cancelado?
        /// </summary>
        [NotMapped]
        public bool EstaCancelado => Estado == "Cancelado";

        /// <summary>
        /// ¿Es una línea normal (no devuelta ni cancelada)?
        /// </summary>
        [NotMapped]
        public bool EsNormal => Estado == "Normal";

        /// <summary>
        /// Nombre a mostrar (del campo desnormalizado o del producto)
        /// </summary>
        [NotMapped]
        public string NombreMostrar =>
            !string.IsNullOrEmpty(NombreProducto) ? NombreProducto : (Producto?.Nombre ?? $"Producto #{ProductoID}");

        /// <summary>
        /// Descripción completa (nombre + color + talla)
        /// </summary>
        [NotMapped]
        public string DescripcionCompleta
        {
            get
            {
                var partes = new List<string> { NombreMostrar };
                if (!string.IsNullOrEmpty(Color)) partes.Add($"Color: {Color}");
                if (!string.IsNullOrEmpty(Talla)) partes.Add($"Talla: {Talla}");
                return string.Join(" | ", partes);
            }
        }

        // ==================== MÉTODOS ====================

        /// <summary>
        /// Calcular subtotal
        /// </summary>
        public void CalcularSubtotal()
        {
            Subtotal = (PrecioUnitario * Cantidad) - Descuento;
        }

        /// <summary>
        /// Calcular comisión
        /// </summary>
        public void CalcularComision(decimal porcentaje)
        {
            PorcentajeComision = porcentaje;
            MontoComision = Math.Round(Subtotal * (porcentaje / 100m), 2);
        }

        /// <summary>
        /// Desnormalizar datos del producto para historial
        /// </summary>
        public void DesnormalizarProducto()
        {
            if (Producto != null)
            {
                NombreProducto = Producto.Nombre;
                //SKU = Producto.SKU;
                Color = Producto.Color;
                Talla = Producto.Talla;
                ImagenProducto = Producto.ImagenPath;
                VendedorID = Producto.VendedorID;
                NombreVendedor = Producto.Usuario?.NombreCompleto;
            }
        }

        /// <summary>
        /// Marcar como devuelto
        /// </summary>
        public void Devolver(string motivo)
        {
            Estado = "Devuelto";
            MotivoDevolucion = motivo;
            FechaDevolucion = DateTime.UtcNow;
        }

        /// <summary>
        /// Cancelar línea
        /// </summary>
        public void Cancelar(string motivo)
        {
            Estado = "Cancelado";
            MotivoDevolucion = motivo;
        }
    }

    /// <summary>
    /// Estados posibles de una línea de pedido
    /// </summary>
    public static class EstadosDetallePedido
    {
        public const string Normal = "Normal";
        public const string Devuelto = "Devuelto";
        public const string Cancelado = "Cancelado";
    }
}