/**
 * ============================================
 * CATÁLOGO - Neo Agora E-Commerce
 * JavaScript Module v2.0
 * Ubicación: wwwroot/js/catalogo.js
 * ============================================
 */

(function () {
    'use strict';

    // ==========================================
    // CONFIGURACIÓN
    // ==========================================
    const CONFIG = {
        colorMap: {
            'negro': '#000000', 'blanco': '#ffffff', 'gris': '#808080',
            'gris claro': '#d3d3d3', 'gris oscuro': '#4b5563', 'plateado': '#c0c0c0',
            'rojo': '#ef4444', 'rojo vino': '#722f37', 'granate': '#7b1e3c',
            'burdeos': '#6d071a', 'coral': '#ff7f7f',
            'azul': '#3b82f6', 'azul marino': '#1e3a5f', 'celeste': '#87ceeb',
            'azul cielo': '#87ceeb', 'turquesa': '#40e0d0', 'cian': '#06b6d4',
            'azul rey': '#4169e1',
            'verde': '#22c55e', 'verde lima': '#84cc16', 'verde menta': '#3eb489',
            'oliva': '#556b2f', 'verde oscuro': '#166534', 'esmeralda': '#50c878',
            'amarillo': '#eab308', 'mostaza': '#ca8a04', 'dorado': '#d4af37',
            'naranja': '#f97316', 'durazno': '#ffcc99', 'salmon': '#fa8072',
            'marron': '#8b4513', 'marrón': '#8b4513', 'café': '#8b4513',
            'chocolate': '#7b3f00', 'beige': '#f5f5dc', 'crema': '#fffdd0',
            'camel': '#c19a6b',
            'morado': '#9333ea', 'violeta': '#7c3aed', 'lila': '#c8a2c8',
            'lavanda': '#e6e6fa', 'rosa': '#ec4899', 'fucsia': '#d946ef',
            'magenta': '#ff00ff', 'rosado': '#ffc0cb'
        },
        sizeOrder: ['XXXS', 'XXS', 'XS', 'S', 'M', 'L', 'XL', 'XXL', 'XXXL', 'XXXXL', '4XL', '5XL'],
        endpoints: {
            addToCart: '/Carrito/AgregarDesdeCard',
            cartInfo: '/Carrito/CartInfo',
            cartPartial: '/Carrito/CartPartial',
            toggleWishlist: '/Favoritos/Toggle'
        },
        selectors: {
            productCard: '[data-product-card]',
            productGrid: '#productGrid',
            colorDot: '[data-color]',
            sortSelect: '#sortSel',
            pageSizeSelect: '#pageSizeSel',
            wishlistForm: '.wishlist-form',
            addToCartBtn: '[data-add-to-cart]',
            toastSuccess: '#toastSuccess',
            toastError: '#toastError'
        }
    };

    // ==========================================
    // UTILIDADES
    // ==========================================
    const Utils = {
        normalize(str) {
            return (str || '').toString().trim().toLowerCase()
                .normalize('NFD').replace(/[\u0300-\u036f]/g, '');
        },
        isHexColor(str) {
            return /^#([0-9a-f]{3}|[0-9a-f]{6})$/i.test((str || '').trim());
        },
        getColorHex(colorName) {
            if (this.isHexColor(colorName)) return colorName;
            return CONFIG.colorMap[this.normalize(colorName)] || null;
        },
        compareSize(a, b) {
            const numA = parseFloat(a), numB = parseFloat(b);
            if (!isNaN(numA) && !isNaN(numB)) return numA - numB;
            const indexA = CONFIG.sizeOrder.indexOf((a || '').toUpperCase());
            const indexB = CONFIG.sizeOrder.indexOf((b || '').toUpperCase());
            if (indexA !== -1 || indexB !== -1)
                return (indexA === -1 ? 999 : indexA) - (indexB === -1 ? 999 : indexB);
            return (a || '').localeCompare(b || '');
        },
        formatCurrency(amount) {
            return new Intl.NumberFormat('es-EC', { style: 'currency', currency: 'USD' }).format(amount);
        },
        debounce(func, wait) {
            let timeout;
            return function (...args) {
                clearTimeout(timeout);
                timeout = setTimeout(() => func(...args), wait);
            };
        },
        getCsrfToken() {
            return document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';
        }
    };

    // ==========================================
    // MÓDULO DE COLORES
    // ==========================================
    const ColorModule = {
        paintColorDots() {
            document.querySelectorAll(CONFIG.selectors.colorDot).forEach(el => {
                const colorName = el.getAttribute('data-color');
                if (!colorName) return;
                const hex = Utils.getColorHex(colorName);
                if (hex) {
                    el.style.backgroundColor = hex;
                    if (hex.toLowerCase() === '#ffffff' || hex.toLowerCase() === '#fff')
                        el.style.border = '2px solid #e5e7eb';
                }
            });
        }
    };

    // ==========================================
    // MÓDULO DE CARRITO
    // ==========================================
    const CartModule = {

        /**
         * Recarga el contenido del panel/dropdown del carrito
         */
        async refreshCartPanel() {
            try {
                // Busca el contenedor del carrito con múltiples selectores para
                // compatibilidad con distintos layouts
                const panelBody = document.querySelector(
                    '#cart-content, ' +
                    '#cartOffcanvas .offcanvas-body, ' +
                    '#cartDrawer .offcanvas-body, ' +
                    '.cart-items-container, ' +
                    '[data-cart-panel] .offcanvas-body, ' +
                    '[data-cart-panel]'
                );
                if (!panelBody) return;

                const res = await fetch(CONFIG.endpoints.cartPartial, {
                    credentials: 'same-origin',
                    headers: { 'X-Requested-With': 'XMLHttpRequest' }
                });
                if (res.ok) {
                    panelBody.innerHTML = await res.text();
                }
            } catch (e) {
                console.warn('No se pudo recargar el panel del carrito:', e);
            }
        },

        /**
         * Actualiza solo el badge (número) del carrito
         */
        async refreshCartBadge() {
            try {
                const res = await fetch(CONFIG.endpoints.cartInfo, {
                    credentials: 'same-origin',
                    headers: { 'X-Requested-With': 'XMLHttpRequest' }
                });
                if (!res.ok) return;
                const data = await res.json();

                document.querySelectorAll('#cartCount, #cart-count, .cart-count, [data-cart-count], .cart-badge')
                    .forEach(el => {
                        if (data.count != null) {
                            el.textContent = data.count;
                            el.style.display = data.count > 0 ? '' : 'none';
                        }
                    });

                document.querySelectorAll('#cartTotal, #cart-total, .cart-total')
                    .forEach(el => {
                        if (data.subtotal != null)
                            el.textContent = Utils.formatCurrency(data.subtotal);
                    });
            } catch (e) {
                console.error('Error refreshing cart badge:', e);
            }
        },

        /**
         * Agrega producto al carrito y refresca badge + panel
         */
        async addToCart(productId, variantId, quantity = 1, button = null) {
            if (button) {
                button.classList.add('btn-loading');
                button.disabled = true;
            }

            try {
                const params = new URLSearchParams();
                params.append('productoID', productId);
                if (variantId) params.append('varianteID', variantId);
                params.append('cantidad', quantity);
                params.append('__RequestVerificationToken', Utils.getCsrfToken());

                const response = await fetch(CONFIG.endpoints.addToCart, {
                    method: 'POST',
                    body: params,
                    credentials: 'same-origin',
                    headers: {
                        'Content-Type': 'application/x-www-form-urlencoded',
                        'X-Requested-With': 'XMLHttpRequest'
                    }
                });

                const data = await response.json();

                if (data.success) {
                    ToastModule.showSuccess(data.message || '¡Producto añadido al carrito!');

                    // Actualizar badge y recargar panel en paralelo
                    await Promise.all([
                        this.refreshCartBadge(),
                        this.refreshCartPanel()
                    ]);

                    return true;
                } else {
                    ToastModule.showError(data.message || 'No se pudo añadir el producto');
                    return false;
                }
            } catch (error) {
                console.error('Error adding to cart:', error);
                ToastModule.showError('Error de conexión. Intenta de nuevo.');
                return false;
            } finally {
                if (button) {
                    button.classList.remove('btn-loading');
                    button.disabled = false;
                }
            }
        }
    };

    // ==========================================
    // MÓDULO DE WISHLIST
    // ==========================================
    const WishlistModule = {
        init() {
            document.querySelectorAll(CONFIG.selectors.wishlistForm).forEach(form => {
                form.addEventListener('submit', this.handleSubmit.bind(this));
            });
        },
        async handleSubmit(event) {
            event.preventDefault();
            const form = event.currentTarget;
            const button = form.querySelector('button');
            const icon = button?.querySelector('i');
            try {
                button.disabled = true;
                const response = await fetch(form.action, {
                    method: 'POST',
                    body: new FormData(form),
                    credentials: 'same-origin',
                    headers: { 'X-Requested-With': 'XMLHttpRequest' }
                });
                const data = await response.json();
                if (icon) {
                    if (data?.esFavorito) {
                        icon.classList.replace('far', 'fas');
                        button.classList.add('is-active');
                    } else {
                        icon.classList.replace('fas', 'far');
                        button.classList.remove('is-active');
                    }
                }
            } catch (e) {
                console.error('Error toggling wishlist:', e);
            } finally {
                button.disabled = false;
            }
        }
    };

    // ==========================================
    // MÓDULO DE TOASTS
    // ==========================================
    const ToastModule = {
        showSuccess(message = 'Producto añadido al carrito') {
            this._showToast('toastSuccess', message, 'success');
        },
        showError(message = 'No se pudo completar la acción') {
            this._showToast('toastError', message, 'error');
        },
        _showToast(toastId, message, type) {
            const toastEl = document.getElementById(toastId);
            if (!toastEl) { this._createToast(message, type); return; }
            const messageEl = toastEl.querySelector('.toast-body');
            if (messageEl) messageEl.textContent = message;
            try {
                bootstrap.Toast.getOrCreateInstance(toastEl, { autohide: true, delay: 4000 }).show();
            } catch (e) { console.error('Error showing toast:', e); }
        },
        _createToast(message, type) {
            let container = document.querySelector('.toast-container');
            if (!container) {
                container = document.createElement('div');
                container.className = 'toast-container position-fixed top-0 end-0 p-3';
                container.style.zIndex = '9999';
                document.body.appendChild(container);
            }
            const isSuccess = type === 'success';
            const toast = document.createElement('div');
            toast.className = 'toast';
            toast.setAttribute('role', 'status');
            toast.innerHTML = `
                <div class="toast-header ${isSuccess ? 'bg-success' : 'bg-danger'} text-white">
                    <i class="fa-solid ${isSuccess ? 'fa-circle-check' : 'fa-triangle-exclamation'} me-2"></i>
                    <strong class="me-auto">${isSuccess ? 'Éxito' : 'Error'}</strong>
                    <button type="button" class="btn-close btn-close-white" data-bs-dismiss="toast"></button>
                </div>
                <div class="toast-body">${message}</div>`;
            container.appendChild(toast);
            const bsToast = new bootstrap.Toast(toast, { autohide: true, delay: 4000 });
            bsToast.show();
            toast.addEventListener('hidden.bs.toast', () => toast.remove());
        }
    };

    // ==========================================
    // MÓDULO DE TARJETAS DE PRODUCTO
    // ==========================================
    const ProductCardModule = {
        init() {
            document.querySelectorAll(CONFIG.selectors.productCard).forEach(card => this.initCard(card));
        },
        initCard(card) {
            const variantsJson = card.querySelector('[data-variants-json]');
            if (!variantsJson) return;
            let variants = [];
            try { variants = JSON.parse(variantsJson.textContent || '[]'); }
            catch (e) { console.error('Error parsing variants:', e); return; }
            if (!variants.length) return;

            const state = { selectedColor: null, selectedSize: null, selectedVariant: null };
            const colorButtons = card.querySelectorAll('[data-color-select]');
            const sizeButtons = card.querySelectorAll('[data-size-select]');
            const addButton = card.querySelector('[data-add-to-cart]');
            const priceElement = card.querySelector('[data-price]');

            colorButtons.forEach(btn => {
                btn.addEventListener('click', () => {
                    state.selectedColor = btn.dataset.colorSelect;
                    this.updateColorSelection(card, state.selectedColor);
                    this.updateAvailableSizes(card, variants, state);
                    this.updateSelectedVariant(variants, state, priceElement);
                });
            });
            sizeButtons.forEach(btn => {
                btn.addEventListener('click', () => {
                    if (btn.disabled) return;
                    state.selectedSize = btn.dataset.sizeSelect;
                    this.updateSizeSelection(card, state.selectedSize);
                    this.updateSelectedVariant(variants, state, priceElement);
                });
            });
            if (addButton) {
                addButton.addEventListener('click', async () => {
                    const productId = card.dataset.productId;
                    const variantId = state.selectedVariant?.id;
                    if (variants.length > 0 && !variantId) {
                        ToastModule.showError('Selecciona color y talla');
                        return;
                    }
                    await CartModule.addToCart(productId, variantId, 1, addButton);
                });
            }
        },
        updateColorSelection(card, selectedColor) {
            card.querySelectorAll('[data-color-select]').forEach(btn =>
                btn.classList.toggle('active', btn.dataset.colorSelect === selectedColor));
        },
        updateSizeSelection(card, selectedSize) {
            card.querySelectorAll('[data-size-select]').forEach(btn =>
                btn.classList.toggle('active', btn.dataset.sizeSelect === selectedSize));
        },
        updateAvailableSizes(card, variants, state) {
            card.querySelectorAll('[data-size-select]').forEach(btn => {
                const size = btn.dataset.sizeSelect;
                const available = variants.some(v =>
                    Utils.normalize(v.color) === Utils.normalize(state.selectedColor) &&
                    Utils.normalize(v.talla) === Utils.normalize(size) &&
                    v.stock > 0);
                btn.disabled = !available;
                btn.classList.toggle('disabled', !available);
            });
            if (state.selectedSize) {
                const stillAvailable = variants.some(v =>
                    Utils.normalize(v.color) === Utils.normalize(state.selectedColor) &&
                    Utils.normalize(v.talla) === Utils.normalize(state.selectedSize) &&
                    v.stock > 0);
                if (!stillAvailable) {
                    state.selectedSize = null;
                    this.updateSizeSelection(card, null);
                }
            }
        },
        updateSelectedVariant(variants, state, priceElement) {
            if (!state.selectedColor || !state.selectedSize) {
                state.selectedVariant = null;
                return;
            }
            state.selectedVariant = variants.find(v =>
                Utils.normalize(v.color) === Utils.normalize(state.selectedColor) &&
                Utils.normalize(v.talla) === Utils.normalize(state.selectedSize) &&
                v.stock > 0) || null;
            if (state.selectedVariant && priceElement && state.selectedVariant.precio)
                priceElement.textContent = Utils.formatCurrency(state.selectedVariant.precio);
        }
    };

    // ==========================================
    // MÓDULO DE FORMULARIOS
    // ==========================================
    const FormsModule = {
        init(currentSort, currentPageSize) {
            const sortSelect = document.querySelector(CONFIG.selectors.sortSelect);
            const pageSizeSelect = document.querySelector(CONFIG.selectors.pageSizeSelect);
            if (sortSelect && currentSort) sortSelect.value = currentSort;
            if (pageSizeSelect && currentPageSize) pageSizeSelect.value = currentPageSize;
        }
    };

    // ==========================================
    // INICIALIZACIÓN PRINCIPAL
    // ==========================================
    const CatalogApp = {
        init(options = {}) {
            const { currentSort = '', currentPageSize = 12 } = options;
            ColorModule.paintColorDots();
            FormsModule.init(currentSort, currentPageSize);
            WishlistModule.init();
            ProductCardModule.init();
            console.log('✓ Catálogo inicializado');
        },
        Cart: CartModule,
        Toast: ToastModule,
        Utils: Utils
    };

    window.CatalogApp = CatalogApp;

})();
