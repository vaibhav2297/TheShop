using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using TheShop.E2E.Tests.Fixtures;

namespace TheShop.E2E.Tests.Auth;

/// <summary>
/// Reads the local Supabase mail catcher over HTTP and extracts the most recent OTP code sent
/// to a given address. Local-stack only — no real email is ever sent, and capture is free and
/// unlimited.
/// </summary>
public static partial class OtpInbox
{
    [GeneratedRegex(@"\b(\d{6})\b")]
    private static partial Regex OtpCode();

    /// <summary>Waits for the newest OTP email addressed to <paramref name="email"/> and returns its 6-digit code.</summary>
    public static async Task<string> WaitForOtpAsync(string email, TimeSpan? timeout = null)
    {
        var baseUrl = E2EEnvironment.Get("INBUCKET_URL").TrimEnd('/');
        using var http = new HttpClient();
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(30));

        while (DateTime.UtcNow < deadline)
        {
            // Mailpit API (current Supabase CLI).
            var search = await http.GetFromJsonAsync<JsonElement>(
                $"{baseUrl}/api/v1/search?query=to:{Uri.EscapeDataString(email)}");
            if (search.TryGetProperty("messages", out var messages) && messages.GetArrayLength() > 0)
            {
                var id = messages[0].GetProperty("ID").GetString();
                var message = await http.GetFromJsonAsync<JsonElement>($"{baseUrl}/api/v1/message/{id}");
                var body = message.GetProperty("Text").GetString() ?? "";
                var match = OtpCode().Match(body);
                if (match.Success)
                {
                    // Delete after reading so a stale code can never match a later search.
                    await http.DeleteAsync($"{baseUrl}/api/v1/messages");
                    return match.Groups[1].Value;
                }
            }
            await Task.Delay(1000);
        }
        throw new TimeoutException($"No OTP email for {email} arrived within the timeout.");
    }
}
