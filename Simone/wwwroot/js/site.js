/**
 * ============================================================================
 * SIMONE E-COMMERCE - Professional JavaScript v3.0 OPTIMIZED
 * ============================================================================
 * Código optimizado, sin conflictos, production-ready
 * Compatible con vistas enterprise y páginas normales
 * ============================================================================
 */

'use strict';

// ============================================================================
// CONFIGURACIÓN GLOBAL
// ============================================================================

const SIMONE_CONFIG = {
    // Timings
    DEBOUNCE_DELAY: 300,
    THROTTLE_DELAY: 150,
    ALERT_DURATION: 5000,
    TOAST_DURATION: 4000,
    ANIMATION_DURATION: 300,

    // Breakpoints
    BREAKPOINTS: {
        sm: 640,
        md: 768,
        lg: 1024,
        xl: 1280,
        xxl: 1536
    },

    // Storage Keys
    STORAGE: {
        darkMode: 'simone_dark_mode',
        sidebar: 'simone_sidebar',
        cart: 'simone_cart',
        prefs: 'simone_prefs'
    },

    // Selectores
    SEL: {
        navbar: '.navbar',
        sidebar: '#sidebar',
        sidebarToggle: '#menu-toggle',
        sidebarClose: '#close-sidebar',
        sidebarBackdrop: '.sidebar-backdrop',
        cartPanel: '#cartPanel',
        darkToggle: '#dark-mode-toggle',
        forms: 'form',
        alerts: '.alert',
        modals: '.modal'
    }
};

// ============================================================================
// UTILIDADES CORE - Renamed from $ to _ to avoid jQuery conflict
// ============================================================================

const _ = {
    /**
     * Query selector seguro
     */
    qs(selector, parent = document) {
        try {
            return parent.querySelector(selector);
        } catch (e) {
            console.warn('Invalid selector:', selector);
            return null;
        }
    },

    /**
     * Query selector all seguro
     */
    qsa(selector, parent = document) {
        try {
            return Array.from(parent.querySelectorAll(selector));
        } catch (e) {
            console.warn('Invalid selector:', selector);
            return [];
        }
    },

    /**
     * Event listener con manejo de errores
     */
    on(element, event, handler, options = {}) {
        if (!element) return;
        try {
            element.addEventListener(event, handler, options);
        } catch (e) {
            console.error('Error adding event listener:', e);
        }
    },

    /**
     * Event delegation
     */
    delegate(parent, selector, event, handler) {
        this.on(parent, event, (e) => {
            const target = e.target.closest(selector);
            if (target) handler.call(target, e);
        });
    },

    /**
     * Debounce
     */
    debounce(func, wait = SIMONE_CONFIG.DEBOUNCE_DELAY) {
        let timeout;
        return function executedFunction(...args) {
            clearTimeout(timeout);
            timeout = setTimeout(() => func(...args), wait);
        };
    },

    /**
     * Throttle
     */
    throttle(func, limit = SIMONE_CONFIG.THROTTLE_DELAY) {
        let inThrottle;
        return function (...args) {
            if (!inThrottle) {
                func.apply(this, args);
                inThrottle = true;
                setTimeout(() => inThrottle = false, limit);
            }
        };
    },

    /**
     * Check if element is in viewport
     */
    isInViewport(element) {
        if (!element) return false;
        const rect = element.getBoundingClientRect();
        return (
            rect.top >= 0 &&
            rect.left >= 0 &&
            rect.bottom <= (window.innerHeight || document.documentElement.clientHeight) &&
            rect.right <= (window.innerWidth || document.documentElement.clientWidth)
        );
    },

    /**
     * Get current breakpoint
     */
    getBreakpoint() {
        const width = window.innerWidth;
        if (width < SIMONE_CONFIG.BREAKPOINTS.sm) return 'xs';
        if (width < SIMONE_CONFIG.BREAKPOINTS.md) return 'sm';
        if (width < SIMONE_CONFIG.BREAKPOINTS.lg) return 'md';
        if (width < SIMONE_CONFIG.BREAKPOINTS.xl) return 'lg';
        if (width < SIMONE_CONFIG.BREAKPOINTS.xxl) return 'xl';
        return 'xxl';
    }
};

