namespace FoodMenu.Models
{
    public class OrderInputModel
    {
        public string TableCode { get; set; }
        public List<OrderItemInput> Items { get; set; } = new List<OrderItemInput>();
    }

    public class OrderItemInput
    {
        public int MenuItemId { get; set; }
        public int Quantity { get; set; }
    }
}
