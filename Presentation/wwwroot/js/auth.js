// Authentication Pages JavaScript Enhancements

// Password Strength Checker
function checkPasswordStrength(password) {
    if (!password) return { strength: 0, text: 'Not Set', color: 'gray' };
    
    let strength = 0;
    
    // Length checks
    if (password.length >= 8) strength++;
    if (password.length >= 12) strength++;
    
    // Character variety checks
    if (/[a-z]/.test(password) && /[A-Z]/.test(password)) strength++;
    if (/\d/.test(password)) strength++;
    if (/[^a-zA-Z\d]/.test(password)) strength++;
    
    // Cap at 4
    strength = Math.min(strength, 4);
    
    const levels = [
        { text: 'Weak', color: 'red' },
        { text: 'Fair', color: 'orange' },
        { text: 'Good', color: 'yellow' },
        { text: 'Strong', color: 'green' }
    ];
    
    return strength > 0 ? levels[strength - 1] : { strength: 0, text: 'Not Set', color: 'gray' };
}

// Toggle Password Visibility
function togglePasswordVisibility(inputId, iconId) {
    const input = document.getElementById(inputId);
    const icon = document.getElementById(iconId);
    
    if (!input || !icon) return;
    
    if (input.type === 'password') {
        input.type = 'text';
        icon.classList.remove('fa-eye');
        icon.classList.add('fa-eye-slash');
    } else {
        input.type = 'password';
        icon.classList.remove('fa-eye-slash');
        icon.classList.add('fa-eye');
    }
}

// Enhanced Form Validation
function initFormValidation() {
    const forms = document.querySelectorAll('form[data-validate="true"]');
    
    forms.forEach(form => {
        form.addEventListener('submit', function(e) {
            const requiredFields = form.querySelectorAll('[required]');
            let isValid = true;
            
            requiredFields.forEach(field => {
                if (!field.value.trim()) {
                    isValid = false;
                    field.classList.add('border-red-500');
                    field.classList.remove('border-gray-200');
                } else {
                    field.classList.remove('border-red-500');
                    field.classList.add('border-gray-200');
                }
            });
            
            if (!isValid) {
                e.preventDefault();
                showNotification('Please fill in all required fields', 'error');
            }
        });
    });
}

// Show Toast Notification
function showNotification(message, type = 'info', duration = 4000) {
    const container = document.getElementById('toastContainer') || createToastContainer();
    
    const toast = document.createElement('div');
    const typeClasses = {
        'success': 'bg-green-600',
        'error': 'bg-red-600',
        'warning': 'bg-yellow-600',
        'info': 'bg-blue-600'
    };
    
    const icons = {
        'success': 'fa-check-circle',
        'error': 'fa-exclamation-circle',
        'warning': 'fa-exclamation-triangle',
        'info': 'fa-info-circle'
    };
    
    toast.className = `flex items-center gap-3 px-6 py-4 rounded-lg shadow-lg text-white transform transition-all duration-300 ${typeClasses[type] || typeClasses.info} translate-x-full`;
    toast.innerHTML = `
        <i class="fas ${icons[type] || icons.info} text-xl"></i>
        <span class="flex-1">${message}</span>
        <button onclick="this.parentElement.remove()" class="text-white hover:text-gray-200 transition">
            <i class="fas fa-times"></i>
        </button>
    `;
    
    container.appendChild(toast);
    
    // Animate in
    setTimeout(() => toast.classList.remove('translate-x-full'), 10);
    
    // Remove after duration
    setTimeout(() => {
        toast.classList.add('translate-x-full');
        setTimeout(() => toast.remove(), 300);
    }, duration);
}

function createToastContainer() {
    const container = document.createElement('div');
    container.id = 'toastContainer';
    container.className = 'fixed bottom-4 right-4 z-50 space-y-2';
    document.body.appendChild(container);
    return container;
}

// Loading Button State
function setLoadingState(buttonId, isLoading, loadingText = 'Loading...') {
    const button = document.getElementById(buttonId);
    if (!button) return;
    
    if (isLoading) {
        button.dataset.originalText = button.innerHTML;
        button.innerHTML = `<i class="fas fa-spinner fa-spin mr-2"></i>${loadingText}`;
        button.disabled = true;
    } else {
        button.innerHTML = button.dataset.originalText || button.innerHTML;
        button.disabled = false;
    }
}

// Email Validation
function isValidEmail(email) {
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    return emailRegex.test(email);
}

