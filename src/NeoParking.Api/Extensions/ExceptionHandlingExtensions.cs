namespace NeoParking.Api.Extensions;

using Microsoft.AspNetCore.Diagnostics;
using NeoParking.Shared.Kernel.Exceptions;

public static class ExceptionHandlingExtensions
{
    /// <summary>
    /// Middleware global de exceções — traduz exceções de domínio conhecidas
    /// (NotFoundException, DomainException) em respostas HTTP padronizadas.
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
                    NotFoundException => StatusCodes.Status404NotFound,
                    DomainException   => StatusCodes.Status422UnprocessableEntity,
                    _                 => StatusCodes.Status500InternalServerError
                };

                var message = exception switch
                {
                    NotFoundException or DomainException => exception.Message,
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
