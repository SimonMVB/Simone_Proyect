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
        [StringLength(100, ErrorMessage = "El nombre no puede tener m�s de 100 caracteres.")]
        public string Nombre { get; set; }

        [Required(ErrorMessage = "El correo electr�nico es obligatorio.")]
        [StringLength(100)]
        [EmailAddress(ErrorMessage = "Debe ser una direcci�n de correo v�lida.")]
        public string Email { get; set; }

        [StringLength(15, ErrorMessage = "El tel�fono no puede tener m�s de 15 caracteres.")]
        public string Telefono { get; set; }

        [StringLength(200, ErrorMessage = "La direcci�n no puede tener m�s de 200 caracteres.")]
        public string Direccion { get; set; }

        [DataType(DataType.Date)]
        public DateTime FechaRegistro { get; set; } = DateTime.Now;

        // Relaci�n con Pedidos (Un Cliente puede tener muchos Pedidos)
        public ICollection<Pedido>? Pedidos { get; set; }
        public ICollection<Reseñas> Reseñas { get; set; }

        public ICollection<CuponesUsados> CuponesUsados { get; set; } = new List<CuponesUsados>();


    }
}
