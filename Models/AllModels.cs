using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ecomm.Models
{
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
        public Category? Category { get; set; }

        public bool IsOnSale { get; set; } = false;
    }

    public class Category
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        public List<Product> Products { get; set; } = new List<Product>();
    }

    public class CartItem
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public Product? Product { get; set; }
        public int Quantity { get; set; } = 1;

        [MaxLength(450)]
        public string UserId { get; set; } = string.Empty;
    }

    public class Order
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; }

        public DateTime OrderDate { get; set; } = DateTime.UtcNow;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [Required]
        public string Status { get; set; } = "Processing"; // Processing, Shipped, Completed, Cancelled

        public string PaymentMethod { get; set; }
        public string TransactionId { get; set; }

        // Shipping information (stored directly in Order for simplicity)
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string ZipCode { get; set; }

        // Navigation properties
        public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }

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
        public decimal Price { get; set; } // Price at time of order

        // Navigation properties
        public virtual Order Order { get; set; }
        public virtual Product Product { get; set; }
    }

    public class ErrorViewModel
    {
        public string? RequestId { get; set; }
        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }

    public class CheckoutViewModel
    {
        [Required(ErrorMessage = "Full name is required")]
        [Display(Name = "Full Name")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Phone number is required")]
        [Display(Name = "Phone Number")]
        [Phone(ErrorMessage = "Please enter a valid phone number")]
        public string Phone { get; set; }

        [Required(ErrorMessage = "Address is required")]
        [Display(Name = "Shipping Address")]
        public string Address { get; set; }

        [Required(ErrorMessage = "City is required")]
        public string City { get; set; }

        [Required(ErrorMessage = "ZIP code is required")]
        [Display(Name = "ZIP Code")]
        public string ZipCode { get; set; }

        [Required(ErrorMessage = "Mobile money provider is required")]
        [Display(Name = "Mobile Money Provider")]
        public string MobileMoneyProvider { get; set; }

        [Required(ErrorMessage = "Mobile money number is required")]
        [Display(Name = "Mobile Money Number")]
        [RegularExpression(@"^\d{10,15}$", ErrorMessage = "Please enter a valid mobile money number")]
        public string MobileMoneyNumber { get; set; }

        // Cart information
        public List<CartItem> CartItems { get; set; } = new List<CartItem>();

        public decimal TotalAmount { get; set; }

        // Terms agreement (for form submission)
        public bool AgreeTerms { get; set; }
    }
}