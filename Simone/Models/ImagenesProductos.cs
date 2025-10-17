// Models/ImagenesProductos.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Simone.Models
{
    /// <summary>
    /// Modelo LEGADO de imágenes por producto.
    /// Mantener para compatibilidad con partes antiguas del sistema.
    /// </summary>
    [Index(nameof(ProductoID), nameof(RutaImagen), IsUnique = true, Name = "IX_ImagenesProductos_Producto_Ruta")] // evita duplicados
    public class ImagenesProductos
    {
        // --------- Clave ---------
        [Key]
        public int ImagenID { get; set; }

        // --------- FK ----------
        [Required]
        public int ProductoID { get; set; }

        // --------- Datos --------
        [Required, StringLength(300)]
        [Unicode(false)] // guarda la ruta como ASCII/UTF8 sin NVARCHAR si usas SQL Server
        public string RutaImagen { get; set; } = null!; // Ruta absoluta o relativa (p.e. /images/Productos/{id}/file.jpg)

        /// <summary>
        /// Orden relativo dentro de la galería (0 por defecto).
        /// </summary>
        [Range(0, int.MaxValue)]
        public int Orden { get; set; }

        /// <summary>
        /// Marca si esta imagen es la principal (fallback para UIs antiguas).
        /// </summary>
        public bool Principal { get; set; }

        // --------- Navegación --------
        [ForeignKey(nameof(ProductoID))]
        public virtual Producto Producto { get; set; } = null!;
    }
}
