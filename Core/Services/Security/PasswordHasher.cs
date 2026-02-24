using System.Security.Cryptography;
using System.Text;

namespace Core.Services.Security
{
    public static class PasswordHasher
    {
        public static string Hash(string password)
        {
            var salt = RandomNumberGenerator.GetBytes(16);
            var hash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                350000,
                HashAlgorithmName.SHA256,
                32);

            return Convert.ToHexString(salt) + ":" + Convert.ToHexString(hash);
        }

        public static bool Verify(string password, string storedHash)
        {
            var parts = storedHash.Split(':');
            var salt = Convert.FromHexString(parts[0]);
            var hash = Convert.FromHexString(parts[1]);

            var attempt = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                350000,
                HashAlgorithmName.SHA256,
                32);

            return CryptographicOperations.FixedTimeEquals(hash, attempt);
        }
    }
}
