using System;
using System.Security.Cryptography;
using System.Text;

namespace TokenDock
{
    static class ProtectedSecretStore
    {
        public static string Protect(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "" : Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value.Trim()), null, DataProtectionScope.CurrentUser));
        }

        public static string Reveal(string encrypted)
        {
            return string.IsNullOrEmpty(encrypted) ? "" : Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(encrypted), null, DataProtectionScope.CurrentUser));
        }
    }
}
