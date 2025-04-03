using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;
using Simone.Models;

namespace Simone.Models
{
    public class Cliente
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ClienteID { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(100, ErrorMessage = "El nombre no puede tener más de 100 caracteres.")]
        public string Nombre { get; set; }

        [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
        [StringLength(100)]
        [EmailAddress(ErrorMessage = "Debe ser una dirección de correo válida.")]
        public string Email { get; set; }

        [StringLength(15, ErrorMessage = "El teléfono no puede tener más de 15 caracteres.")]
        public string Telefono { get; set; }

        [StringLength(200, ErrorMessage = "La dirección no puede tener más de 200 caracteres.")]
        public string Direccion { get; set; }

        [DataType(DataType.Date)]
        public DateTime FechaRegistro { get; set; } = DateTime.Now;

        // Relación con Pedidos (Un Cliente puede tener muchos Pedidos)
        public ICollection<Pedido>? Pedidos { get; set; }
        public ICollection<Reseñas> Reseñas { get; set; }

        public ICollection<CuponesUsados> CuponesUsados { get; set; } = new List<CuponesUsados>();


    }
}
