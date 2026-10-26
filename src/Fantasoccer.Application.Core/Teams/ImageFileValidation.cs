using Fantasoccer.Application.Core.Groups;

namespace Fantasoccer.Application.Core.Teams;

public static class ImageFileValidation
{
    public static (string Extension, string ContentType) Validate(byte[] bytes, int maximumBytes, bool allowGif = false)
    {
        if (bytes.Length <= maximumBytes)
        {
            if (bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return ("png", "image/png");
            if (bytes.Length >= 3 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255) return ("jpg", "image/jpeg");
            if (bytes.Length >= 12 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8)) return ("webp", "image/webp");
            if (allowGif && bytes.Length >= 6 && (bytes.AsSpan(0, 6).SequenceEqual("GIF87a"u8) || bytes.AsSpan(0, 6).SequenceEqual("GIF89a"u8))) return ("gif", "image/gif");
        }
        throw new ApplicationFault(400, "invalid_image", "Formato immagine non supportato o dimensione superiore al limite consentito.");
    }
}
