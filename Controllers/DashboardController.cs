using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace FinalBlog.Controllers
{
  [Authorize]
  public class DashboardController : Controller
  {
    private readonly AppDbContext _context;

    public DashboardController(AppDbContext context)
    {
      _context = context;
    }
    public IActionResult Index()
    {
      // Check if user is admin
      var role = HttpContext.Session.GetString("Role");
      if (role != "Admin")
      {
        return RedirectToAction("Index", "Blog");
      }

      ViewBag.TotalUsers = _context.Users.Count();
      ViewBag.PendingUsers = _context.Users.Count(u => !u.IsApproved);
      ViewBag.PendingBlogs = _context.Blogs.Count(b => !b.IsApproved && !b.IsDraft);

      var activities = _context.Activities
          .OrderByDescending(a => a.CreatedAt)
          .Take(10)
          .ToList();

      return View(activities);
    }


  }
}