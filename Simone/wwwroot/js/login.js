/**
 * Neo Ágora - Login Module v2.2
 * Sincronizado con login.cshtml y login.css
 */
class LoginManager {
    constructor() {
        this.config = {
            MAX_ATTEMPTS: 5,
            LOCKOUT_DURATION: 30,
            DEBOUNCE_DELAY: 300,
            MIN_PASSWORD_LENGTH: 6,
            STORAGE_KEY: 'neo_login_attempts'
        };

        this.state = {
            isSubmitting: false,
            attempts: 0,
            lockoutEndTime: null,
            lockoutInterval: null
        };

        this.elements = {};
        this.init();
    }

    init() {
        this.cacheElements();
        this.initRateLimiting();
        this.initEventListeners();
        this.initAccessibility();
        console.log('✓ Neo Ágora Login Module v2.2 inicializado');
    }

    cacheElements() {
        // Mapeo de elementos por ID
        this.elements = {
            loginForm: document.getElementById('loginForm'),
            submitBtn: document.getElementById('submitBtn'),
            emailInput: document.getElementById('emailInput'),
            passwordInput: document.getElementById('passwordInput'),
            passwordToggle: document.getElementById('passwordToggle'),
            capsWarning: document.getElementById('capsWarning'),
            loginCard: document.getElementById('loginCard'),
            successOverlay: document.getElementById('successOverlay'),
            rateLimitWarning: document.getElementById('rateLimitWarning'),
            rateLimitTimer: document.getElementById('rateLimitTimer'),
            btnGoogle: document.getElementById('btnGoogle'),
            btnFacebook: document.getElementById('btnFacebook'),
            announcer: document.getElementById('announcer')
        };
    }

    initRateLimiting() {
        const stored = this.getStoredAttempts();
        this.state.attempts = stored.attempts;

        if (stored.lockoutEndTime && Date.now() < stored.lockoutEndTime) {
            this.state.lockoutEndTime = stored.lockoutEndTime;
            this.startLockoutTimer();
        }
    }

    initEventListeners() {
        // Password toggle
        if (this.elements.passwordToggle) {
            this.elements.passwordToggle.addEventListener('click', () => this.togglePasswordVisibility());
        }

        // Caps lock detection
        if (this.elements.passwordInput) {
            ['keyup', 'keydown', 'focus'].forEach(event => {
                this.elements.passwordInput.addEventListener(event, (e) => this.checkCapsLock(e));
            });

            this.elements.passwordInput.addEventListener('blur', () => this.validateField(this.elements.passwordInput));
        }

        // Email validation on blur
        if (this.elements.emailInput) {
            this.elements.emailInput.addEventListener('blur', () => this.validateField(this.elements.emailInput));
        }

        // Form validation on input
        if (this.elements.loginForm) {
            const debouncedValidation = this.debounce(() => this.validateForm(), this.config.DEBOUNCE_DELAY);
            this.elements.loginForm.addEventListener('input', debouncedValidation);

            // Form submission
            this.elements.loginForm.addEventListener('submit', (e) => this.handleSubmit(e));
        }

        // Social login
        if (this.elements.btnGoogle) {
            this.elements.btnGoogle.addEventListener('click', () => this.handleSocialLogin('Google'));
        }
        if (this.elements.btnFacebook) {
            this.elements.btnFacebook.addEventListener('click', () => this.handleSocialLogin('Facebook'));
        }

        // Keyboard navigation
        document.addEventListener('keydown', (e) => this.handleKeyboardNavigation(e));
    }

    initAccessibility() {
        // Focus management para el overlay
        if (this.elements.successOverlay) {
            this.elements.successOverlay.addEventListener('keydown', (e) => {
                if (e.key === 'Escape' && this.elements.successOverlay.classList.contains('is-active')) {
                    this.elements.successOverlay.classList.remove('is-active');
                }
            });
        }
    }

    // ==================== CORE FUNCTIONS ====================

