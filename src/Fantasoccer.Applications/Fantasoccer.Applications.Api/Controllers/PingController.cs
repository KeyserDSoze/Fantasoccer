using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web.Resource;

namespace Fantasoccer.Applications.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    [RequiredScope(RequiredScopesConfigurationKey = "AzureAd:Scopes")]
    public class PingController : ControllerBase
    {
        [HttpGet]
        public ValueTask<bool> PingAsync() => ValueTask.FromResult(true);
    }
}