// Real-time Email Validation
function initEmailValidation(inputId) {
    const emailInput = document.getElementById(inputId);
    if (!emailInput) return;
    
    emailInput.addEventListener('blur', function() {
        const email = this.value.trim();
        const feedbackElement = this.nextElementSibling;
        
        if (email && !isValidEmail(email)) {
            this.classList.add('border-red-500');
            if (feedbackElement) {
                feedbackElement.textContent = 'Please enter a valid email address';
                feedbackElement.classList.add('text-red-500');
            }
        } else {
            this.classList.remove('border-red-500');
            if (feedbackElement) {
                feedbackElement.textContent = '';
            }
        }
    });
}

// Confirm Dialog
function showConfirmDialog(title, message, onConfirm, onCancel) {
    const dialog = document.createElement('div');
    dialog.className = 'fixed inset-0 bg-black bg-opacity-50 z-50 flex items-center justify-center p-4';
    dialog.innerHTML = `
        <div class="bg-white rounded-2xl shadow-2xl max-w-md w-full p-8 transform scale-95 transition-all">
            <div class="text-center mb-6">
                <div class="w-16 h-16 mx-auto mb-4 bg-yellow-100 rounded-full flex items-center justify-center">
                    <i class="fas fa-exclamation-triangle text-yellow-600 text-3xl"></i>
                </div>
                <h3 class="text-2xl font-bold text-gray-800 mb-2">${title}</h3>
                <p class="text-gray-600">${message}</p>
            </div>
            <div class="flex gap-3">
                <button onclick="this.closest('.fixed').remove(); (${onCancel || function() {}})();" 
                        class="flex-1 px-6 py-3 border-2 border-gray-300 text-gray-700 rounded-lg font-semibold hover:border-indigo-500 hover:text-indigo-600 transition">
                    Cancel
                </button>
                <button onclick="this.closest('.fixed').remove(); (${onConfirm})();" 
                        class="flex-1 px-6 py-3 bg-gradient-to-r from-red-500 to-pink-600 text-white rounded-lg font-semibold shadow-lg hover:shadow-xl transition">
                    Confirm
                </button>
            </div>
        </div>
    `;
    
    document.body.appendChild(dialog);
    setTimeout(() => dialog.querySelector('.bg-white').classList.remove('scale-95'), 10);
}

// Animate on Scroll
function initAnimateOnScroll() {
    const elements = document.querySelectorAll('[data-animate="true"]');
    
    const observer = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                entry.target.classList.add('fade-in-up');
                observer.unobserve(entry.target);
            }
        });
    }, {
        threshold: 0.1
    });
    
    elements.forEach(el => observer.observe(el));
}

// Auto-dismiss alerts
function initAutoDismissAlerts() {
    const alerts = document.querySelectorAll('[data-auto-dismiss]');
    
    alerts.forEach(alert => {
        const duration = parseInt(alert.dataset.autoDismiss) || 5000;
        setTimeout(() => {
            alert.style.transition = 'opacity 0.3s ease-out';
            alert.style.opacity = '0';
            setTimeout(() => alert.remove(), 300);
        }, duration);
    });
}

// Copy to Clipboard
function copyToClipboard(text, successMessage = 'Copied to clipboard!') {
    if (navigator.clipboard && window.isSecureContext) {
        navigator.clipboard.writeText(text).then(() => {
            showNotification(successMessage, 'success');
        });
    } else {
        // Fallback for older browsers
        const textArea = document.createElement('textarea');
        textArea.value = text;
        textArea.style.position = 'fixed';
        textArea.style.left = '-999999px';
        document.body.appendChild(textArea);
        textArea.select();
        try {
            document.execCommand('copy');
            showNotification(successMessage, 'success');
        } catch (err) {
            showNotification('Failed to copy', 'error');
        }
        document.body.removeChild(textArea);
    }
}

// Initialize on DOM Ready
document.addEventListener('DOMContentLoaded', function() {
    initFormValidation();
    initAnimateOnScroll();
    initAutoDismissAlerts();
    
    // Add smooth scroll behavior
    document.querySelectorAll('a[href^="#"]').forEach(anchor => {
        anchor.addEventListener('click', function (e) {
            const target = document.querySelector(this.getAttribute('href'));
            if (target) {
                e.preventDefault();
                target.scrollIntoView({ behavior: 'smooth' });
            }
        });
    });
});

// Export functions for global use
window.authUtils = {
    checkPasswordStrength,
    togglePasswordVisibility,
    showNotification,
    setLoadingState,
    isValidEmail,
    initEmailValidation,
    showConfirmDialog,
    copyToClipboard
};
