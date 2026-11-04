namespace NeoParking.E2E.Tests;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using NeoParking.Api;
using NeoParking.Management.Application;

/// <summary>TD-405: trocar o agregado pelo DTO não muda os claims do token.</summary>
public class JwtTokenGeneratorTests
{
    [Fact]
    public void Generate_ShouldKeepTheExpectedClaims()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "0123456789abcdef0123456789abcdef",
            })
            .Build();
        var user = new OperatorResponseDTO(Guid.NewGuid(), "Ana", "ana@neo.com", "Admin", true);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(new JwtTokenGenerator(configuration).Generate(user));

        token.Issuer.Should().Be("neoparking");
        token.Audiences.Should().ContainSingle().Which.Should().Be("neoparking");
        token.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Sub).Value.Should().Be(user.Id.ToString());
        token.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Email).Value.Should().Be("ana@neo.com");
        token.Claims.Single(c => c.Type == ClaimTypes.Role).Value.Should().Be("Admin");
        Guid.TryParse(token.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Jti).Value, out _).Should().BeTrue();
    }
}
