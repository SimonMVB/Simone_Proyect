using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Simone.Models
{
    public class DireccionesCliente
    {
        [Key] // ✅ Definir clave primaria
        public int DireccionID { get; set; }

        public int ClienteID { get; set; }  // Clave foránea con Clientes
        public string Calle { get; set; }
        public string Ciudad { get; set; }
        public string EstadoProvincia { get; set; }
        public string CodigoPostal { get; set; }
        public string? TelefonoContacto { get; set; }  // Puede ser nulo
        public DateTime FechaRegistro { get; set; }

        // Relación con Clientes
        [ForeignKey("ClienteID")]
        public Cliente Cliente { get; set; }
    }
}
