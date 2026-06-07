/**
 * Receipt Expense Tracker - Site JavaScript
 * Common utilities and functionality
 */

// Toast notification system
function showToast(type, message, duration = 5000) {
    const container = document.getElementById('toastContainer');
    if (!container) {
        console.error('Toast container not found');
        return;
    }

    const toast = document.createElement('div');
    toast.className = `toast show border-0`;
    toast.setAttribute('role', 'alert');

    const bgClass = type === 'success' ? 'bg-success' :
        type === 'danger' ? 'bg-danger' :
            type === 'warning' ? 'bg-warning' : 'bg-info';

    const icon = type === 'success' ? 'bi-check-circle-fill' :
        type === 'danger' ? 'bi-exclamation-circle-fill' :
            type === 'warning' ? 'bi-exclamation-triangle-fill' : 'bi-info-circle-fill';

    toast.innerHTML = `
        <div class="toast-header ${bgClass} text-white border-0">
            <i class="bi ${icon} me-2"></i>
            <strong class="me-auto">${type.charAt(0).toUpperCase() + type.slice(1)}</strong>
            <button type="button" class="btn-close btn-close-white" data-bs-dismiss="toast"></button>
        </div>
        <div class="toast-body">
            ${message}
        </div>
    `;

    container.appendChild(toast);

    // Auto remove after duration
    setTimeout(() => {
        toast.classList.remove('show');
        setTimeout(() => toast.remove(), 300);
    }, duration);

    // Close button handler
    toast.querySelector('.btn-close').addEventListener('click', () => {
        toast.classList.remove('show');
        setTimeout(() => toast.remove(), 300);
    });
}

// Loading overlay
function showLoading() {
    const overlay = document.getElementById('loadingOverlay');
    if (overlay) {
        overlay.classList.remove('d-none');
    }
}

function hideLoading() {
    const overlay = document.getElementById('loadingOverlay');
    if (overlay) {
        overlay.classList.add('d-none');
    }
}

// Sidebar toggle (mobile)
document.addEventListener('DOMContentLoaded', function () {
    const sidebar = document.getElementById('sidebar');
    const sidebarToggle = document.getElementById('sidebarToggle');
    const sidebarClose = document.getElementById('sidebarClose');
    const backdrop = document.createElement('div');
    backdrop.className = 'sidebar-backdrop d-none position-fixed vw-100 vh-100 bg-dark bg-opacity-50';
    backdrop.style.zIndex = '998';
    document.body.appendChild(backdrop);

    function openSidebar() {
        sidebar?.classList.add('show');
        backdrop.classList.remove('d-none');
        document.body.style.overflow = 'hidden';
    }

    function closeSidebar() {
        sidebar?.classList.remove('show');
        backdrop.classList.add('d-none');
        document.body.style.overflow = '';
    }

    sidebarToggle?.addEventListener('click', openSidebar);
    sidebarClose?.addEventListener('click', closeSidebar);
    backdrop.addEventListener('click', closeSidebar);

    // Close sidebar on nav link click (mobile)
    document.querySelectorAll('.sidebar .nav-link').forEach(link => {
        link.addEventListener('click', closeSidebar);
    });

    // Format currency inputs
    document.querySelectorAll('[data-format="currency"]').forEach(input => {
        input.addEventListener('blur', function () {
            const value = parseFloat(this.value);
            if (!isNaN(value)) {
                this.value = value.toFixed(2);
            }
        });
    });

    // Auto-hide alerts after 5 seconds
    document.querySelectorAll('.alert[data-auto-hide]').forEach(alert => {
        setTimeout(() => {
            alert.classList.add('fade');
            setTimeout(() => alert.remove(), 150);
        }, 5000);
    });

    // Form validation styling
    document.querySelectorAll('form').forEach(form => {
        form.addEventListener('submit', function () {
            if (!this.checkValidity()) {
                this.classList.add('was-validated');
            }
        });
    });

    // Confirm delete actions
    document.querySelectorAll('[data-confirm]').forEach(element => {
        element.addEventListener('click', function (e) {
            const message = this.dataset.confirm;
            if (!confirm(message)) {
                e.preventDefault();
                e.stopPropagation();
            }
        });
    });
});

// URL query parameter helpers
function getQueryParam(name) {
    const params = new URLSearchParams(window.location.search);
    return params.get(name);
}

function setQueryParam(name, value) {
    const params = new URLSearchParams(window.location.search);
    params.set(name, value);
    window.location.search = params.toString();
}

// Format helpers
function formatCurrency(amount) {
    return new Intl.NumberFormat('en-US', {
        style: 'currency',
        currency: 'USD'
    }).format(amount);
}

function formatDate(date) {
    return new Intl.DateTimeFormat('en-US', {
        year: 'numeric',
        month: 'short',
        day: 'numeric'
    }).format(new Date(date));
}

// Export for use in views
window.showToast = showToast;
window.showLoading = showLoading;
window.hideLoading = hideLoading;
window.formatCurrency = formatCurrency;
window.formatDate = formatDate;