// ============================================================================
// STORAGE UTILITIES
// ============================================================================

const Storage = {
    get(key, defaultValue = null) {
        try {
            const item = localStorage.getItem(key);
            return item ? JSON.parse(item) : defaultValue;
        } catch (e) {
            console.warn('Storage get error:', key, e);
            return defaultValue;
        }
    },

    set(key, value) {
        try {
            localStorage.setItem(key, JSON.stringify(value));
            return true;
        } catch (e) {
            console.warn('Storage set error:', key, e);
            return false;
        }
    },

    remove(key) {
        try {
            localStorage.removeItem(key);
        } catch (e) {
            console.warn('Storage remove error:', key, e);
        }
    },

    clear() {
        try {
            localStorage.clear();
        } catch (e) {
            console.warn('Storage clear error:', e);
        }
    }
};

// ============================================================================
// NAVBAR MODULE
// ============================================================================

const Navbar = {
    navbar: null,
    lastScroll: 0,

    init() {
        this.navbar = _.qs(SIMONE_CONFIG.SEL.navbar);
        if (!this.navbar) return;

        this.setupScrollEffect();
        this.setupMobileMenu();
        this.setupDropdowns();
    },

    setupScrollEffect() {
        const handleScroll = _.throttle(() => {
            const scrollTop = window.pageYOffset || document.documentElement.scrollTop;

            // Add scrolled class
            if (scrollTop > 50) {
                this.navbar.classList.add('scrolled');
            } else {
                this.navbar.classList.remove('scrolled');
            }

            this.lastScroll = scrollTop;
        }, SIMONE_CONFIG.THROTTLE_DELAY);

        _.on(window, 'scroll', handleScroll, { passive: true });
    },

    setupMobileMenu() {
        const toggler = _.qs('.navbar-toggler', this.navbar);
        if (!toggler) return;

        _.on(toggler, 'click', (e) => {
            e.preventDefault();
            const collapse = _.qs('.navbar-collapse', this.navbar);
            if (collapse) {
                collapse.classList.toggle('show');
                toggler.setAttribute('aria-expanded',
                    collapse.classList.contains('show'));
            }
        });

        // Close on outside click
        _.on(document, 'click', (e) => {
            const collapse = _.qs('.navbar-collapse', this.navbar);
            if (collapse && collapse.classList.contains('show')) {
                if (!this.navbar.contains(e.target)) {
                    collapse.classList.remove('show');
                    toggler.setAttribute('aria-expanded', 'false');
                }
            }
        });
    },

    setupDropdowns() {
        const dropdowns = _.qsa('.dropdown', this.navbar);

        dropdowns.forEach(dropdown => {
            const toggle = _.qs('.dropdown-toggle', dropdown);
            const menu = _.qs('.dropdown-menu', dropdown);

            if (!toggle || !menu) return;

            _.on(toggle, 'click', (e) => {
                e.preventDefault();
                e.stopPropagation();

                // Close others
                dropdowns.forEach(other => {
                    if (other !== dropdown) {
                        other.classList.remove('show');
                    }
                });

                dropdown.classList.toggle('show');
            });
        });

        // Close on outside click
        _.on(document, 'click', () => {
            dropdowns.forEach(dropdown => {
                dropdown.classList.remove('show');
            });
        });
    }
};

// ============================================================================
// SIDEBAR MODULE
// ============================================================================

