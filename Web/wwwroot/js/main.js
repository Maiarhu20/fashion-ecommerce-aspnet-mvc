(function ($) {
    "use strict";

    // Dropdown on mouse hover
    $(document).ready(function () {
        function toggleNavbarMethod() {
            if ($(window).width() > 992) {
                $('.navbar .dropdown').on('mouseover', function () {
                    $('.dropdown-toggle', this).trigger('click');
                }).on('mouseout', function () {
                    $('.dropdown-toggle', this).trigger('click').blur();
                });
            } else {
                $('.navbar .dropdown').off('mouseover').off('mouseout');
            }
        }
        toggleNavbarMethod();
        $(window).resize(toggleNavbarMethod);
    });


    // Back to top button
    $(window).scroll(function () {
        if ($(this).scrollTop() > 100) {
            $('.back-to-top').fadeIn('slow');
        } else {
            $('.back-to-top').fadeOut('slow');
        }
    });
    $('.back-to-top').click(function () {
        $('html, body').animate({ scrollTop: 0 }, 1500, 'easeInOutExpo');
        return false;
    });


    // Vendor carousel
    $('.vendor-carousel').owlCarousel({
        loop: true,
        margin: 29,
        nav: false,
        autoplay: true,
        smartSpeed: 1000,
        responsive: {
            0: {
                items: 2
            },
            576: {
                items: 3
            },
            768: {
                items: 4
            },
            992: {
                items: 5
            },
            1200: {
                items: 6
            }
        }
    });


    // Related carousel
    $('.related-carousel').owlCarousel({
        loop: true,
        margin: 29,
        nav: false,
        autoplay: true,
        smartSpeed: 1000,
        responsive: {
            0: {
                items: 1
            },
            576: {
                items: 2
            },
            768: {
                items: 3
            },
            992: {
                items: 4
            }
        }
    });


    // Product Quantity
    $('.quantity button').on('click', function () {
        var button = $(this);
        var oldValue = button.parent().parent().find('input').val();
        if (button.hasClass('btn-plus')) {
            var newVal = parseFloat(oldValue) + 1;
        } else {
            if (oldValue > 0) {
                var newVal = parseFloat(oldValue) - 1;
            } else {
                newVal = 0;
            }
        }
        button.parent().parent().find('input').val(newVal);
    });

})(jQuery);

// ADD THIS NEW FUNCTION OUTSIDE THE IIFE
// This will initialize Bootstrap carousels for product images
$(document).ready(function () {
    // Initialize all Bootstrap carousels
    initializeProductImageCarousels();

    // Re-initialize when new content is loaded (if using AJAX)
    $(document).ajaxComplete(function () {
        initializeProductImageCarousels();
    });
});

function initializeProductImageCarousels() {
    // Find all carousels that should auto-rotate (excluding Owl Carousels)
    $('.carousel:not(.owl-carousel):not([data-initialized="true"])').each(function () {
        var $carousel = $(this);

        // Mark as initialized to avoid duplicate initialization
        $carousel.attr('data-initialized', 'true');

        // Check if this is a product carousel (in product cards)
        var isProductCardCarousel = $carousel.closest('.product-item').length > 0;

        // Set different intervals for different carousel types
        var interval = isProductCardCarousel ? 3500 : 5000; // 3.5s for cards, 5s for details

        // Initialize Bootstrap carousel
        $carousel.carousel({
            interval: interval,
            wrap: true,
            pause: 'hover',
            keyboard: true
        });

        // Force it to start cycling
        $carousel.carousel('cycle');

        console.log('Initialized carousel with interval:', interval, 'ms');
    });
}

// Optional: Add this if you want manual control for debugging
function debugCarousels() {
    console.log('Total carousels found:', $('.carousel').length);
    console.log('Bootstrap carousels:', $('.carousel:not(.owl-carousel)').length);
    console.log('Owl carousels:', $('.owl-carousel').length);

    $('.carousel:not(.owl-carousel)').each(function (i) {
        var $carousel = $(this);
        var id = $carousel.attr('id') || 'carousel-' + i;
        var isCycling = $carousel.data('bs.carousel') ? $carousel.data('bs.carousel')._isSliding : 'Not initialized';
        console.log('Carousel', id, '- Status:', isCycling);
    });
}

// You can call debugCarousels() in browser console to check carousel status