    togglePasswordVisibility() {
        if (!this.elements.passwordInput) return;

        const isPassword = this.elements.passwordInput.type === 'password';
        this.elements.passwordInput.type = isPassword ? 'text' : 'password';

        // Actualizar icono
        const icon = this.elements.passwordToggle?.querySelector('.password-toggle-icon');
        if (icon) {
            icon.classList.toggle('fa-eye', !isPassword);
            icon.classList.toggle('fa-eye-slash', isPassword);
        }

        // Actualizar accesibilidad
        if (this.elements.passwordToggle) {
            this.elements.passwordToggle.setAttribute('aria-pressed', isPassword);
            this.elements.passwordToggle.setAttribute('aria-label',
                isPassword ? 'Ocultar contraseña' : 'Mostrar contraseña'
            );
        }

        this.announce(isPassword ? 'Contraseña visible' : 'Contraseña oculta');
        this.elements.passwordInput.focus();
    }

    checkCapsLock(event) {
        const capsLockOn = event.getModifierState && event.getModifierState('CapsLock');
        const warning = this.elements.capsWarning;

        if (warning) {
            warning.classList.toggle('is-hidden', !capsLockOn);

            if (capsLockOn && !warning.classList.contains('announced')) {
                this.announce('Advertencia: Bloq Mayús está activado', 'assertive');
                warning.classList.add('announced');
            } else if (!capsLockOn) {
                warning.classList.remove('announced');
            }
        }
    }

    validateField(input) {
        if (!input) return true;

        const value = input.value.trim();
        let isValid = true;
        let errorMessage = '';

        if (input.id === 'emailInput') {
            if (!value) {
                isValid = false;
                errorMessage = 'El correo electrónico es requerido';
            } else if (!this.isValidEmail(value)) {
                isValid = false;
                errorMessage = 'Ingresa un correo electrónico válido';
            }
        } else if (input.id === 'passwordInput') {
            if (!value) {
                isValid = false;
                errorMessage = 'La contraseña es requerida';
            } else if (value.length < this.config.MIN_PASSWORD_LENGTH) {
                isValid = false;
                errorMessage = `La contraseña debe tener al menos ${this.config.MIN_PASSWORD_LENGTH} caracteres`;
            }
        }

        // Actualizar UI
        if (isValid) {
            this.clearError(input);
        } else {
            this.showError(input, errorMessage);
        }

        input.classList.toggle('is-valid', isValid && value);
        input.classList.toggle('is-invalid', !isValid);
        input.setAttribute('aria-invalid', !isValid);

        return isValid;
    }

    validateForm() {
        const emailValid = this.validateField(this.elements.emailInput);
        const passwordValid = this.validateField(this.elements.passwordInput);

        const isFormValid = emailValid && passwordValid;

        if (this.elements.submitBtn) {
            this.elements.submitBtn.disabled = !isFormValid || this.isLockedOut();
        }

        return isFormValid;
    }

    async handleSubmit(event) {
        event.preventDefault();

        if (this.state.isSubmitting) return;

        if (this.isLockedOut()) {
            this.announce('Cuenta temporalmente bloqueada. Por favor espera.', 'assertive');
            return;
        }

        if (!this.validateForm()) {
            this.shakeCard();
            this.announce('Por favor corrige los errores en el formulario', 'assertive');

            const firstInvalid = this.elements.loginForm?.querySelector('.is-invalid');
            if (firstInvalid) firstInvalid.focus();

            this.incrementAttempts();
            return;
        }

        // Activar estado de carga
        this.state.isSubmitting = true;
        if (this.elements.submitBtn) {
            this.elements.submitBtn.classList.add('is-loading');
            this.elements.submitBtn.disabled = true;
        }
        this.announce('Iniciando sesión...');

        // En una implementación real, aquí iría el submit del formulario
        // Por ahora, dejamos que el formulario se envíe normalmente

        // Descomenta esto para envío real:
        this.elements.loginForm?.submit();

        // O usa esto para demo/pruebas:
        /*
        try {
            await new Promise(resolve => setTimeout(resolve, 1500));
            this.showSuccess();
            this.resetAttempts();
        } catch (error) {
            console.error('Login error:', error);
            this.showNotification('Error al iniciar sesión. Intenta de nuevo.', 'error');
            this.state.isSubmitting = false;
            if (this.elements.submitBtn) {
                this.elements.submitBtn.classList.remove('is-loading');
                this.elements.submitBtn.disabled = false;
            }
            this.incrementAttempts();
        }
        */
    }

