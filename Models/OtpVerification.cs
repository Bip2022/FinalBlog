public class OtpVerification
{
  public int Id { get; set; }
  public string Email { get; set; } = string.Empty;
  public string? OtpCode { get; set; } = string.Empty;
  public DateTime ExpiryTime { get; set; }
  public bool IsUsed { get; set; } = false;
}
