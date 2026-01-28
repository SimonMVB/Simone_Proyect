/**
 * ============================================
 * CATÁLOGO - Neo Agora E-Commerce
 * JavaScript Module v2.0
 * Ubicación: wwwroot/js/catalogo.js
 * ============================================
 */

(function() {
    'use strict';

    // ==========================================
    // CONFIGURACIÓN
    // ==========================================
    const CONFIG = {
        // Mapa de colores nombre -> hex
        colorMap: {
            // Neutros
            'negro': '#000000',
            'blanco': '#ffffff',
            'gris': '#808080',
            'gris claro': '#d3d3d3',
            'gris oscuro': '#4b5563',
            'plateado': '#c0c0c0',
            
            // Rojos
            'rojo': '#ef4444',
            'rojo vino': '#722f37',
            'granate': '#7b1e3c',
            'burdeos': '#6d071a',
            'coral': '#ff7f7f',
            
            // Azules
            'azul': '#3b82f6',
            'azul marino': '#1e3a5f',
            'celeste': '#87ceeb',
            'azul cielo': '#87ceeb',
            'turquesa': '#40e0d0',
            'cian': '#06b6d4',
            'azul rey': '#4169e1',
            
            // Verdes
            'verde': '#22c55e',
            'verde lima': '#84cc16',
            'verde menta': '#3eb489',
            'oliva': '#556b2f',
            'verde oscuro': '#166534',
            'esmeralda': '#50c878',
            
            // Amarillos/Naranjas
            'amarillo': '#eab308',
            'mostaza': '#ca8a04',
            'dorado': '#d4af37',
            'naranja': '#f97316',
            'durazno': '#ffcc99',
            'salmon': '#fa8072',
            
            // Marrones
            'marron': '#8b4513',
            'marrón': '#8b4513',
            'café': '#8b4513',
            'chocolate': '#7b3f00',
            'beige': '#f5f5dc',
            'crema': '#fffdd0',
            'camel': '#c19a6b',
            
            // Morados/Rosas
            'morado': '#9333ea',
            'violeta': '#7c3aed',
            'lila': '#c8a2c8',
            'lavanda': '#e6e6fa',
            'rosa': '#ec4899',
            'fucsia': '#d946ef',
            'magenta': '#ff00ff',
            'rosado': '#ffc0cb'
        },
        
        // Orden de tallas
        sizeOrder: ['XXXS', 'XXS', 'XS', 'S', 'M', 'L', 'XL', 'XXL', 'XXXL', 'XXXXL', '4XL', '5XL'],
        
        // Endpoints API
        endpoints: {
            addToCart: '/Carrito/AgregarDesdeCard',
            cartInfo: '/Carrito/CartInfo',
            toggleWishlist: '/Favoritos/Toggle'
        },
        
        // Selectores
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
        /**
         * Normaliza string para comparación
         */
        normalize(str) {
            return (str || '').toString().trim().toLowerCase()
                .normalize('NFD')
                .replace(/[\u0300-\u036f]/g, '');
        },

        /**
         * Verifica si es color hexadecimal
         */
        isHexColor(str) {
            return /^#([0-9a-f]{3}|[0-9a-f]{6})$/i.test((str || '').trim());
        },

        /**
         * Obtiene color hex desde nombre
         */
        getColorHex(colorName) {
            if (this.isHexColor(colorName)) {
                return colorName;
            }
            return CONFIG.colorMap[this.normalize(colorName)] || null;
        },

        /**
         * Compara tallas para ordenamiento
         */
        compareSize(a, b) {
            const numA = parseFloat(a);
            const numB = parseFloat(b);
            
            // Si ambos son números
            if (!isNaN(numA) && !isNaN(numB)) {
                return numA - numB;
            }
            
            const indexA = CONFIG.sizeOrder.indexOf((a || '').toUpperCase());
            const indexB = CONFIG.sizeOrder.indexOf((b || '').toUpperCase());
            
            // Si están en el array de orden
            if (indexA !== -1 || indexB !== -1) {
                return (indexA === -1 ? 999 : indexA) - (indexB === -1 ? 999 : indexB);
            }
            
            // Orden alfabético como fallback
            return (a || '').localeCompare(b || '');
        },

        /**
         * Formatea precio en moneda
         */
        formatCurrency(amount) {
            return new Intl.NumberFormat('es-EC', {
                style: 'currency',
                currency: 'USD'
            }).format(amount);
        },

        /**
         * Debounce function
         */
        debounce(func, wait) {
            let timeout;
            return function executedFunction(...args) {
                const later = () => {
                    clearTimeout(timeout);
                    func(...args);
                };
                clearTimeout(timeout);
                timeout = setTimeout(later, wait);
            };
        },

        /**
         * Obtiene CSRF token
         */
        getCsrfToken() {
            const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
            return tokenInput ? tokenInput.value : '';
        }
    };

    // ==========================================
    // MÓDULO DE COLORES
    // ==========================================
    const ColorModule = {
        /**
         * Pinta todos los color dots
         */
        paintColorDots() {
            document.querySelectorAll(CONFIG.selectors.colorDot).forEach(element => {
                const colorName = element.getAttribute('data-color');
                if (!colorName) return;

                const hexColor = Utils.getColorHex(colorName);
                if (hexColor) {
                    element.style.backgroundColor = hexColor;
                    
                    // Si es blanco, agregar borde visible
                    if (hexColor.toLowerCase() === '#ffffff' || hexColor.toLowerCase() === '#fff') {
                        element.style.border = '2px solid #e5e7eb';
                    }
                }
            });
        }
    };

    // ==========================================
    // MÓDULO DE CARRITO
    // ==========================================
    const CartModule = {
        /**
         * Agrega producto al carrito
         */
        async addToCart(productId, variantId, quantity = 1, button = null) {
            if (button) {
                button.classList.add('btn-loading');
                button.disabled = true;
            }

            try {
                const formData = new FormData();
                formData.append('productoID', productId);
                if (variantId) {
                    formData.append('varianteID', variantId);
                }
                formData.append('cantidad', quantity);
                formData.append('__RequestVerificationToken', Utils.getCsrfToken());

                const response = await fetch(CONFIG.endpoints.addToCart, {
                    method: 'POST',
                    body: formData,
                    credentials: 'same-origin',
                    headers: {
                        'X-Requested-With': 'XMLHttpRequest'
                    }
                });

                const data = await response.json();

                if (data.success) {
                    ToastModule.showSuccess(data.message || 'Producto añadido al carrito');
                    await this.refreshCartBadge();
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
        },

        /**
         * Actualiza badge del carrito
         */
        async refreshCartBadge() {
            try {
                const response = await fetch(CONFIG.endpoints.cartInfo, {
                    credentials: 'same-origin'
                });

                if (!response.ok) return;

                const data = await response.json();

                // Actualizar contador
                const countElements = document.querySelectorAll('#cartCount, #cart-count, .cart-count');
                countElements.forEach(el => {
                    if (data.count != null) {
                        el.textContent = data.count;
                        el.style.display = data.count > 0 ? '' : 'none';
                    }
                });

                // Actualizar total
                const totalElements = document.querySelectorAll('#cartTotal, #cart-total, .cart-total');
                totalElements.forEach(el => {
                    if (data.subtotal != null) {
                        el.textContent = Utils.formatCurrency(data.subtotal);
                    }
                });
            } catch (error) {
                console.error('Error refreshing cart badge:', error);
            }
        }
    };

    // ==========================================
    // MÓDULO DE WISHLIST
    // ==========================================
    const WishlistModule = {
        /**
         * Inicializa formularios de wishlist
         */
        init() {
            document.querySelectorAll(CONFIG.selectors.wishlistForm).forEach(form => {
                form.addEventListener('submit', this.handleSubmit.bind(this));
            });
        },

        /**
         * Maneja submit de wishlist
         */
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
                    headers: {
                        'X-Requested-With': 'XMLHttpRequest'
                    }
                });

                const data = await response.json();

                if (icon) {
                    if (data?.esFavorito) {
                        icon.classList.remove('far');
                        icon.classList.add('fas');
                        button.classList.add('is-active');
                    } else {
                        icon.classList.remove('fas');
                        icon.classList.add('far');
                        button.classList.remove('is-active');
                    }
                }
            } catch (error) {
                console.error('Error toggling wishlist:', error);
            } finally {
                button.disabled = false;
            }
        }
    };

    // ==========================================
    // MÓDULO DE TOASTS
    // ==========================================
    const ToastModule = {
        /**
         * Muestra toast de éxito
         */
        showSuccess(message = 'Producto añadido al carrito') {
            this._showToast('toastSuccess', message, 'success');
        },

        /**
         * Muestra toast de error
         */
        showError(message = 'No se pudo completar la acción') {
            this._showToast('toastError', message, 'error');
        },

        /**
         * Muestra toast genérico
         */
        _showToast(toastId, message, type) {
            const toastEl = document.getElementById(toastId);
            if (!toastEl) {
                // Crear toast dinámicamente si no existe
                this._createToast(message, type);
                return;
            }

            const messageEl = toastEl.querySelector('.toast-body');
            if (messageEl) {
                messageEl.textContent = message;
            }

            try {
                const toast = bootstrap.Toast.getOrCreateInstance(toastEl, {
                    autohide: true,
                    delay: 4000
                });
                toast.show();
            } catch (error) {
                console.error('Error showing toast:', error);
            }
        },

        /**
         * Crea toast dinámicamente
         */
        _createToast(message, type) {
            // Crear contenedor si no existe
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
                    <button type="button" class="btn-close btn-close-white" data-bs-dismiss="toast" aria-label="Cerrar"></button>
                </div>
                <div class="toast-body">${message}</div>
            `;

            container.appendChild(toast);

            const bsToast = new bootstrap.Toast(toast, {
                autohide: true,
                delay: 4000
            });
            
            bsToast.show();

            // Remover después de ocultar
            toast.addEventListener('hidden.bs.toast', () => {
                toast.remove();
            });
        }
    };

    // ==========================================
    // MÓDULO DE TARJETAS DE PRODUCTO
    // ==========================================
    const ProductCardModule = {
        /**
         * Inicializa todas las tarjetas
         */
        init() {
            document.querySelectorAll(CONFIG.selectors.productCard).forEach(card => {
                this.initCard(card);
            });
        },

        /**
         * Inicializa una tarjeta individual
         */
        initCard(card) {
            const variantsJson = card.querySelector('[data-variants-json]');
            if (!variantsJson) return;

            let variants = [];
            try {
                variants = JSON.parse(variantsJson.textContent || '[]');
            } catch (e) {
                console.error('Error parsing variants:', e);
                return;
            }

            if (!variants.length) return;

            // Estado de la tarjeta
            const state = {
                selectedColor: null,
                selectedSize: null,
                selectedVariant: null
            };

            // Obtener elementos
            const colorButtons = card.querySelectorAll('[data-color-select]');
            const sizeButtons = card.querySelectorAll('[data-size-select]');
            const addButton = card.querySelector('[data-add-to-cart]');
            const priceElement = card.querySelector('[data-price]');

            // Inicializar colores
            colorButtons.forEach(btn => {
                btn.addEventListener('click', () => {
                    state.selectedColor = btn.dataset.colorSelect;
                    this.updateColorSelection(card, state.selectedColor);
                    this.updateAvailableSizes(card, variants, state);
                    this.updateSelectedVariant(variants, state, priceElement);
                });
            });

            // Inicializar tallas
            sizeButtons.forEach(btn => {
                btn.addEventListener('click', () => {
                    if (btn.disabled) return;
                    state.selectedSize = btn.dataset.sizeSelect;
                    this.updateSizeSelection(card, state.selectedSize);
                    this.updateSelectedVariant(variants, state, priceElement);
                });
            });

            // Inicializar botón añadir
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

        /**
         * Actualiza selección de color
         */
        updateColorSelection(card, selectedColor) {
            card.querySelectorAll('[data-color-select]').forEach(btn => {
                btn.classList.toggle('active', btn.dataset.colorSelect === selectedColor);
            });
        },

        /**
         * Actualiza selección de talla
         */
        updateSizeSelection(card, selectedSize) {
            card.querySelectorAll('[data-size-select]').forEach(btn => {
                btn.classList.toggle('active', btn.dataset.sizeSelect === selectedSize);
            });
        },

        /**
         * Actualiza tallas disponibles según color
         */
        updateAvailableSizes(card, variants, state) {
            const sizeButtons = card.querySelectorAll('[data-size-select]');
            
            sizeButtons.forEach(btn => {
                const size = btn.dataset.sizeSelect;
                const available = variants.some(v => 
                    Utils.normalize(v.color) === Utils.normalize(state.selectedColor) &&
                    Utils.normalize(v.talla) === Utils.normalize(size) &&
                    v.stock > 0
                );
                
                btn.disabled = !available;
                btn.classList.toggle('disabled', !available);
            });

            // Reset talla si no está disponible
            if (state.selectedSize) {
                const stillAvailable = variants.some(v =>
                    Utils.normalize(v.color) === Utils.normalize(state.selectedColor) &&
                    Utils.normalize(v.talla) === Utils.normalize(state.selectedSize) &&
                    v.stock > 0
                );
                
                if (!stillAvailable) {
                    state.selectedSize = null;
                    this.updateSizeSelection(card, null);
                }
            }
        },

        /**
         * Actualiza variante seleccionada
         */
        updateSelectedVariant(variants, state, priceElement) {
            if (!state.selectedColor || !state.selectedSize) {
                state.selectedVariant = null;
                return;
            }

            state.selectedVariant = variants.find(v =>
                Utils.normalize(v.color) === Utils.normalize(state.selectedColor) &&
                Utils.normalize(v.talla) === Utils.normalize(state.selectedSize) &&
                v.stock > 0
            );

            // Actualizar precio si hay variante seleccionada
            if (state.selectedVariant && priceElement && state.selectedVariant.precio) {
                priceElement.textContent = Utils.formatCurrency(state.selectedVariant.precio);
            }
        }
    };

    // ==========================================
    // MÓDULO DE FORMULARIOS
    // ==========================================
    const FormsModule = {
        /**
         * Inicializa los selects del toolbar
         */
        init(currentSort, currentPageSize) {
            const sortSelect = document.querySelector(CONFIG.selectors.sortSelect);
            const pageSizeSelect = document.querySelector(CONFIG.selectors.pageSizeSelect);

            if (sortSelect && currentSort) {
                sortSelect.value = currentSort;
            }

            if (pageSizeSelect && currentPageSize) {
                pageSizeSelect.value = currentPageSize;
            }
        }
    };

    // ==========================================
    // INICIALIZACIÓN PRINCIPAL
    // ==========================================
    const CatalogApp = {
        /**
         * Inicializa la aplicación
         */
        init(options = {}) {
            const { currentSort = '', currentPageSize = 12 } = options;

            // Pintar colores
            ColorModule.paintColorDots();

            // Inicializar módulos
            FormsModule.init(currentSort, currentPageSize);
            WishlistModule.init();
            ProductCardModule.init();

            // Log de inicialización
            console.log('✓ Catálogo inicializado');
            if (options.totalProducts) {
                console.log(`  - Productos: ${options.totalProducts}`);
            }
            if (options.activeFilters) {
                console.log(`  - Filtros activos: ${options.activeFilters}`);
            }
        },

        // Exponer módulos para uso externo
        Cart: CartModule,
        Toast: ToastModule,
        Utils: Utils
    };

    // Exponer globalmente
    window.CatalogApp = CatalogApp;

    // Auto-inicialización cuando el DOM esté listo
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => {
            // La inicialización real se hace desde el CSHTML con los parámetros correctos
        });
    }
})();
