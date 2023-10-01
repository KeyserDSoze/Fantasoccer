using System.Security.Cryptography;

namespace Fantasoccer.Domain
{
    public sealed class RsaOptions
    {
        public byte[]? D { get; set; }
        public byte[]? DP { get; set; }
        public byte[]? DQ { get; set; }
        public byte[]? Exponent { get; set; }
        public byte[]? InverseQ { get; set; }
        public byte[]? Modulus { get; set; }
        public byte[]? P { get; set; }
        public byte[]? Q { get; set; }
        public DateTime ExpiringDate { get; set; }
        public RSAParameters ToParameters()
            => new()
            {
                D = D,
                DP = DP,
                DQ = DQ,
                Exponent = Exponent,
                InverseQ = InverseQ,
                Modulus = Modulus,
                P = P,
                Q = Q
            };
    }
}
