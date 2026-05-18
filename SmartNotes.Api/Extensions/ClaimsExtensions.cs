using System.Security.Claims;

namespace SmartNotes.Api.Extensions;

public static class ClaimsExtensions
{
    public static int GetUserId(this ClaimsPrincipal user)
    {
        var claim = user.FindFirst(ClaimTypes.NameIdentifier);
        if (claim == null) throw new UnauthorizedAccessException("Usuari no identificat.");
        return int.Parse(claim.Value);
    }
}
