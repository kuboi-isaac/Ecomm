using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ecomm.Models;
using Ecomm.Data;
using System.Diagnostics;

namespace Ecomm.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var viewModel = new HomeViewModel
            {
                FeaturedProducts = await _context.Products
                    .Where(p => p.IsOnSale)
                    .Take(8)
                    .Include(p => p.Category)
                    .ToListAsync(),

                NewArrivals = await _context.Products
                    .OrderByDescending(p => p.Id)
                    .Take(6)
                    .Include(p => p.Category)
                    .ToListAsync(),

                LowStockAlerts = await _context.Products
                    .Where(p => p.StockCount <= p.LowStockThreshold && p.StockCount > 0)
                    .Take(5)
                    .Include(p => p.Category)
                    .ToListAsync(),

                Categories = await _context.Categories
                    .Include(c => c.Products)
                    .Take(6)
                    .ToListAsync()
            };

            return View(viewModel);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }

    public class HomeViewModel
    {
        public List<Product> FeaturedProducts { get; set; } = new();
        public List<Product> NewArrivals { get; set; } = new();
        public List<Product> LowStockAlerts { get; set; } = new();
        public List<Category> Categories { get; set; } = new();
    }
}