using Microsoft.AspNetCore.Http;
using Rystem.Security.Cryptography;
using Rystem.Text;
using System;

namespace FantacalcioV4.ApiManager
{
    public record FantacalcioBasicState(string Group, int Year, string League);
    public class FantacalcioState
    {
        public string Version { get; } = "4.0.6";
        public string StoragePath { get; } = "https://superfc.blob.core.windows.net/";
        public string FieldImage => $"{StoragePath}static/field.jpg";
        public string FieldInGameImage => $"{StoragePath}static/fieldingame.jpg";
        public string SpecialImage => $"{StoragePath}static/special.png";
        public string GoalImage => $"{StoragePath}static/goal.png";
        public string PenaltyImage => $"{StoragePath}static/goal_blue_v2.png";
        public string AssistImage => $"{StoragePath}static/assist_total.png";
        public string StoppedPenaltyImage => $"{StoragePath}static/goal_green_all.png";
        public string WrongedPenaltyImage => $"{StoragePath}static/goal_silver_all.png";
        public string SufferedGoalImage => $"{StoragePath}static/goal_red_all.png";
        public string OwnGoalImage => $"{StoragePath}static/goal_red_v2.png";
        public string RedWallImage => $"{StoragePath}static/wallred.png";
        public string SitePath => $"{ContextAccessor.HttpContext.Request.Scheme}://{ContextAccessor.HttpContext.Request.Host}";
        public FantacalcioBasicState Basic { get; private set; }
        public SecuredToken Secured { get; }
        private readonly IHttpContextAccessor ContextAccessor;
    }
}
