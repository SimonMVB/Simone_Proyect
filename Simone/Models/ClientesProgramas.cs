using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Simone.Models
{
    /// <summary>
    /// Relación de fidelización entre un usuario y un programa.
    /// </summary>
    public class ClientesProgramas
    {
        // Clave compuesta: UsuarioId + ProgramaID
        [Key, Column(Order = 0)]
        [Required]
        public string UsuarioId { get; set; } = default!;

        [Key, Column(Order = 1)]
        [Required]
        public int ProgramaID { get; set; }

        /// <summary>Fecha en la que el usuario se unió al programa.</summary>
        public DateTime? FechaInicio { get; set; }

        // Navegaciones
        [ForeignKey(nameof(UsuarioId))]
        public virtual Usuario Usuario { get; set; } = null!;

        [ForeignKey(nameof(ProgramaID))]
        public virtual ProgramasFidelizacion Programa { get; set; } = null!;
    }
}
