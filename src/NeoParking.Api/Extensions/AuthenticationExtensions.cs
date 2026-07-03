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
    /// tempo de vida e assinatura). A autorização por role continua sendo feita
    /// dentro de cada Application Service (ex: OperatorService), não aqui.
    /// </summary>
    public static IServiceCollection AddNeoParkingAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

        var jwtSecret = configuration["Jwt:Secret"]
            ?? throw new InvalidOperationException("Jwt:Secret not configured.");

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
                    IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
                };
            });

        services.AddAuthorization();

        return services;
    }
}
