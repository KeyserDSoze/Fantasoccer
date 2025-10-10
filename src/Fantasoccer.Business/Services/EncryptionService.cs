using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;

namespace Fantasoccer.Business.Services
{
    public sealed class EncryptionService
    {
        public EncryptionService(IConfiguration configuration)
        {
            var key = configuration["Encryption:Key"];
            _key = Convert.FromBase64String(key!);
            var iv = configuration["Encryption:IV"];
            _iV = Convert.FromBase64String(iv!);
        }

        private readonly byte[] _key;
        private readonly byte[] _iV;
        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return plainText;
            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = _iV;
            var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            using var ms = new MemoryStream();
            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            using (var sw = new StreamWriter(cs))
            {
                sw.Write(plainText);
            }
            var encrypted = ms.ToArray();
            return Convert.ToBase64String(encrypted);
        }
        public string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
                return cipherText;
            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = _iV;
            var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            using var ms = new MemoryStream(Convert.FromBase64String(cipherText));
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var sr = new StreamReader(cs);
            return sr.ReadToEnd();
        }
    }
}
