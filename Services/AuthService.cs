using System;
using System.Security.Cryptography;
using System.Text;
using OnHouseLocal.Models;

namespace OnHouseLocal.Services
{
    public class AuthService
    {
        // 사용자 지정 가입 승인 마스터 키
        public const string MasterInviteCode = "shgywls";

        public bool ValidateInviteCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return false;
            return string.Equals(code.Trim(), MasterInviteCode, StringComparison.OrdinalIgnoreCase);
        }

        public (string Hash, string Salt) HashPassword(string password)
        {
            byte[] saltBytes = RandomNumberGenerator.GetBytes(16);
            string salt = Convert.ToBase64String(saltBytes);

            using var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, 100_000, HashAlgorithmName.SHA256);
            byte[] hashBytes = pbkdf2.GetBytes(32);
            string hash = Convert.ToBase64String(hashBytes);

            return (hash, salt);
        }

        public bool VerifyPassword(string password, string storedHash, string storedSalt)
        {
            try
            {
                byte[] saltBytes = Convert.FromBase64String(storedSalt);
                using var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, 100_000, HashAlgorithmName.SHA256);
                byte[] hashBytes = pbkdf2.GetBytes(32);
                string computedHash = Convert.ToBase64String(hashBytes);

                return CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(computedHash),
                    Encoding.UTF8.GetBytes(storedHash)
                );
            }
            catch
            {
                return false;
            }
        }
    }
}
