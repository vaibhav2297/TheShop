namespace TheShop.Web.Common;

public static class BusyKeys
{
    public const string Global = "global";

    public static class Auth
    {
        public const string SignIn = "auth.sign-in";
        public const string SignUp = "auth.sign-up";
        public const string SignInVerify = "auth.sign-in.verify";
        public const string SignUpVerify = "auth.sign-up.verify";
        public const string ResendOtp = "auth.resend-otp";
        public const string SignOut = "auth.sign-out";
    }
}
