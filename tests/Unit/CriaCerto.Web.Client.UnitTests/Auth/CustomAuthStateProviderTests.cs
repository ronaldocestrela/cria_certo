using CriaCerto.Web.Client.Auth;
using FluentAssertions;
using System.Text;
using System.Text.Json;

namespace CriaCerto.Web.Client.UnitTests.Auth;

public class CustomAuthStateProviderTests
{
    [Fact]
    public void ParseClaimsFromJwt_ShouldExtractClaimsFromPayload()
    {
        var token = CreateJwt(new Dictionary<string, object>
        {
            ["sub"] = "user-1",
            ["email"] = "test@test.com",
            ["exp"] = 4102444800L
        });

        var claims = CustomAuthStateProvider.ParseClaimsFromJwt(token).ToList();

        claims.Should().Contain(c => c.Type == "sub" && c.Value == "user-1");
        claims.Should().Contain(c => c.Type == "email" && c.Value == "test@test.com");
        claims.Should().Contain(c => c.Type == "exp" && c.Value == "4102444800");
    }

    [Fact]
    public void ParseClaimsFromJwt_ShouldExpandArrayClaims()
    {
        var token = CreateJwt(new Dictionary<string, object>
        {
            ["role"] = new[] { "Admin", "Operator" }
        });

        var claims = CustomAuthStateProvider.ParseClaimsFromJwt(token).ToList();

        claims.Where(c => c.Type == "role").Select(c => c.Value)
            .Should().BeEquivalentTo(["Admin", "Operator"]);
    }

    private static string CreateJwt(Dictionary<string, object> payload)
    {
        var header = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"alg\":\"none\",\"typ\":\"JWT\"}"));
        var payloadJson = JsonSerializer.Serialize(payload);
        var payloadSegment = Convert.ToBase64String(Encoding.UTF8.GetBytes(payloadJson));
        return $"{header}.{payloadSegment}.signature";
    }
}
