using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;
using Simone.Models;

namespace Simone.Models
{
    public class Carrito
    {
        [Key]
        public int CarritoID { get; set; } // Clave primaria

        // Relación con Cliente
        [Required]
        public int ClienteID { get; set; }
        public Cliente Cliente { get; set; }

        [Required]
        public DateTime FechaCreacion { get; set; } = DateTime.Now; // Fecha en la que se creó el carrito

        [Required]
        [StringLength(50)]
        public string EstadoCarrito { get; set; } // Estado del carrito (Ejemplo: "Activo", "Procesado", "Cancelado")

        // Relación con Detalles del Carrito
        public ICollection<CarritoDetalle> CarritoDetalles { get; set; }
    }
}
