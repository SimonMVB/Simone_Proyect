using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Simone.Models;

namespace Simone.Models
{
    public class AuditoriaProductos
    {
        [Key]
        public int AuditoriaID { get; set; } // Clave primaria

        // Relación con Productos (Opcional)
        public int? ProductoID { get; set; }
        public Productos Producto { get; set; }

        [Required]
        [StringLength(500)]
        public string Cambio { get; set; } // Descripción del cambio

        [Required]
        public DateTime FechaCambio { get; set; } = DateTime.Now; // Fecha del cambio

        [Required]
        [StringLength(100)]
        public string Usuario { get; set; } // Usuario que realizó el cambio
    }
}
