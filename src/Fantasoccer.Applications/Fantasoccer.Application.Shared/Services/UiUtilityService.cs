using System.Text.RegularExpressions;
using Microsoft.JSInterop;

namespace Fantasoccer.Application.Shared
{
    public sealed class UiUtilityService
    {
        private static readonly Regex TestMobile = new("android|webos|iphone|ipad|ipod|blackberry|iemobile|opera mini|mobile");
        public UiUtilityService(IJSRuntime jSRuntime)
        {
            JsRuntime = jSRuntime;
        }
        public IJSRuntime JsRuntime { get; }
        public async ValueTask<bool> IsMobileAsync()
        {
            var userAgent = await JsRuntime.InvokeAsync<string>("getUseragent");
            return TestMobile.IsMatch(userAgent?.ToLower() ?? string.Empty);
        }
        public ValueTask<bool> AddClassAsync(string id, string className)
        {
            return JsRuntime.InvokeAsync<bool>("setClass", [id, className]);
        }
        public ValueTask<bool> RemoveClassAsync(string id, string className)
        {
            return JsRuntime.InvokeAsync<bool>("removeClass", [id, className]);
        }
    }
}
