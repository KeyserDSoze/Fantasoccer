using Fantasoccer.Application.App;
using Fantasoccer.Application.Shared.Extensions;

var builder = WebApplication.CreateBuilder(args);
await builder.ConfigureServicesAsync();


var app = builder.Build();
await app.ConfigureMiddlewaresAsync();
app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode()
        .AddAdditionalAssemblies(typeof(Fantasoccer.Application.Shared.AppRouter).Assembly);
app.Run();