const Sidebar = {
    sidebar: null,
    toggle: null,
    backdrop: null,

    init() {
        this.sidebar = _.qs(SIMONE_CONFIG.SEL.sidebar);
        this.toggle = _.qs(SIMONE_CONFIG.SEL.sidebarToggle);

        if (!this.sidebar || !this.toggle) return;

        this.createBackdrop();
        this.setupEvents();
        this.setupKeyboard();
    },

    createBackdrop() {
        if (_.qs(SIMONE_CONFIG.SEL.sidebarBackdrop)) return;

        this.backdrop = document.createElement('div');
        this.backdrop.className = 'sidebar-backdrop';
        document.body.appendChild(this.backdrop);
    },

    setupEvents() {
        // Open
        _.on(this.toggle, 'click', (e) => {
            e.preventDefault();
            this.open();
        });

        // Close button
        const closeBtn = _.qs(SIMONE_CONFIG.SEL.sidebarClose);
        if (closeBtn) {
            _.on(closeBtn, 'click', (e) => {
                e.preventDefault();
                this.close();
            });
        }

        // Backdrop
        if (this.backdrop) {
            _.on(this.backdrop, 'click', () => this.close());
        }

        // ESC key
        _.on(document, 'keydown', (e) => {
            if (e.key === 'Escape' && this.isOpen()) {
                this.close();
            }
        });
    },

    setupKeyboard() {
        const links = _.qsa('a', this.sidebar);

        links.forEach((link, index) => {
            _.on(link, 'keydown', (e) => {
                if (e.key === 'ArrowDown') {
                    e.preventDefault();
                    const next = links[index + 1];
                    if (next) next.focus();
                } else if (e.key === 'ArrowUp') {
                    e.preventDefault();
                    const prev = links[index - 1];
                    if (prev) prev.focus();
                }
            });
        });
    },

    open() {
        this.sidebar.classList.add('show');
        if (this.backdrop) {
            this.backdrop.classList.add('show');
        }
        this.sidebar.setAttribute('aria-hidden', 'false');
        document.body.style.overflow = 'hidden';

        // Focus first link
        setTimeout(() => {
            const firstLink = _.qs('a', this.sidebar);
            if (firstLink) firstLink.focus();
        }, 100);
    },

    close() {
        this.sidebar.classList.remove('show');
        if (this.backdrop) {
            this.backdrop.classList.remove('show');
        }
        this.sidebar.setAttribute('aria-hidden', 'true');
        document.body.style.overflow = '';

        // Return focus
        if (this.toggle) {
            this.toggle.focus();
        }
    },

    isOpen() {
        return this.sidebar.classList.contains('show');
    }
};

// ============================================================================
// DARK MODE MODULE (OPTIONAL)
// ============================================================================

const DarkMode = {
    toggle: null,

    init() {
        this.toggle = _.qs(SIMONE_CONFIG.SEL.darkToggle);
        if (!this.toggle) return;

        this.restore();
        this.setupToggle();
    },

    setupToggle() {
        _.on(this.toggle, 'click', (e) => {
            e.preventDefault();
            this.toggleMode();
        });
    },

    toggleMode() {
        const isDark = document.body.classList.toggle('dark-mode');
        this.updateIcon(isDark);
        Storage.set(SIMONE_CONFIG.STORAGE.darkMode, isDark);
    },

    updateIcon(isDark) {
        const icon = _.qs('i', this.toggle);
        if (icon) {
            icon.className = isDark ? 'fas fa-sun' : 'fas fa-moon';
        }
    },

    restore() {
        const isDark = Storage.get(SIMONE_CONFIG.STORAGE.darkMode, false);
        if (isDark) {
            document.body.classList.add('dark-mode');
            this.updateIcon(true);
        }
    }
};

// ============================================================================
// ALERTS MODULE
// ============================================================================

