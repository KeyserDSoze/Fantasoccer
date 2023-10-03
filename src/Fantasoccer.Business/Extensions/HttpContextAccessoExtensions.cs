using System.Security.Claims;

namespace Microsoft.AspNetCore.Http
{
    public static class HttpContextAccessoExtensions
    {
        public static string? GetEmail(this IHttpContextAccessor httpContextAccessor)
            => httpContextAccessor.HttpContext?.User.Claims.FirstOrDefault(x => x.Type == ClaimTypes.Email)?.Value;
    }
}
