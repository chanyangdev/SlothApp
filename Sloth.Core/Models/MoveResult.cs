namespace Sloth.Core.Models;

public class MoveResult
{
    public string SourcePath { get; set; } = "";
    public string? DestPath { get; set; } = null;

    public string CustomerId { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public string DocCode { get; set; } = "";
    public string FileName { get; set; } = "";

    public bool Executed { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = "";
}