const Alerts = {
    init() {
        this.setupDismiss();
        this.setupAutoHide();
    },

    setupDismiss() {
        _.delegate(document, '.alert .btn-close', 'click', function (e) {
            e.preventDefault();
            const alert = this.closest('.alert');
            if (alert) Alerts.hide(alert);
        });
    },

    setupAutoHide() {
        const alerts = _.qsa(SIMONE_CONFIG.SEL.alerts);
        alerts.forEach(alert => {
            if (!alert.classList.contains('alert-permanent')) {
                setTimeout(() => this.hide(alert), SIMONE_CONFIG.ALERT_DURATION);
            }
        });
    },

    hide(alert) {
        if (!alert) return;
        alert.style.opacity = '0';
        setTimeout(() => alert.remove(), SIMONE_CONFIG.ANIMATION_DURATION);
    },

    show(message, type = 'info') {
        const alert = document.createElement('div');
        alert.className = `alert alert-${type} alert-dismissible fade-in`;
        alert.setAttribute('role', 'alert');
        alert.innerHTML = `
            ${message}
            <button type="button" class="btn-close" aria-label="Cerrar"></button>
        `;

        const container = _.qs('.alert-container') || document.body;
        container.insertBefore(alert, container.firstChild);

        setTimeout(() => this.hide(alert), SIMONE_CONFIG.ALERT_DURATION);
        return alert;
    }
};

// ============================================================================
// TOAST MODULE
// ============================================================================

const Toast = {
    container: null,

    init() {
        this.createContainer();
    },

    createContainer() {
        if (_.qs('#toast-container')) return;

        this.container = document.createElement('div');
        this.container.id = 'toast-container';
        this.container.className = 'position-fixed bottom-0 end-0 p-3';
        this.container.style.zIndex = '9999';
        document.body.appendChild(this.container);
    },

    show(message, type = 'info', duration = SIMONE_CONFIG.TOAST_DURATION) {
        const icons = {
            success: 'fa-check-circle',
            danger: 'fa-exclamation-circle',
            warning: 'fa-exclamation-triangle',
            info: 'fa-info-circle'
        };

        const toast = document.createElement('div');
        toast.className = `toast align-items-center text-white bg-${type} border-0 fade-in`;
        toast.setAttribute('role', 'alert');
        toast.innerHTML = `
            <div class="d-flex">
                <div class="toast-body">
                    <i class="fas ${icons[type]} me-2"></i>
                    ${message}
                </div>
                <button type="button" class="btn-close btn-close-white me-2 m-auto"></button>
            </div>
        `;

        this.container.appendChild(toast);

        const closeBtn = _.qs('.btn-close', toast);
        if (closeBtn) {
            _.on(closeBtn, 'click', () => this.hide(toast));
        }

        if (duration > 0) {
            setTimeout(() => this.hide(toast), duration);
        }

        return toast;
    },

    hide(toast) {
        if (!toast) return;
        toast.style.opacity = '0';
        setTimeout(() => toast.remove(), SIMONE_CONFIG.ANIMATION_DURATION);
    },

    success(msg) { return this.show(msg, 'success'); },
    error(msg) { return this.show(msg, 'danger'); },
    warning(msg) { return this.show(msg, 'warning'); },
    info(msg) { return this.show(msg, 'info'); }
};

// ============================================================================
// FORMS MODULE
// ============================================================================

