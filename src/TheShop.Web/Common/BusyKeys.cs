namespace TheShop.Web.Common;

/// <summary>
/// Well-known key constants for <see cref="BusyState"/>. Centralising them prevents
/// typos and makes it easy to find all callers for a given operation.
/// </summary>
public static class BusyKeys
{
    public const string Global = "global";

    /// <summary>Keys for auth-flow operations.</summary>
    public static class Auth
    {
        public const string SignIn = "auth.sign-in";
        public const string SignUp = "auth.sign-up";
        public const string SignInVerify = "auth.sign-in.verify";
        public const string SignUpVerify = "auth.sign-up.verify";
        public const string ResendOtp = "auth.resend-otp";
    }
}
