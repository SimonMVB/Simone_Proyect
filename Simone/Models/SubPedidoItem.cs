using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Simone.Models
{
    /// <summary>
    /// Producto individual dentro de un SubPedido.
    /// Snapshot de datos al momento de la compra.
    /// </summary>
    public class SubPedidoItem
    {
        [Key]
        public int ItemId { get; set; }

        public int SubPedidoId { get; set; }

        public int ProductoId { get; set; }

        public int? VarianteId { get; set; }

        // -------- Snapshot del producto --------
        [Required, StringLength(200)]
        public string NombreProducto { get; set; } = string.Empty;

        [StringLength(100)]
        public string? SKU { get; set; }

        [StringLength(50)]
        public string? Color { get; set; }

        [StringLength(50)]
        public string? Talla { get; set; }

        [StringLength(300)]
        public string? ImagenPath { get; set; }

        // -------- Cantidades y precios --------
        [Range(1, int.MaxValue)]
        public int Cantidad { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PrecioUnitario { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Subtotal { get; set; }

        // -------- Peso --------
        [Column(TypeName = "decimal(18,3)")]
        public decimal PesoUnitarioKg { get; set; }

        [Column(TypeName = "decimal(18,3)")]
        public decimal PesoTotalKg { get; set; }

        // -------- Navegación --------
        [ForeignKey(nameof(SubPedidoId))]
        public virtual SubPedido SubPedido { get; set; } = null!;

        [ForeignKey(nameof(ProductoId))]
        public virtual Producto? Producto { get; set; }

        [ForeignKey(nameof(VarianteId))]
        public virtual ProductoVariante? Variante { get; set; }

        // -------- Métodos --------
        public void CalcularSubtotal()
        {
            Subtotal = PrecioUnitario * Cantidad;
            PesoTotalKg = PesoUnitarioKg * Cantidad;
        }

        /// <summary>
        /// Llenar datos desde un producto
        /// </summary>
        public void DesnormalizarDesdeProducto(Producto producto, ProductoVariante? variante = null)
        {
            NombreProducto = producto.Nombre;
            ImagenPath = producto.ImagenPrincipalPath;
            PrecioUnitario = variante?.PrecioVenta ?? producto.PrecioVenta;
            Color = variante?.Color ?? producto.Color;
            Talla = variante?.Talla ?? producto.Talla;
            SKU = variante?.SKU;
        }
    }
}