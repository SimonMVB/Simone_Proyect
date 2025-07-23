using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http; // Para IFormFile

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

        // Detalles de Tarjeta
        [Display(Name = "Número de Tarjeta")]
        public string NumeroTarjeta { get; set; }

        [Display(Name = "Expiración")]
        public string Expiracion { get; set; }

        [Display(Name = "CVV")]
        public string CVV { get; set; }

        [Display(Name = "Nombre en la Tarjeta")]
        public string NombreTarjeta { get; set; }

        // Transferencia bancaria
        [Display(Name = "Banco")]
        public string Banco { get; set; }

        [Display(Name = "Comprobante de Depósito")]
        public IFormFile ComprobanteDeposito { get; set; }

        // Lista de productos del carrito (puedes definir tu modelo de ItemCarrito)
        public List<ItemCarritoViewModel> Productos { get; set; }
    }

    // Ejemplo de un modelo para un producto en el carrito
    public class ItemCarritoViewModel
    {
        public int ProductoID { get; set; }
        public string Nombre { get; set; }
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Total { get; set; }
    }
}
