namespace Sloth.Core.Models
{
    public sealed class MoveResult
    {
        public bool Executed { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; } = "";

        public string CustomerId { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string DocCode { get; set; } = "";

        public string FileName { get; set; } = "";
        public string SourcePath { get; set; } = "";
        public string? DestPath { get; set; }

        // NEW: where the customer's root was matched (for review)
        public string? MatchedRoot { get; set; }
    }
}