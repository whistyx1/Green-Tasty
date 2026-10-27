using Amazon.Lambda.APIGatewayEvents;
using APIHandler.Helpers;

namespace APIHandler.Tests.Helpers;

public class ManagementAuthorizationTests
{
    [Theory]
    [InlineData("POST", "/waiters")]
    [InlineData("DELETE", "/waiters")]
    [InlineData("POST", "/locations")]
    [InlineData("POST", "/tables")]
    [InlineData("POST", "/dishes")]
    public void Management_operations_reject_requests_without_trusted_identity(string method, string path)
    {
        var request = new APIGatewayProxyRequest
        {
            HttpMethod = method,
            Path = path,
            Headers = new Dictionary<string, string> { ["Authorization"] = "Bearer untrusted-client-token" }
        };
        Assert.Equal(401, ManagementAuthorization.GetErrorStatus(request));
    }

    [Theory]
    [InlineData("Customer", 403)]
    [InlineData("Waiter", 403)]
    [InlineData("SuperAdmin", 403)]
    [InlineData("[\"SuperAdmin\"]", 403)]
    [InlineData("", 403)]
    [InlineData("Admin", null)]
    [InlineData("Customer,Admin", null)]
    [InlineData("[Admin]", null)]
    [InlineData("[Admin, Customer]", null)]
    [InlineData("[\"Admin\",\"Customer\"]", null)]
    public void Only_exact_admin_group_members_are_allowed(string groups, int? expected)
    {
        var request = new APIGatewayProxyRequest { HttpMethod = "POST", Path = "/waiters/" };
        request.RequestContext = new APIGatewayProxyRequest.ProxyRequestContext();
        request.RequestContext.Authorizer = new APIGatewayCustomAuthorizerContext();
        request.RequestContext.Authorizer.Claims = new Dictionary<string, string>
        {
            ["sub"] = "test-user",
            ["cognito:groups"] = groups
        };
        Assert.Equal(expected, ManagementAuthorization.GetErrorStatus(request));
    }

    [Theory]
    [InlineData("POST", "/register")]
    [InlineData("POST", "/guest/sessions")]
    [InlineData("GET", "/locations")]
    [InlineData("POST", "/reservations")]
    public void Other_routes_keep_their_existing_authorization(string method, string path)
    {
        var request = new APIGatewayProxyRequest { HttpMethod = method, Path = path };
        Assert.Null(ManagementAuthorization.GetErrorStatus(request));
    }
}
