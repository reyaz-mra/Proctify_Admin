using System;
using System.Collections.Generic;

namespace FoodMenu.POCOModels;

public partial class Menuitem
{
    public int MenuItemId { get; set; }

    public string? Name { get; set; }

    public decimal? Price { get; set; }

    public string? ImageUrl { get; set; }

    public int? CategoryId { get; set; }

    public bool? IsActive { get; set; }

    public virtual Menucategory? Category { get; set; }

    public virtual ICollection<Orderitem> Orderitems { get; set; } = new List<Orderitem>();
}
