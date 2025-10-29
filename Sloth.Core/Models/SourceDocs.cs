namespace Sloth.Core.Models;

public class SourceDoc
{
    public string SourcePath { get; set; } = "";
    public string CustomerId { get; set; } = "";   // e.g., C24001
    public string DocCode { get; set; } = "";      // e.g., 현장점검표
}