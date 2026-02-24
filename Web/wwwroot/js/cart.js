// Cart functionality with pink theme
$(document).ready(function () {
    // Initialize cart
    initCart();

    // Load mini cart on dropdown show
    $('#cartDropdown').on('show.bs.dropdown', function () {
        loadMiniCart();
    });

    // Update cart count every 30 seconds
    setInterval(updateCartCount, 30000);

    // Add pulse animation when cart updates
    $(document).on('cartUpdated', function () {
        $('#cartBadge').addClass('cart-badge-updated');
        setTimeout(() => {
            $('#cartBadge').removeClass('cart-badge-updated');
        }, 300);
    });
});

function initCart() {
    updateCartCount();

    // Setup click handlers for add-to-cart buttons
    $(document).on('click', '.quick-add-to-cart, .btn-add-to-cart', function (e) {
        e.preventDefault();
        const $btn = $(this);
        const productId = $btn.data('product-id');
        const defaultQuantity = $btn.data('quantity') || 1;
        const color = $btn.data('color') || null;
        let quantity = defaultQuantity;

        // If button has quantity input nearby
        const $quantityInput = $btn.closest('.product-card').find('.quantity-input');
        if ($quantityInput.length) {
            quantity = parseInt($quantityInput.val()) || 1;
        }

        // If button has color select nearby
        const $colorSelect = $btn.closest('.product-card').find('.color-select');
        if ($colorSelect.length && $colorSelect.val()) {
            color = $colorSelect.val();
        }

        addToCart(productId, quantity, color);
    });
}

function updateCartCount() {
    $.get('/cart/cart-count')
        .done(function (count) {
            const badgeCount = parseInt(count) || 0;
            const $badge = $('#cartBadge');

            // Update badge
            $badge.text(badgeCount).toggle(badgeCount > 0);

            // Update all cart badges
            $('.cart-badge').text(badgeCount).toggle(badgeCount > 0);

            // Trigger update event
            if (badgeCount > 0) {
                $(document).trigger('cartUpdated');
            }
        })
        .fail(function (xhr) {
            console.error('Failed to update cart count:', xhr.statusText);
            $('#cartBadge').hide();
        });
}

function loadMiniCart() {
    $.get('/cart/mini')
        .done(function (html) {
            $('#miniCartContent').html(html);

            // Add event handlers for mini cart buttons
            $('#miniCartContent').find('.btn-remove-item').off('click').on('click', function (e) {
                e.preventDefault();
                const cartItemId = $(this).data('item-id');
                removeFromMiniCart(cartItemId);
            });
        })
        .fail(function (xhr) {
            console.error('Failed to load mini cart:', xhr.statusText);
            $('#miniCartContent').html(`
                <div class="text-center py-4">
                    <div class="empty-cart">
                        <i class="fas fa-exclamation-circle text-pink empty-cart-icon"></i>
                        <p class="text-muted">Unable to load cart</p>
                        <button class="btn btn-outline-pink btn-sm mt-2" onclick="loadMiniCart()">
                            <i class="fas fa-redo mr-1"></i> Retry
                        </button>
                    </div>
                </div>
            `);
        });
}

function getAntiForgeryToken() {
    // Try to get token from hidden input
    var token = $('input[name="__RequestVerificationToken"]').val();

    // If not found, try from meta tag
    if (!token) {
        token = $('meta[name="__RequestVerificationToken"]').attr('content');
    }

    // If still not found, try from cookie
    if (!token) {
        var cookieValue = document.cookie
            .split('; ')
            .find(row => row.startsWith('__RequestVerificationToken='))
            ?.split('=')[1];
        if (cookieValue) {
            token = decodeURIComponent(cookieValue);
        }
    }

    return token;
}

