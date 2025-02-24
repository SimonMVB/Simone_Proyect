using System;
using Simone.Models;

namespace Simone.Models
{
    public class ClientesProgramas
    {
        public int ClienteID { get; set; }  // Clave foránea con Clientes
        public int ProgramaID { get; set; }  // Clave foránea con ProgramasFidelizacion
        public DateTime? FechaInicio { get; set; }  // Puede ser nulo

        // Relación con Clientes
        public Cliente Clientes { get; set; }

        // Relación con ProgramasFidelizacion
        public ProgramasFidelizacion Programa { get; set; }
    }
}
