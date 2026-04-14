using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FinalBlog.Models;
using Microsoft.IdentityModel.Tokens;

namespace FinalBlog.Services
{
  public class JwtTokenGenerator
  {
    private readonly IConfiguration _config;
    private readonly ILogger<JwtTokenGenerator> _logger;

    public JwtTokenGenerator(IConfiguration config, ILogger<JwtTokenGenerator> logger)
    {
      _config = config;
      _logger = logger;
    }

    public string GenerateToken(User user)
    {
      _logger.LogInformation("Generating JWT for {Username}", user.Username);
      var key = _config["Jwt:Key"];
      if (string.IsNullOrEmpty(key))
        throw new Exception("JWT Key is missing in configuration");



      var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
      var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

      var claims = new[]
      {
             new Claim("Username", user.Username),
                new Claim("Email", user.Email),
                new Claim("UserId", user.Id.ToString())
        };

      var token = new JwtSecurityToken(
          issuer: _config["Jwt:Issuer"],
          audience: _config["Jwt:Audience"],
          claims: claims,
          expires: DateTime.UtcNow.AddHours(2),
          signingCredentials: credentials
      );

      var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
      _logger.LogInformation("JWT Token Generated for {Username}", user.Username);
      return tokenString;
    }
  }
}