    handleSocialLogin(provider) {
        const message = `Inicio de sesión con ${provider} - Funcionalidad en desarrollo`;
        this.announce(message);
        this.showNotification(message, 'info');
    }

    handleKeyboardNavigation(event) {
        if (event.key === 'Enter' &&
            document.activeElement === this.elements.emailInput &&
            !event.shiftKey) {
            event.preventDefault();
            this.elements.passwordInput?.focus();
        }
    }

    // ==================== UTILITY FUNCTIONS ====================

    debounce(func, wait) {
        let timeout;
        return (...args) => {
            clearTimeout(timeout);
            timeout = setTimeout(() => func.apply(this, args), wait);
        };
    }

    announce(message, priority = 'polite') {
        let announcer = this.elements.announcer || document.getElementById('announcer');

        if (!announcer) {
            announcer = document.createElement('div');
            announcer.id = 'announcer';
            announcer.className = 'sr-only';
            announcer.setAttribute('role', 'status');
            announcer.setAttribute('aria-live', 'polite');
            announcer.setAttribute('aria-atomic', 'true');
            document.body.appendChild(announcer);
            this.elements.announcer = announcer;
        }

        announcer.setAttribute('aria-live', priority);
        announcer.textContent = '';

        setTimeout(() => {
            announcer.textContent = message;
        }, 100);
    }

