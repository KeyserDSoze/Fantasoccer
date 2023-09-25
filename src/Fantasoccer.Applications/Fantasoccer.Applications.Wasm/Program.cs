using Blazorise;
using Blazorise.Bootstrap5;
using Blazorise.Icons.FontAwesome;
using Fantasoccer.Applications.Wasm;
using Fantasoccer.Applications.Wasm.Extensions;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services
    .AddBlazorise(options =>
    {
        options.Immediate = true;
    })
    .AddBootstrap5Providers()
    .AddFontAwesomeIcons();
builder.Services.AddMsalAuthentication(options =>
{
    builder.Configuration.Bind("AzureAdB2C", options.ProviderOptions.Authentication);
    options.ProviderOptions.DefaultAccessTokenScopes.Add(builder.Configuration["AzureAdB2C:Scopes"]!);
});
builder.Services.AddHttpClientIntegrationForApi(builder.Configuration);
builder.Services.AddSharedBusiness();
builder.Services.AddDefaultAuthorizationInterceptorForApiHttpClient(settings =>
{
    settings.Scopes = new string[] { builder.Configuration["AzureAdB2C:Scopes"]! };
});
//builder.Services.AddDefaultAuthorizationInterceptorForApiHttpClient<TokenManager>(
//                x => { x.Scopes = new string[] { builder.Configuration["AzureAdB2C:Scopes"]! }; }, ServiceLifetime.Scoped);

await builder.Build().RunAsync();
