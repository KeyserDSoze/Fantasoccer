using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Fantasoccer.Application.Core.Cards;
using Fantasoccer.PostgreSql.Tests;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fantasoccer.RelationalApi.Tests;

public sealed partial class WorkspaceApiTests
{
    [PostgreSqlFact]
    public async Task Shared_card_library_requires_trusted_global_administration_and_preserves_readable_rules_versions_and_images()
    {
        using var admin = await factory.AccountAsync(globalAdmin: true);
        using var ordinary = await factory.AccountAsync();
        var condition = new CardConditionDto([-2, 0, -2], new(0, new(), new(HasRed: false, Goals: 0)), new(new(Goals: 3), new()));
        var request = new WriteCardRequest("SQL card", "Readable conditions", 0, 3.25f, -1.5f, 2.5f, condition);
        Assert.Equal(HttpStatusCode.Forbidden, (await ordinary.PostAsJsonAsync("/api/cards", request)).StatusCode);
        var created = await admin.PostAsJsonAsync("/api/cards", request);
        created.EnsureSuccessStatusCode();
        var card = (await created.Content.ReadFromJsonAsync<CardDto>())!;
        Assert.Equal(-1.5f, card.NegativePoints);
        var json = await ordinary.GetFromJsonAsync<JsonElement>($"/api/cards/{card.Id}");
        Assert.True(json.TryGetProperty("myCondition", out _));
        Assert.False(json.TryGetProperty("mc", out _));
        Assert.NotNull(card.MyCondition!.ConditionForPlayer!.Minimum);
        Assert.NotNull(card.MyCondition.Condition!.Minimum);
        var replacement = request with { Name = "Updated SQL card", ExpectedVersion = card.Version, MyCondition = null, EnemyCondition = condition };
        var updated = await admin.PutAsJsonAsync($"/api/cards/{card.Id}", replacement);
        updated.EnsureSuccessStatusCode();
        card = (await updated.Content.ReadFromJsonAsync<CardDto>())!;
        Assert.Null(card.MyCondition);
        Assert.Equal(new[] { -2, 0, -2 }, card.EnemyCondition!.RolesInvolved);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync($"/api/cards/{card.Id}", replacement)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/cards/{card.Id}", replacement with { ExpectedVersion = card.Version, EnemyCondition = condition with { RolesInvolved = [99] } })).StatusCode);
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jC9sAAAAASUVORK5CYII=");
        MultipartFormDataContent Body()
        { var content = new MultipartFormDataContent(); content.Add(new ByteArrayContent(png), "file", "card.png"); return content; }
        using var forbiddenImage = Body();
        Assert.Equal(HttpStatusCode.Forbidden, (await ordinary.PostAsync($"/api/cards/{card.Id}/image?expectedVersion={card.Version}", forbiddenImage)).StatusCode);
        using var image = Body();
        var uploaded = await admin.PostAsync($"/api/cards/{card.Id}/image?expectedVersion={card.Version}", image);
        uploaded.EnsureSuccessStatusCode();
        card = (await uploaded.Content.ReadFromJsonAsync<CardDto>())!;
        Assert.Contains("/cardimages/", card.ImageUrl);
        (await admin.DeleteAsync($"/api/cards/{card.Id}?expectedVersion={card.Version}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await ordinary.GetAsync($"/api/cards/{card.Id}")).StatusCode);
        await using var database = factory.Database.CreateContext();
        var archived = await database.RuleCards.SingleAsync(x => x.Id == card.Id);
        Assert.True(archived.IsArchived);
        Assert.NotNull(archived.ImageBlobName);
    }
}
