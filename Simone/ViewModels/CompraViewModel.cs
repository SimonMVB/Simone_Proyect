using System.ComponentModel.DataAnnotations;

namespace Simone.Models
{
    public class CompraViewModel
    {
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [Display(Name = "Nombre del Cliente")]
        public string NombreCliente { get; set; }

        [Required(ErrorMessage = "El correo es obligatorio.")]
        [EmailAddress(ErrorMessage = "Debe ser un correo válido.")]
        [Display(Name = "Correo Electrónico")]
        public string EmailCliente { get; set; }

        [Required(ErrorMessage = "La dirección es obligatoria.")]
        [Display(Name = "Dirección de Entrega")]
        public string Direccion { get; set; }

        [Required(ErrorMessage = "Debe seleccionar un método de pago.")]
        [Display(Name = "Método de Pago")]
        public string MetodoPago { get; set; }

        [Display(Name = "Total a Pagar")]
        public decimal Total { get; set; }
    }
}
