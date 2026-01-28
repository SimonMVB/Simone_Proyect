(function() {
    'use strict';

    // ===== COPY GENERIC =====
    document.querySelectorAll('[data-copy]').forEach(btn => {
        btn.addEventListener('click', () => {
            const text = btn.getAttribute('data-copy');
            if (!text) return;

            navigator.clipboard.writeText(text).then(() => {
                const prev = btn.innerHTML;
                btn.innerHTML = '<i class="fa-solid fa-check"></i>';
                setTimeout(() => btn.innerHTML = prev, 1200);
            });
        });
    });

    // ===== COPY ORDER =====
    const btnCopyOrder = document.getElementById('copyOrder');
    if (btnCopyOrder) {
        btnCopyOrder.addEventListener('click', () => {
            navigator.clipboard.writeText('#@Model.VentaID').then(() => {
                const prev = btnCopyOrder.innerHTML;
                btnCopyOrder.innerHTML = '<i class="fa-solid fa-check"></i><span>Copiado</span>';
                setTimeout(() => btnCopyOrder.innerHTML = prev, 1500);
            });
        });
    }

    // ===== COPY LABEL =====
    const btnCopy = document.getElementById('btnCopy');
    if (btnCopy) {
        btnCopy.addEventListener('click', () => {
            const text = document.getElementById('copyContent').innerText;
            navigator.clipboard.writeText(text).then(() => {
                const prev = btnCopy.innerHTML;
                btnCopy.innerHTML = '<i class="fa-solid fa-check"></i>Copiado';
                setTimeout(() => btnCopy.innerHTML = prev, 1800);
            });
        });
    }

    // ===== DOWNLOAD LABEL =====
    const btnDownload = document.getElementById('btnDownload');
    if (btnDownload) {
        btnDownload.addEventListener('click', () => {
            const text = document.getElementById('copyContent').innerText;
            const blob = new Blob([text], { type: 'text/plain;charset=utf-8' });
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = 'etiqueta-venta-' + '@Model.VentaID' + '.txt';
            a.click();
            URL.revokeObjectURL(url);
        });
    }

    // ===== COPY DEPOSITANTE =====
    const btnDep = document.getElementById('copyDepositante');
    if (btnDep) {
        btnDep.addEventListener('click', () => {
            const name = document.getElementById('depositanteText').innerText.trim();
            if (!name || name === '—') return;

            navigator.clipboard.writeText(name).then(() => {
                const prev = btnDep.innerHTML;
                btnDep.innerHTML = '<i class="fa-solid fa-check"></i>';
                setTimeout(() => btnDep.innerHTML = prev, 1400);
            });
        });
    }

    // ===== MARK AS SHIPPED =====
    const form = document.getElementById('markShippedForm');
    const btnShipped = document.getElementById('btnMarkShipped');

    if (form && btnShipped) {
        form.addEventListener('submit', async (e) => {
            e.preventDefault();
            if (btnShipped.disabled) return;

            btnShipped.disabled = true;
            btnShipped.classList.add('btn-loading');
            const prevHTML = btnShipped.innerHTML;
            btnShipped.innerHTML = '<span style="visibility:hidden;">Procesando...</span>';

            try {
                const res = await fetch(form.action, {
                    method: 'POST',
                    body: new FormData(form),
                    headers: { 'X-Requested-With': 'XMLHttpRequest' }
                });

                if (res.ok) {
                    location.reload();
                } else {
                    btnShipped.disabled = false;
                    btnShipped.classList.remove('btn-loading');
                    btnShipped.innerHTML = prevHTML;
                    alert('No se pudo marcar como enviado');
                }
            } catch {
                btnShipped.disabled = false;
                btnShipped.classList.remove('btn-loading');
                btnShipped.innerHTML = prevHTML;
                alert('Error de red');
            }
        });
    }

    console.log('✓ Admin detail initialized');
})();
