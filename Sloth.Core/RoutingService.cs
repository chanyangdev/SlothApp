using Sloth.Core.Models;

namespace Sloth.Core
{
    public static class RoutingService
    {
        public static string GetDocumentSetKey(Customer cust)
        {
            // was: cust.Type
            var key = string.IsNullOrWhiteSpace(cust.Category)
                ? "주택"   // default if blank; adjust if you prefer another default
                : cust.Category.Trim();

            return key;
        }
    }
}