namespace FinalBlog.Models
{
  public class Activity
  {
    public int Id { get; set; }
    public string Action { get; set; } = "";
    public string? Username { get; set; }
    public string? Type { get; set; } // "User" or "Blog"
    public string? BlogTitle { get; set; }

    public string? ImagePath { get; set; } // ✅ add this
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
  }
}