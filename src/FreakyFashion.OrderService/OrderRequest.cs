namespace FreakyFashion.OrderService
{
    public class OrderRequest
    {
        public string CustomerId { get; set; } = string.Empty;
        public double TotalPrice { get; set; }
        public string[] Items { get; set; } = Array.Empty<string>();
    }
}