namespace Fantasoccer.Application.Core.Editorial;

public sealed class EditorialAiOptions
{
    public bool Enabled { get; set; }
    public string Endpoint { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string ModelName { get; set; } = "";
}
public interface IEditorialTextGenerator
{
    Task<string> GenerateAsync(string instruction, string facts, CancellationToken cancellationToken);
}
public sealed record NewsArticleDto(int Ordinal, Guid? LeagueId, string LeagueName, string Title, string Content, DateTimeOffset Date,
    string? Image, string Format, int? SourceVersion);
public sealed record NewsEditionDto(Guid GroupId, int Year, int Day, int Version, NewsArticleDto[] Articles);
public sealed record TeamAdviceDto(Guid GroupId, Guid TeamId, int Year, int Day, string Message, int Version, bool IsGenerated, int? SourceRosterVersion);
public sealed record GenerateNewsRequest(int ExpectedVersion);
public sealed record GenerateAdviceRequest(int ExpectedVersion, int ExpectedRosterVersion);
