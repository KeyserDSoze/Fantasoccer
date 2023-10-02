using System.Security.Cryptography;

namespace Fantasoccer.Domain
{
    public static class RsaParametersExtensions
    {
        public static RsaOptions ToRsaOptions(this RSAParameters parameters)
            => new()
            {
                D = parameters.D,
                DP = parameters.DP,
                DQ = parameters.DQ,
                Exponent = parameters.Exponent,
                InverseQ = parameters.InverseQ,
                Modulus = parameters.Modulus,
                P = parameters.P,
                Q = parameters.Q,
                ExpiringDate = DateTime.UtcNow.AddDays(7)
            };
    }
}
