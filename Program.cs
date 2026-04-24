using FinalBlog.Data;
using FinalBlog.Helpers;
using FinalBlog.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
// Add services to the container.
builder.Services.AddScoped<ImageHelper>();

// -------------------- SERVICES --------------------

// MVC
builder.Services.AddControllersWithViews();
//Email Service
builder.Services.AddScoped<EmailService>();
// DB (PostgreSQL)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
);
// Session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
// JWT Key (safe check)
var jwtKey = builder.Configuration["Jwt:Key"];
// -------------------- AUTH --------------------
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})

// Cookie Auth
.AddCookie(options =>
{
    options.LoginPath = "/Account/Signin";
})
// Google Auth
.AddGoogle(options =>
{
    options.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;
    options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
})
// JWT Auth (API support)
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,

        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey!))
    };
});

// JWT Service
builder.Services.AddScoped<JwtTokenGenerator>();
// Email Service
builder.Services.AddScoped<EmailService>();

// -------------------- APP BUILD --------------------
var app = builder.Build();

// 🔥 Run migrations and seeder on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAdminAsync(db);
    await DbSeeder.SeedCategoriesAsync(db);
}

// -------------------- PIPELINE --------------------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
// Serve runtime files from wwwroot (e.g., uploaded blog images under /images/blogs)
app.UseStaticFiles();
app.UseRouting();

// Session must be before auth
app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

// Routes
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Blog}/{action=Index}/{id?}"
)
;

app.Run();