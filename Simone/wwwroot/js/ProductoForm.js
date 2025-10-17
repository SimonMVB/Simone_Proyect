// wwwroot/js/ProductoForm.js
(function () {
    // ===== Variantes ON/OFF =====
    function toggleVariantesUI(on) {
        if (on) {
            $('#variantes-container').show();
            $('#wrap-precio-venta, #wrap-simple').hide();
            $('#precioVenta, #stock, #talla, #color').prop('disabled', true).prop('required', false);
        } else {
            $('#variantes-container').hide();
            $('#wrap-precio-venta, #wrap-simple').show();
            $('#precioVenta, #stock').prop('disabled', false).prop('required', true);
            $('#talla, #color').prop('disabled', false);
        }
    }

    $('#UsarVariantes').on('change', function () {
        toggleVariantesUI($(this).is(':checked'));
    });
    toggleVariantesUI($('#UsarVariantes').is(':checked'));

    // ===== Agregar / quitar variante =====
    $('#btn-agregar-variante').on('click', function () {
        const tpl = `
      <div class="variante-item card mb-3">
        <div class="card-body">
          <div class="row g-2">
            <div class="col-md-3">
              <label class="form-label">Color *</label>
              <input class="form-control" name="VarColor" required>
            </div>
            <div class="col-md-3">
              <label class="form-label">Talla *</label>
              <input class="form-control" name="VarTalla" required>
            </div>
            <div class="col-md-2">
              <label class="form-label">Precio Venta *</label>
              <input type="number" inputmode="decimal" step="0.01" min="0" class="form-control" name="VarPrecioVenta" required>
            </div>
            <div class="col-md-2">
              <label class="form-label">Stock *</label>
              <input type="number" min="0" class="form-control" name="VarStock" required>
            </div>
            <div class="col-md-2 d-flex align-items-end">
              <button type="button" class="btn btn-outline-danger w-100 btn-quitar-variante">Quitar</button>
            </div>
          </div>
        </div>
      </div>`;
        $('#variantes-list').append(tpl);
    });

    $(document).on('click', '.btn-quitar-variante', function () {
        $(this).closest('.variante-item').remove();
    });

    // ===== Imágenes =====
    let imagenesIgnore = [];                  // URLs existentes a eliminar
    const $imagenesInput = $('#Imagenes');
    const fileMap = new Map();                // uid -> File (permite borrar antes de enviar)
    let uidSeq = 0;

    function updateFileInputFromMap() {
        const dt = new DataTransfer();
        for (const f of fileMap.values()) dt.items.add(f);
        $imagenesInput[0].files = dt.files;
    }

    function updateImgCount() {
        const count = $('#imagenes-preview .imagen-preview-item').length;
        $('#img-count').text(count);
    }

    function reindexNewImageRadios() {
        const $newItems = $('#imagenes-preview .imagen-preview-item[data-is-new="1"]');
        $newItems.each(function (idx) {
            $(this).find('input[type=radio][name=ImagenPrincipalIndex]').val(idx);
        });
        if ($('input[name="ImagenPrincipalIndex"]:checked').length === 0 &&
            $('input[name="existingImagenPath"]:checked').length === 0) {
            $('#ImagenPrincipalIndex').val(-1);
        }
    }

    // Carga nuevas imágenes
    $imagenesInput.on('change', function () {
        const files = Array.from(this.files || []);
        const $preview = $('#imagenes-preview');
        const current = $preview.children().length;

        if (current + files.length > 8) {
            alert('No puede exceder 8 imágenes.');
            updateFileInputFromMap(); // revertir selección
            return;
        }

        files.forEach((file) => {
            if ([...fileMap.values()].includes(file)) return; // evitar duplicar

            const uid = 'f' + (++uidSeq);
            fileMap.set(uid, file);

            const reader = new FileReader();
            reader.onload = (e) => {
                const card = `
          <div class="col-md-3 mb-3 imagen-preview-item" data-is-new="1" data-uid="${uid}">
            <div class="card">
              <img src="${e.target.result}" class="card-img-top" style="height:150px;object-fit:cover;">
              <div class="card-body p-2 d-flex flex-column gap-2">
                <div class="form-check">
                  <input class="form-check-input" type="radio" name="ImagenPrincipalIndex" value="0">
                  <label class="form-check-label">Principal</label>
                </div>
                <button type="button" class="btn btn-sm btn-danger btn-eliminar-imagen">Eliminar</button>
              </div>
            </div>
          </div>`;
                $preview.append(card);
                reindexNewImageRadios();
                updateImgCount();
            };
            reader.readAsDataURL(file);
        });

        updateFileInputFromMap();
    });

    // Elegir principal (existente) => desmarca nuevas
    $(document).on('change', 'input[name="existingImagenPath"]', function () {
        $('input[name="ImagenPrincipalIndex"]').prop('checked', false);
        $('#ImagenPrincipalIndex').val(-1);
    });

    // Eliminar imagen (nueva o existente)
    $(document).on('click', '.btn-eliminar-imagen', function () {
        const $item = $(this).closest('.imagen-preview-item');
        const isNew = $item.attr('data-is-new') === '1';
        const url = $item.attr('data-url');

        if (isNew) {
            const uid = $item.attr('data-uid');
            if (uid && fileMap.has(uid)) {
                fileMap.delete(uid);
                updateFileInputFromMap();
            }
        } else if (url) {
            imagenesIgnore.push(url);
            $('#ImagenesIgnore').val(JSON.stringify(imagenesIgnore));
        }

        if ($item.find('input[type=radio]:checked').length) {
            $('input[name="ImagenPrincipalIndex"]').prop('checked', false);
            $('input[name="existingImagenPath"]').prop('checked', false);
            $('#ImagenPrincipalIndex').val(-1);
        }

        $item.remove();
        reindexNewImageRadios();
        updateImgCount();
    });

    // ===== Subcategorías por categoría (AJAX) =====
    function fillSubcats(catId, selected) {
        const $sub = $('#subcategoriaID');
        $sub.empty().append('<option value="">Seleccione una subcategoría</option>');
        if (!catId) return;

        $.get($('#producto-form').data('subcats-url') || '/Panel/GetSubcategoriasByCategoria', { categoriaID: catId }, function (data) {
            $.each(data, function (_, item) {
                const $opt = $('<option/>', { value: item.value, text: item.text });
                if (selected && String(item.value) === String(selected)) $opt.attr('selected', 'selected');
                $sub.append($opt);
            });
        });
    }

    $('#categoriaID').on('change', function () {
        fillSubcats($(this).val(), null);
    });

    // Si abrimos en edición y el select de subcats está vacío, cargarlo
    (function initSubcatsOnLoad() {
        const catVal = $('#categoriaID').val();
        const selected = $('#subcategoriaID').data('selected');
        if (catVal && $('#subcategoriaID option').length <= 1) {
            fillSubcats(catVal, selected);
        }
    })();
})();
