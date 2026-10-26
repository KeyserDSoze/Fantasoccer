using Fantasoccer.DataMigration;
using Fantasoccer.StorageMigration;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fantasoccer.PostgreSql.Tests;

public sealed partial class CoreMigrationTests
{
    [CoreFact]
    public async Task Invalid_email_is_a_Blob_document_error_even_without_tables_and_has_safe_field_diagnostics()
    {
        const string invalid = "private-invalid-value-not-for-error-output";
        await PutAccountAsync(invalid, IdentityRole.Admin, IdentityRole.Participant);
        var manifest = await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        Assert.Empty(manifest.Tables);
        var error = await Assert.ThrowsAsync<CoreImportException>(() => CoreDatasetImporter.AnalyzeAsync(_dump));
        Assert.Equal("invalid_email", error.Message);
        Assert.Equal("account.Email", error.Field);
        Assert.StartsWith("CoreDatasetImporter.cs:", error.Reader);
        Assert.DoesNotContain(invalid, error.ToString() + error.Field + error.Reader);
        await using var database = fixture.CreateContext();
        Assert.Empty(await database.Groups.ToArrayAsync());
    }
}
