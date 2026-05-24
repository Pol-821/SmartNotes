using System.Security.Claims;

namespace SmartNotes.Api.Extensions;

public static class ClaimsExtensions
{
    public static int GetUserId(this ClaimsPrincipal user)
    {
        var claim = user.FindFirst(ClaimTypes.NameIdentifier)
                    ?? user.FindFirst("sub");
        if (claim == null || !int.TryParse(claim.Value, out var userId))
            throw new UnauthorizedAccessException("Usuari no identificat o token invàlid.");
        return userId;
    }
}
