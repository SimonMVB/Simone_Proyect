/**
 * ============================================================================
 * PRODUCTO FORM - JAVASCRIPT MODULAR OPTIMIZADO
 * Archivo: wwwroot/js/ProductoForm.js
 * Versión: 2.1 - FIX PRECIOS
 * ============================================================================
 */

(function () {
    'use strict';

    // ========== CONFIGURACIÓN ==========
    const CONFIG = {
        MAX_IMAGES: 8,
        MAX_IMAGE_MB: 8,
        ALLOWED_IMAGE_TYPES: ['image/jpeg', 'image/png', 'image/webp', 'image/gif'],
        DEBOUNCE_MS: 120,
        TOAST_DURATION: 3600
    };

    // ========== HELPERS ==========
    const Helpers = {
        /**
         * Selector rápido por ID
         */
        $: (id) => document.getElementById(id),

        /**
         * Selector múltiple
         */
        $$: (sel, root = document) => Array.from(root.querySelectorAll(sel)),

        /**
         * Convierte string a número seguro (maneja formato español)
         */
        toNum: (x) => {
            if (typeof x === 'number') return x;

            let s = (x ?? '').toString().trim();

            // Contar cuántos puntos y comas hay
            const dotCount = (s.match(/\./g) || []).length;
            const commaCount = (s.match(/,/g) || []).length;

            // Formato español: 1.200,50 (punto = miles, coma = decimal)
            if (dotCount > 0 && commaCount === 1 && s.lastIndexOf(',') > s.lastIndexOf('.')) {
                s = s.replace(/\./g, '').replace(',', '.');
            }
            // Formato inglés: 1,200.50 (coma = miles, punto = decimal)
            else if (commaCount > 0 && dotCount === 1 && s.lastIndexOf('.') > s.lastIndexOf(',')) {
                s = s.replace(/,/g, '');
            }
            // Solo coma: asumir decimal español (1,50)
            else if (commaCount === 1 && dotCount === 0) {
                s = s.replace(',', '.');
            }
            // Solo puntos múltiples: asumir miles español (1.200)
            else if (dotCount > 1 && commaCount === 0) {
                s = s.replace(/\./g, '');
            }
            // Limpiar espacios
            s = s.replace(/\s+/g, '');

            const n = parseFloat(s);
            return Number.isFinite(n) ? n : 0;
        },

        /**
         * Formatea número como USD
         */
        fmtUSD: (n) => {
            return new Intl.NumberFormat('es-EC', {
                style: 'currency',
                currency: 'USD',
                minimumFractionDigits: 2
            }).format(n || 0);
        },

        /**
         * Debounce function
         */
        debounce: (fn, ms = CONFIG.DEBOUNCE_MS) => {
            let timeout;
            return (...args) => {
                clearTimeout(timeout);
                timeout = setTimeout(() => fn(...args), ms);
            };
        },

        /**
         * Throttle function
         */
        throttle: (fn, ms = 200) => {
            let inThrottle;
            return (...args) => {
                if (!inThrottle) {
                    fn(...args);
                    inThrottle = true;
                    setTimeout(() => inThrottle = false, ms);
                }
            };
        },

        /**
         * Toast notification
         */
        toast: (text, kind = 'info', duration = CONFIG.TOAST_DURATION) => {
            const backgrounds = {
                info: 'linear-gradient(135deg, var(--primary), var(--primary-light))',
                success: 'linear-gradient(135deg, var(--success), #34d399)',
                warning: 'linear-gradient(135deg, var(--warning), #fbbf24)',
                error: 'linear-gradient(135deg, var(--error), #f87171)'
            };

            if (typeof Toastify !== 'undefined') {
                Toastify({
                    text,
                    duration,
                    gravity: 'top',
                    position: 'right',
                    stopOnFocus: true,
                    style: { background: backgrounds[kind] || backgrounds.info }
                }).showToast();
            } else {
                console.log(`[${kind.toUpperCase()}] ${text}`);
            }
        },

        /**
         * Log seguro
         */
        log: (message, data = null) => {
            if (data) {
                console.log(`[ProductoForm] ${message}`, data);
            } else {
                console.log(`[ProductoForm] ${message}`);
            }
        },

        /**
         * Log de error
         */
        error: (message, error = null) => {
            console.error(`[ProductoForm ERROR] ${message}`, error || '');
        }
    };

    // ========== ESTADO GLOBAL ==========
    const State = {
        hasVariants: false,
        isSubmitting: false,
        validationErrors: [],
        objectUrls: new Set(),
        subcategorias: [],
        imageFiles: [],
        removedExistingImages: new Set(),

        /**
         * Actualiza el estado de variantes
         */
        setHasVariants(value) {
            this.hasVariants = Boolean(value);
            Helpers.log(`Variants mode: ${this.hasVariants ? 'ON' : 'OFF'}`);
        },

        /**
         * Limpia URLs de objeto
         */
        cleanup() {
            this.objectUrls.forEach(url => URL.revokeObjectURL(url));
            this.objectUrls.clear();
            Helpers.log('Object URLs cleaned up');
        }
    };

    // ========== MÓDULO: SUBCATEGORÍAS ==========
    const SubcategoriesModule = {
        init() {
            const selCat = Helpers.$('categoriaID');
            const selSub = Helpers.$('Subcategoria');

            if (!selCat || !selSub) {
                Helpers.error('Category or subcategory selects not found');
                return;
            }

            // Cargar datos iniciales
            try {
                const dataScript = Helpers.$('formData');
                if (dataScript) {
                    const data = JSON.parse(dataScript.textContent);
                    State.subcategorias = data.subcategorias || [];
                    State.setHasVariants(data.hasVariants || false);
                }
            } catch (e) {
                Helpers.error('Failed to load form data', e);
            }

            // Inicializar con categoría actual
            const currentCat = parseInt(selCat.value || '0', 10);
            if (currentCat) {
                this.fillSubcategories(currentCat, true);
            }

            // Event listeners
            selCat.addEventListener('change', (e) => {
                const catId = parseInt(e.target.value || '0', 10);
                this.fillSubcategories(catId, false);
                ValidationModule.validateField(selCat);
            });

            selSub.addEventListener('change', () => {
                ValidationModule.validateField(selSub);
            });
        },

        fillSubcategories(categoriaId, preserveSelection = false) {
            const selSub = Helpers.$('Subcategoria');
            const note = Helpers.$('subcategoriaNote');

            if (!selSub) return;

            const currentValue = preserveSelection ? selSub.value : '';

            selSub.innerHTML = '<option value="">Seleccione una subcategoría</option>';

            if (!categoriaId) {
                selSub.disabled = true;
                if (note) note.style.display = 'block';
                return;
            }

            const filtered = State.subcategorias.filter(s => s.CategoriaID == categoriaId);

            filtered.forEach(s => {
                const option = document.createElement('option');
                option.value = s.SubcategoriaID;
                option.textContent = s.NombreSubcategoria;

                if (preserveSelection && s.SubcategoriaID == currentValue) {
                    option.selected = true;
                }

                selSub.appendChild(option);
            });

            selSub.disabled = filtered.length === 0;
            if (note) note.style.display = filtered.length === 0 ? 'block' : 'none';

            if (filtered.length === 0 && !preserveSelection) {
                Helpers.toast('No hay subcategorías para esta categoría', 'warning');
            }

            Helpers.log(`Loaded ${filtered.length} subcategories for category ${categoriaId}` +
                (currentValue ? ` (preserved: ${currentValue})` : ''));
        }
    };

    // ========== MÓDULO: PRECIOS ==========
    const PricingModule = {
        init() {
            const pcInput = Helpers.$('PrecioCompra');
            const pvInput = Helpers.$('PrecioVenta');
            const margin = Helpers.$('pvMargin');

            if (!pcInput || !pvInput || !margin) return;

            const updateMargin = Helpers.debounce(() => {
                const pc = Helpers.toNum(pcInput.value);
                const pv = Helpers.toNum(pvInput.value);

                if (!State.hasVariants) {
                    const diff = pv - pc;
                    const ok = pv > pc;
                    margin.textContent = `Margen vs compra: ${Helpers.fmtUSD(diff)} ${ok ? '' : '(insuficiente)'}`;
                    margin.style.color = ok ? 'var(--text-muted)' : 'var(--error)';
                } else {
                    margin.textContent = 'Con variantes, ingresa el precio final por combinación.';
                    margin.style.color = 'var(--text-muted)';
                }

                PreviewModule.update();
            }, 80);

            ['input', 'change', 'blur'].forEach(event => {
                pcInput.addEventListener(event, updateMargin);
                pvInput.addEventListener(event, updateMargin);
            });

            updateMargin();
        }
    };

    // ========== MÓDULO: IMÁGENES ==========
    const ImagesModule = {
        init() {
            const uploadArea = Helpers.$('imageUploadArea');
            const filesInput = Helpers.$('Imagenes');
            const gallery = Helpers.$('galeria');

            if (!uploadArea || !filesInput || !gallery) return;

            uploadArea.addEventListener('click', () => filesInput.click());
            uploadArea.addEventListener('dragover', (e) => this.handleDragOver(e));
            uploadArea.addEventListener('dragleave', () => this.handleDragLeave());
            uploadArea.addEventListener('drop', (e) => this.handleDrop(e));
            filesInput.addEventListener('change', (e) => this.handleFileSelect(e.target.files));

            window.addEventListener('beforeunload', () => State.cleanup());

            gallery.addEventListener('click', (e) => {
                const btn = e.target.closest('.image-btn');
                if (!btn) return;

                const card = btn.closest('.image-thumbnail');
                if (!card) return;

                const action = btn.dataset.action;
                if (action === 'cover') {
                    this.markAsCover(card);
                } else if (action === 'delete') {
                    this.removeImage(card);
                }
            });

            this.updateImageCount();
        },

        handleDragOver(e) {
            e.preventDefault();
            const area = Helpers.$('imageUploadArea');
            if (area) area.classList.add('drag-over');
        },

        handleDragLeave() {
            const area = Helpers.$('imageUploadArea');
            if (area) area.classList.remove('drag-over');
        },

        handleDrop(e) {
            e.preventDefault();
            this.handleDragLeave();
            this.handleFileSelect(e.dataTransfer.files);
        },

        handleFileSelect(fileList) {
            const files = Array.from(fileList);
            const validFiles = files.filter(f => this.validateFile(f));

            if (validFiles.length === 0) return;

            const currentCount = this.getCurrentImageCount();
            const availableSlots = CONFIG.MAX_IMAGES - currentCount;

            if (availableSlots <= 0) {
                this.showError(`Límite de ${CONFIG.MAX_IMAGES} imágenes alcanzado.`);
                return;
            }

            const filesToAdd = validFiles.slice(0, availableSlots);

            this.showProgress();

            State.imageFiles.push(...filesToAdd);
            filesToAdd.forEach(file => this.createThumbnail(file));

            const filesInput = Helpers.$('Imagenes');
            if (filesInput) {
                const dt = new DataTransfer();
                State.imageFiles.forEach(f => dt.items.add(f));
                filesInput.files = dt.files;
            }

            this.updateImageCount();
            this.hideProgress();

            Helpers.toast(`${filesToAdd.length} imagen(es) agregada(s)`, 'success');
        },

        validateFile(file) {
            if (!CONFIG.ALLOWED_IMAGE_TYPES.includes(file.type)) {
                this.showError('Formato no permitido. Use JPEG, PNG, WebP o GIF.');
                return false;
            }

            const maxBytes = CONFIG.MAX_IMAGE_MB * 1024 * 1024;
            if (file.size > maxBytes) {
                this.showError(`Archivo muy grande. Máximo ${CONFIG.MAX_IMAGE_MB}MB.`);
                return false;
            }

            return true;
        },

        createThumbnail(file) {
            const gallery = Helpers.$('galeria');
            if (!gallery) return;

            const card = document.createElement('div');
            card.className = 'image-thumbnail';
            card.dataset.new = '1';

            const img = document.createElement('img');
            const url = URL.createObjectURL(file);
            State.objectUrls.add(url);
            img.src = url;
            img.alt = 'Vista previa de imagen';
            img.loading = 'lazy';

            const actions = document.createElement('div');
            actions.className = 'image-actions';

            actions.innerHTML = `
                <button type="button" class="image-btn" data-action="cover" title="Marcar como principal">
                    <i class="fas fa-star"></i>
                </button>
                <button type="button" class="image-btn" data-action="delete" title="Eliminar imagen">
                    <i class="fas fa-trash"></i>
                </button>
            `;

            card.appendChild(img);
            card.appendChild(actions);
            gallery.appendChild(card);
        },

        markAsCover(card) {
            Helpers.$$('.image-badge').forEach(badge => badge.remove());
            Helpers.$$('.image-btn.active').forEach(btn => btn.classList.remove('active'));

            const badge = document.createElement('span');
            badge.className = 'image-badge';
            badge.textContent = 'Principal';
            card.appendChild(badge);

            const starBtn = card.querySelector('[data-action="cover"]');
            if (starBtn) starBtn.classList.add('active');

            const isNew = card.dataset.new === '1';
            if (isNew) {
                const index = Helpers.$$('#galeria .image-thumbnail[data-new="1"]').indexOf(card);
                const input = Helpers.$('ImagenPrincipalIndex');
                if (input) input.value = index;
            } else {
                const url = card.dataset.url;
                const input = Helpers.$('ExistingImagenPath');
                if (input) input.value = url;
            }

            Helpers.log('Cover image updated');
        },

        removeImage(card) {
            if (!confirm('¿Está seguro de que desea eliminar esta imagen?')) return;

            const isNew = card.dataset.new === '1';

            if (isNew) {
                const index = Helpers.$$('#galeria .image-thumbnail[data-new="1"]').indexOf(card);
                State.imageFiles.splice(index, 1);

                const filesInput = Helpers.$('Imagenes');
                if (filesInput) {
                    const dt = new DataTransfer();
                    State.imageFiles.forEach(f => dt.items.add(f));
                    filesInput.files = dt.files;
                }
            } else {
                const url = card.dataset.url;
                State.removedExistingImages.add(url);

                const ignoreInput = Helpers.$('ImagenesIgnore');
                if (ignoreInput) {
                    ignoreInput.value = JSON.stringify(Array.from(State.removedExistingImages));
                }

                card.classList.add('removed');
            }

            card.remove();
            this.updateImageCount();
            Helpers.toast('Imagen eliminada', 'success');
        },

        getCurrentImageCount() {
            const existing = Helpers.$$('#galeria .image-thumbnail[data-existing="1"]:not(.removed)').length;
            const newImages = State.imageFiles.length;
            return existing + newImages;
        },

        updateImageCount() {
            const count = this.getCurrentImageCount();

            const badge = Helpers.$('imageCountBadge');
            if (badge) badge.textContent = `${count}/${CONFIG.MAX_IMAGES} imágenes`;

            const preview = Helpers.$('previewImages');
            if (preview) preview.textContent = count;
        },

        showError(message) {
            const alert = Helpers.$('imgAlert');
            if (alert) {
                alert.textContent = message;
                alert.style.display = 'block';
                setTimeout(() => alert.style.display = 'none', 5000);
            }
            Helpers.toast(message, 'error');
        },

        showProgress() {
            const bar = Helpers.$('uploadProgress');
            const fill = Helpers.$('progressFill');
            if (!bar || !fill) return;

            bar.style.display = 'block';
            fill.style.width = '0%';

            let progress = 0;
            const interval = setInterval(() => {
                progress += Math.random() * 30;
                if (progress >= 100) {
                    progress = 100;
                    clearInterval(interval);
                    setTimeout(() => this.hideProgress(), 500);
                }
                fill.style.width = progress + '%';
            }, 100);
        },

        hideProgress() {
            const bar = Helpers.$('uploadProgress');
            if (bar) bar.style.display = 'none';
        }
    };

    // ========== MÓDULO: VARIANTES ==========
    const VariantsModule = {
        init() {
            const checkbox = Helpers.$('chkVariantes');
            const box = Helpers.$('boxVariantes');

            if (!checkbox || !box) return;

            checkbox.addEventListener('change', () => this.toggleVariants());

            this.initTagInputs();

            const btnGen = Helpers.$('btnGen');
            const btnClear = Helpers.$('btnClearVars');

            if (btnGen) btnGen.addEventListener('click', () => this.generateCombinations());
            if (btnClear) btnClear.addEventListener('click', () => this.clearVariants());

            this.initPriceRules();

            const table = Helpers.$('tblVars');
            if (table) {
                table.addEventListener('click', (e) => {
                    const btn = e.target.closest('[data-action="remove-variant"]');
                    if (btn) {
                        const row = btn.closest('tr');
                        if (row) this.removeVariant(row);
                    }
                });

                table.addEventListener('input', (e) => {
                    if (e.target.classList.contains('variant-input')) {
                        this.validateVariantInput(e.target);
                    }
                });
            }

            this.updateCount();
        },

        toggleVariants() {
            const checkbox = Helpers.$('chkVariantes');
            const box = Helpers.$('boxVariantes');
            const baseFields = Helpers.$$('.base-only');
            const badge = Helpers.$('productTypeBadge');

            if (!checkbox) return;

            const enabled = checkbox.checked;
            State.setHasVariants(enabled);

            if (box) box.style.display = enabled ? 'block' : 'none';
            baseFields.forEach(field => field.style.display = enabled ? 'none' : 'flex');

            if (badge) badge.textContent = enabled ? 'Con Variantes' : 'Producto Simple';

            const previewType = Helpers.$('previewType');
            if (previewType) previewType.textContent = enabled ? 'Con Variantes' : 'Producto Simple';

            PreviewModule.update();
        },

        initTagInputs() {
            const colorInput = Helpers.$('inpColores');
            const sizeInput = Helpers.$('inpTallas');
            const colorTags = Helpers.$('tagsColores');
            const sizeTags = Helpers.$('tagsTallas');

            if (colorInput && colorTags) {
                this.setupTagInput(colorInput, colorTags, 'color');
            }

            if (sizeInput && sizeTags) {
                this.setupTagInput(sizeInput, sizeTags, 'talla');
            }

            [colorTags, sizeTags].forEach(container => {
                if (!container) return;
                container.addEventListener('click', (e) => {
                    const btn = e.target.closest('.tag-remove');
                    if (btn) {
                        const tag = btn.closest('.tag');
                        if (tag) tag.remove();
                        this.updateCount();
                    }
                });
            });
        },

        setupTagInput(input, container, type) {
            const addTag = (value) => {
                const trimmed = value.trim();
                if (!trimmed) return;

                const existing = Helpers.$$('.tag input', container).map(i => i.value.toLowerCase());
                if (existing.includes(trimmed.toLowerCase())) {
                    Helpers.toast(`"${trimmed}" ya existe`, 'warning');
                    return;
                }

                const tag = document.createElement('span');
                tag.className = 'tag';
                tag.innerHTML = `
                    ${trimmed}
                    <button type="button" class="tag-remove" data-tag-type="${type}" aria-label="Eliminar ${type} ${trimmed}">
                        <i class="fas fa-times"></i>
                    </button>
                    <input type="hidden" name="${type === 'color' ? 'Colores' : 'Tallas'}" value="${trimmed}" />
                `;
                container.appendChild(tag);
                this.updateCount();
            };

            input.addEventListener('keydown', (e) => {
                if (e.key === 'Enter' || e.key === ',') {
                    e.preventDefault();
                    addTag(input.value);
                    input.value = '';
                }
            });

            input.addEventListener('blur', () => {
                if (input.value.trim()) {
                    addTag(input.value);
                    input.value = '';
                }
            });
        },

        generateCombinations() {
            const colors = Helpers.$$('#tagsColores .tag input').map(i => i.value);
            const sizes = Helpers.$$('#tagsTallas .tag input').map(i => i.value);
            const tbody = Helpers.$('tblVars')?.querySelector('tbody');
            const note = Helpers.$('noteVars');

            if (!tbody) return;

            if (colors.length === 0 || sizes.length === 0) {
                Helpers.toast('Agregue al menos un color y una talla', 'warning');
                return;
            }

            tbody.innerHTML = '';

            colors.forEach(color => {
                sizes.forEach(size => {
                    const row = this.createVariantRow(color, size);
                    tbody.appendChild(row);
                });
            });

            if (note) note.style.display = 'none';

            const summary = Helpers.$('variantSummary');
            if (summary) summary.style.display = 'block';

            this.updateCount();
            this.updateSummary();

            Helpers.toast(`${colors.length * sizes.length} combinaciones generadas`, 'success');
        },

        createVariantRow(color, size) {
            const tr = document.createElement('tr');
            tr.innerHTML = `
                <td>
                    <input type="hidden" name="VarColor" value="${color}" />
                    <span>${color}</span>
                </td>
                <td>
                    <input type="hidden" name="VarTalla" value="${size}" />
                    <span>${size}</span>
                </td>
                <td>
                    <input class="variant-input" 
                           name="VarPrecio" 
                           type="number" 
                           step="0.01" 
                           min="0.01" 
                           lang="en"
                           inputmode="decimal"
                           required 
                           aria-label="Precio para ${color} ${size}" />
                </td>
                <td>
                    <input class="variant-input" 
                           name="VarStock" 
                           type="number" 
                           step="1" 
                           min="0" 
                           inputmode="numeric"
                           value="0" 
                           required 
                           aria-label="Stock para ${color} ${size}" />
                </td>
                <td>
                    <div class="variant-actions">
                        <button type="button" 
                                class="image-btn" 
                                data-action="remove-variant"
                                title="Eliminar variante"
                                aria-label="Eliminar variante ${color} ${size}">
                            <i class="fas fa-trash"></i>
                        </button>
                    </div>
                </td>
            `;
            return tr;
        },

        removeVariant(row) {
            if (!confirm('¿Eliminar esta variante?')) return;

            row.remove();
            this.updateCount();
            this.updateSummary();

            const remaining = Helpers.$$('#tblVars tbody tr').length;
            if (remaining === 0) {
                const note = Helpers.$('noteVars');
                if (note) note.style.display = 'block';

                const summary = Helpers.$('variantSummary');
                if (summary) summary.style.display = 'none';
            }

            Helpers.toast('Variante eliminada', 'success');
        },

        clearVariants() {
            if (!confirm('¿Está seguro de que desea limpiar todas las variantes?')) return;

            const tbody = Helpers.$('tblVars')?.querySelector('tbody');
            if (tbody) tbody.innerHTML = '';

            const note = Helpers.$('noteVars');
            if (note) note.style.display = 'block';

            const summary = Helpers.$('variantSummary');
            if (summary) summary.style.display = 'none';

            this.updateCount();
            Helpers.toast('Variantes limpiadas', 'success');
        },

        validateVariantInput(input) {
            const value = Helpers.toNum(input.value);
            const min = Helpers.toNum(input.getAttribute('min') || 0);

            if (value < min) {
                input.classList.add('error');
            } else {
                input.classList.remove('error');
            }

            this.updateSummary();
        },

        updateCount() {
            const count = Helpers.$$('#tblVars tbody tr').length;
            const badge = Helpers.$('variantCountBadge');
            if (badge) badge.textContent = `${count} variante${count !== 1 ? 's' : ''}`;
        },

        updateSummary() {
            const rows = Helpers.$$('#tblVars tbody tr');
            const summary = Helpers.$('variantSummary');

            if (rows.length === 0) {
                if (summary) summary.style.display = 'none';
                return;
            }

            let totalStock = 0;
            let minPrice = Infinity;
            let maxPrice = 0;

            rows.forEach(row => {
                const priceInput = row.querySelector('input[name="VarPrecio"]');
                const stockInput = row.querySelector('input[name="VarStock"]');

                const price = Helpers.toNum(priceInput?.value);
                const stock = Helpers.toNum(stockInput?.value);

                totalStock += stock;

                if (price > 0) {
                    minPrice = Math.min(minPrice, price);
                    maxPrice = Math.max(maxPrice, price);
                }
            });

            const totalEl = Helpers.$('summaryTotal');
            const stockEl = Helpers.$('summaryStock');
            const minEl = Helpers.$('summaryMinPrice');
            const maxEl = Helpers.$('summaryMaxPrice');

            if (totalEl) totalEl.textContent = rows.length;
            if (stockEl) stockEl.textContent = totalStock;
            if (minEl) minEl.textContent = Helpers.fmtUSD(minPrice === Infinity ? 0 : minPrice);
            if (maxEl) maxEl.textContent = Helpers.fmtUSD(maxPrice);

            if (summary) summary.style.display = 'block';

            const previewStock = Helpers.$('previewStock');
            if (previewStock) previewStock.textContent = `${totalStock} unidades`;
        },

        initPriceRules() {
            const chkGlobal = Helpers.$('chkPrecioGlobal');
            const inpGlobal = Helpers.$('inpPrecioGlobal');
            const btnApplyGlobal = Helpers.$('btnApplyGlobal');

            if (chkGlobal && inpGlobal && btnApplyGlobal) {
                chkGlobal.addEventListener('change', () => {
                    const enabled = chkGlobal.checked;
                    inpGlobal.disabled = !enabled;
                    btnApplyGlobal.disabled = !enabled;
                });

                btnApplyGlobal.addEventListener('click', () => {
                    const price = Helpers.toNum(inpGlobal.value);
                    if (price <= 0) {
                        Helpers.toast('Ingrese un precio válido', 'warning');
                        return;
                    }

                    Helpers.$$('input[name="VarPrecio"]').forEach(input => {
                        input.value = price.toFixed(2);
                    });

                    this.updateSummary();
                    Helpers.toast('Precio aplicado a todas las variantes', 'success');
                });
            }

            const chkPorTalla = Helpers.$('chkPrecioPorTalla');
            const boxPorTalla = Helpers.$('boxPrecioPorTalla');

            if (chkPorTalla && boxPorTalla) {
                chkPorTalla.addEventListener('change', () => {
                    const enabled = chkPorTalla.checked;
                    boxPorTalla.style.display = enabled ? 'block' : 'none';

                    if (enabled) {
                        this.renderPriceBySize(boxPorTalla);
                    }
                });
            }
        },

        renderPriceBySize(container) {
            const sizes = Helpers.$$('#tagsTallas .tag input').map(i => i.value);
            container.innerHTML = '';

            sizes.forEach(size => {
                const row = document.createElement('div');
                row.style.cssText = 'display:flex;align-items:center;gap:8px;margin-bottom:8px;';
                row.innerHTML = `
                    <span style="min-width:80px;">${size}</span>
                    <input type="number" 
                           step="0.01" 
                           class="form-input price-rule-input" 
                           data-size="${size}" 
                           placeholder="0.00" />
                `;
                container.appendChild(row);
            });

            const btnApply = document.createElement('button');
            btnApply.type = 'button';
            btnApply.className = 'btn btn-success';
            btnApply.textContent = 'Aplicar';
            btnApply.style.marginTop = '8px';
            btnApply.addEventListener('click', () => this.applyPriceBySize());
            container.appendChild(btnApply);
        },

        applyPriceBySize() {
            const priceMap = {};

            Helpers.$$('input[data-size]').forEach(input => {
                const size = input.dataset.size;
                const price = Helpers.toNum(input.value);
                if (price > 0) {
                    priceMap[size] = price;
                }
            });

            Helpers.$$('#tblVars tbody tr').forEach(row => {
                const sizeInput = row.querySelector('input[name="VarTalla"]');
                const priceInput = row.querySelector('input[name="VarPrecio"]');

                if (sizeInput && priceInput) {
                    const size = sizeInput.value;
                    if (priceMap[size]) {
                        priceInput.value = priceMap[size].toFixed(2);
                    }
                }
            });

            this.updateSummary();
            Helpers.toast('Precios aplicados por talla', 'success');
        }
    };

    // ========== MÓDULO: VALIDACIÓN ==========
    const ValidationModule = {
        init() {
            const desc = Helpers.$('Descripcion');
            const charCount = Helpers.$('charCount');

            if (desc && charCount) {
                const updateCount = Helpers.debounce(() => {
                    const length = desc.value.length;
                    charCount.textContent = length;
                    charCount.style.color = length > 500 ? 'var(--error)' : 'var(--text-muted)';
                    this.validateField(desc);
                }, 100);

                desc.addEventListener('input', updateCount);
            }

            Helpers.$$('[data-validation]').forEach(field => {
                field.addEventListener('blur', () => this.validateField(field));
                field.addEventListener('input', () => {
                    field.classList.remove('input-has-error', 'error');
                    const errorEl = Helpers.$(field.id + 'Error');
                    if (errorEl) errorEl.style.display = 'none';
                });
            });
        },

        validateField(field) {
            const rules = field.getAttribute('data-validation') || '';
            const value = (field.value || '').trim();
            const errorEl = Helpers.$(field.id + 'Error');

            let isValid = true;
            let message = '';

            if (rules.includes('required') && !value) {
                isValid = false;
                message = errorEl?.textContent || 'Este campo es requerido';
            }
            else if (rules.includes('number') && value) {
                const num = Helpers.toNum(value);
                if (isNaN(num)) {
                    isValid = false;
                    message = 'Debe ser un número válido';
                } else if (rules.includes('min:0.01') && num < 0.01) {
                    isValid = false;
                    message = 'Debe ser mayor a 0';
                } else if (rules.includes('min:0') && num < 0) {
                    isValid = false;
                    message = 'Debe ser mayor o igual a 0';
                }
            }

            if (isValid) {
                field.classList.remove('input-has-error', 'error');
                if (errorEl) errorEl.style.display = 'none';
            } else {
                field.classList.add('input-has-error', 'error');
                if (errorEl) {
                    errorEl.textContent = message;
                    errorEl.style.display = 'block';
                }
            }

            return isValid;
        },

        validateForm() {
            if (State.isSubmitting) {
                Helpers.toast('El formulario ya se está enviando...', 'warning');
                return false;
            }

            State.validationErrors = [];

            Helpers.$$('[data-validation]').forEach(field => {
                if (!this.validateField(field)) {
                    const label = field.previousElementSibling?.textContent?.replace('*', '').trim() || field.name;
                    State.validationErrors.push(`${label} es requerido o inválido`);
                }
            });

            if (!State.hasVariants) {
                const pc = Helpers.toNum(Helpers.$('PrecioCompra')?.value);
                const pv = Helpers.toNum(Helpers.$('PrecioVenta')?.value);

                if (!(pv > pc)) {
                    State.validationErrors.push('El precio de venta debe ser mayor al precio de compra');
                    Helpers.$('PrecioVenta')?.classList.add('input-has-error', 'error');
                }
            }

            if (State.hasVariants) {
                const rows = Helpers.$$('#tblVars tbody tr');

                if (rows.length === 0) {
                    State.validationErrors.push('Debe agregar al menos una variante cuando las variantes están activadas');
                } else {
                    let hasErrors = false;
                    rows.forEach(row => {
                        const priceInput = row.querySelector('input[name="VarPrecio"]');
                        const stockInput = row.querySelector('input[name="VarStock"]');

                        if (!priceInput?.value || Helpers.toNum(priceInput.value) <= 0) {
                            priceInput?.classList.add('error');
                            hasErrors = true;
                        }

                        if (!stockInput?.value || Helpers.toNum(stockInput.value) < 0) {
                            stockInput?.classList.add('error');
                            hasErrors = true;
                        }
                    });

                    if (hasErrors) {
                        State.validationErrors.push('Algunas variantes tienen precios o stocks inválidos');
                    }
                }
            }

            const imageCount = Helpers.$$('#galeria .image-thumbnail:not(.removed)').length;
            if (imageCount === 0) {
                State.validationErrors.push('Debe agregar al menos una imagen');
            }

            if (State.validationErrors.length > 0) {
                this.showErrors();
                Helpers.toast('Corrija los errores antes de enviar', 'error');
                return false;
            }

            return true;
        },

        showErrors() {
            const card = Helpers.$('validationCard');
            const list = Helpers.$('validationErrors');

            if (!card || !list) return;

            list.innerHTML = '';
            State.validationErrors.forEach(error => {
                const li = document.createElement('li');
                li.textContent = error;
                list.appendChild(li);
            });

            card.style.display = 'block';

            const firstError = document.querySelector('.input-has-error, .error');
            if (firstError) {
                firstError.scrollIntoView({ behavior: 'smooth', block: 'center' });
                firstError.focus();
            }
        }
    };

    // ========== MÓDULO: PREVIEW ==========
    const PreviewModule = {
        update: Helpers.debounce(function () {
            const pc = Helpers.toNum(Helpers.$('PrecioCompra')?.value);
            const pv = Helpers.toNum(Helpers.$('PrecioVenta')?.value);

            const stock = State.hasVariants
                ? Helpers.toNum(Helpers.$('summaryStock')?.textContent || 0)
                : Helpers.toNum(Helpers.$('Stock')?.value);

            const margin = pv - pc;

            const priceEl = Helpers.$('previewPrice');
            const marginEl = Helpers.$('previewMargin');
            const stockEl = Helpers.$('previewStock');

            if (priceEl) priceEl.textContent = Helpers.fmtUSD(pv);
            if (marginEl) marginEl.textContent = Helpers.fmtUSD(margin);
            if (stockEl) stockEl.textContent = `${stock} unidades`;
        }, 200)
    };

    // ========== MÓDULO: SUBMIT ==========
    const SubmitModule = {
        init() {
            const form = Helpers.$('productoForm');
            if (!form) return;

            form.addEventListener('submit', (e) => {
                if (!this.prepareSubmit()) {
                    e.preventDefault();
                    State.isSubmitting = false;
                    this.resetSubmitButton();
                }
            });
        },

        prepareSubmit() {
            // Validar
            if (!ValidationModule.validateForm()) {
                return false;
            }

            // Preparar envío
            State.isSubmitting = true;
            this.disableSubmitButton();

            // ✅ CRITICAL FIX: NO NORMALIZAR VALORES
            // El InvariantDecimalModelBinder en el servidor ya maneja la normalización
            // Dejar los valores tal cual están para que el navegador los envíe correctamente

            // Remover variantes si modo simple
            if (!State.hasVariants) {
                Helpers.$$('input[name^="Var"]').forEach(input => input.remove());
            }

            Helpers.toast('Guardando producto...', 'info');
            Helpers.log('Form submitted');

            return true;
        },

        disableSubmitButton() {
            const btn = Helpers.$('btnSubmit');
            if (btn) {
                btn.disabled = true;
                btn.innerHTML = '<i class="fas fa-spinner spinner"></i> Guardando...';
            }
        },

        resetSubmitButton() {
            const btn = Helpers.$('btnSubmit');
            if (btn) {
                btn.disabled = false;
                btn.innerHTML = '<i class="fas fa-floppy-disk"></i> Guardar Producto';
            }
        }
    };

    // ========== INICIALIZACIÓN ==========
    function init() {
        try {
            Helpers.log('Initializing ProductoForm...');

            SubcategoriesModule.init();
            PricingModule.init();
            ImagesModule.init();
            VariantsModule.init();
            ValidationModule.init();
            SubmitModule.init();

            PreviewModule.update();

            const fieldsToWatch = ['#Nombre', '#Marca', '#Stock', '#PrecioCompra', '#PrecioVenta'];
            fieldsToWatch.forEach(selector => {
                const el = document.querySelector(selector);
                if (el) {
                    el.addEventListener('input', Helpers.debounce(() => PreviewModule.update(), 200));
                }
            });

            if (window.innerWidth < 768) {
                Helpers.$$('.form-input, .form-select, .form-textarea').forEach(input => {
                    input.addEventListener('focus', function () {
                        this.scrollIntoView({ behavior: 'smooth', block: 'center' });
                    });
                });
            }

            Helpers.log('ProductoForm initialized successfully ✓');
        } catch (error) {
            Helpers.error('Failed to initialize ProductoForm', error);
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

})();