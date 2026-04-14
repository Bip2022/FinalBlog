using Microsoft.EntityFrameworkCore;
using FinalBlog.Models;

public class AppDbContext : DbContext
{
  public AppDbContext(DbContextOptions<AppDbContext> options)
      : base(options) { }

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    modelBuilder.Entity<Blog>()
        .HasOne(b => b.AuthorUser)
        .WithMany()
        .HasForeignKey(b => b.AuthorUserId)
        .OnDelete(DeleteBehavior.SetNull);
  }

  public DbSet<User> Users { get; set; }
  public DbSet<OtpVerification> OtpVerifications { get; set; }
  public DbSet<Blog> Blogs { get; set; }

  public DbSet<Category> Categories { get; set; }
  public DbSet<Activity> Activities { get; set; }
  public DbSet<Comment> Comments { get; set; }
  public DbSet<Like> Likes { get; set; }
  public DbSet<Notification> Notifications { get; set; }




}