using FoodMenu.DataBaseContext;
using FoodMenu.Models;
using FoodMenu.POCOModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FoodMenu.Controllers
{
    public class MenuController : Controller
    {
        private readonly RestaurantDbContext _context;
        private readonly ILogger<MenuController> _logger;

        public MenuController(RestaurantDbContext context, ILogger<MenuController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Index(string code)
        {
            if (string.IsNullOrEmpty(code)) return NotFound();

            var table = await _context.Tables.FirstOrDefaultAsync(t => t.TableCode == code && (t.IsActive == true || t.IsActive == null));
            if (table == null) return NotFound("Invalid or inactive table.");

            var categories = await _context.Menucategories
                .Where(c => c.IsActive == true)
                .Include(c => c.Menuitems.Where(i => i.IsActive == true))
                .ToListAsync();

            ViewBag.TableCode = code;
            return View(categories);
        }

        [HttpPost]
        public async Task<IActionResult> PlaceOrder([FromForm] OrderInputModel input)
        {
            try
            {
                                 _logger.LogInformation("PlaceOrder called with TableCode: {TableCode}", input?.TableCode);
                 _logger.LogInformation("Items count: {ItemsCount}", input?.Items?.Count ?? 0);
                 if (input?.Items != null)
                 {
                     foreach (var item in input.Items)
                     {
                         _logger.LogInformation("Item: MenuItemId={MenuItemId}, Quantity={Quantity}", item.MenuItemId, item.Quantity);
                     }
                 }
                
                if (input == null || string.IsNullOrEmpty(input.TableCode))
                {
                    _logger.LogWarning("Invalid input: TableCode is null or empty");
                    return BadRequest("Invalid table code.");
                }

                var table = await _context.Tables.FirstOrDefaultAsync(t => t.TableCode == input.TableCode && (t.IsActive == true || t.IsActive == null));
                if (table == null)
                {
                    _logger.LogWarning("Table not found or inactive for code: {TableCode}", input.TableCode);
                    return NotFound("Invalid or inactive table.");
                }

                // Check if there are any items to order
                if (input.Items == null || !input.Items.Any(item => item.Quantity > 0))
                {
                    _logger.LogWarning("No items selected for order");
                    return BadRequest("No items selected for order.");
                }

                var order = new Order
                {
                    TableId = table.TableId,
                    OrderTime = DateTime.Now,
                    Status = "Pending"
                };

                _context.Orders.Add(order);
                await _context.SaveChangesAsync(); // Save to get OrderId

                _logger.LogInformation("Created order with ID: {OrderId}", order.OrderId);

                foreach (var item in input.Items)
                {
                    if (item.Quantity > 0)
                    {
                        var menuItem = await _context.Menuitems.FindAsync(item.MenuItemId);
                        if (menuItem != null)
                        {
                            var orderItem = new Orderitem
                            {
                                OrderId = order.OrderId,
                                MenuItemId = menuItem.MenuItemId,
                                Quantity = item.Quantity,
                                PriceAtOrder = menuItem.Price
                            };
                            
                            _context.Orderitems.Add(orderItem);
                            _logger.LogInformation("Added order item: {MenuItemId} x {Quantity}", menuItem.MenuItemId, item.Quantity);
                        }
                        else
                        {
                            _logger.LogWarning("MenuItem not found: {MenuItemId}", item.MenuItemId);
                        }
                    }
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Order saved successfully with {OrderId}", order.OrderId);
                
                return RedirectToAction("ThankYou");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while placing order");
                return StatusCode(500, "An error occurred while placing your order. Please try again.");
            }
        }

        [HttpPost]
        public IActionResult DebugOrder([FromForm] OrderInputModel input)
        {
            var debugInfo = new
            {
                TableCode = input?.TableCode,
                Items = input?.Items,
                FormData = Request.Form.ToDictionary(x => x.Key, x => x.Value.ToString()),
                HasItems = input?.Items != null && input.Items.Any(x => x.Quantity > 0)
            };
            
            return Json(debugInfo);
        }

        [HttpGet]
        public IActionResult Test()
        {
            return Json(new { message = "MenuController is working", timestamp = DateTime.Now });
        }

        [HttpGet]
        public IActionResult SimpleTest()
        {
            return Content("MenuController is accessible", "text/plain");
        }

        [HttpGet]
        public IActionResult Ping()
        {
            return Ok("Pong");
        }



        public IActionResult ThankYou()
        {
            return View();
        }
    }
}
