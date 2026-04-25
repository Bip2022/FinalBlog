using FinalBlog.Helpers;
using FinalBlog.Models;
using FinalBlog.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace FinalBlog.Controllers;

[Authorize(Roles = "Admin,User")]
public class BlogController : Controller
{
    private readonly AppDbContext _context;

    private readonly ILogger<BlogController> _logger;
    private readonly ImageHelper _imageHelper;

    public BlogController(AppDbContext context, ILogger<BlogController> logger, ImageHelper imageHelper)
    {
        _context = context;
        _logger = logger;
        _imageHelper = imageHelper;
    }

    private static IQueryable<Blog> PublishedOnly(IQueryable<Blog> query) =>
        query.Where(b => b.IsApproved && !b.IsDraft);

    [AllowAnonymous]
    public async Task<IActionResult> Index()
    {
        _logger.LogInformation("Fetching all blogs started.");
        var blogs = await PublishedOnly(_context.Blogs)
            .Include(b => b.Category)
            .Include(b => b.Likes)
            .OrderByDescending(b => b.CreatedDate)
            .ToListAsync();

        var categories = await _context.Categories
            .OrderBy(c => c.Name)
            .ToListAsync();

        var trendingBlogs = await PublishedOnly(_context.Blogs)
            .Include(b => b.Category)
            .Include(b => b.Likes)
            .OrderByDescending(b => b.Likes.Count)
            .ThenByDescending(b => b.CreatedDate)
            .Take(3)
            .ToListAsync();

        ViewBag.Categories = categories;
        ViewBag.TrendingBlogs = trendingBlogs;
        ViewBag.LikedBlogIds = await GetLikedBlogIdsForUserAsync(blogs.Select(b => b.Id));

        _logger.LogInformation("Fetched {Count} blogs successfully.", blogs.Count);
        return View(blogs);
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Search(string query)
    {
        _logger.LogInformation("Search request received: {Query}", query);
        if (string.IsNullOrWhiteSpace(query))
        {
            _logger.LogWarning("Empty search query redirected to Index");
            TempData["ErrorMessage"] = "Please enter something to search.";
            return RedirectToAction("Index");
        }

        var blogs = await PublishedOnly(_context.Blogs)
            .Include(b => b.Category)
            .Include(b => b.Likes)
            .Where(b =>
                b.Title.Contains(query) ||
                b.Text.Contains(query) ||
                (b.Category != null && b.Category.Name.Contains(query)))
            .OrderByDescending(b => b.CreatedDate)
            .ToListAsync();

        var categories = await _context.Categories
            .OrderBy(c => c.Name)
            .ToListAsync();

        var trendingBlogs = await PublishedOnly(_context.Blogs)
            .Include(b => b.Category)
            .Include(b => b.Likes)
            .OrderByDescending(b => b.Likes.Count)
            .ThenByDescending(b => b.CreatedDate)
            .Take(3)
            .ToListAsync();

        ViewBag.Categories = categories;
        ViewBag.TrendingBlogs = trendingBlogs;
        ViewBag.LikedBlogIds = await GetLikedBlogIdsForUserAsync(blogs.Select(b => b.Id));
        _logger.LogInformation("Search completed. Found {Count} results for {Query}", blogs.Count, query);
        if (blogs.Count == 0)
        {
            TempData["Info"] = "No blogs found for your search.";
        }
        ViewBag.Query = query;

        return View("Index", blogs);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        _logger.LogInformation("Create blog page opened");
        await PopulateCreateViewBagsAsync();
        var blogViewModel = new BlogViewModel();
        blogViewModel.Blog.Author = User.Identity?.Name!;
        return View(blogViewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BlogViewModel blogViewModel, string submitAction)
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId == null)
            return Challenge();

        var saveAsDraft = string.Equals(submitAction, "draft", StringComparison.OrdinalIgnoreCase);
        ApplyPublishOrDraftValidation(saveAsDraft, blogViewModel);

        if (!ModelState.IsValid)
        {
            await PopulateCreateViewBagsAsync();
            blogViewModel.Blog.Author = User.Identity?.Name!;
            return View(blogViewModel);
        }

        string? imagePath;
        try
        {
            imagePath = await _imageHelper.UploadBlogImageAsync(blogViewModel.ImageFile);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Blog image upload failed");
            ModelState.AddModelError(nameof(BlogViewModel.ImageFile), "Invalid image. Use JPG, PNG, or GIF under 5MB.");
            await PopulateCreateViewBagsAsync();
            blogViewModel.Blog.Author = User.Identity?.Name ?? string.Empty;
            return View(blogViewModel);
        }

        var title = saveAsDraft
            ? NormalizeDraftTitle(blogViewModel.Blog.Title)
            : (blogViewModel.Blog.Title ?? "").Trim();
        var body = blogViewModel.Blog.Text ?? string.Empty;
        var isAdmin = User.IsInRole("Admin");
        var blog = new Blog
        {
            Title = title,
            Text = body,
            Author = User.Identity?.Name ?? string.Empty,
            AuthorUserId = userId,
            CategoryId = blogViewModel.Blog.CategoryId,
            ImagePath = imagePath,
            CreatedDate = DateTime.UtcNow,
            IsDraft = saveAsDraft,
            IsApproved = !saveAsDraft && isAdmin // Admin posts are auto-approved when not draft
        };

        _context.Blogs.Add(blog);

        if (!saveAsDraft)
        {
            if (isAdmin)
            {
                // Admin creating a non-draft blog - auto-approved
                _context.Activities.Add(new Activity
                {
                    Action = "Blog Created",
                    Username = User.Identity?.Name,
                    Type = "Blog",
                    BlogTitle = blog.Title,
                    CreatedAt = DateTime.UtcNow
                });
                TempData["SuccessMessage"] = "Blog published successfully.";
            }
            else
            {
                // Regular user creating a non-draft blog - needs approval
                _context.Activities.Add(new Activity
                {
                    Action = "Blog Created",
                    Username = User.Identity?.Name,
                    Type = "Blog",
                    BlogTitle = blog.Title,
                    CreatedAt = DateTime.UtcNow
                });
                TempData["Info"] = "Your blog will be posted after admin approval.";
            }
        }
        else
        {
            // Saving as draft
            TempData["SuccessMessage"] = "Draft saved. You can continue editing from this page anytime.";
        }

        await _context.SaveChangesAsync();
        return saveAsDraft ? RedirectToAction(nameof(Create)) : RedirectToAction("Index");
    }