function addToCart(productId, quantity, color) {
    console.log('Adding to cart:', { productId, quantity, color });

    // Show loading on badge
    const $badge = $('#cartBadge');
    const originalText = $badge.text();
    $badge.html('<i class="fas fa-spinner fa-spin"></i>');

    // Get anti-forgery token
    const token = getAntiForgeryToken();
    console.log('Anti-forgery token available:', !!token);

    // Create form data
    const formData = new FormData();
    formData.append('ProductId', productId);
    formData.append('Quantity', quantity);
    if (color) formData.append('SelectedColor', color);

    // Add anti-forgery token if available
    if (token) {
        formData.append('__RequestVerificationToken', token);
    } else {
        console.warn('Anti-forgery token not found!');
    }

    // Log form data for debugging
    console.log('FormData contents:');
    for (let pair of formData.entries()) {
        console.log(pair[0] + ': ' + pair[1]);
    }

    $.ajax({
        url: '/cart/add-to-cart',
        type: 'POST',
        data: formData,
        processData: false,
        contentType: false,
        headers: {
            'X-Requested-With': 'XMLHttpRequest'
        },
        beforeSend: function (xhr) {
            console.log('Sending request to /cart/add-to-cart');
        },
        success: function (response) {
            console.log('Cart response:', response);

            // Handle both JSON and HTML responses
            if (typeof response === 'object' && response !== null) {
                // JSON response
                if (response.success) {
                    // Update cart count
                    updateCartCount();

                    // Show pink success notification
                    showPinkNotification(response.message || '✓ Item added to cart!', 'success');

                    // Reload mini cart if dropdown is open
                    if ($('#cartDropdown').hasClass('show')) {
                        loadMiniCart();
                    }

                    // If on cart page, reload it
                    if (window.location.pathname === '/cart') {
                        setTimeout(() => window.location.reload(), 500);
                    }
                } else {
                    showPinkNotification(response.message || 'Failed to add to cart', 'error');
                }
            } else {
                // HTML response (redirect or other)
                console.log('Received HTML response');
                updateCartCount();
                showPinkNotification('Item added to cart!', 'success');
            }
        },
        error: function (xhr, status, error) {
            console.error('Cart error:', {
                status: xhr.status,
                statusText: xhr.statusText,
                responseText: xhr.responseText,
                error: error
            });

            let errorMessage = 'Error adding item to cart';
            try {
                // Try to parse JSON error response
                const errorResponse = JSON.parse(xhr.responseText);
                if (errorResponse && errorResponse.message) {
                    errorMessage = errorResponse.message;
                } else if (errorResponse && errorResponse.error) {
                    errorMessage = errorResponse.error;
                }
            } catch (e) {
                // If not JSON, check status
                if (xhr.status === 401) {
                    errorMessage = 'Please login to add items to cart';
                } else if (xhr.status === 400) {
                    errorMessage = 'Invalid request. Please check your input.';
                } else if (xhr.status === 404) {
                    errorMessage = 'Product not found';
                } else if (xhr.status === 500) {
                    errorMessage = 'Server error. Please try again later.';
                }
            }

            showPinkNotification(errorMessage, 'error');
        },
        complete: function () {
            $badge.text(originalText);
        }
    });
}

function removeFromMiniCart(cartItemId) {
    if (!confirm('Remove this item from cart?')) return;

    const formData = new FormData();
    formData.append('cartItemId', cartItemId);

    // Get anti-forgery token
    const token = getAntiForgeryToken();
    if (token) {
        formData.append('__RequestVerificationToken', token);
    }

    $.ajax({
        url: '/cart/remove-item',
        type: 'POST',
        data: formData,
        processData: false,
        contentType: false,
        headers: {
            'X-Requested-With': 'XMLHttpRequest'
        },
        success: function (response) {
            updateCartCount();
            loadMiniCart();
            showPinkNotification('Item removed from cart', 'success');
        },
        error: function (xhr) {
            console.error('Remove item error:', xhr.responseText);
            showPinkNotification('Error removing item', 'error');
        }
    });
}

function showPinkNotification(message, type = 'success') {
    // Remove existing notifications
    $('.pink-notification').remove();

    // Icon based on type
    const icon = type === 'success' ? 'fa-check-circle' : 'fa-exclamation-circle';
    const bgClass = type === 'success' ? 'pink-success' : 'pink-error';

    // Create notification
    const notification = $(`
        <div class="pink-notification alert ${bgClass} alert-dismissible fade show" 
             style="position: fixed; top: 80px; right: 20px; z-index: 9999; min-width: 300px;">
            <div class="d-flex align-items-center">
                <i class="fas ${icon} mr-2" style="font-size: 1.2rem;"></i>
                <span>${message}</span>
                <button type="button" class="close ml-auto" data-dismiss="alert">
                    <span>&times;</span>
                </button>
            </div>
        </div>
    `);

    $('body').append(notification);

    // Auto remove after 3 seconds
    setTimeout(function () {
        notification.alert('close');
    }, 3000);
}

