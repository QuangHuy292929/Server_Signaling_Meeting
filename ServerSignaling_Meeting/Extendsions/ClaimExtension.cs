using System.Security.Claims;

namespace ServerSignaling_Meeting.Extensions
{
    public static class ClaimExtension
    {
        public static Guid GetCurrentUserId(this ClaimsPrincipal principal)
        {
            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdClaim))
            {
                throw new UnauthorizedAccessException("User ID not found in token");
            }

            if (!Guid.TryParse(userIdClaim, out Guid userId))
            {
                throw new UnauthorizedAccessException("Invalid User ID format");
            }

            return userId;
        }

        public static string GetUserName(this ClaimsPrincipal principal)
        {
            var userNameClaim = principal.FindFirst(ClaimTypes.Name)?.Value;
            if (string.IsNullOrEmpty(userNameClaim))
            {
                throw new UnauthorizedAccessException("User Name not found in token");
            }
            return userNameClaim;
        }

        public static string GetEmail(this ClaimsPrincipal principal)
        {
            var emailClaim = principal.FindFirst(ClaimTypes.Email)?.Value;
            if (string.IsNullOrEmpty(emailClaim))
            {
                throw new UnauthorizedAccessException("Email not found in token");
            }
            return emailClaim;
        }
    }
}
