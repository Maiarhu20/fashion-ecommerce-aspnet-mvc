namespace Core.DTOs.Products
{
    public class ProductResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal FinalPrice { get; set; }
        public int StockQuantity { get; set; }
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public decimal? DiscountPercent { get; set; }
        public DateTime CreatedDate { get; set; }
        public bool IsDeleted { get; set; }
        public int OrderCount { get; set; }
        public int ReviewCount { get; set; }

        // Multiple images
        public List<ProductImageDto> Images { get; set; } = new();

        // Colors with hex codes
        public List<ProductColorDto> Colors { get; set; } = new();
    }

    public class ProductImageDto
    {
        public int Id { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public bool IsPrimary { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class ProductColorDto
    {
        public int Id { get; set; }
        public string ColorName { get; set; } = string.Empty;
        public string ColorHexCode { get; set; } = string.Empty;
    }
}