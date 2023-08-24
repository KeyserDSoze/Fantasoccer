using System;

namespace Fantasoccer
{
    public static class PlayerValue
    {
        private static Func<string, RealPlayer> GetRealPlayer;
        public static void SetPlayerValue(Func<string, RealPlayer> getRealPlayer) 
            => GetRealPlayer = getRealPlayer;
        public static RealPlayer Instance(string key) 
            => GetRealPlayer(key);
    }
}
