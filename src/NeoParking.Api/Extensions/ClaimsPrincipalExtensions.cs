namespace NeoParking.Api.Extensions;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using NeoParking.Shared.Kernel.Exceptions;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Id do operador autenticado, lido do claim "sub". O JwtBearer pode mapeá-lo para
    /// NameIdentifier na entrada, por isso os dois nomes são aceitos.
    /// </summary>
    public static Guid GetOperatorId(this ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(sub, out var id) ? id : throw new InvalidCredentialsException();
    }
}
