namespace One.Domain.DTOs
{
    public class Response
    {
        public int StatusCode { get; set; }
        public string Message { get; set; } = "";
        public List<string> Messages { get; set; } = new();
    }

    public class AuthResponseDto : Response
    {
        public string Token { get; set; } = "";
        public DateTime Expiration { get; set; }
        public string RefreshToken { get; set; } = "";
        public DateTime RefreshTokenExpiration { get; set; }
    }
}
