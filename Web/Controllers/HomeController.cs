using Microsoft.AspNetCore.Mvc;
using Core.Services;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

namespace Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly HomeMediaService _homeMediaService;
        private readonly ProductService _productService;
        private readonly ReviewService _reviewService;

        public HomeController(
            HomeMediaService homeMediaService,
            ProductService productService,
            ReviewService reviewService)
        {
            _homeMediaService = homeMediaService;
            _productService = productService;
            _reviewService = reviewService;
        }

        public async Task<IActionResult> Index()
        {
            // Get carousel items
            var carouselItemsResult = await _homeMediaService.GetActiveCarouselItemsAsync();
            ViewBag.CarouselItems = carouselItemsResult.Succeeded ?
                carouselItemsResult.Data :
                new List<Core.DTOs.HomeMedia.HomeMediaListDto>();

            // Get last 4 new arrival products
            var productsResult = await _productService.GetActiveProductsAsync();
            if (productsResult.Succeeded)
            {
                var newArrivals = productsResult.Data
                    .OrderByDescending(p => p.CreatedDate)
                    .Take(4)
                    .ToList();
                ViewBag.NewArrivals = newArrivals;
            }
            else
            {
                ViewBag.NewArrivals = new List<Core.DTOs.Products.ProductListDto>();
            }

            // Get approved reviews for the carousel
            var reviewsResult = await _reviewService.GetAllReviewsAsync(Domain.Models.ReviewStatus.Approved);
            if (reviewsResult.Succeeded)
            {
                //var reviews = reviewsResult.Data.Take(6).ToList();
                //ViewBag.Reviews = reviews;
                var reviews = reviewsResult.Data
                    .Where(r => r.Rating >= 4) // Only show 4 and 5 star reviews
                    .OrderByDescending(r => r.CreatedDate) // Optional: show newest first
                    .Take(6) // Optional: limit to 6 reviews
                    .ToList();
                ViewBag.Reviews = reviews;
            }
            else
            {
                ViewBag.Reviews = new List<Core.DTOs.Reviews.ReviewResponseDto>();
            }

            return View();
        }


        public IActionResult About()
        {
            return View();
        }
    }
}