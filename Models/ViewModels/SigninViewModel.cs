using System.ComponentModel.DataAnnotations;

namespace FinalBlog.Models.ViewModels
{
  public class SigninViewModel
  {
    [Required(ErrorMessage = "UsernameOrEmail is required")]
    [StringLength(70, ErrorMessage = "UsernameOrEmail must be under 70 characters")]
    public string UsernameOrEmail { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Range(typeof(bool), "false", "true")]
    public bool RememberMe { get; set; }

    [Required]
    public string Role { get; set; } = "User";

    /// <summary>Local URL to open after successful sign-in (e.g. /Blog/Details/5).</summary>
    public string? ReturnUrl { get; set; }
  }
}