using System;
using System.Collections.Generic;

namespace FoodMenu.POCOModels;

public partial class Orderitem
{
    public int OrderItemId { get; set; }

    public int? OrderId { get; set; }

    public int? MenuItemId { get; set; }

    public int? Quantity { get; set; }

    public decimal? PriceAtOrder { get; set; }

    public virtual Menuitem? MenuItem { get; set; }

    public virtual Order? Order { get; set; }
}
