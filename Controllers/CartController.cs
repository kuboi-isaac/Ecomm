using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ecomm.Models;
using Ecomm.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace Ecomm.Controllers
{
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

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
            return userId.StartsWith("guest_");
        }

        // GET: Cart
        public async Task<IActionResult> Index()
        {
            var userId = GetUserId();

            // Get cart items with products
            var cartItems = await _context.CartItems
                .Where(c => c.UserId == userId)
                .Include(c => c.Product)
                .ThenInclude(p => p != null ? p.Category : null)
                .Where(ci => ci.Product != null) // Filter out deleted products
                .ToListAsync();

            var viewModel = new CartViewModel
            {
                CartItems = cartItems,
                TotalAmount = cartItems.Sum(item => item.Product!.Price * item.Quantity),
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
                // Validate request
                if (request.Quantity <= 0)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Quantity must be greater than 0"
                    });
                }

                // Get or create user ID
                var userId = GetUserId();
                var isGuestUser = IsGuestUser(userId);

                // Get product
                var product = await _context.Products
                    .FirstOrDefaultAsync(p => p.Id == request.ProductId);

                if (product == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Product not found"
                    });
                }

                // Check stock availability
                if (product.StockCount < request.Quantity)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Only {product.StockCount} items available in stock."
                    });
                }

                // Find existing cart item
                var existingCartItem = await _context.CartItems
                    .FirstOrDefaultAsync(c =>
                        c.UserId == userId &&
                        c.ProductId == request.ProductId);

                if (existingCartItem != null)
                {
                    // Check if total quantity exceeds stock
                    var newTotalQuantity = existingCartItem.Quantity + request.Quantity;
                    if (newTotalQuantity > product.StockCount)
                    {
                        return Json(new
                        {
                            success = false,
                            message = $"Cannot add more items. Only {product.StockCount} available in stock."
                        });
                    }

                    existingCartItem.Quantity = newTotalQuantity;
                    _context.CartItems.Update(existingCartItem);
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

                // Get updated cart count
                var cartCount = await _context.CartItems
                    .Where(c => c.UserId == userId)
                    .SumAsync(c => (int?)c.Quantity) ?? 0;

                return Json(new
                {
                    success = true,
                    message = $"{product.Name} added to cart successfully!",
                    cartCount = cartCount,
                    isGuestUser = isGuestUser,
                    productName = product.Name
                });
            }
            catch (Exception ex)
            {
                // Log the exception (use proper logging in production)
                Console.WriteLine($"Error adding to cart: {ex.Message}");
                return Json(new
                {
                    success = false,
                    message = "An error occurred while adding to cart. Please try again."
                });
            }
        }

        // POST: Cart/UpdateQuantity
        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> UpdateQuantity([FromBody] UpdateQuantityRequest request)
        {
            var userId = GetUserId();
            var cartItem = await _context.CartItems
                .Include(ci => ci.Product)
                .FirstOrDefaultAsync(ci => ci.Id == request.CartItemId && ci.UserId == userId);

            if (cartItem == null || cartItem.Product == null)
            {
                return Json(new { success = false, message = "Item not found" });
            }

            cartItem.Quantity = request.Quantity;
            await _context.SaveChangesAsync();

            const decimal RATE = 3800;

            // Calculate totals in UGX
            var itemTotalUGX = cartItem.Quantity * (cartItem.Product.Price * RATE);

            var cartItems = await _context.CartItems
                .Include(ci => ci.Product)
                .Where(ci => ci.UserId == userId)
                .ToListAsync();

            var totalUGX = cartItems.Sum(ci => ci.Quantity * (cartItem.Product.Price * RATE));
            var cartCount = cartItems.Sum(ci => ci.Quantity);

            return Json(new
            {
                success = true,
                itemTotal = $"Ushs {itemTotalUGX:N0}",
                totalAmount = $"Ushs {totalUGX:N0}",
                cartCount = cartCount
            });
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
                    .FirstOrDefaultAsync(ci =>
                        ci.Id == request.CartItemId &&
                        ci.UserId == userId);

                if (cartItem == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Item not found in your cart"
                    });
                }

                _context.CartItems.Remove(cartItem);
                await _context.SaveChangesAsync();

                // Get updated cart data
                var cartItems = await _context.CartItems
                    .Include(ci => ci.Product)
                    .Where(ci => ci.UserId == userId && ci.Product != null)
                    .ToListAsync();

                var cartCount = cartItems.Sum(c => c.Quantity);
                var totalAmount = cartItems.Sum(ci => ci.Product!.Price * ci.Quantity);

                return Json(new
                {
                    success = true,
                    message = "Item removed from cart successfully!",
                    cartCount = cartCount,
                    totalAmount = totalAmount.ToString("C2"),
                    itemCount = cartItems.Count
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error removing item: {ex.Message}");
                return Json(new
                {
                    success = false,
                    message = "Error removing item from cart"
                });
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
            catch
            {
                TempData["Error"] = "Error clearing cart.";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Cart/MergeGuestCart
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
                    // Get guest cart items
                    var guestCartItems = await _context.CartItems
                        .Where(c => c.UserId == guestSessionId)
                        .ToListAsync();

                    foreach (var guestItem in guestCartItems)
                    {
                        // Check if user already has this product in cart
                        var existingItem = await _context.CartItems
                            .FirstOrDefaultAsync(c =>
                                c.UserId == authenticatedUserId &&
                                c.ProductId == guestItem.ProductId);

                        if (existingItem != null)
                        {
                            // Merge quantities (check stock limits)
                            var product = await _context.Products
                                .FindAsync(guestItem.ProductId);

                            if (product != null &&
                                existingItem.Quantity + guestItem.Quantity <= product.StockCount)
                            {
                                existingItem.Quantity += guestItem.Quantity;
                            }

                            _context.CartItems.Remove(guestItem);
                        }
                        else
                        {
                            // Transfer to authenticated user
                            guestItem.UserId = authenticatedUserId;
                        }
                    }

                    await _context.SaveChangesAsync();

                    // Clear guest session
                    HttpContext.Session.Remove("GuestSessionId");
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex) // This is the line with the warning
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

                await _context.SaveChangesAsync();
                TempData["Success"] = "Product added to cart successfully!";
                return RedirectToAction("Index", "Products");
            }
            catch
            {
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