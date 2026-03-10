using Shared.Common.Results;
using System.Diagnostics;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace API.Common.Extensions;

public static class ResultExtensions
{
    public static IResult ToHttpResult(this Result result)
    {
        if (result.IsSuccess)
            return HttpResults.Ok();

        return CreateProblemDetails(result.Error!);
    }

    public static IResult ToHttpResult<T>(this Result<T> result)
    {
        if (result.IsSuccess)
            return HttpResults.Ok(result.Value);

        return CreateProblemDetails(result.Error!);
    }

    private static IResult CreateProblemDetails(Error error)
    {
        var extensions = new Dictionary<string, object?>
        {
            ["errorCode"] = error.Code,
            ["traceId"] = Activity.Current?.Id ?? Guid.NewGuid().ToString()
        };

        // Add detail if provided
        if (!string.IsNullOrEmpty(error.Detail))
        {
            extensions["detail"] = error.Detail;
        }

        return HttpResults.Problem(
            type: error.GetTypeUri(),
            title: error.Message,
            statusCode: error.StatusCode,
            extensions: extensions
        );
    }
}
