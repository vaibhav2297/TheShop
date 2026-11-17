using System.Text;
using System.Text.Json;
using Xunit;

namespace TheShop.E2E.Tests.Auth;

public sealed class AuthStateFactoryTests
{
    [Theory]
    [InlineData(-60, false)]
    [InlineData(60, false)]
    [InlineData(3600, true)]
    public void CachedSession_Expiry_RequiresTimeForTheNextJourney(int remainingSeconds, bool usable)
    {
        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            exp = DateTimeOffset.UtcNow.AddSeconds(remainingSeconds).ToUnixTimeSeconds()
        }))).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var session = JsonSerializer.Serialize(new { AccessToken = $"header.{payload}.signature" });
        var state = JsonSerializer.Serialize(new
        {
            origins = new[] { new { localStorage = new[] { new { name = "shop.auth.session", value = session } } } }
        });
        AssertUsable(state, usable);
    }

    [Theory]
    [InlineData("{\"origins\":[]}")]
    [InlineData("{}")]
    [InlineData("invalid json")]
    public void CachedSession_EmptyOrMalformedState_RequiresSignIn(string state) => AssertUsable(state, false);

    private static void AssertUsable(string state, bool expected)
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, state);
            Assert.Equal(expected, AuthStateFactory.HasUsableSession(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
