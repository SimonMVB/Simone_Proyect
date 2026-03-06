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
                    // También limpiar modales
                    modalManager.forceCleanup();
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
                    // Limpiar cualquier backdrop huérfano al volver a la pestaña
                    modalManager.forceCleanup();
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
        /**
         * Limpieza completa y forzada de TODOS los overlays y backdrops
         */
        forceCleanup: () => {
            const openModals = document.querySelectorAll('.modal.show');

            // Si no hay modales abiertos, limpiar TODO
            if (openModals.length === 0) {
                // 1. Eliminar TODOS los modal-backdrop
                document.querySelectorAll('.modal-backdrop').forEach(el => el.remove());

                // 2. Ocultar el global-overlay (el que tiene el blur)
                const globalOverlay = document.getElementById('globalOverlay');
                if (globalOverlay) {
                    globalOverlay.classList.remove('show');
                    globalOverlay.setAttribute('aria-hidden', 'true');
                }

                // 3. Restaurar el body completamente
                document.body.classList.remove('modal-open', 'no-scroll');
                document.body.style.removeProperty('padding-right');
                document.body.style.removeProperty('overflow');
                document.body.style.removeProperty('position');
                document.body.style.removeProperty('width');
                document.body.style.removeProperty('height');

                // 4. Asegurar que no hay paneles abiertos que no deberían estar
                const sidebar = document.getElementById('sidebar');
                const cartPanel = document.getElementById('cartPanel');

                if (sidebar && !sidebar.classList.contains('show') &&
                    cartPanel && !cartPanel.classList.contains('show')) {
                    // Ningún panel abierto, asegurar que overlay está oculto
                    if (globalOverlay) {
                        globalOverlay.classList.remove('show');
                    }
                }

                console.log('✓ Modal cleanup completed');
            }
        },

        /**
         * Limpiar backdrops extras (cuando hay más de los necesarios)
         */
        cleanupExtraBackdrops: () => {
            const openModals = document.querySelectorAll('.modal.show');
            const backdrops = document.querySelectorAll('.modal-backdrop');

            // Si hay más backdrops que modales, eliminar los extras
            if (backdrops.length > openModals.length) {
                for (let i = openModals.length; i < backdrops.length; i++) {
                    backdrops[i].remove();
                }
            }
        },

        initialize: () => {
            // Mantener el orden de capas por defecto de Bootstrap
            document.addEventListener('shown.bs.modal', () => {
                const visibleModals = document.querySelectorAll('.modal.show');
                const backdrops = document.querySelectorAll('.modal-backdrop');

                visibleModals.forEach((modal, index) => {
                    modal.style.zIndex = String(1055 + index * 20);
                });

                backdrops.forEach((backdrop, index) => {
                    backdrop.style.zIndex = String(1050 + index * 20);
                });
            });

            // Limpiar al cerrar modales
            document.addEventListener('hidden.bs.modal', () => {
                // Múltiples intentos de limpieza para asegurar
                setTimeout(() => modalManager.forceCleanup(), 100);
                setTimeout(() => modalManager.forceCleanup(), 300);
                setTimeout(() => modalManager.forceCleanup(), 500);
            });

            // Interceptar envío de formularios dentro de modales
            document.addEventListener('submit', (e) => {
                const modal = e.target.closest('.modal');
                if (modal) {
                    // Cerrar el modal inmediatamente
                    try {
                        const bsModal = bootstrap.Modal.getInstance(modal);
                        if (bsModal) {
                            bsModal.hide();
                        }
                    } catch (err) {
                        // Bootstrap no disponible, cerrar manualmente
                        modal.classList.remove('show');
                        modal.style.display = 'none';
                        modal.setAttribute('aria-hidden', 'true');
                    }

                    // Limpiar todo después
                    setTimeout(() => modalManager.forceCleanup(), 50);
                    setTimeout(() => modalManager.forceCleanup(), 200);
                    setTimeout(() => modalManager.forceCleanup(), 400);
                }
            });

            // Observer para detectar y eliminar backdrops huérfanos
            const observer = new MutationObserver((mutations) => {
                mutations.forEach((mutation) => {
                    mutation.addedNodes.forEach((node) => {
                        // Si se añade un backdrop pero no hay modal abierto
                        if (node.nodeType === 1 && node.classList?.contains('modal-backdrop')) {
                            setTimeout(() => {
                                const openModals = document.querySelectorAll('.modal.show');
                                if (openModals.length === 0) {
                                    node.remove();
                                    modalManager.forceCleanup();
                                }
                            }, 100);
                        }
                    });
                });
            });

            observer.observe(document.body, { childList: true, subtree: false });

            // Verificación periódica cada 3 segundos
            setInterval(() => {
                const openModals = document.querySelectorAll('.modal.show');
                const backdrops = document.querySelectorAll('.modal-backdrop');
                const globalOverlay = document.getElementById('globalOverlay');
                const sidebar = document.getElementById('sidebar');
                const cartPanel = document.getElementById('cartPanel');

                const sidebarOpen = sidebar?.classList.contains('show');
                const cartOpen = cartPanel?.classList.contains('show');

                // Si hay backdrops pero no modales, limpiar
                if (openModals.length === 0 && backdrops.length > 0) {
                    console.warn('Detectados backdrops huérfanos, limpiando...');
                    modalManager.forceCleanup();
                }

                // Si el globalOverlay está visible pero no hay paneles ni modales abiertos
                if (globalOverlay?.classList.contains('show') &&
                    !sidebarOpen && !cartOpen && openModals.length === 0) {
                    console.warn('GlobalOverlay huérfano detectado, limpiando...');
                    modalManager.forceCleanup();
                }
            }, 3000);

            // Exponer función de limpieza globalmente
            window.cleanupModalBackdrops = modalManager.forceCleanup;
            window.forceCleanupAll = modalManager.forceCleanup;
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

            // Limpiar cualquier residuo al cargar la página
            modalManager.forceCleanup();

            // Exponer API global
            window.NeoAgora = {
                panels: panelManager,
                cart: cartManager,
                utils: utils,
                modals: modalManager,
                cleanup: modalManager.forceCleanup,
                version: '1.0.2'
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
