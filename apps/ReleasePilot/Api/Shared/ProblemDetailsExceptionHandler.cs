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

        switch (exception)
        {
            case BadHttpRequestException:
                status = StatusCodes.Status400BadRequest;
                code = "malformed_input";
                title = "The request is malformed.";
                break;
            case InvalidInput invalidInput:
                status = StatusCodes.Status400BadRequest;
                code = invalidInput.Code;
                title = invalidInput.Message;
                break;
            case MissingActor missingActor:
                status = StatusCodes.Status401Unauthorized;
                code = missingActor.Code;
                title = missingActor.Message;
                break;
            case UnknownActor unknownActor:
                status = StatusCodes.Status401Unauthorized;
                code = unknownActor.Code;
                title = unknownActor.Message;
                break;
            case ResourceNotFound resourceNotFound:
                status = StatusCodes.Status404NotFound;
                code = resourceNotFound.Code;
                title = resourceNotFound.Message;
                break;
            case DeploymentUnavailable deploymentUnavailable:
                status = StatusCodes.Status503ServiceUnavailable;
                code = deploymentUnavailable.Code;
                title = deploymentUnavailable.Message;
                break;
            case OnlyApproverCanApprove onlyApproverCanApprove:
                status = StatusCodes.Status403Forbidden;
                code = onlyApproverCanApprove.Code;
                title = onlyApproverCanApprove.Message;
                break;
            case DomainException domainException:
                status = StatusCodes.Status409Conflict;
                code = domainException.Code;
                title = domainException.Message;
                break;
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
