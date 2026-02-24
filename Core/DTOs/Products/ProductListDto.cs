namespace Core.DTOs.Products
{
    public class ProductListDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal FinalPrice { get; set; }
        public int StockQuantity { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public decimal? DiscountPercent { get; set; }
        public DateTime CreatedDate { get; set; }
        public bool IsDeleted { get; set; }
        public int OrderCount { get; set; }
        public int ReviewCount { get; set; }

        // Primary image for list display
        public string PrimaryImageUrl { get; set; } = string.Empty;

        // Colors with hex codes
        public List<ProductColorListDto> Colors { get; set; } = new();

        public List<ProductImageDto> Images { get; set; } = new List<ProductImageDto>();
    }

    public class ProductColorListDto
    {
        public string ColorName { get; set; } = string.Empty;
        public string ColorHexCode { get; set; } = string.Empty;
    }
}