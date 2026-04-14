using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinalBlog.Models
{
  public class Comment
  {
    [Key]
    public int Id { get; set; }
    [Required(ErrorMessage = "User name is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "User name must be between 2 and 100 characters.")]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Comment text cannot be empty.")]
    [StringLength(500, MinimumLength = 2, ErrorMessage = "Comment must be between 2 and 500 characters.")]
    [Display(Name = "Your Comment")]
    public string Text { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    public DateTime CommentDate { get; set; } = DateTime.UtcNow;

    public int BlogId { get; set; } // Foreign key to Blog
    [ForeignKey("BlogId")]
    public Blog? Blog { get; set; } // Navigation property to Blog

  }
}