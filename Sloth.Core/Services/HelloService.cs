namespace Sloth.Core.Services;

public static class HelloService
{
    public static string Hello(Sloth.Core.Models.Customer c)
        => $"Hello, {c.Name} ({c.CustomerId})";
}
