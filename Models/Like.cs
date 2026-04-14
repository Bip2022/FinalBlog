using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinalBlog.Models
{
  public class Like
  {
    [Key]
    public int Id { get; set; }

    // User who liked
    public int UserId { get; set; }

    [ForeignKey("UserId")]
    public User User { get; set; } = null!;

    // Post that is liked
    public int BlogId { get; set; }

    [ForeignKey("BlogId")]
    public Blog Blog { get; set; } = null!;

  }
}