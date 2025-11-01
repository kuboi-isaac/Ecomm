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
            return User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException("User not authenticated");
        }

        // GET: Cart
        public async Task<IActionResult> Index()
        {
            try
            {
                var userId = GetUserId();
                var cartItems = await _context.CartItems
                    .Where(c => c.UserId == userId)
                    .Include(c => c.Product!)
                    .ThenInclude(p => p!.Category)
                    .ToListAsync();

                var viewModel = new CartViewModel
                {
                    CartItems = cartItems,
                    TotalAmount = cartItems.Sum(item => item.Product?.Price * item.Quantity ?? 0)
                };

                return View(viewModel);
            }
            catch (UnauthorizedAccessException)
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }
        }

        // POST: Cart/AddToCart (AJAX version)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> AddToCart([FromBody] AddToCartRequest request)
        {
            try
            {
                var userId = GetUserId();

                if (request.Quantity <= 0)
                {
                    return Json(new { success = false, message = "Quantity must be greater than 0" });
                }

                var product = await _context.Products.FindAsync(request.ProductId);
                if (product == null)
                {
                    return Json(new { success = false, message = "Product not found" });
                }

                if (product.StockCount < request.Quantity)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Only {product.StockCount} items available in stock."
                    });
                }

                var existingCartItem = await _context.CartItems
                    .FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == request.ProductId);

                if (existingCartItem != null)
                {
                    // Check if total quantity exceeds stock
                    if (existingCartItem.Quantity + request.Quantity > product.StockCount)
                    {
                        return Json(new
                        {
                            success = false,
                            message = $"Cannot add more items. Only {product.StockCount} available in stock."
                        });
                    }
                    existingCartItem.Quantity += request.Quantity;
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
                    .SumAsync(c => c.Quantity);

                return Json(new
                {
                    success = true,
                    message = "Product added to cart successfully!",
                    cartCount = cartCount
                });
            }
            catch (UnauthorizedAccessException)
            {
                return Json(new { success = false, message = "Please log in to add items to cart" });
            }
            catch (Exception)
            {
                // Log the exception (remove 'ex' parameter since it's not used)
                return Json(new { success = false, message = "Error adding product to cart" });
            }
        }

        // POST: Cart/AddToCartRedirect (Traditional form post)
        [HttpPost]
        [ValidateAntiForgeryToken]
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
            catch (UnauthorizedAccessException)
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }
        }

        // POST: Cart/UpdateQuantity
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> UpdateQuantity([FromBody] UpdateQuantityRequest request)
        {
            try
            {
                var userId = GetUserId();

                if (request.Quantity <= 0)
                {
                    return Json(new { success = false, message = "Quantity must be greater than 0" });
                }

                var cartItem = await _context.CartItems
                    .Include(ci => ci.Product)
                    .FirstOrDefaultAsync(ci => ci.Id == request.CartItemId && ci.UserId == userId);

                if (cartItem == null)
                {
                    return Json(new { success = false, message = "Item not found" });
                }

                // Validate quantity doesn't exceed stock
                if (cartItem.Product == null)
                {
                    return Json(new { success = false, message = "Product information not found" });
                }

                if (request.Quantity > cartItem.Product.StockCount)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Only {cartItem.Product.StockCount} items available in stock."
                    });
                }

                cartItem.Quantity = request.Quantity;
                await _context.SaveChangesAsync();

                // Calculate updated totals
                var cartItems = await _context.CartItems
                    .Include(ci => ci.Product)
                    .Where(ci => ci.UserId == userId)
                    .ToListAsync();

                var totalAmount = cartItems.Sum(ci => (ci.Product?.Price ?? 0) * ci.Quantity);
                var cartCount = cartItems.Sum(ci => ci.Quantity);
                var itemTotal = (cartItem.Product?.Price ?? 0) * request.Quantity;

                return Json(new
                {
                    success = true,
                    itemTotal = itemTotal.ToString("C2"),
                    totalAmount = totalAmount.ToString("C2"),
                    cartCount = cartCount
                });
            }
            catch (UnauthorizedAccessException)
            {
                return Json(new { success = false, message = "Please log in to update cart" });
            }
            catch (Exception)
            {
                return Json(new { success = false, message = "Error updating quantity" });
            }
        }

        // POST: Cart/RemoveFromCart
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> RemoveFromCart([FromBody] RemoveFromCartRequest request)
        {
            try
            {
                var userId = GetUserId();
                var cartItem = await _context.CartItems
                    .FirstOrDefaultAsync(ci => ci.Id == request.CartItemId && ci.UserId == userId);

                if (cartItem != null)
                {
                    _context.CartItems.Remove(cartItem);
                    await _context.SaveChangesAsync();

                    // Get updated cart count
                    var cartCount = await _context.CartItems
                        .Where(c => c.UserId == userId)
                        .SumAsync(c => c.Quantity);

                    return Json(new
                    {
                        success = true,
                        message = "Item removed from cart",
                        cartCount = cartCount
                    });
                }

                return Json(new { success = false, message = "Item not found" });
            }
            catch (UnauthorizedAccessException)
            {
                return Json(new { success = false, message = "Please log in to modify cart" });
            }
            catch (Exception)
            {
                return Json(new { success = false, message = "Error removing item" });
            }
        }

        // GET: Cart/GetCartCount
        [HttpGet]
        public async Task<JsonResult> GetCartCount()
        {
            try
            {
                var userId = GetUserId();
                var count = await _context.CartItems
                    .Where(c => c.UserId == userId)
                    .SumAsync(c => c.Quantity);

                return Json(new { count = count });
            }
            catch (UnauthorizedAccessException)
            {
                return Json(new { count = 0 });
            }
        }

        // POST: Cart/ClearCart
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearCart()
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
                else
                {
                    TempData["Info"] = "Cart is already empty.";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (UnauthorizedAccessException)
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }
        }
    }

    // Request models for better type safety
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
    }
}