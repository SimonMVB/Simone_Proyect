using System.ComponentModel.DataAnnotations;

namespace Simone.ViewModels
{
    public class CambiarPasswordViewModel
    {
        [Required(ErrorMessage = "Debes ingresar tu contraseña actual.")]
        [DataType(DataType.Password)]
        [Display(Name = "Contraseña actual")]
        public string PasswordActual { get; set; }

        [Required(ErrorMessage = "Ingresa una nueva contraseña.")]
        [StringLength(100, ErrorMessage = "Debe tener al menos {2} caracteres.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Nueva contraseña")]
        public string NuevaPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirmar nueva contraseña")]
        [Compare("NuevaPassword", ErrorMessage = "La nueva contraseña y la confirmación no coinciden.")]
        public string ConfirmarPassword { get; set; }
    }
}
