namespace One.Domain.DTOs.Identity
{
    /// <summary>
    /// Request DTO for triggering OTP-based login
    /// </summary>
    public class OtpRequestDto
    {
        public string Email { get; set; } = "";
    }

    /// <summary>
    /// Request DTO for verifying OTP
    /// </summary>
    public class OtpVerifyDto
    {
        public string Email { get; set; } = "";
        public string Otp { get; set; } = "";
    }
}
