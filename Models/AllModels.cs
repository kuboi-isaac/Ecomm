using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ecomm.Models
{
    // ----------------------------
    // Product
    // ----------------------------
    public class Product
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        public int StockCount { get; set; }
        public int LowStockThreshold { get; set; } = 5;

        [MaxLength(500)]
        public string ImageUrl { get; set; } = "/images/products/default.png";

        public int CategoryId { get; set; }
        public string? WhatsAppPhone { get; set; }


        // Nullable because a product may not have a category loaded yet
        public Category Category { get; set; } = null!;

        public bool IsOnSale { get; set; } = false;
    }

    // ----------------------------
    // Category
    // ----------------------------
    public class Category
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        public List<Product> Products { get; set; } = new();
    }

    // ----------------------------
    // CartItem
    // ----------------------------
    public class CartItem
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public int ProductId { get; set; }
        public Product? Product { get; set; }
        public int Quantity { get; set; } = 1;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        // Add this property
        public bool IsGuestCart { get; set; } = false;
    }

    // ----------------------------
    // Order
    // ----------------------------
    public class Order
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        public DateTime OrderDate { get; set; } = DateTime.UtcNow;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [Required]
        public string Status { get; set; } = "Processing";  // Default state

        public string PaymentMethod { get; set; } = string.Empty;
        public string TransactionId { get; set; } = string.Empty;

        // Shipping information
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string ZipCode { get; set; } = string.Empty;

        // Navigation
        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }

    // ----------------------------
    // OrderItem
    // ----------------------------
    public class OrderItem
    {
        public int Id { get; set; }

        [Required]
        public int OrderId { get; set; }

        [Required]
        public int ProductId { get; set; }

        [Required]
        public int Quantity { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        public Order Order { get; set; } = null!;
        public Product Product { get; set; } = null!;
    }

    // ----------------------------
    // ErrorViewModel
    // ----------------------------
    public class ErrorViewModel
    {
        public string? RequestId { get; set; }

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }

    // ----------------------------
    // Checkout ViewModel
    // ----------------------------
    public class CheckoutViewModel
    {
        [Required(ErrorMessage = "Full name is required")]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone number is required")]
        [Display(Name = "Phone Number")]
        [Phone(ErrorMessage = "Please enter a valid phone number")]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Address is required")]
        [Display(Name = "Shipping Address")]
        public string Address { get; set; } = string.Empty;

        [Required(ErrorMessage = "City is required")]
        public string City { get; set; } = string.Empty;

        [Required(ErrorMessage = "ZIP code is required")]
        [Display(Name = "ZIP Code")]
        public string ZipCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mobile money provider is required")]
        [Display(Name = "Mobile Money Provider")]
        public string MobileMoneyProvider { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mobile money number is required")]
        [Display(Name = "Mobile Money Number")]
        [RegularExpression(@"^\d{10,15}$", ErrorMessage = "Please enter a valid mobile money number")]
        public string MobileMoneyNumber { get; set; } = string.Empty;

        public List<CartItem> CartItems { get; set; } = new();

        public decimal TotalAmount { get; set; }

        public bool AgreeTerms { get; set; }
    }
}
