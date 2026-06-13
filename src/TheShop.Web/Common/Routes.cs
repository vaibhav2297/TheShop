namespace TheShop.Web.Common;

/// <summary>
/// Centralised route constants and URL-building helpers for the application.
/// All page <c>[Route]</c> attributes reference values from this class.
/// </summary>
public static class Routes
{
    public const string Home = "/";
    public const string NotFound = "/not-found";
    public const string Cart = "/cart";
    public const string Products = "/products";
    public const string Categories = "/categories";
    public const string Brands = "/brands";
    public const string Deals = "/deals";

    /// <summary>Auth-flow route constants and query-string helpers.</summary>
    public static class Auth
    {
        public const string SignIn = "/sign-in";
        public const string SignInVerify = "/sign-in/verify";
        public const string SignUp = "/sign-up";
        public const string SignUpVerify = "/sign-up/verify";

        /// <summary>
        /// Builds the sign-in URL with <paramref name="returnUrl"/> encoded as a query parameter
        /// so the user is redirected back after authenticating.
        /// </summary>
        public static string SignInWithReturn(string returnUrl) =>
            $"{SignIn}?returnUrl={Uri.EscapeDataString(returnUrl)}";

        /// <summary>
        /// Builds the sign-in verify URL, embedding the target <paramref name="email"/> and
        /// an optional <paramref name="returnUrl"/> as query parameters.
        /// </summary>
        public static string SignInVerifyWith(string email, string? returnUrl = null)
        {
            var url = $"{SignInVerify}?email={Uri.EscapeDataString(email)}";
            if (!string.IsNullOrWhiteSpace(returnUrl))
                url += $"&returnUrl={Uri.EscapeDataString(returnUrl)}";
            return url;
        }
    }
}
