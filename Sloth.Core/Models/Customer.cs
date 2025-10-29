namespace Sloth.Core.Models;

// Record with init-only properties + friendly constructors
public record Customer
{
    public int No { get; init; }
    public string CustomerId { get; init; } = "";
    public string Name { get; init; } = "";
    public string Category { get; init; } = "";     // "주택" | "건물"
    public string Corp { get; init; } = "";         // 법인
    public string RoadAddress { get; init; } = "";  // 고객_도로명
    public string LotAddress { get; init; } = "";   // 고객_지번
    public string? FolderPath { get; init; }        // optional

    // Parameterless (for serializers/migration)
    public Customer() { }

    // Legacy/minimal constructor (keeps older call sites compiling)
    public Customer(int no, string customerId, string name)
    {
        No = no;
        CustomerId = customerId ?? "";
        Name = name ?? "";
    }

    // Full constructor (new code path)
    public Customer(int no, string customerId, string name,
                    string category, string corp, string roadAddress, string lotAddress,
                    string? folderPath)
    {
        No = no;
        CustomerId = customerId ?? "";
        Name = name ?? "";
        Category = category ?? "";
        Corp = corp ?? "";
        RoadAddress = roadAddress ?? "";
        LotAddress = lotAddress ?? "";
        FolderPath = folderPath;
    }
}
