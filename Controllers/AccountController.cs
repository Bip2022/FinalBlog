using Microsoft.AspNetCore.Mvc;
using FinalBlog.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using FinalBlog.Models.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using FinalBlog.Services;
using System.Security.Claims;
using FinalBlog.Helpers;

namespace FinalBlog.Controllers
{
  public class AccountController : Controller
  {
    private readonly AppDbContext _context;
    private readonly ILogger<AccountController> _logger;
    private readonly IConfiguration _config;
    private readonly JwtTokenGenerator _jwtTokenGenerator;

    private readonly EmailService _emailService;
    private readonly ImageHelper _imageHelper;

    public AccountController(AppDbContext context, ILogger<AccountController> logger, JwtTokenGenerator jwtTokenGenerator, IConfiguration config, EmailService emailService, ImageHelper imageHelper)
    {
      _context = context;
      _config = config;
      _logger = logger;
      _jwtTokenGenerator = jwtTokenGenerator;
      _emailService = emailService;
      _imageHelper = imageHelper;
    }

    // 🟢 SIGNUP (Register)
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Signup()
    {
      _logger.LogInformation("Signup page requested");
      return View();
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Signup(SignupViewModel model)
    {
      if (!ModelState.IsValid)
      {
        _logger.LogWarning("Signup form validation failed");
        TempData["ErrorMessage"] = "Invalid input data. Please check your form.";
        return View(model);
      }
      var existingUser = _context.Users.FirstOrDefault(u => u.Email == model.Email);
      if (existingUser != null)
      {
        _logger.LogWarning("Attempt to register with existing email: {Email}", model.Email);
        TempData["ErrorMessage"] = "Email already registered. Please use a different email.";
        return View(model);
      }
      //Token generate garne
      var token = Guid.NewGuid().ToString();

      //ViewModel ma token rakhne
      var user = new User
      {
        Username = model.Username,
        Email = model.Email,
        Password = BCrypt.Net.BCrypt.HashPassword(model.Password), // Note: In production, always hash passwords!
        EmailToken = token,
        IsEmailConfirmed = false
      };

      _context.Users.Add(user);
      await _context.SaveChangesAsync();
      _logger.LogInformation("New user registered: {Username}", user.Username);
      // Email verification link 
      var verificationLink = Url.Action("VerifyEmail", "Account", new { token = token }, Request.Scheme);

      // Email send garne
      await _emailService.SendEmailAsync(user.Email, "Verify your email", $"Please verify your email by clicking on this link: {verificationLink}");
      _logger.LogInformation("Verification email sent to: {Email}", user.Email);
      TempData["SuccessMessage"] = "Registration successful! Please check your email to verify your account.";
      return RedirectToAction("Signin");
    }
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyEmail(string token)
    {
      if (string.IsNullOrWhiteSpace(token))
      {
        _logger.LogWarning("Email verification attempted with empty token");
        TempData["ErrorMessage"] = "Invalid verification link.";
        return RedirectToAction("Signin");
      }

      var user = await _context.Users
          .FirstOrDefaultAsync(u => u.EmailToken == token);

      if (user == null)
      {
        _logger.LogWarning("Email verification attempted with invalid token: {Token}", token);
        TempData["ErrorMessage"] = "Invalid verification link.";
        return RedirectToAction("Signin");
      }

      // ✅ EXPIRY CHECK
      if (user.EmailTokenExpiry < DateTime.UtcNow)
      {
        _logger.LogWarning("Email verification attempted with expired token for email: {Email}", user.Email);
        TempData["ErrorMessage"] = "Verification link expired.";
        return RedirectToAction("Signin");
      }

      // ✅ VERIFY USER
      user.IsEmailConfirmed = true;
      user.Role = "User";
      user.EmailToken = Guid.NewGuid().ToString(); // Optional: Regenerate token to prevent reuse
      user.EmailTokenExpiry = DateTime.UtcNow.AddMinutes(15); // Optional: Set a short expiry for the token after verification for security

      await _context.SaveChangesAsync();
      _logger.LogInformation("Email verified successfully for: {Email}", user.Email);
      TempData["SuccessMessage"] = "Email verified successfully!";
      return RedirectToAction("Signin");
    }

    [HttpPost]
    public async Task<IActionResult> ResendVerification(string email)
    {
      var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

      if (user == null)
        return NotFound();

      if (user.IsEmailConfirmed)
      {
        _logger.LogInformation("Resend verification requested for already verified email: {Email}", email);
        TempData["SuccessMessage"] = "Email already verified.";
        return RedirectToAction("Signin");
      }

      // 🔥 regenerate token
      user.EmailToken = Guid.NewGuid().ToString();
      user.EmailTokenExpiry = DateTime.UtcNow.AddMinutes(30);

      await _context.SaveChangesAsync();

      var link = Url.Action("VerifyEmail", "Account",
          new { token = user.EmailToken }, Request.Scheme);

      // 👉 send email here
      _logger.LogInformation("Resending verification email to: {Email}", email);
      TempData["SuccessMessage"] = "Verification email sent again.";
      return RedirectToAction("Signin");
    }


    // 🔵 SIGNIN (Login)
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Signin(string? returnUrl)
    {
      var url = returnUrl
          ?? Request.Query["ReturnUrl"].FirstOrDefault()
          ?? string.Empty;
      _logger.LogInformation("Signin page requested");
      return View(new SigninViewModel { ReturnUrl = string.IsNullOrWhiteSpace(url) ? null : url });
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Signin(SigninViewModel model)
    {
      var user = await _context.Users.FirstOrDefaultAsync
      (x =>
      x.Email == model.UsernameOrEmail || x.Username == model.UsernameOrEmail);

      if (user == null || !BCrypt.Net.BCrypt.Verify(model.Password, user.Password))
      {
        _logger.LogWarning("Invalid login attempt for: {UsernameOrEmail}", model.UsernameOrEmail);
        TempData["ErrorMessage"] = "Invalid credentials. Please try again .";
        return View(model);
      }

      if (!user.IsEmailConfirmed)
      {
        _logger.LogWarning("Login attempt with unverified email: {Email}", user.Email);
        TempData["ErrorMessage"] = "Please verify your email before logging in.";
        return View(model);
      }

      if (user.IsLocked)
      {
        _logger.LogWarning("Login attempt with blocked user: {Email}", user.Email);
        TempData["ErrorMessage"] = "Your account has been blocked. Please contact the administrator.";
        return View(model);
      }

      var token = _jwtTokenGenerator.GenerateToken(user);
      Response.Cookies.Append("JwtToken", token, new CookieOptions
      {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Expires = DateTime.UtcNow.AddHours(2)
      });

      // Sign in with cookie auth
      var claims = new List<Claim>
      {
        new Claim(ClaimTypes.Name, user.Username),
        new Claim(ClaimTypes.Email, user.Email),
        new Claim("UserId", user.Id.ToString()),
        new Claim(ClaimTypes.Role, user.Role)
      };
      var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
      var principal = new ClaimsPrincipal(identity);
      await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

      // Session set
      HttpContext.Session.SetString("UserEmail", user.Email);
      HttpContext.Session.SetString("Role", user.Role);

      if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        return Redirect(model.ReturnUrl);

      if (user.Role == "Admin")
        return RedirectToAction("Dashboard", "Admin");
      return RedirectToAction("Index", "Blog");
    }



    // 🔴 LOGOUT - GET (Show confirmation page)
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Signout()
    {
      _logger.LogInformation("Signout confirmation page requested for user: {Username}", User.Identity?.Name);
      return View();
    }

    // 🔴 LOGOUT - POST (Actual sign out)
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Signout(string confirmSignout = "")
    {
      var username = User.Identity?.Name ?? "Unknown User";
      _logger.LogInformation("User is signing out: {Username}", username);

      await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
      HttpContext.Session.Clear();

      _logger.LogInformation("User successfully signed out: {Username}", username);
      TempData["SuccessMessage"] = "You have been signed out successfully.";

      return RedirectToAction("Index", "Blog");
    }


    //Forgot Password
    [HttpGet]
    [AllowAnonymous]
    public IActionResult ForgotPassword()
    {
      _logger.LogInformation("Forgot Password page requested");
      return View();
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
      if (!ModelState.IsValid)
      {
        _logger.LogWarning("Forgot Password form validation failed");
        TempData["ErrorMessage"] = "Invalid input data. Please check your form.";
        return View(model);
      }

      var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
      if (user == null)
      {
        _logger.LogWarning("Forgot Password requested for non-existent email: {Email}", model.Email);
        TempData["ErrorMessage"] = "No account found with that email address.";
        return View(model);
      }

      //Delete old OTPs for this email
      var oldOtps = _context.OtpVerifications.Where(o => o.Email == model.Email);
      _context.OtpVerifications.RemoveRange(oldOtps);
      await _context.SaveChangesAsync();

      // Generate new 6 digit OTP and save to DB
      var otp = new Random().Next(100000, 999999).ToString();
      var otpEntry = new OtpVerification
      {
        Email = model.Email,
        OtpCode = otp,
        ExpiryTime = DateTime.UtcNow.AddMinutes(10), // OTP valid for 10 minutes
        IsUsed = false
      };
      _context.OtpVerifications.Add(otpEntry);
      _logger.LogInformation("Generated OTP for email: {Email}", model.Email);
      await _context.SaveChangesAsync();
      //Send OTP email to user
      await _emailService.SendEmailAsync(user.Email, "Your Password Reset OTP", $"Your OTP for password reset is: {otp}. It is valid for 10 minutes.");
      _logger.LogInformation("Password reset OTP email sent to: {Email}", user.Email);
      TempData["SuccessMessage"] = "An OTP has been sent to your email address. Please check your email.";

      //redirect to OTP verification page
      return RedirectToAction("VerifyOtp", new { email = model.Email });
    }






    [HttpGet]
    [AllowAnonymous]
    public IActionResult VerifyOtp(string email)
    {
      if (string.IsNullOrWhiteSpace(email))
      {
        _logger.LogWarning("Otp verification requested without email");
        TempData["ErrorMessage"] = "Email is required for OTP verification.";
        return RedirectToAction("ForgotPassword");
      }

      var model = new OtpVerification

      {
        Email = email
      };

      return View(model);
    }


    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyOtp(OtpVerification model)
    {
      if (string.IsNullOrEmpty(model.Email) || string.IsNullOrEmpty(model.OtpCode))
      {
        _logger.LogWarning("OTP verification attempted with missing email or OTP");
        TempData["ErrorMessage"] = "Email and OTP are required.";
        return View(model);
      }

      var otpEntry = await _context.OtpVerifications
          .FirstOrDefaultAsync(o =>
              o.Email == model.Email &&
              o.OtpCode == model.OtpCode);

      if (otpEntry == null)
      {
        _logger.LogWarning("Invalid OTP attempt for email: {Email}", model.Email);
        TempData["ErrorMessage"] = "Invalid OTP.";
        return View(model);
      }

      // already used check
      if (otpEntry.IsUsed)
      {
        _logger.LogWarning("Attempt to reuse OTP for email: {Email}", model.Email);
        TempData["ErrorMessage"] = "OTP already used. Request new one.";
        return View(model);
      }

      // expiry check (IMPORTANT)
      if (otpEntry.ExpiryTime < DateTime.UtcNow)
      {
        _logger.LogWarning("Expired OTP attempt for email: {Email}", model.Email);
        TempData["ErrorMessage"] = "OTP expired.";
        return View(model);
      }

      // mark as used
      otpEntry.IsUsed = true;
      await _context.SaveChangesAsync();
      _logger.LogInformation("OTP verified successfully for email: {Email}", model.Email);

      TempData["SuccessMessage"] = "OTP verified successfully.";

      return RedirectToAction("ResetPassword", new { email = model.Email });
    }
    [HttpGet]
    [AllowAnonymous]
    public IActionResult ResetPassword(string email)
    {
      if (string.IsNullOrWhiteSpace(email))
      {
        _logger.LogWarning("Empty email for password reset");
        TempData["ErrorMessage"] = "Invalid password reset request.";
        return RedirectToAction("ForgotPassword");
      }
      var model = new ResetPasswordViewModel
      {
        Token = email // Reusing the Token property to pass the email for simplicity
      };
      return View(model);
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
      if (!ModelState.IsValid)
      {
        _logger.LogWarning("Reset Password form validation failed");
        TempData["ErrorMessage"] = "Invalid input data. Please check your form.";
        return View(model);
      }
      var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Token);
      if (user == null)
      {
        _logger.LogWarning("Reset Password attempted for non-existent email: {Email}", model.Token);
        TempData["ErrorMessage"] = "No account found with that email address.";
        return View(model);
      }

      // Update the user's password
      user.Password = BCrypt.Net.BCrypt.HashPassword(model.NewPassword); // Hash the new password
      user.EmailToken = null; // Clear the reset token
      await _context.SaveChangesAsync();
      _logger.LogInformation("Password reset successful for: {Email}", user.Email);
      TempData["SuccessMessage"] = "Your password has been reset successfully! You can now log in with your new password.";

      return RedirectToAction("Signin");
    }









    [HttpGet]
    public IActionResult GoogleLogin()
    {
      var redirectUrl = Url.Action("GoogleResponse", "Account");

      var properties = new AuthenticationProperties
      {
        RedirectUri = redirectUrl
      };

      return Challenge(properties, "Google");
    }
    public async Task<IActionResult> GoogleResponse()
    {
      // Google login success after redirect
      return RedirectToAction("Index", "Blog");
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Profile()
    {
      var username = User.Identity?.Name;

      var user = await _context.Users
          .FirstOrDefaultAsync(u => u.Username == username);

      if (user == null)
      {
        return NotFound();
      }

      return View(user);
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> EditProfile()
    {
      var username = User.Identity?.Name;

      var user = await _context.Users
          .FirstOrDefaultAsync(u => u.Username == username);

      if (user == null)
      {
        return NotFound();
      }

      var model = new EditProfileViewModel
      {
        Username = user.Username,
        Email = user.Email,
        ExistingProfileImage = user.ProfileImage
      };

      return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> EditProfile(EditProfileViewModel model)
    {
      _logger.LogInformation("EditProfile POST called for user: {User}", User.Identity?.Name);

      if (!ModelState.IsValid)
      {
        _logger.LogWarning("ModelState is invalid. Errors: {Errors}",
          string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
        TempData["ErrorMessage"] = "Invalid input data. Please check your form.";
        return View(model);
      }

      var username = User.Identity?.Name;

      var user = await _context.Users
          .FirstOrDefaultAsync(u => u.Username == username);

      if (user == null)
        return NotFound();

      user.Username = model.Username!;
      user.Email = model.Email!;

      if (model.ProfileImage != null)
      {
        _logger.LogInformation("Starting profile image upload for user: {Username}", username);
        var result = await _imageHelper.UploadProfileImageAsync(model.ProfileImage);
        _logger.LogInformation("Upload result - Success: {Success}, IsDuplicate: {IsDuplicate}, Url: {Url}",
          result.Success, result.IsDuplicate, result.Url);

        if (!result.Success)
        {
          _logger.LogWarning("Profile image upload failed: {ErrorMessage}", result.ErrorMessage);
          TempData["ErrorMessage"] = result.ErrorMessage ?? "Failed to upload image.";
          return View(model);
        }

        if (result.IsDuplicate || (!string.IsNullOrEmpty(user.ProfileImage) && user.ProfileImage == result.Url))
        {
          _logger.LogInformation("Duplicate/same image detected for user: {Username}", username);
          TempData["Info"] = "This image is already your profile picture.";
        }

        // 🔥 delete old image if exists and it's different from the new one
        if (!string.IsNullOrEmpty(user.ProfileImage) && user.ProfileImage != result.Url)
        {
          _logger.LogInformation("Deleting old profile image for user: {Username}", username);
          _imageHelper.DeleteImage(user.ProfileImage);
        }

        user.ProfileImage = result.Url;
        _logger.LogInformation("Profile image updated for user: {Username} to {Url}", username, result.Url);
      }

      _context.Update(user);
      await _context.SaveChangesAsync();

      // Only show success message if profile was actually changed
      if (model.ProfileImage != null || user.Username != model.Username || user.Email != model.Email)
      {
        TempData["Success"] = "Profile updated successfully";
      }

      return RedirectToAction("Profile");
    }

    [HttpGet]
    public IActionResult ChangePassword()
    {
      return View();
    }
    [HttpPost]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
      if (!ModelState.IsValid)
      {
        return View(model);
      }

      var username = User.Identity?.Name;

      var user = await _context.Users
          .FirstOrDefaultAsync(u => u.Username == username);

      if (user == null)
        return NotFound();

      // 🔐 check current password (IMPORTANT)
      var isValidPassword = BCrypt.Net.BCrypt.Verify(model.CurrentPassword, user.Password);

      if (!isValidPassword)
      {
        TempData["Error"] = "Current password is incorrect.";
        return View(model);
      }

      // 🔐 new password & confirm check (extra safety)
      if (model.NewPassword != model.ConfirmPassword)
      {
        TempData["Error"] = "New password and confirm password do not match.";
        return View(model);
      }

      // 🔐 hash new password
      user.Password = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);

      _context.Update(user);
      await _context.SaveChangesAsync();

      TempData["Success"] = "Password changed successfully.";

      return RedirectToAction("Profile");
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Notifications()
    {
      var userId = _context.Users.Where(u => u.Username == User.Identity!.Name).Select(u => u.Id).FirstOrDefault();
      var notifications = await _context.Notifications
          .Where(n => n.UserId == userId)
          .OrderByDescending(n => n.CreatedAt)
          .ToListAsync();

      var unread = notifications.Where(n => !n.IsRead).ToList();
      if (unread.Count > 0)
      {
        foreach (var n in unread)
        {
          n.IsRead = true;
        }
        await _context.SaveChangesAsync();
      }

      return View(notifications);
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> MarkAsRead(int id)
    {
      var notification = await _context.Notifications.FindAsync(id);
      if (notification != null && notification.UserId == _context.Users.Where(u => u.Username == User.Identity!.Name).Select(u => u.Id).FirstOrDefault())
      {
        notification.IsRead = true;
        _context.Update(notification);
        await _context.SaveChangesAsync();
      }

      return Json(new { success = true });
    }



  }
}