    [HttpGet]
    public async Task<IActionResult> EditDraft(int id)
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId == null)
            return Challenge();

        var blog = await _context.Blogs
            .FirstOrDefaultAsync(b => b.Id == id && b.IsDraft && b.AuthorUserId == userId.Value);
        if (blog == null)
        {
            TempData["ErrorMessage"] = "Draft not found.";
            return RedirectToAction(nameof(Create));
        }

        await PopulateCreateViewBagsAsync();
        var vm = new BlogViewModel { Blog = blog };
        return View("Create", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditDraft(BlogViewModel blogViewModel, string submitAction)
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId == null)
            return Challenge();

        var existing = await _context.Blogs
            .FirstOrDefaultAsync(b => b.Id == blogViewModel.Blog.Id && b.IsDraft && b.AuthorUserId == userId.Value);
        if (existing == null)
        {
            TempData["ErrorMessage"] = "Draft not found.";
            return RedirectToAction(nameof(Create));
        }

        var saveAsDraft = string.Equals(submitAction, "draft", StringComparison.OrdinalIgnoreCase);
        var isAdmin = User.IsInRole("Admin");
        ApplyPublishOrDraftValidation(saveAsDraft, blogViewModel);

        if (!ModelState.IsValid)
        {
            await PopulateCreateViewBagsAsync();
            blogViewModel.Blog.Author = User.Identity?.Name!;
            return View("Create", blogViewModel);
        }

        if (blogViewModel.ImageFile is { Length: > 0 })
        {
            try
            {
                var newPath = await _imageHelper.UploadBlogImageAsync(blogViewModel.ImageFile);
                if (!string.IsNullOrEmpty(newPath))
                    existing.ImagePath = newPath;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Blog image upload failed");
                ModelState.AddModelError(nameof(BlogViewModel.ImageFile), "Invalid image. Use JPG, PNG, or GIF under 5MB.");
                await PopulateCreateViewBagsAsync();
                blogViewModel.Blog.Author = User.Identity?.Name!;
                return View("Create", blogViewModel);
            }
        }

        existing.Title = saveAsDraft
            ? NormalizeDraftTitle(blogViewModel.Blog.Title)
            : (blogViewModel.Blog.Title ?? "").Trim();
        existing.Text = blogViewModel.Blog.Text ?? string.Empty;
        existing.CategoryId = blogViewModel.Blog.CategoryId;
        existing.Author = User.Identity?.Name ?? existing.Author;

        if (saveAsDraft)
        {
            existing.IsDraft = true;
            existing.IsApproved = false;
            TempData["SuccessMessage"] = "Draft saved.";
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(EditDraft), new { id = existing.Id });
        }

        existing.IsDraft = false;
        existing.IsApproved = !isAdmin; // Admin posts are auto-approved when published from draft
        existing.Author = User.Identity?.Name ?? existing.Author;

        _context.Activities.Add(new Activity
        {
            Action = "Blog Created",
            Username = User.Identity?.Name,
            Type = "Blog",
            BlogTitle = existing.Title,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        if (isAdmin)
        {
            TempData["SuccessMessage"] = "Blog published successfully.";
        }
        else
        {
            TempData["Info"] = "Your blog will be posted after admin approval.";
        }
        return RedirectToAction("Index");
    }

    public async Task<IActionResult> Details(int id)
    {
        var blog = await PublishedOnly(_context.Blogs)
            .Where(b => b.Id == id)
            .Include(b => b.Category)
            .Include(b => b.Likes)
            .Include(b => b.Comments)
            .FirstOrDefaultAsync();

        if (blog == null)
        {
            _logger.LogWarning("Blog with ID {Id} not found or not approved", id);
            TempData["ErrorMessage"] = "Blog not found.";
            return NotFound();
        }
        _logger.LogInformation("Blog with ID {Id} retrieved successfully", id);

        var userId = await GetCurrentUserIdAsync();
        var isLiked = userId.HasValue &&
            await _context.Likes.AnyAsync(l => l.BlogId == id && l.UserId == userId.Value);

        // Load sidebar data
        var categories = await _context.Categories
            .OrderBy(c => c.Name)
            .ToListAsync();

        var trendingBlogs = await PublishedOnly(_context.Blogs)
            .Include(b => b.Category)
            .Include(b => b.Likes)
            .OrderByDescending(b => b.Likes.Count)
            .ThenByDescending(b => b.CreatedDate)
            .Take(3)
            .ToListAsync();

        ViewBag.Categories = categories;
        ViewBag.TrendingBlogs = trendingBlogs;

        var vm = new BlogDetailsViewModel
        {
            Blog = blog,
            IsLikedByCurrentUser = isLiked
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(int blogId, string text)
    {
        text = text?.Trim() ?? string.Empty;
        if (text.Length < 2 || text.Length > 500)
        {
            TempData["ErrorMessage"] = "Comment must be between 2 and 500 characters.";
            return RedirectToAction(nameof(Details), new { id = blogId });
        }

        var blogExists = await PublishedOnly(_context.Blogs).AnyAsync(b => b.Id == blogId);
        if (!blogExists)
        {
            TempData["ErrorMessage"] = "Blog not found.";
            return RedirectToAction(nameof(Index));
        }

        var userId = await GetCurrentUserIdAsync();
        if (userId == null)
        {
            TempData["ErrorMessage"] = "You must be signed in to comment.";
            return RedirectToAction(nameof(Details), new { id = blogId });
        }

        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId.Value);
        if (user == null)
        {
            TempData["ErrorMessage"] = "Account not found.";
            return RedirectToAction(nameof(Details), new { id = blogId });
        }

        _context.Comments.Add(new Comment
        {
            BlogId = blogId,
            UserName = user.Username,
            Text = text,
            CommentDate = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Comment posted.";
        return RedirectToAction(nameof(Details), new { id = blogId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleLike(int blogId, string returnUrl)
    {
        try
        {
            var blogExists = await PublishedOnly(_context.Blogs).AnyAsync(b => b.Id == blogId);
            if (!blogExists)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "Blog not found." });
                }
                TempData["Error"] = "Blog not found.";
                return RedirectToAction(returnUrl == "Index" ? nameof(Index) : nameof(Details), returnUrl == "Index" ? null : new { id = blogId });
            }

            var userId = await GetCurrentUserIdAsync();
            if (userId == null)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "You must be signed in to like posts." });
                }
                TempData["Error"] = "You must be signed in to like posts.";
                return RedirectToAction(returnUrl == "Index" ? nameof(Index) : nameof(Details), returnUrl == "Index" ? null : new { id = blogId });
            }

            var existing = await _context.Likes
                .FirstOrDefaultAsync(l => l.BlogId == blogId && l.UserId == userId.Value);

            bool isLiked = false;
            int likeCount;
            string message;

            if (existing != null)
            {
                _context.Likes.Remove(existing);
                isLiked = false;
                message = "Like removed.";
            }
            else
            {
                _context.Likes.Add(new Like
                {
                    BlogId = blogId,
                    UserId = userId.Value
                });
                isLiked = true;
                message = "Thanks for liking this post!";
            }

            await _context.SaveChangesAsync();

            likeCount = await _context.Likes.CountAsync(l => l.BlogId == blogId);

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success = true, liked = isLiked, likeCount = likeCount, message = message });
            }

            TempData[isLiked ? "Success" : "Info"] = message;
            return RedirectToAction(returnUrl == "Index" ? nameof(Index) : nameof(Details), returnUrl == "Index" ? null : new { id = blogId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ToggleLike for blogId {BlogId}", blogId);
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success = false, message = "An error occurred while processing your request." });
            }
            TempData["Error"] = $"An error occurred: {ex.Message}";
            return RedirectToAction(returnUrl == "Index" ? nameof(Index) : nameof(Details), returnUrl == "Index" ? null : new { id = blogId });
        }
    }

    private async Task PopulateCreateViewBagsAsync()
    {
        ViewBag.Categories = await _context.Categories
            .OrderBy(c => c.Name)
            .Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Name
            })
            .ToListAsync();
        await LoadDraftsForCreateViewAsync();
        _logger.LogInformation("Categories loaded successfully.");
    }

    private async Task LoadDraftsForCreateViewAsync()
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId == null)
        {
            ViewBag.Drafts = new List<Blog>();
            return;
        }

        ViewBag.Drafts = await _context.Blogs
            .AsNoTracking()
            .Where(b => b.IsDraft && b.AuthorUserId == userId.Value)
            .OrderByDescending(b => b.CreatedDate)
            .ToListAsync();
    }

    private void ApplyPublishOrDraftValidation(bool saveAsDraft, BlogViewModel blogViewModel)
    {
        if (saveAsDraft)
        {
            foreach (var key in ModelState.Keys.Where(k =>
                         k.StartsWith("Blog.", StringComparison.Ordinal) &&
                         (k.EndsWith("Title", StringComparison.Ordinal) ||
                          k.EndsWith("Text", StringComparison.Ordinal) ||
                          k.EndsWith("CategoryId", StringComparison.Ordinal))).ToList())
                ModelState.Remove(key);
            return;
        }

        if (!blogViewModel.Blog.CategoryId.HasValue || blogViewModel.Blog.CategoryId.Value <= 0)
            ModelState.AddModelError("Blog.CategoryId", "Please select a category.");

        var text = blogViewModel.Blog.Text?.Trim() ?? string.Empty;
        if (text.Length < 20)
            ModelState.AddModelError("Blog.Text", "Text should be at least 20 characters long.");
    }

    private static string NormalizeDraftTitle(string? title)
    {
        var t = string.IsNullOrWhiteSpace(title) ? "Untitled draft" : title.Trim();
        return t.Length > 200 ? t[..200] : t;
    }

    private async Task<int?> GetCurrentUserIdAsync()
    {
        if (User?.Identity?.IsAuthenticated != true)
            return null;

        var idStr = User.FindFirst("UserId")?.Value;
        if (int.TryParse(idStr, out var id))
            return id;

        var name = User.Identity!.Name;
        if (string.IsNullOrEmpty(name))
            return null;

        var user = await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == name);
        return user?.Id;
    }

    private async Task<HashSet<int>> GetLikedBlogIdsForUserAsync(IEnumerable<int> blogIds)
    {
        var idList = blogIds.Distinct().ToList();
        if (idList.Count == 0)
            return new HashSet<int>();

        var userId = await GetCurrentUserIdAsync();
        if (userId == null)
            return new HashSet<int>();

        var liked = await _context.Likes
            .AsNoTracking()
            .Where(l => l.UserId == userId.Value && idList.Contains(l.BlogId))
            .Select(l => l.BlogId)
            .ToListAsync();
        return liked.ToHashSet();
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> ByCategory(int id)
    {
        var category = await _context.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (category == null)
        {
            TempData["ErrorMessage"] = "Category not found.";
            return RedirectToAction(nameof(Index));
        }

        var blogs = await PublishedOnly(_context.Blogs)
            .Where(b => b.CategoryId == id)
            .Include(b => b.Category)
            .Include(b => b.Likes)
            .OrderByDescending(b => b.CreatedDate)
            .ToListAsync();

        var categories = await _context.Categories
            .OrderBy(c => c.Name)
            .ToListAsync();

        var trendingBlogs = await PublishedOnly(_context.Blogs)
            .Include(b => b.Category)
            .Include(b => b.Likes)
            .OrderByDescending(b => b.Likes.Count)
            .ThenByDescending(b => b.CreatedDate)
            .Take(3)
            .ToListAsync();

        ViewBag.Categories = categories;
        ViewBag.TrendingBlogs = trendingBlogs;
        ViewBag.CategoryFilterName = category.Name;
        ViewBag.ActiveCategoryId = id;
        ViewBag.LikedBlogIds = await GetLikedBlogIdsForUserAsync(blogs.Select(b => b.Id));

        _logger.LogInformation("ByCategory {CategoryId} ({Name}): {Count} posts", id, category.Name, blogs.Count);
        return View(nameof(Index), blogs);
    }

    // GET: Blog/Delete/5
    [HttpGet]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var isAdmin = User.IsInRole("Admin");
        if (!isAdmin)
        {
            return Forbid();
        }

        var blog = await _context.Blogs
            .Include(b => b.Category)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (blog == null)
        {
            return NotFound();
        }

        return View(blog);
    }

    // POST: Blog/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var isAdmin = User.IsInRole("Admin");
        if (!isAdmin)
        {
            return Forbid();
        }

        var blog = await _context.Blogs
            .Include(b => b.Category)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (blog == null)
        {
            return NotFound();
        }

        // Delete the associated image file if exists
        if (!string.IsNullOrEmpty(blog.ImagePath))
        {
            _imageHelper.DeleteImage(blog.ImagePath);
        }

        // Remove associated likes and comments first (if not handled by cascade)
        var likes = _context.Likes.Where(l => l.BlogId == id);
        _context.Likes.RemoveRange(likes);

        var comments = _context.Comments.Where(c => c.BlogId == id);
        _context.Comments.RemoveRange(comments);

        // Add activity log
        _context.Activities.Add(new Activity
        {
            Action = "Blog Deleted",
            Username = blog.Author,
            Type = "Blog",
            BlogTitle = blog.Title,
            CreatedAt = DateTime.UtcNow
        });

        _context.Blogs.Remove(blog);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Blog deleted successfully.";
        return RedirectToAction(nameof(Index));
    }
}