const Forms = {
    init() {
        this.setupValidation();
        this.setupRealTimeValidation();
        this.setupPasswordToggle();
    },

    setupValidation() {
        const forms = _.qsa(SIMONE_CONFIG.SEL.forms);

        forms.forEach(form => {
            _.on(form, 'submit', (e) => {
                if (!this.validateForm(form)) {
                    e.preventDefault();
                    e.stopPropagation();

                    const firstInvalid = _.qs('.is-invalid', form);
                    if (firstInvalid) firstInvalid.focus();
                }

                form.classList.add('was-validated');
            });
        });
    },

    setupRealTimeValidation() {
        _.delegate(document, 'input[required], textarea[required], select[required]',
            'blur', function () {
                Forms.validateField(this);
            });

        _.delegate(document, 'input[type="email"]', 'blur', function () {
            Forms.validateEmail(this);
        });
    },

    validateForm(form) {
        if (!form) return false;

        const requiredFields = _.qsa('[required]', form);
        let isValid = true;

        requiredFields.forEach(field => {
            if (!this.validateField(field)) {
                isValid = false;
            }
        });

        return isValid;
    },

    validateField(field) {
        if (!field) return false;

        const value = field.value.trim();
        const isValid = value.length > 0;

        this.setFieldState(field, isValid);
        return isValid;
    },

    validateEmail(field) {
        if (!field) return false;

        const value = field.value.trim();
        const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
        const isValid = emailRegex.test(value);

        this.setFieldState(field, isValid);
        return isValid;
    },

    setFieldState(field, isValid) {
        if (!field) return;

        if (isValid) {
            field.classList.remove('is-invalid');
            field.classList.add('is-valid');
        } else {
            field.classList.remove('is-valid');
            field.classList.add('is-invalid');
        }
    },

    setupPasswordToggle() {
        _.delegate(document, '.password-toggle', 'click', function () {
            const input = _.qs('input', this.parentElement);
            if (!input) return;

            const type = input.type === 'password' ? 'text' : 'password';
            input.type = type;

            const icon = _.qs('i', this);
            if (icon) {
                icon.classList.toggle('fa-eye');
                icon.classList.toggle('fa-eye-slash');
            }
        });
    }
};

// ============================================================================
// SMOOTH SCROLL MODULE
// ============================================================================

const SmoothScroll = {
    init() {
        this.setupAnchors();
        this.setupScrollToTop();
    },

    setupAnchors() {
        _.delegate(document, 'a[href^="#"]', 'click', function (e) {
            const href = this.getAttribute('href');
            if (!href || href === '#') return;

            const target = _.qs(href);
            if (!target) return;

            e.preventDefault();
            SmoothScroll.scrollTo(target);
        });
    },

    setupScrollToTop() {
        const btn = _.qs('#scroll-to-top');
        if (!btn) return;

        const handleScroll = _.throttle(() => {
            if (window.pageYOffset > 300) {
                btn.classList.add('show');
            } else {
                btn.classList.remove('show');
            }
        }, SIMONE_CONFIG.THROTTLE_DELAY);

        _.on(window, 'scroll', handleScroll, { passive: true });

        _.on(btn, 'click', (e) => {
            e.preventDefault();
            window.scrollTo({ top: 0, behavior: 'smooth' });
        });
    },

    scrollTo(target, offset = 80) {
        const element = typeof target === 'string' ? _.qs(target) : target;
        if (!element) return;

        const rect = element.getBoundingClientRect();
        const scrollTop = window.pageYOffset || document.documentElement.scrollTop;
        const elementTop = rect.top + scrollTop;

        window.scrollTo({
            top: elementTop - offset,
            behavior: 'smooth'
        });
    }
};

// ============================================================================
// LAZY LOAD MODULE
// ============================================================================

const LazyLoad = {
    observer: null,

    init() {
        if ('IntersectionObserver' in window) {
            this.setupObserver();
        } else {
            this.loadAll();
        }
    },

    setupObserver() {
        this.observer = new IntersectionObserver((entries) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    this.loadImage(entry.target);
                    this.observer.unobserve(entry.target);
                }
            });
        }, {
            root: null,
            rootMargin: '50px',
            threshold: 0.01
        });

        const images = _.qsa('img[loading="lazy"]');
        images.forEach(img => this.observer.observe(img));
    },

    loadImage(img) {
        if (!img) return;

        const src = img.dataset.src || img.getAttribute('data-src');
        if (src) {
            img.src = src;
            img.classList.add('loaded');
        }
    },

    loadAll() {
        const images = _.qsa('img[loading="lazy"]');
        images.forEach(img => this.loadImage(img));
    }
};

