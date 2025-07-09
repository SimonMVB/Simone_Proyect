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

        [Phone]
        public string Telefono { get; set; } = string.Empty;

        [StringLength(200)]
        public string? Direccion { get; set; }

        [StringLength(200)]
        public string? Referencia { get; set; }

        public string? FotoPerfil { get; set; }

        public DateTime FechaRegistro { get; set; } = DateTime.Now;

        public bool Activo { get; set; } = true;

        // Relación Roles
        [Required]
        public string RolID { get; set; }
        [StringLength(100)]
        public string? Ciudad { get; set; }

        [StringLength(100)]
        public string? Provincia { get; set; }

    }
}
