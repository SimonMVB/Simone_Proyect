/**
 * LAYOUT.JS - Neo Agora
 * JavaScript para el layout principal
 * Versión Enterprise Optimizada
 */

(function () {
    'use strict';

    // ======= CONFIGURACIÓN =======
    const CONFIG = {
        selectors: {
            sidebar: '#sidebar',
            cartPanel: '#cartPanel',
            overlay: '#globalOverlay',
            openSidebar: '#open-sidebar',
            closeSidebar: '#close-sidebar',
            openCart: '#open-cart',
            closeCart: '#close-cart',
            cartContent: '#cart-content',
            cartCount: '#cart-count',
            backToTop: '#backToTop'
        },
        classes: {
            show: 'show',
            loading: 'loading',
            noScroll: 'no-scroll'
        },
        endpoints: {
            cartInfo: '/Compras/CartInfo',
            cartMini: '/Compras/Mini'
        }
    };

    // ======= ESTADO GLOBAL =======
    const STATE = {
        isCartLocked: false,
        activePanel: null,
        isLoading: false
    };

    // ======= ELEMENTOS DEL DOM =======
    const DOM = {};

    /**
     * Inicializa referencias del DOM
     */
    function initializeDOM() {
        Object.keys(CONFIG.selectors).forEach(key => {
            DOM[key] = document.querySelector(CONFIG.selectors[key]);
        });

        // Inicializar estado de cart locked
        STATE.isCartLocked = window.NeoAgoraConfig?.cartLocked === true;
    }

    // ======= UTILIDADES =======
    const utils = {
        /**
         * Formateador de moneda
         */
        formatCurrency: (amount) => {
            return new Intl.NumberFormat('es-EC', {
                style: 'currency',
                currency: 'USD'
            }).format(amount || 0);
        },

        /**
         * Fetch con manejo de errores
         */
        fetchJSON: async (url, options = {}) => {
            try {
                const response = await fetch(url, {
                    credentials: 'same-origin',
                    headers: {
                        'X-Requested-With': 'XMLHttpRequest',
                        ...options.headers
                    },
                    ...options
                });

                if (!response.ok) throw new Error(`HTTP ${response.status}`);
                return await response.json();
            } catch (error) {
                console.error('Fetch error:', error);
                throw error;
            }
        },

        /**
         * Bloquear/desbloquear scroll del body
         */
        lockScroll: (lock) => {
            document.body.classList.toggle(CONFIG.classes.noScroll, lock);
        },

        /**
         * Establecer aria-expanded
         */
        setAriaExpanded: (element, expanded) => {
            if (element) {
                element.setAttribute('aria-expanded', expanded.toString());
            }
        },

        /**
         * Mostrar estado de carga
         */
        showLoading: (element) => {
            if (element) {
                element.classList.add(CONFIG.classes.loading);
                element.disabled = true;
            }
        },

        /**
         * Ocultar estado de carga
         */
        hideLoading: (element) => {
            if (element) {
                element.classList.remove(CONFIG.classes.loading);
                element.disabled = false;
            }
        },

        /**
         * Debounce function
         */
        debounce: (func, wait) => {
            let timeout;
            return function executedFunction(...args) {
                const later = () => {
                    clearTimeout(timeout);
                    func(...args);
                };
                clearTimeout(timeout);
                timeout = setTimeout(later, wait);
            };
        }
    };

    // ======= GESTIÓN DE PANELES =======
    const panelManager = {
        /**
         * Abrir panel
         */
        open: (panel, trigger) => {
            if (!panel || STATE.isLoading) return;

            // Cerrar panel activo si existe y es diferente
            if (STATE.activePanel && STATE.activePanel !== panel) {
                panelManager.close(STATE.activePanel);
            }

            panel.classList.add(CONFIG.classes.show);
            utils.setAriaExpanded(trigger, true);
            utils.lockScroll(true);

            // Mostrar overlay
            if (DOM.overlay) {
                DOM.overlay.classList.add(CONFIG.classes.show);
                DOM.overlay.setAttribute('aria-hidden', 'false');
            }

            STATE.activePanel = panel;
        },

        /**
         * Cerrar panel
         */
        close: (panel) => {
            if (!panel) return;

            panel.classList.remove(CONFIG.classes.show);

            // Actualizar aria-expanded de los triggers
            const triggers = document.querySelectorAll(`[aria-controls="${panel.id}"]`);
            triggers.forEach(trigger => utils.setAriaExpanded(trigger, false));

            utils.lockScroll(false);

            // Ocultar overlay si no hay paneles activos
            if (DOM.overlay && !document.querySelector('.sidebar.show, .cart-panel.show')) {
                DOM.overlay.classList.remove(CONFIG.classes.show);
                DOM.overlay.setAttribute('aria-hidden', 'true');
            }

            STATE.activePanel = null;
        },

        /**
         * Cerrar todos los paneles
         */
        closeAll: () => {
            [DOM.sidebar, DOM.cartPanel].forEach(panel => {
                if (panel) panel.classList.remove(CONFIG.classes.show);
            });

            utils.lockScroll(false);

            if (DOM.overlay) {
                DOM.overlay.classList.remove(CONFIG.classes.show);
                DOM.overlay.setAttribute('aria-hidden', 'true');
            }

            STATE.activePanel = null;
        },

        /**
         * Toggle panel
         */
        toggle: (panel, trigger) => {
            if (!panel) return;

            if (panel.classList.contains(CONFIG.classes.show)) {
                panelManager.close(panel);
            } else {
                panelManager.open(panel, trigger);
            }
        }
    };

    // ======= GESTIÓN DEL CARRITO =======
    const cartManager = {
        /**
         * Actualizar badge del carrito
         */
        updateBadge: async () => {
            try {
                const endpoints = window.NeoAgoraConfig?.endpoints || CONFIG.endpoints;
                const data = await utils.fetchJSON(endpoints.cartInfo);

                if (DOM.cartCount && data.count != null) {
                    DOM.cartCount.textContent = data.count;

                    // Actualizar aria-label del botón carrito
                    if (DOM.openCart) {
                        DOM.openCart.setAttribute('aria-label', `Ver carrito (${data.count} items)`);
                    }
                }
            } catch (error) {
                console.warn('No se pudo actualizar el badge del carrito:', error);
            }
        },

        /**
         * Refrescar contenido del carrito
         */
        refreshContent: async () => {
            try {
                const endpoints = window.NeoAgoraConfig?.endpoints || CONFIG.endpoints;
                const response = await fetch(endpoints.cartMini, {
                    credentials: 'same-origin',
                    headers: { 'X-Requested-With': 'XMLHttpRequest' }
                });

                if (response.ok) {
                    const html = await response.text();
                    if (DOM.cartContent) {
                        DOM.cartContent.innerHTML = html;
                    }
                }
            } catch (error) {
                console.warn('No se pudo actualizar el contenido del carrito:', error);
            }
        },

        /**
         * Acción después de modificar el carrito
         */
        afterMutation: async () => {
            await cartManager.updateBadge();
            await cartManager.refreshContent();

            // Sincronizar entre pestañas usando localStorage
            try {
                localStorage.setItem('simone_cart_updated', Date.now().toString());
            } catch (e) {
                // LocalStorage no disponible
            }
        },

        /**
         * Manejar bloqueo del carrito
         */
        handleLock: () => {
            if (!STATE.isCartLocked) return;

            // Deshabilitar interacciones con el carrito
            const cartElements = document.querySelectorAll('#open-cart, [data-cart-open], .cart-toggle');
            cartElements.forEach(el => {
                el.style.pointerEvents = 'none';
                el.style.opacity = '0.5';
                el.title = 'Carrito bloqueado durante el pago';
            });

            // Prevenir apertura del panel
            if (DOM.openCart) {
                DOM.openCart.addEventListener('click', (e) => {
                    e.preventDefault();
                    e.stopPropagation();
                    console.log('Carrito bloqueado durante el pago');
                }, true);
            }
        }
    };

    // ======= BACK TO TOP =======
    const backToTop = {
        /**
         * Scroll to top
         */
        scrollToTop: () => {
            window.scrollTo({
                top: 0,
                behavior: 'smooth'
            });
        },

        /**
         * Toggle visibility based on scroll
         */
        toggleVisibility: () => {
            if (!DOM.backToTop) return;

            if (window.pageYOffset > 300) {
                DOM.backToTop.style.opacity = '1';
                DOM.backToTop.style.visibility = 'visible';
            } else {
                DOM.backToTop.style.opacity = '0';
                DOM.backToTop.style.visibility = 'hidden';
            }
        },

        /**
         * Inicializar
         */
        initialize: () => {
            if (DOM.backToTop) {
                DOM.backToTop.addEventListener('click', backToTop.scrollToTop);

                // Usar debounce para optimizar performance
                const debouncedToggle = utils.debounce(backToTop.toggleVisibility, 100);
                window.addEventListener('scroll', debouncedToggle, { passive: true });

                // Initial check
                backToTop.toggleVisibility();
            }

            // Exponer función global para onclick inline
            window.scrollToTop = backToTop.scrollToTop;
        }
    };

    // ======= MANEJO DE EVENTOS =======
    const eventManager = {
        /**
         * Inicializar todos los event listeners
         */
        initialize: () => {
            // Sidebar events
            if (DOM.openSidebar) {
                DOM.openSidebar.addEventListener('click', (e) => {
                    e.preventDefault();
                    e.stopPropagation();
                    panelManager.toggle(DOM.sidebar, DOM.openSidebar);
                });
            }

            if (DOM.closeSidebar) {
                DOM.closeSidebar.addEventListener('click', (e) => {
                    e.preventDefault();
                    panelManager.close(DOM.sidebar);
                });
            }

            // Cart events
            if (DOM.openCart) {
                DOM.openCart.addEventListener('click', (e) => {
                    if (STATE.isCartLocked) {
                        e.preventDefault();
                        e.stopPropagation();
                        return;
                    }
                    e.preventDefault();
                    e.stopPropagation();
                    panelManager.toggle(DOM.cartPanel, DOM.openCart);
                });
            }

            if (DOM.closeCart) {
                DOM.closeCart.addEventListener('click', (e) => {
                    e.preventDefault();
                    panelManager.close(DOM.cartPanel);
                });
            }

            // Click outside to close panels
            document.addEventListener('click', (e) => {
                const target = e.target;

                // Check sidebar
                if (DOM.sidebar?.classList.contains(CONFIG.classes.show) &&
                    !DOM.sidebar.contains(target) &&
                    !(DOM.openSidebar && DOM.openSidebar.contains(target))) {
                    panelManager.close(DOM.sidebar);
                }

                // Check cart panel
                if (DOM.cartPanel?.classList.contains(CONFIG.classes.show) &&
                    !DOM.cartPanel.contains(target) &&
                    !(DOM.openCart && DOM.openCart.contains(target))) {
                    panelManager.close(DOM.cartPanel);
                }
            });

            // Escape key to close panels
            document.addEventListener('keydown', (e) => {
                if (e.key === 'Escape' || e.key === 'Esc') {
                    panelManager.closeAll();
                }
            });

            // Overlay click to close
            if (DOM.overlay) {
                DOM.overlay.addEventListener('click', () => {
                    panelManager.closeAll();
                });
            }

            // Storage event for cross-tab sync
            window.addEventListener('storage', (e) => {
                if (e.key === 'simone_cart_updated') {
                    cartManager.afterMutation();
                }
            });

            // Visibility change for refresh
            document.addEventListener('visibilitychange', () => {
                if (!document.hidden) {
                    cartManager.updateBadge();
                }
            });

            // Keyboard shortcuts
            document.addEventListener('keydown', (e) => {
                if (e.ctrlKey || e.metaKey) {
                    switch (e.key) {
                        case '1':
                            e.preventDefault();
                            window.location.href = '/';
                            break;
                        case '2':
                            e.preventDefault();
                            DOM.openSidebar?.click();
                            break;
                        case '3':
                            e.preventDefault();
                            if (!STATE.isCartLocked) {
                                DOM.openCart?.click();
                            }
                            break;
                    }
                }
            });
        }
    };

    // ======= BOOTSTRAP MODAL FIXES =======
    const modalManager = {
        initialize: () => {
            // Prevenir problemas con múltiples backdrops
            if (typeof bootstrap !== 'undefined' && bootstrap.Modal) {
                const originalModal = bootstrap.Modal.prototype.constructor;

                bootstrap.Modal = class extends originalModal {
                    _showBackdrop() {
                        super._showBackdrop();
                        // Asegurar z-index correcto
                        const backdrop = document.querySelector('.modal-backdrop');
                        if (backdrop) {
                            backdrop.style.zIndex = '1079';
                        }
                    }
                };
            }

            // Limpiar backdrops al cerrar modales
            document.addEventListener('hidden.bs.modal', () => {
                const backdrops = document.querySelectorAll('.modal-backdrop');
                if (backdrops.length > 1) {
                    backdrops.forEach((backdrop, index) => {
                        if (index > 0) backdrop.remove();
                    });
                }
            });
        }
    };

    // ======= PERFORMANCE OPTIMIZATIONS =======
    const performanceManager = {
        initialize: () => {
            // Deshabilitar animaciones en conexiones lentas
            if ('connection' in navigator) {
                const connection = navigator.connection || navigator.mozConnection || navigator.webkitConnection;
                if (connection && (connection.saveData || connection.effectiveType?.includes('2g'))) {
                    document.documentElement.style.setProperty('--transition-base', '0.01s');
                    document.documentElement.style.setProperty('--transition-fast', '0.01s');
                }
            }
        }
    };

    // ======= INICIALIZACIÓN PRINCIPAL =======
    function initialize() {
        try {
            // Inicializar DOM
            initializeDOM();

            // Inicializar managers
            eventManager.initialize();
            backToTop.initialize();
            cartManager.handleLock();
            modalManager.initialize();
            performanceManager.initialize();

            // Cargar datos iniciales
            cartManager.updateBadge();

            // Exponer API global
            window.NeoAgora = {
                panels: panelManager,
                cart: cartManager,
                utils: utils,
                version: '1.0.0'
            };

            console.log('✓ Layout inicializado correctamente');
        } catch (error) {
            console.error('Error al inicializar layout:', error);
        }
    }

    // Inicializar cuando el DOM esté listo
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initialize);
    } else {
        initialize();
    }
})();
