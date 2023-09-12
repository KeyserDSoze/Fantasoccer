using Rystem.Business;
using System;

namespace FantacalcioV4.Database
{
    public class StoredGroup : IDocument
    {
        public string PrimaryKey { get; set; }
        public string SecondaryKey { get; set; }
        public DateTime Timestamp { get; set; }
    }
}