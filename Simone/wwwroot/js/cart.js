/**
 * ============================================================================
 * SIMONE E-COMMERCE - Cart Module v1.0
 * ============================================================================
 * Módulo para manejar el carrito de compras dinámicamente
 * - Actualiza el badge del carrito
 * - Recarga el contenido del panel/offcanvas
 * - Compatible con Bootstrap 5
 * ============================================================================
 */

'use strict';

const Cart = {
    // Configuración
    config: {
        endpoints: {
            info: '/Carrito/CartInfo',
            partial: '/Carrito/CartPartial',
            add: '/Carrito/AgregarAlCarrito',
            update: '/Carrito/ActualizarCarrito',
            remove: '/Carrito/EliminarDelCarrito'
        },
        selectors: {
            badge: '[data-cart-count], .cart-count, .cart-badge, #cartCount',
            panel: '#cartPanel, #cartOffcanvas, .cart-panel, .offcanvas-cart',
            panelBody: '.offcanvas-body, .cart-panel-body, .cart-items',
            total: '[data-cart-total], .cart-total, #cartTotal',
            itemList: '.cart-items-list, .cart-products'
        },
        toastDuration: 3000
    },

    /**
     * Inicializa el módulo del carrito
     */
    init() {
        this.bindEvents();
        this.updateBadge(); // Actualizar al cargar
        console.log('🛒 Cart module initialized');
    },

    /**
     * Vincula eventos globales
     */
    bindEvents() {
        // Delegación para botones de añadir al carrito en toda la página
        document.addEventListener('click', (e) => {
            const addBtn = e.target.closest('[data-add-to-cart]');
            if (addBtn) {
                e.preventDefault();
                this.handleAddToCart(addBtn);
            }
        });

        // Escuchar evento personalizado de carrito actualizado
        document.addEventListener('cart:updated', () => {
            this.updateBadge();
            this.refreshPanel();
        });
    },

    /**
     * Actualiza el badge del carrito (número de items)
     */
    async updateBadge() {
        try {
            const response = await fetch(this.config.endpoints.info, {
                method: 'GET',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'
                },
                credentials: 'same-origin'
            });

            if (!response.ok) {
                console.warn('Cart info request failed:', response.status);
                return;
            }

            const data = await response.json();
            this.setBadgeCount(data.count || 0);
            this.setTotal(data.subtotal || 0);

            console.log('🛒 Cart badge updated:', data.count);
        } catch (error) {
            console.error('Error updating cart badge:', error);
        }
    },

    /**
     * Establece el número en todos los badges del carrito
     */
    setBadgeCount(count) {
        const badges = document.querySelectorAll(this.config.selectors.badge);
        badges.forEach(badge => {
            badge.textContent = count;

            // Mostrar/ocultar badge si es 0
            if (count > 0) {
                badge.classList.remove('d-none', 'hidden');
                badge.style.display = '';
            } else {
                // Opcional: ocultar si es 0
                // badge.classList.add('d-none');
            }

            // Animación de pulso
            badge.classList.add('pulse');
            setTimeout(() => badge.classList.remove('pulse'), 300);
        });
    },

    /**
     * Establece el total en los elementos correspondientes
     */
    setTotal(total) {
        const totalElements = document.querySelectorAll(this.config.selectors.total);
        const formatted = this.formatCurrency(total);
        totalElements.forEach(el => {
            el.textContent = formatted;
        });
    },

    /**
     * Formatea un número como moneda
     */
    formatCurrency(amount) {
        return new Intl.NumberFormat('es-EC', {
            style: 'currency',
            currency: 'USD'
        }).format(amount);
    },

    /**
     * Recarga el contenido del panel del carrito
     */
    async refreshPanel() {
        const panels = document.querySelectorAll(this.config.selectors.panel);

        if (panels.length === 0) {
            console.log('No cart panel found to refresh');
            return;
        }

        try {
            const response = await fetch(this.config.endpoints.partial, {
                method: 'GET',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'
                },
                credentials: 'same-origin'
            });

            if (!response.ok) {
                // Si no existe el endpoint de partial, solo actualizamos el badge
                console.log('Cart partial endpoint not available');
                return;
            }

            const html = await response.text();

            panels.forEach(panel => {
                const body = panel.querySelector(this.config.selectors.panelBody);
                if (body) {
                    body.innerHTML = html;
                } else {
                    // Si no hay body específico, actualizar todo el panel
                    panel.innerHTML = html;
                }
            });

            console.log('🛒 Cart panel refreshed');
        } catch (error) {
            console.error('Error refreshing cart panel:', error);
        }
    },

    /**
     * Añade un producto al carrito
     */
    async add(productId, quantity = 1, variantId = null, button = null) {
        // Estado de carga en el botón
        let originalContent = null;
        if (button) {
            originalContent = button.innerHTML;
            button.disabled = true;
            button.innerHTML = '<i class="fa-solid fa-spinner fa-spin me-2"></i>Añadiendo...';
        }

        try {
            // Obtener token CSRF
            const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

            const formData = new FormData();
            formData.append('productoId', productId);
            formData.append('cantidad', quantity);
            if (variantId) {
                formData.append('productoVarianteId', variantId);
            }
            if (token) {
                formData.append('__RequestVerificationToken', token);
            }

            const response = await fetch(this.config.endpoints.add, {
                method: 'POST',
                body: formData,
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'
                },
                credentials: 'same-origin'
            });

            const data = await response.json();

            if (data.ok) {
                // Actualizar UI
                this.setBadgeCount(data.count);
                this.setTotal(data.total);

                // Refrescar panel
                await this.refreshPanel();

                // Mostrar toast de éxito
                this.showToast('success', data.message || 'Producto añadido al carrito');

                // Disparar evento personalizado
                document.dispatchEvent(new CustomEvent('cart:itemAdded', {
                    detail: { productId, quantity, variantId, data }
                }));

                return { success: true, data };
            } else if (data.needLogin) {
                // Redirigir a login
                window.location.href = '/Cuenta/Login?returnUrl=' + encodeURIComponent(window.location.pathname);
                return { success: false, needLogin: true };
            } else {
                this.showToast('error', data.error || 'Error al añadir al carrito');
                return { success: false, error: data.error };
            }
        } catch (error) {
            console.error('Error adding to cart:', error);
            this.showToast('error', 'Error de conexión');
            return { success: false, error: 'Error de conexión' };
        } finally {
            // Restaurar botón
            if (button && originalContent) {
                button.disabled = false;
                button.innerHTML = originalContent;
            }
        }
    },

    /**
     * Actualiza la cantidad de un item en el carrito
     */
    async updateQuantity(carritoDetalleId, cantidad) {
        try {
            const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

            const formData = new FormData();
            formData.append('carritoDetalleId', carritoDetalleId);
            formData.append('cantidad', cantidad);
            if (token) {
                formData.append('__RequestVerificationToken', token);
            }

            const response = await fetch(this.config.endpoints.update, {
                method: 'POST',
                body: formData,
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'
                },
                credentials: 'same-origin'
            });

            const data = await response.json();

            if (data.ok) {
                this.setBadgeCount(data.count);
                this.setTotal(data.total);

                document.dispatchEvent(new CustomEvent('cart:updated', {
                    detail: { carritoDetalleId, cantidad, data }
                }));

                return { success: true, data };
            } else {
                this.showToast('error', data.error || 'Error al actualizar');
                return { success: false, error: data.error };
            }
        } catch (error) {
            console.error('Error updating cart:', error);
            return { success: false, error: 'Error de conexión' };
        }
    },

    /**
     * Elimina un item del carrito
     */
    async remove(carritoDetalleId) {
        try {
            const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

            const formData = new FormData();
            formData.append('carritoDetalleId', carritoDetalleId);
            if (token) {
                formData.append('__RequestVerificationToken', token);
            }

            const response = await fetch(this.config.endpoints.remove, {
                method: 'POST',
                body: formData,
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'
                },
                credentials: 'same-origin'
            });

            const data = await response.json();

            if (data.ok) {
                this.setBadgeCount(data.count);
                this.setTotal(data.total);
                await this.refreshPanel();

                this.showToast('success', 'Producto eliminado del carrito');

                document.dispatchEvent(new CustomEvent('cart:itemRemoved', {
                    detail: { carritoDetalleId, data }
                }));

                return { success: true, data };
            } else {
                this.showToast('error', data.error || 'Error al eliminar');
                return { success: false, error: data.error };
            }
        } catch (error) {
            console.error('Error removing from cart:', error);
            return { success: false, error: 'Error de conexión' };
        }
    },

    /**
     * Maneja el click en botón de añadir al carrito
     */
    handleAddToCart(button) {
        const productId = button.dataset.productId || button.dataset.addToCart;
        const quantity = parseInt(button.dataset.quantity) || 1;
        const variantId = button.dataset.variantId || null;

        if (!productId) {
            console.error('No product ID found on button');
            return;
        }

        this.add(productId, quantity, variantId, button);
    },

    /**
     * Muestra un toast de notificación
     */
    showToast(type, message) {
        // Intentar usar el sistema de toast existente (Simone)
        if (window.Simone?.Toast) {
            if (type === 'success') {
                window.Simone.Toast.success(message);
            } else {
                window.Simone.Toast.error(message);
            }
            return;
        }

        // Intentar usar Bootstrap toast
        const toastId = type === 'success' ? 'toastSuccess' : 'toastError';
        const toastEl = document.getElementById(toastId);

        if (toastEl && window.bootstrap?.Toast) {
            const msgEl = toastEl.querySelector('.toast-body span, .toast-body');
            if (msgEl) {
                msgEl.textContent = message;
            }
            new bootstrap.Toast(toastEl).show();
            return;
        }

        // Fallback: crear toast dinámico
        this.createDynamicToast(type, message);
    },

    /**
     * Crea un toast dinámico si no hay uno predefinido
     */
    createDynamicToast(type, message) {
        // Buscar o crear contenedor
        let container = document.querySelector('.toast-container');
        if (!container) {
            container = document.createElement('div');
            container.className = 'toast-container position-fixed bottom-0 end-0 p-3';
            container.style.zIndex = '1100';
            document.body.appendChild(container);
        }

        const toast = document.createElement('div');
        toast.className = `toast align-items-center text-white bg-${type === 'success' ? 'success' : 'danger'} border-0`;
        toast.setAttribute('role', 'alert');
        toast.innerHTML = `
            <div class="d-flex">
                <div class="toast-body">
                    <i class="fa-solid fa-${type === 'success' ? 'check-circle' : 'exclamation-circle'} me-2"></i>
                    ${message}
                </div>
                <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast"></button>
            </div>
        `;

        container.appendChild(toast);

        if (window.bootstrap?.Toast) {
            const bsToast = new bootstrap.Toast(toast, { delay: this.config.toastDuration });
            bsToast.show();
            toast.addEventListener('hidden.bs.toast', () => toast.remove());
        } else {
            // Fallback sin Bootstrap
            toast.classList.add('show');
            setTimeout(() => {
                toast.classList.remove('show');
                setTimeout(() => toast.remove(), 300);
            }, this.config.toastDuration);
        }
    }
};

// ============================================================================
// CSS para animaciones del badge
// ============================================================================
const cartStyles = document.createElement('style');
cartStyles.textContent = `
    @keyframes cartPulse {
        0% { transform: scale(1); }
        50% { transform: scale(1.3); }
        100% { transform: scale(1); }
    }
    
    [data-cart-count].pulse,
    .cart-count.pulse,
    .cart-badge.pulse {
        animation: cartPulse 0.3s ease-in-out;
    }
    
    .cart-updating {
        opacity: 0.6;
        pointer-events: none;
    }
`;
document.head.appendChild(cartStyles);

// ============================================================================
// Inicialización automática
// ============================================================================
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => Cart.init());
} else {
    Cart.init();
}

// ============================================================================
// Exportar globalmente
// ============================================================================
window.Cart = Cart;

// También añadir a Simone si existe
if (window.Simone) {
    window.Simone.Cart = Cart;
}
