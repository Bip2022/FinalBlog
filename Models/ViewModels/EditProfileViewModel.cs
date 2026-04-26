
using System.ComponentModel.DataAnnotations;

namespace FinalBlog.Models.ViewModels
{
  public class EditProfileViewModel
  {
    [Required]
    public string? Username { get; set; }

    [Required]
    [EmailAddress]
    public string? Email { get; set; }

    public string? ExistingProfileImage { get; set; }

  public IFormFile? ProfileImage { get; set; }


}
}