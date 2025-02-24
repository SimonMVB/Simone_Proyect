// ================================
// 🔹 SIMONE - Funcionalidades Globales
// ================================

// Espera a que el DOM se cargue completamente antes de ejecutar el código
document.addEventListener("DOMContentLoaded", function () {
    // ✅ Sidebar de categorías
    configurarSidebar();

    // ✅ Botones de interacción
    configurarBotonesInteraccion();

    // ✅ Mensajes de alerta automáticos
    configurarMensajesAlertas();

    // ✅ Validación de formularios
    inicializarValidaciones();

    // ✅ Scroll suave para los enlaces de la página
    configurarScrollSuave();
});

// ================================
// 🔹 Configurar Sidebar Desplegable
// ================================
function configurarSidebar() {
    let menuToggle = document.getElementById("menu-toggle");
    let sidebar = document.getElementById("sidebar");
    let closeSidebar = document.getElementById("close-sidebar");

    if (menuToggle && sidebar) {
        menuToggle.addEventListener("click", function () {
            sidebar.style.left = "0";
        });

        closeSidebar.addEventListener("click", function () {
            sidebar.style.left = "-250px";
        });
    }
}

// ================================
// 🔹 Configurar Botones de Interacción (Favoritos, Carrito, etc.)
// ================================
function configurarBotonesInteraccion() {
    let botones = document.querySelectorAll(".btn-interaccion");

    botones.forEach(boton => {
        boton.addEventListener("click", function () {
            this.classList.toggle("activo");
        });
    });
}

// ================================
// 🔹 Configurar Mensajes de Alertas Automáticas
// ================================
function configurarMensajesAlertas() {
    let alertas = document.querySelectorAll(".alert");

    alertas.forEach(alerta => {
        setTimeout(() => {
            alerta.classList.add("fade-out");
            setTimeout(() => alerta.remove(), 500);
        }, 4000);
    });
}

// ================================
// 🔹 Inicializar Validaciones de Formularios
// ================================
function inicializarValidaciones() {
    let formularios = document.querySelectorAll("form");

    formularios.forEach(formulario => {
        formulario.addEventListener("submit", function (event) {
            let camposRequeridos = formulario.querySelectorAll("[required]");
            let esValido = true;

            camposRequeridos.forEach(campo => {
                if (!campo.value.trim()) {
                    esValido = false;
                    campo.classList.add("is-invalid");
                } else {
                    campo.classList.remove("is-invalid");
                }
            });

            if (!esValido) {
                event.preventDefault();
            }
        });
    });
}

// ================================
// 🔹 Scroll Suave para los Enlaces de la Página
// ================================
function configurarScrollSuave() {
    document.querySelectorAll('a[href^="#"]').forEach(ancla => {
        ancla.addEventListener("click", function (event) {
            event.preventDefault();
            let destino = document.querySelector(this.getAttribute("href"));
            if (destino) {
                destino.scrollIntoView({ behavior: "smooth" });
            }
        });
    });
}
