using System.ComponentModel.DataAnnotations;
namespace FinalBlog.Models
{
  public class User
  {


    public int Id { get; set; }


    [Required(ErrorMessage = "Username is required")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid Email Address")]
    public string Email { get; set; } = string.Empty;

    public string? EmailToken { get; set; }

    public DateTime? EmailTokenExpiry { get; set; }

    public bool IsEmailConfirmed { get; set; } = false;

    [Required(ErrorMessage = "Password is required")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    // Everyone starts as a "User"
    public string Role { get; set; } = "User";

    // Defaults to false; Admin must manually flip this to true
    public bool IsApproved { get; set; } = false; 

    public bool IsLocked { get; set; } = false;
    
  public string? ProfileImage { get; set; }
  }
}