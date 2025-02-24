using System;

namespace Simone.Models
{
    public class LogIniciosSesion
    {
        public int LogID { get; set; }  // Clave primaria
        public string Usuario { get; set; }  // Nombre del usuario que inició sesión
        public DateTime? FechaInicio { get; set; }  // Puede ser nulo
        public bool? Exitoso { get; set; }  // Puede ser nulo

    }
}

