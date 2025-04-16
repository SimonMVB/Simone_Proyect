using System.ComponentModel.DataAnnotations;

namespace Simone.ViewModels
{
    public class ForgotPasswordViewModel
    {
        [Required, EmailAddress]
        [Display(Name = "Correo electrónico")]
        public string Email { get; set; }
    }
}
