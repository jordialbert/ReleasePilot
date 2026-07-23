using Microsoft.AspNetCore.Diagnostics;
using ReleaseManagement.Application;
using ReleaseManagement.Domain;

namespace ReleasePilot.Api.Shared;

public sealed class ProblemDetailsExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var status = StatusCodes.Status500InternalServerError;
        var code = "unexpected_error";
        var title = "An unexpected error occurred.";

        if (exception is BadHttpRequestException)
        {
            status = StatusCodes.Status400BadRequest;
            code = "malformed_input";
            title = "The request is malformed.";
        }
        else if (exception is InvalidInput invalidInput)
        {
            status = StatusCodes.Status400BadRequest;
            code = invalidInput.Code;
            title = invalidInput.Message;
        }
        else if (exception is UnknownActor unknownActor)
        {
            status = StatusCodes.Status401Unauthorized;
            code = unknownActor.Code;
            title = unknownActor.Message;
        }
        else if (exception is ResourceNotFound resourceNotFound)
        {
            status = StatusCodes.Status404NotFound;
            code = resourceNotFound.Code;
            title = resourceNotFound.Message;
        }
        else if (exception is OnlyApproverCanApprove onlyApproverCanApprove)
        {
            status = StatusCodes.Status403Forbidden;
            code = onlyApproverCanApprove.Code;
            title = onlyApproverCanApprove.Message;
        }
        else if (exception is DomainException domainException)
        {
            status = StatusCodes.Status409Conflict;
            code = domainException.Code;
            title = domainException.Message;
        }

        await Results.Problem(
            statusCode: status,
            title: title,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["traceId"] = context.TraceIdentifier
            }).ExecuteAsync(context);
        return true;
    }
}
