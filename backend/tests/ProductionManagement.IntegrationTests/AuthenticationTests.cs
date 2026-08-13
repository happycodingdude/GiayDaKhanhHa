using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace ProductionManagement.IntegrationTests;

public class AuthenticationTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task An_unauthenticated_request_to_a_business_endpoint_is_rejected()
    {
        var client = Factory.CreateClient();

        var response = await client.GetAsync("/api/v1/orders");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_establishes_a_session_usable_by_the_current_user_endpoint()
    {
        var client = await ClientAsync();

        var response = await client.GetAsync("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_with_a_wrong_password_returns_401_without_revealing_the_reason()
    {
        var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { username = ApiFactory.TestUsername, password = "wrong" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("INVALID_CREDENTIALS", (await response.ReadErrorAsync()).Code);
    }

    [Fact]
    public async Task Login_with_an_unknown_username_returns_the_same_error_as_a_wrong_password()
    {
        var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { username = "nobody", password = "whatever" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("INVALID_CREDENTIALS", (await response.ReadErrorAsync()).Code);
    }

    [Fact]
    public async Task Login_without_credentials_is_a_validation_error()
    {
        var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { username = "", password = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", (await response.ReadErrorAsync()).Code);
    }

    [Fact]
    public async Task Logout_ends_the_session()
    {
        var client = await ClientAsync();

        var logout = await client.PostAsync("/api/v1/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var afterLogout = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }

    [Fact]
    public async Task The_login_response_never_contains_the_password_hash()
    {
        var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { username = ApiFactory.TestUsername, password = ApiFactory.TestPassword });

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("passwordHash", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ApiFactory.TestPassword, body, StringComparison.Ordinal);
    }
}
