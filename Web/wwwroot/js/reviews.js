$(document).ready(function () {
    const ratingDescriptions = {
        1: 'Terrible - Not satisfied',
        2: 'Poor - Below expectations',
        3: 'Average - Not bad but could be better',
        4: 'Good - Very satisfied',
        5: 'Excellent - Highly recommend!'
    };

    // Star rating hover preview
    $('.star-label-select').on('mouseenter', function () {
        const value = $(this).data('value');
        const isSelected = $('input[name="Rating"]:checked').val();

        if (!isSelected) {
            highlightStars(value, false);
            updateRatingDisplay(value);
        }
    });

    // Reset preview on mouse out
    $('.rating-stars-selector').on('mouseleave', function () {
        const isSelected = $('input[name="Rating"]:checked').val();

        if (isSelected) {
            highlightStars(isSelected, true);
            updateRatingDisplay(isSelected);
        } else {
            resetStars();
            $('#rating-text').text('Click stars to rate (1-5 stars)');
            $('.selected-rating').hide();
        }
    });

    // Star rating click
    $('.star-label-select').on('click', function (e) {
        e.preventDefault();
        const value = $(this).data('value');

        // Set radio button
        $(`#star-${value}`).prop('checked', true);

        // Highlight permanently
        highlightStars(value, true);
        updateRatingDisplay(value);
    });

    // Character counter
    $('#review-comment').on('input', function () {
        const length = $(this).val().length;
        $('#char-count').text(length);

        if (length > 1000) {
            $(this).val($(this).val().substring(0, 1000));
            $('#char-count').text('1000');
        }
    });

    // Form submission
    $('#review-form').on('submit', function (e) {
        e.preventDefault();

        const formData = {
            ProductId: $('#product-id').val(),
            GuestName: $('#guest-name').val().trim(),
            GuestEmail: $('#guest-email').val().trim(),
            Rating: $('input[name="Rating"]:checked').val(),
            Title: $('#review-title').val().trim(),
            Comment: $('#review-comment').val().trim()
        };

        // Validation
        const errorMessages = [];

        if (!formData.GuestName) {
            errorMessages.push('Please enter your name');
        }
        if (!formData.GuestEmail) {
            errorMessages.push('Please enter your email');
        }
        if (!isValidEmail(formData.GuestEmail) && formData.GuestEmail) {
            errorMessages.push('Please enter a valid email address');
        }
        if (!formData.Rating) {
            errorMessages.push('Please select a rating by clicking the stars');
        }
        if (!formData.Comment) {
            errorMessages.push('Please write your review');
        }
        if (formData.Comment.length < 10) {
            errorMessages.push('Your review must be at least 10 characters');
        }

        if (errorMessages.length > 0) {
            showToast(errorMessages.join('<br>'), 'warning');
            return;
        }

        submitReview(formData);
    });

    // Helper functions
    function highlightStars(value, permanent = false) {
        $('.star-label-select').each(function () {
            const starValue = parseInt($(this).data('value'));
            const $icon = $(this).find('i');

            if (starValue <= value) {
                $icon.css('color', '#ffc107');
                if (!permanent) {
                    $icon.css('transform', 'scale(1.1)');
                } else {
                    $icon.css('transform', 'scale(1)');
                }
            } else {
                $icon.css('color', '#ddd');
                $icon.css('transform', 'scale(1)');
            }
        });
    }

    function resetStars() {
        $('.star-label-select i').css({
            'color': '#ddd',
            'transform': 'scale(1)'
        });
    }

    function updateRatingDisplay(value) {
        $('#selected-rating-value').text(value);
        $('#rating-description').text(ratingDescriptions[value]);
        $('#rating-text').hide();
        $('.selected-rating').show();
    }

    function isValidEmail(email) {
        const re = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
        return re.test(email);
    }

    function submitReview(formData) {
        const $btn = $('#submit-review-btn');
        const originalText = $btn.html();

        $btn.prop('disabled', true).html('<i class="fa fa-spinner fa-spin mr-2"></i> Submitting...');

        const token = $('input[name="__RequestVerificationToken"]').val();

        $.ajax({
            url: '/reviews/create',
            type: 'POST',
            data: JSON.stringify(formData),
            contentType: 'application/json',
            headers: {
                'RequestVerificationToken': token,
                'Accept': 'application/json'
            },
            success: function (response) {
                if (response.success) {
                    showReviewSuccessModal();
                    resetForm();

                    // Reload reviews after 3 seconds
                    setTimeout(function () {
                        location.reload();
                    }, 3000);
                } else {
                    showToast(response.message || 'Failed to submit review. Please try again.', 'error');
                    $btn.prop('disabled', false).html(originalText);
                }
            },
            error: function (xhr) {
                console.error('Review submission error:', xhr);
                let message = 'An error occurred while submitting your review. Please try again.';

                if (xhr.responseJSON && xhr.responseJSON.message) {
                    message = xhr.responseJSON.message;
                } else if (xhr.status === 400) {
                    message = 'Invalid form data. Please check your input.';
                } else if (xhr.status === 500) {
                    message = 'Server error. Please try again later.';
                }

                showToast(message, 'error');
                $btn.prop('disabled', false).html(originalText);
            }
        });
    }

    function resetForm() {
        $('#review-form')[0].reset();
        resetStars();
        $('#rating-text').text('Click stars to rate (1-5 stars)').show();
        $('.selected-rating').hide();
        $('#char-count').text('0');
        $('input[name="Rating"]').prop('checked', false);
    }
});

