namespace TheShop.Web.Common;

public static class Routes
{
    public const string Home = "/";
    public const string NotFound = "/not-found";
    public const string Cart = "/cart";

    public static class Auth
    {
        public const string SignIn = "/sign-in";
        public const string SignInVerify = "/sign-in/verify";
        public const string SignUp = "/sign-up";
        public const string SignUpVerify = "/sign-up/verify";

        public static string SignInWithReturn(string returnUrl) =>
            $"{SignIn}?returnUrl={Uri.EscapeDataString(returnUrl)}";

        public static string SignInVerifyWith(string email, string? returnUrl = null)
        {
            var url = $"{SignInVerify}?email={Uri.EscapeDataString(email)}";
            if (!string.IsNullOrWhiteSpace(returnUrl))
                url += $"&returnUrl={Uri.EscapeDataString(returnUrl)}";
            return url;
        }
    }
}