// ============================================================================
// MODALS MODULE (OPTIONAL)
// ============================================================================

const Modals = {
    init() {
        this.setupTriggers();
        this.setupClose();
    },

    setupTriggers() {
        _.delegate(document, '[data-toggle="modal"]', 'click', function (e) {
            e.preventDefault();
            const target = this.getAttribute('data-target');
            if (target) Modals.open(target);
        });
    },

    setupClose() {
        _.delegate(document, '.modal [data-dismiss="modal"]', 'click', function (e) {
            e.preventDefault();
            const modal = this.closest('.modal');
            if (modal) Modals.close(modal);
        });

        _.delegate(document, '.modal-backdrop', 'click', function () {
            Modals.closeAll();
        });
    },

    open(selector) {
        const modal = _.qs(selector);
        if (!modal) return;

        // Create backdrop
        const backdrop = document.createElement('div');
        backdrop.className = 'modal-backdrop show';
        document.body.appendChild(backdrop);

        modal.classList.add('show');
        modal.style.display = 'block';
        document.body.style.overflow = 'hidden';
    },

    close(modal) {
        if (!modal) return;

        modal.classList.remove('show');
        setTimeout(() => {
            modal.style.display = 'none';
        }, SIMONE_CONFIG.ANIMATION_DURATION);

        const backdrop = _.qs('.modal-backdrop');
        if (backdrop) backdrop.remove();

        document.body.style.overflow = '';
    },

    closeAll() {
        const modals = _.qsa('.modal.show');
        modals.forEach(modal => this.close(modal));
    }
};

// ============================================================================
// ANALYTICS MODULE (READY FOR INTEGRATION)
// ============================================================================

const Analytics = {
    track(category, action, label = '', value = 0) {
        // Google Analytics 4
        if (typeof gtag !== 'undefined') {
            gtag('event', action, {
                event_category: category,
                event_label: label,
                value: value
            });
        }

        // Console log para desarrollo
        if (window.location.hostname === 'localhost') {
            console.log('📊 Analytics:', { category, action, label, value });
        }
    },

    pageView(path = window.location.pathname) {
        if (typeof gtag !== 'undefined') {
            gtag('config', 'GA_MEASUREMENT_ID', {
                page_path: path
            });
        }
    }
};

// ============================================================================
// INITIALIZATION
// ============================================================================

function initSimone() {
    console.log('🚀 Initializing Simone v3.0...');

    try {
        // Core modules
        Navbar.init();
        Sidebar.init();
        DarkMode.init();

        // UI modules
        Alerts.init();
        Toast.init();
        Forms.init();
        SmoothScroll.init();

        // Optimization modules
        LazyLoad.init();
        Modals.init();

        console.log('✅ Simone initialized successfully');

        // Track page view
        Analytics.pageView();

    } catch (error) {
        console.error('❌ Error initializing Simone:', error);
    }
}

// ============================================================================
// EVENT LISTENERS
// ============================================================================

// DOM Ready
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initSimone);
} else {
    initSimone();
}

// Page Load
_.on(window, 'load', () => {
    document.body.classList.remove('loading');

    // Log performance
    if (window.performance && window.performance.timing) {
        const perfData = window.performance.timing;
        const loadTime = perfData.loadEventEnd - perfData.navigationStart;
        console.log(`⚡ Page loaded in ${loadTime}ms`);
    }
});

// Online/Offline
_.on(window, 'online', () => Toast.success('Conexión restaurada'));
_.on(window, 'offline', () => Toast.warning('Sin conexión'));

// ============================================================================
// GLOBAL EXPORT
// ============================================================================

window.Simone = {
    _,
    Storage,
    Navbar,
    Sidebar,
    DarkMode,
    Alerts,
    Toast,
    Forms,
    SmoothScroll,
    LazyLoad,
    Modals,
    Analytics,
    CONFIG: SIMONE_CONFIG
};

// ============================================================================
// END
// ============================================================================