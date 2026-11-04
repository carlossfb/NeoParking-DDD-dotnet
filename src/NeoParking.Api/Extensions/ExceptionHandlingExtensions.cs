namespace NeoParking.Api.Extensions;

using Microsoft.AspNetCore.Diagnostics;
using NeoParking.Shared.Kernel.Exceptions;

public static class ExceptionHandlingExtensions
{
    /// <summary>
    /// Middleware global de exceções — traduz exceções de domínio conhecidas
    /// (credenciais, permissão, não encontrado, conflito, regra de negócio) em respostas HTTP padronizadas.
    /// Qualquer exceção não mapeada vira 500, sem vazar detalhes internos.
    /// </summary>
    public static WebApplication UseNeoParkingExceptionHandling(this WebApplication app)
    {
        app.UseExceptionHandler(errorApp =>
        {
            errorApp.Run(async context =>
            {
                var exception = context.Features
                    .Get<IExceptionHandlerFeature>()?.Error;

                context.Response.ContentType = "application/json";
                context.Response.StatusCode = exception switch
                {
                    InvalidCredentialsException => StatusCodes.Status401Unauthorized,
                    ForbiddenException          => StatusCodes.Status403Forbidden,
                    NotFoundException           => StatusCodes.Status404NotFound,
                    ConflictException           => StatusCodes.Status409Conflict,
                    DomainException             => StatusCodes.Status422UnprocessableEntity,
                    _                           => StatusCodes.Status500InternalServerError
                };

                var message = exception switch
                {
                    InvalidCredentialsException or ForbiddenException
                        or NotFoundException or DomainException => exception.Message,
                    _ => "An unexpected error occurred"
                };

                await context.Response.WriteAsJsonAsync(new
                {
                    status = context.Response.StatusCode,
                    error  = message
                });
            });
        });

        return app;
    }
}
