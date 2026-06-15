using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

namespace PPFAttendanceApi.Helper
{
    public class ClaimsService
    {
        private readonly IHttpContextAccessor httpContextAccessor;
        private readonly IConfiguration configuration;
        private Dictionary<string, string> _claims;
        public ClaimsService(IHttpContextAccessor _httpContextAccessor, IConfiguration _configuration)
        {
            httpContextAccessor = _httpContextAccessor;
            configuration = _configuration;
            _claims = [];
            string ipAddress = httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "0";
            if (httpContextAccessor?.HttpContext?.Request?.Headers != null &&
                httpContextAccessor.HttpContext.Request.Headers.TryGetValue("Authorization", out var tokenHeader))
            {
                string token = tokenHeader.ToString().Replace("Bearer ", "");
                SetClaimsFromToken(token, ipAddress);
            }
        }

        public string this[string key]
        {
            get
            {
                if (_claims == null) return "";
                if (_claims.ContainsKey(key)) return _claims[key];
                return "";
            }
            set
            {
                throw new Exception("Claims are readonly");
            }
        }

        private void SetClaimsFromToken(string token, string ipAddress)
        {
            string secretKey = configuration["JWTSettings:SecretKey"]?.ToString() ?? "0";
            string issuer = configuration["JWTSettings:Issuer"]?.ToString() ?? "0";
            string audience = configuration["JWTSettings:Audience"]?.ToString() ?? "0";
            int expiresIn = int.Parse(configuration["JWTSettings:ExpiryHours"]?.ToString() ?? "1");

            var tokenHandler = new JwtSecurityTokenHandler();

            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = audience,
                ValidateLifetime = true
            }, out SecurityToken validatedToken);

            var jwtToken = (JwtSecurityToken)validatedToken;
            var claims = jwtToken.Claims.ToList();
            foreach (var claim in claims)
            {
                _claims.Add(claim.Type, claim.Value);
            }

            if (ipAddress != null || ipAddress != "")
            {
                _claims.Add("ipAddress", ipAddress ?? "0");
            }
        }
    }
}
