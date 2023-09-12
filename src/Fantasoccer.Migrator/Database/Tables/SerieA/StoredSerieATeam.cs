using Rystem.Business;
using System;

namespace FantacalcioV4.Database
{
    public class StoredSerieATeam : IDocument
    {
        public string PrimaryKey { get; set; }
        public int FantacalcioId { get; set; }
        public string Abbreviation { get; set; }
        public string SecondaryKey { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public bool IsActive { get; set; }
    }
}