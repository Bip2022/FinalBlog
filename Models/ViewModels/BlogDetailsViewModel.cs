namespace FinalBlog.Models.ViewModels;

public class BlogDetailsViewModel
{
    public Blog Blog { get; set; } = null!;
    public bool IsLikedByCurrentUser { get; set; }
}
