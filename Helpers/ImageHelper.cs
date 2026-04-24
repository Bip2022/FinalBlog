using System.Security.Cryptography;
using FinalBlog.Models;

namespace FinalBlog.Helpers
{
  public class ImageHelper
  {
    private readonly IWebHostEnvironment _webHostEnvironment;
    private readonly ILogger<ImageHelper> _logger;

    public ImageHelper(IWebHostEnvironment webHostEnvironment, ILogger<ImageHelper> logger)
    {
      _webHostEnvironment = webHostEnvironment;
      _logger = logger;
    }

    // =========================
    // BLOG IMAGE UPLOAD
    // =========================
    public async Task<string?> UploadBlogImageAsync(IFormFile? imageFile)
    {
      return await UploadImageInternal(imageFile, "blogs");
    }

    // =========================
    // PROFILE IMAGE UPLOAD
    // =========================
    public async Task<ImageUploadResult> UploadProfileImageAsync(IFormFile? imageFile)
    {
      var result = new ImageUploadResult();

      try
      {
        if (imageFile == null || imageFile.Length == 0)
        {
          result.Success = false;
          result.ErrorMessage = "No image file provided.";
          return result;
        }

        var allowed = new[] { ".jpg", ".jpeg", ".png", ".gif" };
        var ext = Path.GetExtension(imageFile.FileName).ToLower();

        if (!allowed.Contains(ext))
        {
          result.Success = false;
          result.ErrorMessage = "Invalid image format. Only JPG, JPEG, PNG, and GIF are allowed.";
          return result;
        }

        if (imageFile.Length > 5 * 1024 * 1024)
        {
          result.Success = false;
          result.ErrorMessage = "Image too large. Maximum size is 5MB.";
          return result;
        }

        // folder selection
        var folder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "profile");
        Directory.CreateDirectory(folder);

        // create hash
        string hash;
        using (var ms = new MemoryStream())
        {
          await imageFile.CopyToAsync(ms);
          var bytes = ms.ToArray();

          using (var sha = SHA256.Create())
          {
            hash = Convert.ToHexString(sha.ComputeHash(bytes));
          }
        }

        // check duplicate
        var existingFile = Directory.GetFiles(folder)
            .FirstOrDefault(f => Path.GetFileNameWithoutExtension(f) == hash);

        _logger.LogInformation("Checking for duplicate image. Hash: {Hash}, Existing file: {ExistingFile}",
          hash, existingFile != null ? Path.GetFileName(existingFile) : "none");

        if (existingFile != null)
        {
          _logger.LogInformation("Duplicate image found: {FileName}", Path.GetFileName(existingFile));
          result.Success = true;
          result.IsDuplicate = true;
          result.Url = $"/images/profile/{Path.GetFileName(existingFile)}";
          return result;
        }

        var fileName = $"{hash}{ext}";
        var filePath = Path.Combine(folder, fileName);

        await using var stream = imageFile.OpenReadStream();
        await using var fileStream = new FileStream(filePath, FileMode.Create);
        await stream.CopyToAsync(fileStream);

        result.Success = true;
        result.IsDuplicate = false;
        result.Url = $"/images/profile/{fileName}";
        return result;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error uploading profile image");
        result.Success = false;
        result.ErrorMessage = "An error occurred while uploading the image.";
        return result;
      }
    }

    public void DeleteImage(string imageUrl)
    {
      if (string.IsNullOrEmpty(imageUrl))
        return;

      try
      {
        // We only allow deleting from under "/images/"
        if (!imageUrl.StartsWith("/images/"))
          return;

        // Get the relative path under "images"
        var relativePath = imageUrl.Substring("/images/".Length); // e.g., "blogs/hash.jpg" or "profile/hash.jpg"

        // Combine with WebRootPath/images
        var fullPath = Path.Combine(_webHostEnvironment.WebRootPath, "images", relativePath);

        // Security: ensure the path is still under the images directory
        var imagesRoot = Path.Combine(_webHostEnvironment.WebRootPath, "images");
        if (!Path.GetFullPath(fullPath).StartsWith(Path.GetFullPath(imagesRoot)))
        {
          _logger.LogWarning("Attempted to delete image outside of images directory: {ImageUrl}", imageUrl);
          return;
        }

        if (File.Exists(fullPath))
        {
          File.Delete(fullPath);
          _logger.LogInformation($"Image deleted: {relativePath}");
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error deleting image: {ImageUrl}", imageUrl);
      }
    }
    // =========================
    // COMMON LOGIC
    // =========================
    private async Task<string?> UploadImageInternal(IFormFile? imageFile, string folderName)
    {
      if (imageFile == null || imageFile.Length == 0)
        return null;

      var allowed = new[] { ".jpg", ".jpeg", ".png", ".gif" };
      var ext = Path.GetExtension(imageFile.FileName).ToLower();

      if (!allowed.Contains(ext))
        throw new Exception("Invalid image format");

      if (imageFile.Length > 5 * 1024 * 1024)
        throw new Exception("Image too large");

      // folder selection
      var folder = Path.Combine(_webHostEnvironment.WebRootPath, "images", folderName);
      Directory.CreateDirectory(folder);

      // create hash
      string hash;
      using (var ms = new MemoryStream())
      {
        await imageFile.CopyToAsync(ms);
        var bytes = ms.ToArray();

        using (var sha = SHA256.Create())
        {
          hash = Convert.ToHexString(sha.ComputeHash(bytes));
        }
      }

      // check duplicate
      var existingFile = Directory.GetFiles(folder)
          .FirstOrDefault(f => Path.GetFileNameWithoutExtension(f) == hash);
      _logger.LogInformation("Checking for duplicate image. Hash: {Hash}, Existing file: {ExistingFile}", hash, existingFile != null ? Path.GetFileName(existingFile) : "none");


      if (existingFile != null)
      {
        _logger.LogInformation("Duplicate image found: {FileName}", Path.GetFileName(existingFile));
        return $"/images/{folderName}/{Path.GetFileName(existingFile)}";
      }

      var fileName = $"{hash}{ext}";
      var filePath = Path.Combine(folder, fileName);

      await using var stream = imageFile.OpenReadStream();
      await using var fileStream = new FileStream(filePath, FileMode.Create);
      await stream.CopyToAsync(fileStream);


      return $"/images/{folderName}/{fileName}";

    }

  }
}