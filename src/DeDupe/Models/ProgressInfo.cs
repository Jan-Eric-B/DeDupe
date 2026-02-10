namespace DeDupe.Models
{
    public record ProgressInfo(int CurrentItem, int TotalItems, string OperationName, string? CurrentItemName = null)
    {
        /// <summary>
        /// Progress percentage (0 to 100).
        /// </summary>
        public double Percentage => TotalItems > 0 ? (double)CurrentItem / TotalItems * 100 : 0;

        /// <summary>
        /// Progress value (0.0 to 1.0).
        /// </summary>
        public double NormalizedValue => TotalItems > 0 ? (double)CurrentItem / TotalItems : 0;

        public bool IsComplete => CurrentItem >= TotalItems;

        /// <summary>
        /// Status string (Processing 252/1500 (16.8%))
        /// </summary>
        public string StatusText => TotalItems > 0 ? $"{OperationName} {CurrentItem:N0}/{TotalItems:N0} ({Percentage:F1}%)" : OperationName;
    }
}