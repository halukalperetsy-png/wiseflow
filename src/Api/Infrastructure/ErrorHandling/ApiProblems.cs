using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace CommerceOps.Api.Infrastructure.ErrorHandling;

/// <summary>
/// Builds the RFC 7807 documents the API returns for expected failures. The
/// central GlobalExceptionHandler stays what it is -- the last resort for
/// unexpected exceptions -- and domain failures never reach it.
/// </summary>
internal static class ApiProblems
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    internal static IResult Problem(int statusCode, string code, string title) =>
        TypedResults.Problem(new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Extensions = { ["code"] = code },
        });

    internal static IResult Validation(IDictionary<string, string[]> errors) =>
        TypedResults.ValidationProblem(
            errors,
            title: "Girdiğiniz bilgilerde hata var.",
            extensions: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["code"] = ProblemCodes.ValidationFailed,
            });

    internal static IResult NotFound(string title = "Kayıt bulunamadı.") =>
        Problem(StatusCodes.Status404NotFound, ProblemCodes.NotFound, title);

    internal static IResult Forbidden(string code, string title) =>
        Problem(StatusCodes.Status403Forbidden, code, title);

    internal static IResult Conflict(string code, string title) =>
        Problem(StatusCodes.Status409Conflict, code, title);

    /// <summary>
    /// Writes a problem document straight to the response. Used from the cookie
    /// authentication events, which run outside the endpoint pipeline and so
    /// cannot return an IResult.
    /// </summary>
    internal static Task WriteAsync(HttpResponse response, int statusCode, string code, string title)
    {
        ArgumentNullException.ThrowIfNull(response);

        response.StatusCode = statusCode;
        response.ContentType = "application/problem+json; charset=utf-8";

        return response.WriteAsync(JsonSerializer.Serialize(
            new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Extensions = { ["code"] = code },
            },
            SerializerOptions));
    }
}
