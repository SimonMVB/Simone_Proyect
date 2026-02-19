using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Simone.Models
{
    /// <summary>
    /// Grupo de tiendas que comparten configuración de envío.
    /// Todas las tiendas de una alianza deben estar en el mismo Hub.
    /// </summary>
    public class AlianzaEnvio
    {
        public int AlianzaId { get; set; }

        [Required]
        public int HubId { get; set; }  // Todas las tiendas deben estar en este Hub

        [Required, StringLength(100)]
        public string Nombre { get; set; } = string.Empty;  // "ModaEC" (interno)

        [StringLength(500)]
        public string? Descripcion { get; set; }

        public bool Activo { get; set; } = true;

        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

        // -------- Navegación --------
        public virtual HubEnvio Hub { get; set; } = null!;
        public virtual ICollection<Vendedor> Vendedores { get; set; } = new List<Vendedor>();

        // -------- Propiedades calculadas --------
        public int TotalVendedores => Vendedores?.Count ?? 0;
    }
}