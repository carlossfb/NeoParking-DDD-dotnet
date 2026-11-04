namespace NeoParking.Api.Extensions;

using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using NeoParking.Api;

public static class AuthenticationExtensions
{
    /// <summary>
    /// Registra o gerador de tokens JWT e configura a autenticação Bearer usada
    /// por todos os endpoints protegidos da API (validação de issuer, audience,
    /// tempo de vida e assinatura) e as policies da matriz de autorização.
    /// Em Management, o OperatorService ainda confere papel e estado no banco.
    /// </summary>
    public static IServiceCollection AddNeoParkingAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

        var jwtSecret = configuration["Jwt:Secret"];
        JwtSecretValidator.Validate(jwtSecret, environment.IsDevelopment());

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer           = true,
                    ValidIssuer              = configuration["Jwt:Issuer"] ?? "neoparking",
                    ValidateAudience         = true,
                    ValidAudience            = configuration["Jwt:Audience"] ?? "neoparking",
                    ValidateLifetime         = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret!))
                };
            });

        services.AddAuthorization(AuthorizationPolicies.Configure);

        return services;
    }
}
