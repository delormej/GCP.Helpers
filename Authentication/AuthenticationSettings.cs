namespace GcpHelpers.Authentication
{   
    public class AuthenticationSettings
    {
        public static string Section = "Authentication";
        public const string AdminRole = "Admin";

        public AuthenticationSettings()
        {}

        public bool Enabled { get; set; }
        public string ValidIssuer { get; set; } = "";
        public string ValidAudience { get; set; } = "";
        public string PublicKeyPemFile { get; set; } = "";
        public string PrivateKeyPemFile { get; set; } = "";
        public string GoogleClientId { get; set; } = "";
        public string GoogleClientSecret { get; set; } = "";
        public string RedirectUri { get; set; } = "";
        public int ExpiresInMinutes { get; set; } = 60;
    }
}                    