using MahoSoft.Negocio;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace MahoSoft.Api.Errores;

/// <summary>
/// Turns a <see cref="NegocioException"/> thrown by a service into a ProblemDetails response whose
/// <c>detail</c> is the message for the user. Any other exception is left alone and ends as a 500.
/// </summary>
public class NegocioExceptionFilter(ProblemDetailsFactory problemDetails) : IExceptionFilter
{
    public void OnException(ExceptionContext ctx)
    {
        if (ctx.Exception is not NegocioException e)
            return;

        var status = e switch
        {
            NoAutenticadoException => StatusCodes.Status401Unauthorized,
            AccesoDenegadoException => StatusCodes.Status403Forbidden,
            NoEncontradoException => StatusCodes.Status404NotFound,
            ConflictoException => StatusCodes.Status409Conflict,
            ServicioNoDisponibleException => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status400BadRequest,
        };
        ctx.Result = new ObjectResult(problemDetails.CreateProblemDetails(ctx.HttpContext, status, detail: e.Message))
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" },
        };
        ctx.ExceptionHandled = true;
    }
}
