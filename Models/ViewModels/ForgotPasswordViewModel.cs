using System.ComponentModel.DataAnnotations;

namespace FinalBlog.Models.ViewModels
{
  public class ForgotPasswordViewModel
  {
    [Required(ErrorMessage = "Email address is required")]
    [EmailAddress(ErrorMessage = "Invalid email address format")]
    [Display(Name = "Registered Email Address")]
    public string Email { get; set; } = string.Empty;
  }
}
