using System;
using Simone.Models;

namespace Simone.Models
{
    public class DireccionesCliente
    {
        public int DireccionID { get; set; }  // Clave primaria
        public int ClienteID { get; set; }  // Clave foránea con Clientes
        public string Calle { get; set; }  // Nombre de la calle
        public string Ciudad { get; set; }  // Ciudad
        public string EstadoProvincia { get; set; }  // Estado o provincia
        public string CodigoPostal { get; set; }  // Código postal
        public string? TelefonoContacto { get; set; }  // Puede ser nulo
        public DateTime FechaRegistro { get; set; }  // Fecha de registro

        // Relación con Clientes
        public Cliente Clientes { get; set; }
    }
}
