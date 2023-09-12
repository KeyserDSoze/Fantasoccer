using Rystem.Business;
using System;

namespace FantacalcioV4
{
    public class FantacalcioLog<TEntity> : IDocument
    {
        public string PrimaryKey { get; set; }
        public string SecondaryKey { get; set; }
        public DateTime Timestamp { get; set; }
        public string Message { get; set; }
        public string Error { get; set; }
    }
}