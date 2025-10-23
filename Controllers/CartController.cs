using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ecomm.Models;
using Ecomm.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Ecomm.Controllers
{
    [Authorize]
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CartController(ApplicationDbContext context)
        {
            _context = context;
        }

        private string GetUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        }

        // GET: Cart
        public async Task<IActionResult> Index()
        {
            var userId = GetUserId();
            var cartItems = await _context.CartItems
                .Where(c => c.UserId == userId)
                .Include(c => c.Product)
                .ThenInclude(p => p.Category)
                .ToListAsync();

            var viewModel = new CartViewModel
            {
                CartItems = cartItems,
                TotalAmount = cartItems.Sum(item => item.Product!.Price * item.Quantity)
            };

            return View(viewModel);
        }

        // POST: Cart/AddToCart
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToCart(int productId, int quantity = 1)
        {
            var userId = GetUserId();
            var product = await _context.Products.FindAsync(productId);
            if (product == null)
            {
                return NotFound();
            }

            if (product.StockCount < quantity)
            {
                TempData["Error"] = $"Only {product.StockCount} items available in stock.";
                return RedirectToAction("Details", "Products", new { id = productId });
            }

            var existingCartItem = await _context.CartItems
                .FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == productId);

            if (existingCartItem != null)
            {
                existingCartItem.Quantity += quantity;
            }
            else
            {
                var cartItem = new CartItem
                {
                    UserId = userId,
                    ProductId = productId,
                    Quantity = quantity
                };
                _context.CartItems.Add(cartItem);
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Product added to cart successfully!";
            return RedirectToAction("Index", "Products");
        }

        // POST: Cart/UpdateQuantity
        [HttpPost]
        public async Task<JsonResult> UpdateQuantity(int cartItemId, int quantity)
        {
            var userId = GetUserId();
            var cartItem = await _context.CartItems
                .Include(c => c.Product)
                .FirstOrDefaultAsync(c => c.Id == cartItemId && c.UserId == userId);

            if (cartItem == null)
            {
                return Json(new { success = false, message = "Cart item not found" });
            }

            if (quantity <= 0)
            {
                _context.CartItems.Remove(cartItem);
            }
            else
            {
                if (cartItem.Product!.StockCount < quantity)
                {
                    return Json(new { success = false, message = $"Only {cartItem.Product.StockCount} items available." });
                }
                cartItem.Quantity = quantity;
            }

            await _context.SaveChangesAsync();

            var updatedCartItems = await _context.CartItems
                .Where(c => c.UserId == userId)
                .Include(c => c.Product)
                .ToListAsync();

            var totalAmount = updatedCartItems.Sum(item => item.Product!.Price * item.Quantity);
            var itemTotal = cartItem?.Product!.Price * (quantity > 0 ? quantity : 0) ?? 0;

            return Json(new
            {
                success = true,
                totalAmount = totalAmount.ToString("C2"),
                itemTotal = itemTotal.ToString("C2"),
                cartCount = updatedCartItems.Sum(item => item.Quantity)
            });
        }

        // POST: Cart/RemoveFromCart
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveFromCart(int cartItemId)
        {
            var userId = GetUserId();
            var cartItem = await _context.CartItems
                .FirstOrDefaultAsync(c => c.Id == cartItemId && c.UserId == userId);

            if (cartItem != null)
            {
                _context.CartItems.Remove(cartItem);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Item removed from cart.";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Cart/GetCartCount
        [HttpGet]
        public async Task<JsonResult> GetCartCount()
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Json(new { count = 0 });
            }

            var count = await _context.CartItems
                .Where(c => c.UserId == userId)
                .SumAsync(c => c.Quantity);

            return Json(new { count = count });
        }
    }

    public class CartViewModel
    {
        public List<CartItem> CartItems { get; set; } = new();
        public decimal TotalAmount { get; set; }
    }
}