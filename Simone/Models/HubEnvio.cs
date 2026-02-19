// Simone/Models/HubEnvio.cs
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Simone.Models
{
    /// <summary>
    /// Punto físico de consolidación de pedidos.
    /// Ejemplo: Hub Milagro, Hub Santo Domingo
    /// </summary>
    public class HubEnvio
    {
        public int HubId { get; set; }
        public virtual ICollection<AlianzaEnvio> Alianzas { get; set; } = new List<AlianzaEnvio>();

        [Required, StringLength(100)]
        public string Nombre { get; set; } = string.Empty;  // "Hub Milagro"

        [Required, StringLength(100)]
        public string Provincia { get; set; } = string.Empty;  // "Guayas"

        [Required, StringLength(100)]
        public string Ciudad { get; set; } = string.Empty;  // "Milagro"

        [StringLength(300)]
        public string? Direccion { get; set; }  // Dirección física del punto

        [StringLength(20)]
        public string? Telefono { get; set; }  // Teléfono de contacto

        public bool Activo { get; set; } = true;

        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

        // -------- Navegación --------

        /// <summary>
        /// Tiendas (vendedores) que entregan sus productos en este Hub
        /// </summary>
        public virtual ICollection<Vendedor> Vendedores { get; set; } = new List<Vendedor>();

        /// <summary>
        /// Usuarios responsables de este Hub (pueden gestionar recolecciones)
        /// </summary>
        public virtual ICollection<Usuario> Responsables { get; set; } = new List<Usuario>();

        // -------- Propiedades calculadas --------

        public int TotalVendedores => Vendedores?.Count ?? 0;
        public int TotalResponsables => Responsables?.Count ?? 0;
    }
}