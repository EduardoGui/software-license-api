using System.Text.Json;
using SoftwareLicense.Api.Exceptions;

namespace SoftwareLicense.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (NotFoundException ex)
        {
            await WriteResponse(context, StatusCodes.Status404NotFound, ex.Message);
        }
        catch (BusinessRuleException ex)
        {
            await WriteResponse(context, StatusCodes.Status400BadRequest, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro não tratado ao processar {Path}", context.Request.Path);
            await WriteResponse(context, StatusCodes.Status500InternalServerError, "Ocorreu um erro inesperado.");
        }
    }

    private static Task WriteResponse(HttpContext context, int statusCode, string message)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;
        return context.Response.WriteAsync(JsonSerializer.Serialize(new { message }));
    }
}
