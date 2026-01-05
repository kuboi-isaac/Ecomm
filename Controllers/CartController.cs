using Ecomm.Data;
using Ecomm.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Ecomm.Controllers
{
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private const decimal UGX_RATE = 3800m; // example conversion rate

        public CartController(
            ApplicationDbContext context,
            UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        private string GetUserId()
        {
            // For authenticated users, use their UserId
            var authenticatedUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(authenticatedUserId))
            {
                return authenticatedUserId;
            }

            // For guest users, use session ID
            var guestSessionId = HttpContext.Session.GetString("GuestSessionId");
            if (string.IsNullOrEmpty(guestSessionId))
            {
                guestSessionId = $"guest_{Guid.NewGuid()}";
                HttpContext.Session.SetString("GuestSessionId", guestSessionId);
            }

            return guestSessionId;
        }

        private bool IsGuestUser(string userId)
        {
            return userId?.StartsWith("guest_") == true;
        }

        // GET: Cart
        public async Task<IActionResult> Index()
        {
            var userId = GetUserId();

            var cartItems = await _context.CartItems
                .Where(c => c.UserId == userId)
                .Include(c => c.Product)
                .ThenInclude(p => p.Category)
                .Where(ci => ci.Product != null)
                .ToListAsync();

            var viewModel = new CartViewModel
            {
                CartItems = cartItems,
                TotalAmount = cartItems.Sum(item => (item.Product?.Price ?? 0m) * item.Quantity),
                IsGuestUser = IsGuestUser(userId)
            };

            return View(viewModel);
        }

        // GET: Cart/GetCartCount
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> GetCartCount()
        {
            try
            {
                var userId = GetUserId();
                var count = await _context.CartItems
                    .Where(c => c.UserId == userId)
                    .SumAsync(c => (int?)c.Quantity) ?? 0;
                return Json(new { count = count });
            }
            catch
            {
                return Json(new { count = 0 });
            }
        }

        // POST: Cart/AddToCart
        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> AddToCart([FromBody] AddToCartRequest request)
        {
            try
            {
                if (request == null || request.Quantity <= 0)
                {
                    return Json(new { success = false, message = "Invalid request" });
                }

                var userId = GetUserId();
                var product = await _context.Products.FindAsync(request.ProductId);
                if (product == null)
                {
                    return Json(new { success = false, message = "Product not found" });
                }

                if (product.StockCount < request.Quantity)
                {
                    return Json(new { success = false, message = $"Only {product.StockCount} items available in stock." });
                }

                var existing = await _context.CartItems
                    .FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == request.ProductId);

                if (existing != null)
                {
                    var newQty = existing.Quantity + request.Quantity;
                    if (newQty > product.StockCount)
                    {
                        return Json(new { success = false, message = $"Cannot add more items. Only {product.StockCount} available in stock." });
                    }
                    existing.Quantity = newQty;
                    _context.CartItems.Update(existing);
                }
                else
                {
                    var cartItem = new CartItem
                    {
                        UserId = userId,
                        ProductId = request.ProductId,
                        Quantity = request.Quantity
                    };
                    _context.CartItems.Add(cartItem);
                }

                await _context.SaveChangesAsync();

                var cartCount = await _context.CartItems.Where(c => c.UserId == userId).SumAsync(c => (int?)c.Quantity) ?? 0;

                return Json(new
                {
                    success = true,
                    message = "Added to cart successfully",
                    cartCount = cartCount,
                    productName = product.Name
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding to cart: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while adding to cart." });
            }
        }

        // POST: Cart/UpdateQuantity
        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> UpdateQuantity([FromBody] UpdateQuantityRequest request)
        {
            try
            {
                var userId = GetUserId();
                var cartItem = await _context.CartItems
                    .Include(ci => ci.Product)
                    .FirstOrDefaultAsync(ci => ci.Id == request.CartItemId && ci.UserId == userId);

                if (cartItem == null || cartItem.Product == null)
                {
                    return Json(new { success = false, message = "Item not found" });
                }

                // Ensure requested quantity respects stock limits
                if (request.Quantity < 1) request.Quantity = 1;
                if (request.Quantity > cartItem.Product.StockCount) request.Quantity = cartItem.Product.StockCount;

                cartItem.Quantity = request.Quantity;
                await _context.SaveChangesAsync();

                // calculate totals in UGX (server-side)
                var itemTotalUGX = cartItem.Quantity * (cartItem.Product.Price * UGX_RATE);

                var cartItems = await _context.CartItems
                    .Include(ci => ci.Product)
                    .Where(ci => ci.UserId == userId && ci.Product != null)
                    .ToListAsync();

                var totalUGX = cartItems.Sum(ci => ci.Quantity * (ci.Product!.Price * UGX_RATE));
                var cartCount = cartItems.Sum(ci => ci.Quantity);

                return Json(new
                {
                    success = true,
                    itemTotal = $"Ushs {itemTotalUGX:N0}",
                    totalAmount = $"Ushs {totalUGX:N0}",
                    cartCount = cartCount
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating quantity: {ex.Message}");
                return Json(new { success = false, message = "Error updating quantity" });
            }
        }

        // POST: Cart/RemoveItem
        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> RemoveItem([FromBody] RemoveFromCartRequest request)
        {
            try
            {
                var userId = GetUserId();
                var cartItem = await _context.CartItems
                    .Include(ci => ci.Product)
                    .FirstOrDefaultAsync(ci => ci.Id == request.CartItemId && ci.UserId == userId);

                if (cartItem == null)
                {
                    return Json(new { success = false, message = "Item not found in your cart" });
                }

                _context.CartItems.Remove(cartItem);
                await _context.SaveChangesAsync();

                var cartItems = await _context.CartItems
                    .Include(ci => ci.Product)
                    .Where(ci => ci.UserId == userId && ci.Product != null)
                    .ToListAsync();

                var cartCount = cartItems.Sum(c => c.Quantity);
                var totalUGX = cartItems.Sum(ci => ci.Quantity * (ci.Product!.Price * UGX_RATE));

                return Json(new
                {
                    success = true,
                    message = "Item removed from cart successfully!",
                    cartCount = cartCount,
                    totalAmount = $"Ushs {totalUGX:N0}",
                    itemCount = cartItems.Count
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error removing item: {ex.Message}");
                return Json(new { success = false, message = "Error removing item from cart" });
            }
        }

        // POST: Cart/Clear
        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> Clear()
        {
            try
            {
                var userId = GetUserId();
                var cartItems = await _context.CartItems.Where(c => c.UserId == userId).ToListAsync();

                if (cartItems.Any())
                {
                    _context.CartItems.RemoveRange(cartItems);
                    await _context.SaveChangesAsync();
                }

                TempData["Success"] = "Cart cleared successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing cart: {ex.Message}");
                TempData["Error"] = "Error clearing cart.";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Cart/MergeGuestCart
        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> MergeGuestCart()
        {
            try
            {
                var authenticatedUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(authenticatedUserId))
                {
                    return RedirectToAction(nameof(Index));
                }

                var guestSessionId = HttpContext.Session.GetString("GuestSessionId");
                if (!string.IsNullOrEmpty(guestSessionId))
                {
                    var guestCartItems = await _context.CartItems
                        .Where(c => c.UserId == guestSessionId)
                        .ToListAsync();

                    foreach (var guestItem in guestCartItems)
                    {
                        var existingItem = await _context.CartItems
                            .FirstOrDefaultAsync(c => c.UserId == authenticatedUserId && c.ProductId == guestItem.ProductId);

                        if (existingItem != null)
                        {
                            var product = await _context.Products.FindAsync(guestItem.ProductId);
                            if (product != null && existingItem.Quantity + guestItem.Quantity <= product.StockCount)
                            {
                                existingItem.Quantity += guestItem.Quantity;
                            }

                            _context.CartItems.Remove(guestItem);
                        }
                        else
                        {
                            guestItem.UserId = authenticatedUserId;
                        }
                    }

                    await _context.SaveChangesAsync();
                    HttpContext.Session.Remove("GuestSessionId");
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error merging cart: {ex.Message}");
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Cart/AddToCartRedirect (Traditional form post)
        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> AddToCartRedirect(int productId, int quantity = 1)
        {
            try
            {
                var userId = GetUserId();
                var product = await _context.Products.FindAsync(productId);
                if (product == null)
                {
                    TempData["Error"] = "Product not found.";
                    return RedirectToAction("Index", "Products");
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

                object value = await _context.SaveChangesAsync();
                TempData["Success"] = "Product added to cart.";
                return RedirectToAction("Details", "Products", new { id = productId });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding to cart (redirect): {ex.Message}");
                TempData["Error"] = "Error adding product to cart.";
                return RedirectToAction("Index", "Products");
            }
        }
    }

    // Request models
    public class AddToCartRequest
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; } = 1;
    }

    public class UpdateQuantityRequest
    {
        public int CartItemId { get; set; }
        public int Quantity { get; set; }
    }

    public class RemoveFromCartRequest
    {
        public int CartItemId { get; set; }
    }

    public class CartViewModel
    {
        public List<CartItem> CartItems { get; set; } = new();
        public decimal TotalAmount { get; set; }
        public bool IsGuestUser { get; set; }
    }
}