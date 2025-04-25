using Microsoft.AspNetCore.Identity;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Simone.Models
{
    public class Usuario : IdentityUser
    {

        [Required, StringLength(100)]
        public string NombreCompleto { get; set; } = string.Empty;

        public DateTime FechaRegistro { get; set; } = DateTime.Now;

        public bool Activo { get; set; } = true;

        // Relación Roles
        [Required]
        public string? RolID { get; set; }

    }
}
