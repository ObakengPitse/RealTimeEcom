namespace RealTimeEcom.WebApp.Models
{
    public class OrderDto
    {
        public Guid? Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = "card";
        public decimal Total { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public List<OrderItemDto> Items { get; set; } = new();
    }

    public class OrderItemDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Qty { get; set; }
        public decimal Price { get; set; }
    }
}
