using Fantasoccer.Business.Services;
using Microsoft.Extensions.Configuration;

namespace Fantasoccer.Tests;

public sealed class EncryptionServiceTest
{
    private readonly EncryptionService _service = new(new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Encryption:Key"] = Convert.ToBase64String(new byte[32]),
            ["Encryption:IV"] = Convert.ToBase64String(new byte[16])
        })
        .Build());

    [Fact]
    public void Encrypt_and_decrypt_round_trip_plain_text()
    {
        const string value = "player-key-123";

        var encrypted = _service.Encrypt(value);

        Assert.NotEqual(value, encrypted);
        Assert.Equal(value, _service.Decrypt(encrypted));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Empty_values_are_preserved(string? value)
    {
        Assert.Equal(value, _service.Encrypt(value!));
        Assert.Equal(value, _service.Decrypt(value!));
    }
}
