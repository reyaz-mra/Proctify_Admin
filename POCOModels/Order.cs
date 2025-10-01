using System;
using System.Collections.Generic;

namespace FoodMenu.POCOModels;

public partial class Order
{
    public int OrderId { get; set; }

    public int? TableId { get; set; }

    public DateTime? OrderTime { get; set; }

    public string? Status { get; set; }

    public virtual ICollection<Orderitem> Orderitems { get; set; } = new List<Orderitem>();

    public virtual Table? Table { get; set; }
}
