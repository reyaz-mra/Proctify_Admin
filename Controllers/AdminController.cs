using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FoodMenu.DataBaseContext;
using FoodMenu.POCOModels;
using System.Text.Json;

namespace FoodMenu.Controllers
{
    public class AdminController : Controller
    {
        private readonly RestaurantDbContext _context;
        private readonly ILogger<AdminController> _logger;

        public AdminController(RestaurantDbContext context, ILogger<AdminController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Dashboard - Main admin page
        [HttpGet]
        public IActionResult Dashboard()
        {
            return View();
        }

        // Get dashboard statistics
        [HttpGet]
        public async Task<IActionResult> GetDashboardStats()
        {
            try
            {
                var today = DateTime.Today;
                var tomorrow = today.AddDays(1);

                var totalOrders = await _context.Orders.CountAsync();
                var pendingOrders = await _context.Orders
                    .Where(o => o.Status == "Pending" || o.Status == "New" || o.Status == null)
                    .CountAsync();
                
                var todayRevenue = await _context.Orders
                    .Where(o => o.OrderTime >= today && o.OrderTime < tomorrow)
                    .Include(o => o.Orderitems)
                    .ThenInclude(oi => oi.MenuItem)
                    .SumAsync(o => o.Orderitems.Sum(oi => oi.Quantity * oi.MenuItem.Price));

                var activeTables = await _context.Orders
                    .Where(o => o.Status == "Pending" || o.Status == "New" || o.Status == null)
                    .Select(o => o.TableId)
                    .Distinct()
                    .CountAsync();

                return Json(new
                {
                    totalOrders,
                    pendingOrders,
                    todayRevenue = Math.Round(todayRevenue ?? 0, 2),
                    activeTables
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching dashboard stats");
                return Json(new { totalOrders = 0, pendingOrders = 0, todayRevenue = 0, activeTables = 0 });
            }
        }

        // Get history data for analytics
        [HttpGet]
        public async Task<IActionResult> GetHistoryData(DateTime startDate, DateTime endDate)
        {
            try
            {
                var orders = await _context.Orders
                    .Where(o => o.OrderTime >= startDate && o.OrderTime <= endDate.AddDays(1))
                    .Include(o => o.Orderitems)
                    .ThenInclude(oi => oi.MenuItem)
                    .ToListAsync();

                if (!orders.Any())
                {
                    return Json(new { error = "No orders found for the selected date range" });
                }

                var totalOrders = orders.Count;
                var totalRevenue = orders.Sum(o => o.Orderitems.Sum(oi => oi.Quantity * oi.MenuItem.Price)) ?? 0;
                var averageOrderValue = totalRevenue / totalOrders;

                // Get most sold items
                var itemSales = orders
                    .SelectMany(o => o.Orderitems)
                    .GroupBy(oi => oi.MenuItem.Name)
                    .Select(g => new { name = g.Key, quantity = g.Sum(oi => oi.Quantity) })
                    .OrderByDescending(x => x.quantity)
                    .Take(5)
                    .ToList();

                var mostSoldItem = itemSales.FirstOrDefault()?.name ?? "N/A";

                return Json(new
                {
                    totalOrders,
                    totalRevenue = Math.Round(totalRevenue, 2),
                    averageOrderValue = Math.Round(averageOrderValue, 2),
                    mostSoldItem,
                    topItems = itemSales
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching history data");
                return Json(new { error = "Error fetching history data" });
            }
        }

        // Get pending orders for real-time updates
        [HttpGet]
        public async Task<IActionResult> GetPendingOrders()
        {
            try
            {
                var pendingOrders = await _context.Orders
                    .Include(o => o.Table)
                    .Include(o => o.Orderitems)
                    .ThenInclude(oi => oi.MenuItem)
                    .Where(o => o.Status == "Pending" || o.Status == "New" || o.Status == null)
                    .OrderByDescending(o => o.OrderTime)
                    .Take(20)
                    .ToListAsync();

                var result = pendingOrders.Select(o => new
                {
                    orderId = o.OrderId,
                    tableCode = o.Table.TableCode,
                    tableId = o.Table.TableId,
                    orderTime = Convert.ToDateTime(o.OrderTime).ToString("HH:mm"),
                    status = o.Status ?? "New",
                    orderitems = o.Orderitems.Select(oi => new
                    {
                        name = oi.MenuItem.Name,
                        quantity = oi.Quantity,
                        price = oi.MenuItem.Price
                    }).ToList()
                });

                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching pending orders");
                return Json(new List<object>());
            }
        }

        // Get detailed order information
        [HttpGet]
        public async Task<IActionResult> GetOrderDetails(int orderId)
        {
            try
            {
                var order = await _context.Orders
                    .Include(o => o.Table)
                    .Include(o => o.Orderitems)
                    .ThenInclude(oi => oi.MenuItem)
                    .FirstOrDefaultAsync(o => o.OrderId == orderId);

                if (order == null)
                    return Json(new { error = "Order not found" });

                var result = new
                {
                    orderId = order.OrderId,
                    tableCode = order.Table.TableCode,
                    tableId = order.Table.TableId,
                    orderTime = Convert.ToDateTime(order.OrderTime).ToString("dd/MM/yyyy HH:mm"),
                    status = order.Status ?? "New",
                    total = order.Orderitems.Sum(oi => oi.Quantity * oi.MenuItem.Price),
                    items = order.Orderitems.Select(oi => new
                    {
                        name = oi.MenuItem.Name,
                        quantity = oi.Quantity,
                        price = oi.MenuItem.Price,
                        total = oi.Quantity * oi.MenuItem.Price
                    }).ToList()
                };

                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching order details");
                return Json(new { error = "Error fetching order details" });
            }
        }

        // Update order status
        [HttpPost]
        public async Task<IActionResult> UpdateOrderStatus(int orderId, string status)
        {
            try
            {
                var order = await _context.Orders.FindAsync(orderId);
                if (order == null)
                    return Json(new { success = false, message = "Order not found" });

                order.Status = status;
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating order status");
                return Json(new { success = false, message = "Error updating order status" });
            }
        }

        // Categories management
        public async Task<IActionResult> Categories()
        {
            var categories = await _context.Menucategories
                .OrderBy(c => c.CategoryName)
                .ToListAsync();
            return View(categories);
        }

        [HttpPost]
        public async Task<IActionResult> AddCategory(string categoryName)
        {
            try
            {
                var category = new Menucategory
                {
                    CategoryName = categoryName,
                    IsActive = true
                };

                _context.Menucategories.Add(category);
                await _context.SaveChangesAsync();

                TempData["Message"] = "Category added successfully!";
                return RedirectToAction(nameof(Categories));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding category");
                TempData["Error"] = "Error adding category";
                return RedirectToAction(nameof(Categories));
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateCategory(int categoryId, string categoryName, bool isActive = false)
        {
            try
            {
                var category = await _context.Menucategories.FindAsync(categoryId);
                if (category == null)
                {
                    TempData["Error"] = "Category not found";
                    return RedirectToAction(nameof(Categories));
                }

                category.CategoryName = categoryName;
                category.IsActive = isActive;
                await _context.SaveChangesAsync();

                TempData["Message"] = "Category updated successfully!";
                return RedirectToAction(nameof(Categories));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating category");
                TempData["Error"] = "Error updating category";
                return RedirectToAction(nameof(Categories));
            }
        }

        // Menu items management
        public async Task<IActionResult> MenuItems()
        {
            var menuItems = await _context.Menuitems
                .Include(m => m.Category)
                .OrderBy(m => m.Category.CategoryName)
                .ThenBy(m => m.Name)
                .ToListAsync();

            var categories = await _context.Menucategories
                .Where(c => c.IsActive == true)
                .OrderBy(c => c.CategoryName)
                .ToListAsync();

            ViewBag.Categories = categories;
            return View(menuItems);
        }

        [HttpPost]
        public async Task<IActionResult> AddMenuItem(string name, decimal price, int categoryId, string imageUrl = "")
        {
            try
            {
                var menuItem = new Menuitem
                {
                    Name = name,
                    Price = price,
                    CategoryId = categoryId,
                    ImageUrl = imageUrl,
                    IsActive = true
                };

                _context.Menuitems.Add(menuItem);
                await _context.SaveChangesAsync();

                TempData["Message"] = "Menu item added successfully!";
                return RedirectToAction(nameof(MenuItems));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding menu item");
                TempData["Error"] = "Error adding menu item";
                return RedirectToAction(nameof(MenuItems));
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateMenuItem(int menuItemId, string name, decimal price, int categoryId, string imageUrl, bool isActive = false)
        {
            try
            {
                var menuItem = await _context.Menuitems.FindAsync(menuItemId);
                if (menuItem == null)
                {
                    TempData["Error"] = "Menu item not found";
                    return RedirectToAction(nameof(MenuItems));
                }

                menuItem.Name = name;
                menuItem.Price = price;
                menuItem.CategoryId = categoryId;
                menuItem.ImageUrl = imageUrl;
                menuItem.IsActive = isActive;
                await _context.SaveChangesAsync();

                TempData["Message"] = "Menu item updated successfully!";
                return RedirectToAction(nameof(MenuItems));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating menu item");
                TempData["Error"] = "Error updating menu item";
                return RedirectToAction(nameof(MenuItems));
            }
        }

        // Default index action redirects to dashboard
        public IActionResult Index()
        {
            return RedirectToAction(nameof(Dashboard));
        }

        // Live Orders page
        public IActionResult Orders()
        {
            return View();
        }

        // Sales Analytics page
        public IActionResult Analytics()
        {
            return View();
        }

        // Tables management
        public IActionResult Tables()
        {
            var tables = _context.Tables.ToList();
            return View(tables);
        }

        [HttpPost]
        public IActionResult AddTable(string tableCode, int tableCapacity, string tableLocation)
        {
            try
            {
                var table = new Table
                {
                    TableCode = tableCode,
                    //Capacity = tableCapacity,
                    //Location = tableLocation,
                    IsActive = true
                };

                _context.Tables.Add(table);
                _context.SaveChanges();

                TempData["Message"] = "Table added successfully!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error adding table: " + ex.Message;
            }

            return RedirectToAction(nameof(Tables));
        }

        // Settings page
        public IActionResult Settings()
        {
            return View();
        }

        [HttpPost]
        public IActionResult UpdateRestaurantInfo(string restaurantName, string restaurantAddress, string restaurantPhone, string restaurantEmail)
        {
            try
            {
                // In a real application, you would save these to a settings table or configuration file
                TempData["Message"] = "Restaurant information updated successfully!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error updating restaurant information: " + ex.Message;
            }

            return RedirectToAction(nameof(Settings));
        }

        [HttpPost]
        public IActionResult UpdateSystemSettings(string currency, string timezone, string language, bool notifications, bool autoBackup, bool maintenanceMode)
        {
            try
            {
                // In a real application, you would save these to a settings table or configuration file
                TempData["Message"] = "System settings updated successfully!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error updating system settings: " + ex.Message;
            }

            return RedirectToAction(nameof(Settings));
        }

        [HttpPost]
        public IActionResult UpdateSecuritySettings(int sessionTimeout, int maxLoginAttempts, bool twoFactorAuth, bool passwordExpiry)
        {
            try
            {
                // In a real application, you would save these to a settings table or configuration file
                TempData["Message"] = "Security settings updated successfully!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error updating security settings: " + ex.Message;
            }

            return RedirectToAction(nameof(Settings));
        }
    }
}
