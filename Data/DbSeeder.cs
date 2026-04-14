using FinalBlog.Models;
using Microsoft.EntityFrameworkCore;

namespace FinalBlog.Data
{
  public static class DbSeeder
  {
    public static async Task SeedAdminAsync(AppDbContext context)
    {
      var adminExists = await context.Users
          .AnyAsync(x => x.Role == "Admin");

      if (adminExists)
        return;

      var admin = new User
      {
        Username = "Admin",
        Email = "admin@blog.com",
        Password = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
        Role = "Admin",
        IsEmailConfirmed = true,
        IsApproved = true
      };

      context.Users.Add(admin);
      await context.SaveChangesAsync();
    }

    public static async Task SeedCategoriesAsync(AppDbContext db)
    {
      if (db.Categories.Any()) return;

      db.Categories.AddRange(
          new Category { Name = "Technology" },
          new Category { Name = "Science" },
          new Category { Name = "Programming" },
          new Category { Name = "Sports" },
          new Category { Name = "Social-Media" }
      );

      await db.SaveChangesAsync();
    }
  }
}