// Add function to update quantity in cart (for cart page)
function updateCartQuantity(cartItemId, quantity) {
    const formData = new FormData();
    formData.append('CartItemId', cartItemId);
    formData.append('Quantity', quantity);

    // Get anti-forgery token
    const token = getAntiForgeryToken();
    if (token) {
        formData.append('__RequestVerificationToken', token);
    }

    $.ajax({
        url: '/cart/update-quantity',
        type: 'POST',
        data: formData,
        processData: false,
        contentType: false,
        headers: {
            'X-Requested-With': 'XMLHttpRequest'
        },
        success: function (response) {
            if (response && response.success) {
                showPinkNotification(response.message || 'Cart updated successfully!', 'success');
                updateCartCount();

                // Reload page if on cart page
                if (window.location.pathname === '/cart') {
                    setTimeout(() => window.location.reload(), 500);
                }
            } else {
                showPinkNotification('Failed to update cart', 'error');
            }
        },
        error: function (xhr) {
            console.error('Update quantity error:', xhr.responseText);
            showPinkNotification('Error updating cart quantity', 'error');
        }
    });
}

// Add function to clear cart
function clearCart() {
    if (!confirm('Are you sure you want to clear your cart?')) return;

    const formData = new FormData();

    // Get anti-forgery token
    const token = getAntiForgeryToken();
    if (token) {
        formData.append('__RequestVerificationToken', token);
    }

    $.ajax({
        url: '/cart/clear-cart',
        type: 'POST',
        data: formData,
        processData: false,
        contentType: false,
        headers: {
            'X-Requested-With': 'XMLHttpRequest'
        },
        success: function (response) {
            updateCartCount();
            loadMiniCart();
            showPinkNotification('Cart cleared successfully!', 'success');

            // Reload page if on cart page
            if (window.location.pathname === '/cart') {
                setTimeout(() => window.location.reload(), 500);
            }
        },
        error: function (xhr) {
            console.error('Clear cart error:', xhr.responseText);
            showPinkNotification('Error clearing cart', 'error');
        }
    });
}

// Add CSS classes for notifications on page load
$(document).ready(function () {
    // Add styles if not already present
    if (!$('#cart-styles').length) {
        $('head').append(`
            <style id="cart-styles">
                .pink-success {
                    background-color: #f8e6ed !important;
                    color: #912356 !important;
                    border: 1px solid #912356 !important;
                    border-left: 4px solid #912356 !important;
                }
                
                .pink-error {
                    background-color: #f8e6ed !important;
                    color: #dc3545 !important;
                    border: 1px solid #dc3545 !important;
                    border-left: 4px solid #dc3545 !important;
                }
                
                /* Loading spinner */
                .fa-spinner {
                    animation: fa-spin 1s infinite linear;
                }
                
                @keyframes fa-spin {
                    0% { transform: rotate(0deg); }
                    100% { transform: rotate(360deg); }
                }
                
                /* Cart badge animation */
                .cart-badge-updated {
                    animation: badgePulse 0.3s ease-in-out;
                }
                
                @keyframes badgePulse {
                    0% { transform: scale(1); }
                    50% { transform: scale(1.2); }
                    100% { transform: scale(1); }
                }
            </style>
        `);
    }

    // Setup event handlers for cart page buttons
    $(document).on('submit', 'form[action*="/cart/update-quantity"]', function (e) {
        e.preventDefault();
        const $form = $(this);
        const cartItemId = $form.find('input[name="CartItemId"]').val();
        const quantity = $form.find('button[name="Quantity"]').val();
        updateCartQuantity(cartItemId, quantity);
    });

    $(document).on('submit', 'form[action*="/cart/remove-item"]', function (e) {
        e.preventDefault();
        const $form = $(this);
        const cartItemId = $form.find('input[name="cartItemId"]').val();
        removeFromMiniCart(cartItemId);
    });

    $(document).on('submit', 'form[action*="/cart/clear-cart"]', function (e) {
        e.preventDefault();
        clearCart();
    });
});