namespace Sloth.Core.Models;

public record Customer(
    int No,
    string CustomerId,
    string Name,
    string Category,     // "주택" | "건물"
    string Corp,         // 법인
    string RoadAddress,  // 고객_도로명
    string LotAddress,   // 고객_지번
    string? FolderPath   // optional direct folder path
);