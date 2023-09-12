using Rystem.Business;
using System;

namespace FantacalcioV4
{
    public class Debug : IDocument
    {
        public string Name { get; set; }
        public string RowKey { get; set; }
        public DateTime Timestamp { get; set; }
        public object Value { get; set; }
    }
}