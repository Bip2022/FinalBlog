using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace FinalBlog.Models
{
  public class Blog
  {
    [Key]
    public int Id { get; set; }

    [Required(ErrorMessage = "Title is required.")]
    [StringLength(200, MinimumLength = 5, ErrorMessage = "Title must be between 5 and 200 characters.")]
    public string Title { get; set; } = string.Empty;

    [DataType(DataType.MultilineText)]
    public string Text { get; set; } = string.Empty;

    [Required(ErrorMessage = "Author name is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Author name must be between 2 and 100 characters.")]
    public string Author { get; set; } = string.Empty;

    public int? AuthorUserId { get; set; }
    [ForeignKey("AuthorUserId")]
    public User? AuthorUser { get; set; }

    [Display(Name = "Product Image")]
    public string? ImagePath { get; set; }

    [DataType(DataType.Date)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public int? CategoryId { get; set; }
    [ForeignKey("CategoryId")]
    public Category? Category { get; set; }

    public ICollection<Comment>? Comments { get; set; } // Navigation property to related comments

    public ICollection<Like> Likes { get; set; } = new List<Like>();

    [NotMapped]
    public int LikeCount => Likes?.Count ?? 0;

    public bool IsApproved { get; set; } = false;

    public bool IsDraft { get; set; }

  }
}