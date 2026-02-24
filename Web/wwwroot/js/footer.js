// wwwroot/js/footer.js
document.addEventListener('DOMContentLoaded', function () {
    // Newsletter form submission
    var newsletterForm = document.getElementById('newsletter-form');
    if (newsletterForm) {
        newsletterForm.addEventListener('submit', function (e) {
            e.preventDefault();
            var emailInput = this.querySelector('input[type="email"]');
            var email = emailInput.value.trim();

            if (!email) {
                showToast('Please enter your email address', 'warning');
                return;
            }

            if (!isValidEmail(email)) {
                showToast('Please enter a valid email address', 'warning');
                return;
            }

            // Simulate submission (replace with actual API call)
            showToast('Thank you for subscribing to our newsletter!', 'success');
            emailInput.value = '';
        });
    }

    function isValidEmail(email) {
        var re = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
        return re.test(email);
    }

    function showToast(message, type) {
        // Remove existing toast if any
        var existingToast = document.querySelector('.toast-notification');
        if (existingToast) {
            existingToast.remove();
        }

        // Create toast
        var toast = document.createElement('div');
        toast.className = 'toast-notification alert alert-' + (type === 'success' ? 'success' : 'warning') + ' alert-dismissible fade show position-fixed';
        toast.style.cssText = 'bottom: 20px; right: 20px; z-index: 9999; min-width: 300px;';
        toast.innerHTML = '<button type="button" class="close" data-dismiss="alert">&times;</button><strong>' + message + '</strong>';

        document.body.appendChild(toast);

        // Auto remove after 5 seconds
        setTimeout(function () {
            if (toast.parentNode) {
                toast.remove();
            }
        }, 5000);

        // Add Bootstrap dismiss functionality
        var closeBtn = toast.querySelector('.close');
        if (closeBtn) {
            closeBtn.addEventListener('click', function () {
                toast.remove();
            });
        }
    }
});