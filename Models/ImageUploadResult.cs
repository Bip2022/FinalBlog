namespace FinalBlog.Models
{
  public class ImageUploadResult
  {
    public string? Url { get; set; }
    public bool IsDuplicate { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
  }
}