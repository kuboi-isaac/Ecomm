using System.Globalization;

namespace Ecomm.Helpers
{
    public static class CurrencyHelper
    {
        private const decimal EXCHANGE_RATE = 3800; // Example: 1 USD = 3800 UGX

        public static string FormatUGX(decimal amountInDollars)
        {
            // Convert from dollars to Uganda Shillings
            decimal amountInShillings = amountInDollars * EXCHANGE_RATE;

            // Format with Uganda Shillings symbol
            return $"Ushs {amountInShillings:N0}";
        }

        public static string FormatUGXWithCommas(decimal amountInDollars)
        {
            decimal amountInShillings = amountInDollars * EXCHANGE_RATE;
            return $"Ushs {amountInShillings:#,##0}";
        }

        // If you want to show both currencies for comparison
        public static string FormatBothCurrencies(decimal amountInDollars)
        {
            decimal amountInShillings = amountInDollars * EXCHANGE_RATE;
            return $"{amountInDollars:C} (Ushs {amountInShillings:N0})";
        }
    }
}