using System.ComponentModel.DataAnnotations;

namespace FinalBlog.Models
{
  public class Category
  {
    [Key] // Marks this as the Primary Key for the database
    public int Id { get; set; }

    [Required(ErrorMessage = "Category name is required.")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 50 characters.")]
    [Display(Name = "Category Name")]
    [RegularExpression(@"^[a-zA-Z\s]+$", ErrorMessage = "Only letters and spaces are allowed.")]
    public string Name { get; set; } = string.Empty;


    public string? Description { get; set; }

    public ICollection<Blog>? Blogs { get; set; } // Navigation property to related blogs


  }
}