using System.ComponentModel.DataAnnotations;
namespace FinalBlog.Models.ViewModels
{
  public class SignupViewModel
  {

    [Required(ErrorMessage = "Username is required")]
    [StringLength(50, ErrorMessage = "Username must be under 50 characters")]
    public string Username { get; set; } = string.Empty;
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 6)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Compare("Password", ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
  }

}