using Amazon.Lambda.APIGatewayEvents;
using System;
using System.Collections.Generic;
using System.Linq;

namespace APIHandler.Helpers;

public static class ManagementAuthorization
{
    private static readonly HashSet<(string Method, string Path)> AdminOperations =
    [
        ("POST", "/waiters"),
        ("DELETE", "/waiters"),
        ("POST", "/locations"),
        ("POST", "/tables"),
        ("POST", "/dishes")
    ];

    // Use claims supplied by the Gateway authorizer, never client-supplied headers.
    public static int? GetErrorStatus(APIGatewayProxyRequest request)
    {
        if (!AdminOperations.Contains((request.HttpMethod.ToUpperInvariant(), request.Path.TrimEnd('/'))))
            return null;

        var claims = request.RequestContext?.Authorizer?.Claims;
        if (claims == null || !claims.TryGetValue("sub", out var subject) || string.IsNullOrWhiteSpace(subject))
            return 401;

        if (!claims.TryGetValue("cognito:groups", out var groups) || string.IsNullOrWhiteSpace(groups))
            return 403;

        // Gateway can stringify a multi-value claim with or without JSON quotes.
        var isAdmin = groups.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Any(group => group.Trim(' ', '[', ']', '"') == "Admin");
        return isAdmin ? null : 403;
    }
}
