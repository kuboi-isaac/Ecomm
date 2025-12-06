using Ecomm.Data;
using Ecomm.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

public class CheckoutController : Controller
{
    private readonly ApplicationDbContext _context;

    public CheckoutController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var cartItems = await _context.CartItems
            .Include(ci => ci.Product)
            .Where(ci => ci.UserId == userId)
            .ToListAsync();

        if (!cartItems.Any())
        {
            TempData["Error"] = "Your cart is empty!";
            return RedirectToAction("Index", "Cart");
        }

        var model = new CheckoutViewModel
        {
            CartItems = cartItems,
            TotalAmount = cartItems.Sum(ci => (ci.Product?.Price ?? 0) * ci.Quantity)
        };

        return View(model);
    }

    public IActionResult OrderHistory()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var orders = _context.Orders
            .Where(o => o.UserId == userId)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .OrderByDescending(o => o.OrderDate)
            .ToList();

        return View(orders);
    }
}