    isValidEmail(email) {
        return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email);
    }

    getStoredAttempts() {
        try {
            const stored = sessionStorage.getItem(this.config.STORAGE_KEY);
            if (stored) {
                const data = JSON.parse(stored);
                if (data.lockoutEndTime && Date.now() > data.lockoutEndTime) {
                    sessionStorage.removeItem(this.config.STORAGE_KEY);
                    return { attempts: 0, lockoutEndTime: null };
                }
                return data;
            }
        } catch (e) {
            console.warn('Error reading stored attempts:', e);
        }
        return { attempts: 0, lockoutEndTime: null };
    }

    storeAttempts(attempts, lockoutEndTime = null) {
        try {
            sessionStorage.setItem(this.config.STORAGE_KEY, JSON.stringify({
                attempts,
                lockoutEndTime,
                timestamp: Date.now()
            }));
        } catch (e) {
            console.warn('Error storing attempts:', e);
        }
    }

    incrementAttempts() {
        this.state.attempts++;

        if (this.state.attempts >= this.config.MAX_ATTEMPTS) {
            this.state.lockoutEndTime = Date.now() + (this.config.LOCKOUT_DURATION * 1000);
            this.storeAttempts(this.state.attempts, this.state.lockoutEndTime);
            this.startLockoutTimer();
        } else {
            this.storeAttempts(this.state.attempts);
        }
    }

    resetAttempts() {
        this.state.attempts = 0;
        this.state.lockoutEndTime = null;
        sessionStorage.removeItem(this.config.STORAGE_KEY);

        if (this.elements.rateLimitWarning) {
            this.elements.rateLimitWarning.classList.remove('is-visible');
        }
    }

    startLockoutTimer() {
        if (!this.elements.rateLimitWarning || !this.elements.rateLimitTimer) return;

        // Mostrar warning - usa clase is-visible
        this.elements.rateLimitWarning.classList.add('is-visible');

        if (this.elements.submitBtn) {
            this.elements.submitBtn.disabled = true;
        }

        this.announce(`Demasiados intentos. Por favor espera ${this.config.LOCKOUT_DURATION} segundos.`, 'assertive');

        const updateTimer = () => {
            const remaining = Math.ceil((this.state.lockoutEndTime - Date.now()) / 1000);

            if (remaining <= 0) {
                clearInterval(this.state.lockoutInterval);
                this.state.lockoutInterval = null;
                this.resetAttempts();
                this.announce('Puedes intentar iniciar sesión nuevamente');

                if (this.elements.submitBtn) {
                    this.elements.submitBtn.disabled = false;
                }
            } else if (this.elements.rateLimitTimer) {
                this.elements.rateLimitTimer.textContent = remaining;
            }
        };

        updateTimer();
        this.state.lockoutInterval = setInterval(updateTimer, 1000);
    }

    isLockedOut() {
        return this.state.lockoutEndTime && Date.now() < this.state.lockoutEndTime;
    }

    shakeCard() {
        if (this.elements.loginCard) {
            this.elements.loginCard.classList.add('shake');
            setTimeout(() => {
                this.elements.loginCard.classList.remove('shake');
            }, 500);
        }
    }

    showError(input, message) {
        if (!input) return;

        const errorId = `${input.id}Error`;
        let errorElement = document.getElementById(errorId);

        if (!errorElement) {
            // Buscar el span de validación de ASP.NET
            const parent = input.closest('.form-group') || input.parentNode;
            errorElement = parent.querySelector('.validation-message, .field-validation-error');

            if (!errorElement) {
                errorElement = document.createElement('span');
                errorElement.id = errorId;
                errorElement.className = 'validation-message';
                parent.appendChild(errorElement);
            }
        }

        errorElement.textContent = message;
        errorElement.setAttribute('role', 'alert');
    }

    clearError(input) {
        if (!input) return;

        const errorId = `${input.id}Error`;
        const errorElement = document.getElementById(errorId);

        if (errorElement) {
            errorElement.textContent = '';
            errorElement.removeAttribute('role');
        }

        // También limpiar validación de ASP.NET
        const parent = input.closest('.form-group') || input.parentNode;
        const aspValidation = parent.querySelector('.field-validation-error');
        if (aspValidation) {
            aspValidation.textContent = '';
        }
    }

    showSuccess() {
        if (this.elements.successOverlay) {
            this.elements.successOverlay.classList.add('is-active');
            this.elements.successOverlay.setAttribute('aria-hidden', 'false');
            this.announce('¡Inicio de sesión exitoso! Redirigiendo...');

            // Auto-redirect después de 2 segundos
            setTimeout(() => {
                // En implementación real, redirigir al dashboard
                console.log('Redirecting to dashboard...');
            }, 2000);
        }
    }

    showNotification(message, type = 'info') {
        const notification = document.createElement('div');
        notification.className = `notification notification-${type}`;
        notification.setAttribute('role', 'alert');
        notification.setAttribute('aria-live', 'polite');

        const iconMap = {
            info: 'info-circle',
            success: 'check-circle',
            error: 'exclamation-circle',
            warning: 'exclamation-triangle'
        };

        const colorMap = {
            info: '#2196f3',
            success: '#4caf50',
            error: '#f44336',
            warning: '#ff9800'
        };

        notification.innerHTML = `
            <i class="fa-solid fa-${iconMap[type] || iconMap.info}"></i>
            <span>${message}</span>
        `;

        notification.style.cssText = `
            position: fixed;
            top: 20px;
            right: 20px;
            background: ${colorMap[type] || colorMap.info};
            color: white;
            padding: 14px 24px;
            border-radius: 12px;
            box-shadow: 0 6px 20px rgba(0,0,0,0.15);
            z-index: 10000;
            animation: slideInRight 0.3s ease;
            display: flex;
            align-items: center;
            gap: 10px;
            font-family: 'Plus Jakarta Sans', sans-serif;
            font-weight: 500;
            font-size: 0.9375rem;
        `;

        document.body.appendChild(notification);

        // Remover después de 5 segundos
        setTimeout(() => {
            notification.style.animation = 'slideOutRight 0.3s ease forwards';
            setTimeout(() => notification.remove(), 300);
        }, 5000);
    }
}

// Inicializar cuando el DOM esté listo
document.addEventListener('DOMContentLoaded', () => {
    window.loginManager = new LoginManager();
});

// Export global para uso externo
window.NeoAgoraLogin = {
    showSuccess: () => {
        const overlay = document.getElementById('successOverlay');
        if (overlay) {
            overlay.classList.add('is-active');
            overlay.setAttribute('aria-hidden', 'false');
        }
    },
    hideSuccess: () => {
        const overlay = document.getElementById('successOverlay');
        if (overlay) {
            overlay.classList.remove('is-active');
            overlay.setAttribute('aria-hidden', 'true');
        }
    }
};