// Show success modal function
function showReviewSuccessModal() {
    $('#review-success-modal').remove();

    const modalHtml = `
        <div class="modal fade" id="review-success-modal" tabindex="-1" role="dialog" aria-hidden="true">
            <div class="modal-dialog modal-dialog-centered" role="document">
                <div class="modal-content">
                    <div class="modal-header bg-success text-white">
                        <h5 class="modal-title">
                            <i class="fa fa-check-circle mr-2"></i>Review Submitted Successfully!
                        </h5>
                        <button type="button" class="close text-white" data-dismiss="modal" aria-label="Close">
                            <span aria-hidden="true">&times;</span>
                        </button>
                    </div>
                    <div class="modal-body text-center">
                        <div class="mb-4">
                            <i class="fa fa-check-circle fa-4x text-success mb-3"></i>
                            <h4 class="text-success">Thank You for Your Review!</h4>
                        </div>
                        <div class="alert alert-info text-left">
                            <h6><i class="fa fa-info-circle mr-2"></i>What happens next?</h6>
                            <ul class="mb-0 pl-3">
                                <li>Your review has been submitted for approval</li>
                                <li>Our team will review it within 24-48 hours</li>
                                <li>You'll receive an email when it's approved</li>
                                <li>Once approved, your review will appear here</li>
                            </ul>
                        </div>
                        <p class="text-muted mb-0">
                            <small>We appreciate your feedback!</small>
                        </p>
                    </div>
                    <div class="modal-footer">
                        <button type="button" class="btn btn-secondary" data-dismiss="modal">Close</button>
                        <button type="button" class="btn btn-primary" onclick="location.reload();">
                            <i class="fa fa-refresh mr-2"></i>Refresh Page
                        </button>
                    </div>
                </div>
            </div>
        </div>
    `;

    $('body').append(modalHtml);
    $('#review-success-modal').modal('show');

    // Auto-close after 5 seconds
    setTimeout(function () {
        if ($('#review-success-modal').length) {
            $('#review-success-modal').modal('hide');
        }
    }, 5000);
}

// Toast notification function
function showToast(message, type) {
    $('.toast-alert').remove();

    const alertClass = `alert-${type === 'error' ? 'danger' : type}`;
    const iconClass = type === 'error' ? 'fa-exclamation-circle' :
        type === 'warning' ? 'fa-exclamation-triangle' :
            'fa-check-circle';

    const toast = $(`
        <div class="toast-alert alert ${alertClass} alert-dismissible fade show position-fixed" 
             style="top: 20px; right: 20px; z-index: 9999; min-width: 300px;">
            <button type="button" class="close" data-dismiss="alert" aria-label="Close">
                <span aria-hidden="true">&times;</span>
            </button>
            <i class="fa ${iconClass} mr-2"></i>
            <strong>${type.charAt(0).toUpperCase() + type.slice(1)}!</strong>
            <div>${message}</div>
        </div>
    `);

    $('body').append(toast);

    // Auto remove after 5 seconds
    setTimeout(function () {
        toast.fadeOut(300, function () {
            $(this).remove();
        });
    }, 5000);
}