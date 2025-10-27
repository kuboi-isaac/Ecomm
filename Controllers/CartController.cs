using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ecomm.Models;
using Ecomm.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace Ecomm.Controllers
{
    [Authorize]
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public CartController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        private string GetUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        }

        private string GetCurrentUserId()
        {
            return _userManager.GetUserId(User);
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

            // Set cart count for navbar
            ViewBag.CartCount = cartItems.Sum(item => item.Quantity);

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
                // Check if total quantity exceeds stock
                if (existingCartItem.Quantity + quantity > product.StockCount)
                {
                    TempData["Error"] = $"Cannot add more items. Only {product.StockCount} available in stock.";
                    return RedirectToAction("Details", "Products", new { id = productId });
                }
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
            try
            {
                var userId = GetUserId();
                var cartItem = await _context.CartItems
                    .Include(ci => ci.Product)
                    .FirstOrDefaultAsync(ci => ci.Id == cartItemId && ci.UserId == userId);

                if (cartItem != null)
                {
                    // Validate quantity doesn't exceed stock
                    if (quantity > cartItem.Product.StockCount)
                    {
                        return Json(new { success = false, message = "Quantity exceeds available stock" });
                    }

                    cartItem.Quantity = quantity;
                    await _context.SaveChangesAsync();

                    // Calculate updated totals
                    var cartItems = await _context.CartItems
                        .Include(ci => ci.Product)
                        .Where(ci => ci.UserId == userId)
                        .ToListAsync();

                    var totalAmount = cartItems.Sum(ci => ci.Product.Price * ci.Quantity);
                    var cartCount = cartItems.Sum(ci => ci.Quantity);

                    return Json(new
                    {
                        success = true,
                        itemTotal = (cartItem.Product.Price * quantity).ToString("C2"),
                        totalAmount = totalAmount.ToString("C2"),
                        cartCount = cartCount
                    });
                }

                return Json(new { success = false, message = "Item not found" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error updating quantity" });
            }
        }

        // POST: Cart/RemoveFromCart (AJAX version)
        [HttpPost]
        public async Task<JsonResult> RemoveFromCart(int cartItemId)
        {
            try
            {
                var userId = GetUserId();
                var cartItem = await _context.CartItems
                    .FirstOrDefaultAsync(ci => ci.Id == cartItemId && ci.UserId == userId);

                if (cartItem != null)
                {
                    _context.CartItems.Remove(cartItem);
                    await _context.SaveChangesAsync();

                    // Get updated cart count
                    var cartCount = await _context.CartItems
                        .Where(c => c.UserId == userId)
                        .SumAsync(c => c.Quantity);

                    return Json(new { success = true, cartCount = cartCount });
                }

                return Json(new { success = false, message = "Item not found" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error removing item" });
            }
        }

        // POST: Cart/RemoveFromCart (Redirect version)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveFromCartRedirect(int cartItemId)
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

        // POST: Cart/ClearCart
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearCart()
        {
            var userId = GetUserId();
            var cartItems = await _context.CartItems
                .Where(c => c.UserId == userId)
                .ToListAsync();

            if (cartItems.Any())
            {
                _context.CartItems.RemoveRange(cartItems);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Cart cleared successfully!";
            }

            return RedirectToAction(nameof(Index));
        }
    }

    public class CartViewModel
    {
        public List<CartItem> CartItems { get; set; } = new();
        public decimal TotalAmount { get; set; }
    }
}