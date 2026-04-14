public class ActivityViewModel
{
    public string? Action { get; set; }     // e.g. "New Blog Pending"
    public string? Type { get; set; }       // "Blog" or "User"
    public string? Username { get; set; }   // Name of user
    public string? BlogTitle { get; set; }  // Title if it's a blog activity
    public DateTime CreatedAt { get; set; }
}