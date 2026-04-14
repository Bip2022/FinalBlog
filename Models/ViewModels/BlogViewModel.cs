
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace FinalBlog.Models.ViewModels
{
  public class BlogViewModel
  {
    public Blog Blog { get; set; } = new Blog();


    public IEnumerable<SelectListItem> Categories { get; set; } = new List<SelectListItem>();

    public string? ImagePath { get; set; }


    // **Form upload property, not mapped to DB**
    [NotMapped]
    [Display(Name = "Upload Image")]
    public IFormFile? ImageFile { get; set; }

    // ---------------- LIKE FEATURE ----------------

    // Total likes for this blog/post
    public int LikeCount { get; set; }

    // Whether current user already liked this post
    public bool IsLikedByCurrentUser { get; set; }
  }
}