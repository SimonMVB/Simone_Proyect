using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Simone.Models
{
    public class Usuario : IdentityUser
    {
        // -------- Perfil / contacto --------
        [Required, StringLength(100)]
        public string NombreCompleto { get; set; } = string.Empty;

        [Column(TypeName = "nvarchar(10)")]
        [StringLength(10)]
        public string? Cedula { get; set; }

        [Phone, StringLength(20)]
        public string? Telefono { get; set; }

        [StringLength(300)]
        public string? FotoPerfil { get; set; }

        public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
        public bool Activo { get; set; } = true;

        [Required]
        public string RolID { get; set; } = string.Empty;

        // -------- Información de envío --------
        [StringLength(200)]
        public string? Direccion { get; set; }

        [StringLength(100)]
        public string? Ciudad { get; set; }

        [StringLength(100)]
        public string? Provincia { get; set; }

        [StringLength(20)]
        public string? CodigoPostal { get; set; }

        [StringLength(200)]
        public string? Referencia { get; set; }

        [StringLength(150)]
        public string? NombreContactoEnvio { get; set; }

        [StringLength(1000)]
        public string? InstruccionesEnvio { get; set; }

        // -------- Pago por depósito / transferencia --------
        [StringLength(150)]
        public string? NombreDepositante { get; set; }

        [StringLength(300)]
        public string? FotoComprobanteDeposito { get; set; }

        // -------- Asociación a tienda (Vendedor) --------
        public int? VendedorId { get; set; }

        [ForeignKey(nameof(VendedorId))]
        public Vendedor? Vendedor { get; set; }

        // -------- Relaciones existentes --------
        public virtual ICollection<ActividadUsuario> Actividades { get; set; } = new HashSet<ActividadUsuario>();
        public virtual ICollection<LogIniciosSesion> LogsInicioSesion { get; set; } = new HashSet<LogIniciosSesion>();
    }
}
