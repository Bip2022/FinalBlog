using FinalBlog.Models;
using FinalBlog.Models.ViewModels;
using FinalBlog.Services;
using FinalBlog.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinalBlog.Controllers
{
  [Authorize(Roles = "Admin")]
  public class AdminController : Controller
  {
    private readonly AppDbContext _context;
    private readonly EmailService _emailService;
    private readonly ImageHelper _imageHelper;

    public AdminController(AppDbContext context, EmailService emailService, ImageHelper imageHelper)
    {
      _context = context;
      _emailService = emailService;
      _imageHelper = imageHelper;
    }

    public async Task<IActionResult> Dashboard()
    {
      ViewBag.TotalUsers = await _context.Users.CountAsync();
      ViewBag.PendingUsers = await _context.Users.CountAsync(u => !u.IsApproved);
      ViewBag.PendingBlogs = await _context.Blogs.CountAsync(b => !b.IsApproved && !b.IsDraft);
      ViewBag.TotalBlogs = await _context.Blogs.CountAsync();
      ViewBag.TotalComments = await _context.Comments.CountAsync();
      ViewBag.TotalLikes = await _context.Likes.CountAsync();

      var activities = await _context.Activities
          .OrderByDescending(a => a.CreatedAt)
          .Take(5)
          .ToListAsync();

      return View(activities);
    }

    [HttpGet]
    public IActionResult EditProfile()
    {
      return RedirectToAction("EditProfile", "Account");
    }

    // 🔥 Pending users list
    public async Task<IActionResult> PendingUsers()
    {
      var pendingUsers = await _context.Users
          .Where(u => !u.IsApproved)
          .ToListAsync();

      return View(pendingUsers);
    }

    // ✅ Approve user
    public async Task<IActionResult> ApproveUser(int id)
    {
      var user = await _context.Users.FindAsync(id);

      if (user == null)
      {
        TempData["ErrorMessage"] = "User not found!";
        return NotFound();
      }
      user.IsApproved = true;
      _context.Update(user);
      await _context.SaveChangesAsync();

      TempData["Success"] = "User approved successfully!";
      return RedirectToAction("Users");
    }

    // 📋 View all activity logs
    public async Task<IActionResult> Logs(int page = 1)
    {
      const int pageSize = 20;
      var totalActivities = await _context.Activities.CountAsync();
      var activities = await _context.Activities
          .OrderByDescending(a => a.CreatedAt)
          .Skip((page - 1) * pageSize)
          .Take(pageSize)
          .ToListAsync();

      ViewBag.CurrentPage = page;
      ViewBag.TotalPages = (int)Math.Ceiling(totalActivities / (double)pageSize);
      ViewBag.TotalActivities = totalActivities;

      return View(activities);
    }




    // ❌ Reject / Delete user (optional)
    public async Task<IActionResult> DeleteUser(int id)
    {
      var user = await _context.Users.FindAsync(id);

      if (user == null)
        return NotFound();

      _context.Users.Remove(user);
      _context.Activities.Add(new Activity
      {
        Action = "User Deleted",
        Username = user.Username,
        Type = "User",
        CreatedAt = DateTime.UtcNow
      });
      await _context.SaveChangesAsync();

      TempData["Success"] = "User deleted!";
      return RedirectToAction("PendingUsers");
    }

    public async Task<IActionResult> Users()
    {
      var users = await _context.Users
          .Where(u => u.IsApproved == true)
          .ToListAsync();

      return View(users);
    }

    // 🔥 Pending blogs list
    public async Task<IActionResult> PendingBlogs()
    {
      var pendingBlogs = await _context.Blogs
          .Where(b => !b.IsApproved && !b.IsDraft)
          .Include(b => b.Category)
          .ToListAsync();

      return View(pendingBlogs);
    }

    // ✅ Approve blog
    public async Task<IActionResult> ApproveBlog(int id)
    {
      var blog = await _context.Blogs.FindAsync(id);

      if (blog == null)
      {
        TempData["ErrorMessage"] = "Blog not found!";
        return NotFound();
      }
      if (blog.IsDraft)
      {
        TempData["ErrorMessage"] = "Draft posts cannot be approved from the admin queue.";
        return RedirectToAction(nameof(PendingBlogs));
      }
      blog.IsApproved = true;
      _context.Update(blog);
      _context.Activities.Add(new Activity
      {
        Action = "Blog Approved",
        Username = blog.Author,
        Type = "Blog",
        BlogTitle = blog.Title,
        CreatedAt = DateTime.UtcNow
      });

      await _context.SaveChangesAsync();

      // Send in-app notification to the blog author
      var author = await _context.Users.FirstOrDefaultAsync(u => u.Username == blog.Author);
      if (author != null)
      {
        _context.Notifications.Add(new Notification
        {
          UserId = author.Id,
          Message = $"Your blog '{blog.Title}' has been approved and is now live on the site.",
          IsRead = false,
          CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
      }

      TempData["Success"] = "Blog approved successfully!";
      return RedirectToAction("PendingBlogs");
    }



    public async Task<IActionResult> LockUser(int id)
    {
      var user = await _context.Users.FindAsync(id);

      if (user == null)
      {
        TempData["Error"] = "User not found!";
        return RedirectToAction("Users");
      }

      // 🔥 prevent admin from blocking himself
      if (user.Username == User.Identity?.Name)
      {
        TempData["Error"] = "You cannot block your own account!";
        return RedirectToAction("Users");
      }

      user.IsLocked = true;
      _context.Update(user);

      _context.Activities.Add(new Activity
      {
        Action = "User Blocked",
        Username = user.Username,
        Type = "User",
        CreatedAt = DateTime.UtcNow
      });

      await _context.SaveChangesAsync();

      TempData["Success"] = "User blocked successfully!";
      return RedirectToAction("Users");
    }

    public async Task<IActionResult> UnlockUser(int id)
    {
      var user = await _context.Users.FindAsync(id);

      if (user == null)
      {
        TempData["Error"] = "User not found!";
        return RedirectToAction("Users");
      }

      // 🔥 prevent admin from unlocking himself (optional safety)
      if (user.Username == User.Identity?.Name)
      {
        TempData["Error"] = "You cannot change your own status!";
        return RedirectToAction("Users");
      }

      user.IsLocked = false;
      _context.Update(user);
      _context.Activities.Add(new Activity
      {
        Action = "User Unblocked",
        Username = user.Username,
        Type = "User",
        CreatedAt = DateTime.UtcNow
      });

      _context.Activities.Add(new Activity
      {
        Action = "User Approved",
        Username = user.Username,
        Type = "User",
        CreatedAt = DateTime.UtcNow
      });
      await _context.SaveChangesAsync();
      TempData["Success"] = "User unblocked successfully!";
      return RedirectToAction("Users");
    }

    // 👁️ View pending blog details
    public async Task<IActionResult> ViewPendingBlog(int id)
    {
      var blog = await _context.Blogs
          .Include(b => b.Category)
          .FirstOrDefaultAsync(b => b.Id == id);

      if (blog == null)
      {
        TempData["ErrorMessage"] = "Blog not found.";
        return RedirectToAction("PendingBlogs");
      }

      return View(blog);
    }

    // 📋 All blogs (draft, pending, live) — manage / delete from admin
    public async Task<IActionResult> AllBlogs()
    {
      var blogs = await _context.Blogs
          .AsNoTracking()
          .Include(b => b.Category)
          .Include(b => b.Likes)
          .OrderByDescending(b => b.CreatedDate)
          .ToListAsync();

      return View(blogs);
    }

    // 🗑️ Delete blog (POST only; redirect to All Blogs — not exposed on pending review UI)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteBlog(int id)
    {
      var blog = await _context.Blogs.FindAsync(id);

      if (blog == null)
      {
        TempData["ErrorMessage"] = "Blog not found!";
        return RedirectToAction(nameof(AllBlogs));
      }
      // ✅ fix: get image path from blog
      var imagePath = blog.ImagePath;

      // Delete the actual image file from storage if exists
      if (!string.IsNullOrEmpty(imagePath))
      {
        _imageHelper.DeleteImage(imagePath);
      }

      // Check if the blog is pending and send notification to author
      if (!blog.IsApproved && !blog.IsDraft)
      {
        var author = await _context.Users.FirstOrDefaultAsync(u => u.Username == blog.Author);
        if (author != null)
        {
          _context.Notifications.Add(new Notification
          {
            UserId = author.Id,
            Message = $"Your blog '{blog.Title}' has been deleted by an administrator.",
            IsRead = false,
            CreatedAt = DateTime.UtcNow
          });
        }
      }

      _context.Blogs.Remove(blog);
      _context.Activities.Add(new Activity
      {
        Action = "Blog Deleted",
        Username = blog.Author,
        ImagePath = imagePath,
        Type = "Blog",
        BlogTitle = blog.Title,
        CreatedAt = DateTime.UtcNow
      });

      await _context.SaveChangesAsync();

      TempData["Success"] = "Blog deleted successfully.";
      return RedirectToAction(nameof(AllBlogs));
    }

  }
}