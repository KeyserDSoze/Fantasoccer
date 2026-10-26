using System.ClientModel;
using Azure.AI.OpenAI;
using Fantasoccer.Application.Core.Editorial;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace Fantasoccer.Application.Api.Editorial;

public sealed class AzureEditorialTextGenerator(IOptions<EditorialAiOptions> configuration) : IEditorialTextGenerator
{
    public async Task<string> GenerateAsync(string instruction, string facts, CancellationToken cancellationToken)
    {
        var options = configuration.Value;
        if (!options.Enabled) throw new InvalidOperationException("Editorial generation disabled.");
        var client = new AzureOpenAIClient(new Uri(options.Endpoint), new ApiKeyCredential(options.ApiKey)).GetChatClient(options.ModelName);
        var result = await client.CompleteChatAsync([new SystemChatMessage(instruction), new UserChatMessage(facts)],
            new ChatCompletionOptions { MaxOutputTokenCount = 4096 }, cancellationToken);
        return string.Concat(result.Value.Content.Select(x => x.Text));
    }
}
