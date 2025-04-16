using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Simone.Models
{
    /// <summary>
    /// Representa la relación de fidelización entre un cliente y un programa.
    /// </summary>
    public class ClientesProgramas
    {
        [Key, Column(Order = 1)]
        public int ClienteID { get; set; }

        [Key, Column(Order = 2)]
        public int ProgramaID { get; set; }

        /// <summary>
        /// Fecha en la que el cliente se unió al programa.
        /// </summary>
        public DateTime? FechaInicio { get; set; }

        /// <summary>
        /// Cliente relacionado con el programa.
        /// </summary>
        [ForeignKey(nameof(ClienteID))]
        public virtual Cliente Cliente { get; set; } = null!;

        /// <summary>
        /// Programa de fidelización relacionado.
        /// </summary>
        [ForeignKey(nameof(ProgramaID))]
        public virtual ProgramasFidelizacion Programa { get; set; } = null!;
    }
}
