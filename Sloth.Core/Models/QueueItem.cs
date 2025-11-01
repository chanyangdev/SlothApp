namespace Sloth.Core.Models
{
    public sealed class QueueItem
    {
        public string CustomerId { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string DocCode { get; set; } = "";    // allow "AUTO"
        public string SourceDir { get; set; } = "";
    }
}