using System;
using System.Collections.Generic;

namespace FoodMenu.POCOModels;

public partial class Table
{
    public int TableId { get; set; }

    public string TableCode { get; set; } = null!;

    public bool? IsActive { get; set; }

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
}
