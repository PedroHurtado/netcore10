// Caso práctico Noticom (día 5): el <script> de la vista Lote/Index.cshtml de MVC 5, sacado a un fichero.
//
// Lo que cambia respecto al legacy (ver docs/day-05/03-caso-noticom.md):
//   - Sin Razor: las URLs llegan en atributos data-* del contenedor (#lotes). El fichero se puede cachear y la CSP
//     (script-src 'self') lo permite; un <script> inline con '@Model.TipoEjecucion' lo bloquearía.
//   - Sin jQuery: fetch y addEventListener (el legacy cargaba jQuery solo para esto).
//   - MEJORA PROGRESIVA: sin JavaScript todo funciona (formulario GET, enlaces de paginación, formularios POST).
//     Con JavaScript, la búsqueda y la paginación recargan solo la tabla.
//   - Nada de ocultar columnas por número ni de guardar el filtro en sessionStorage: eso lo resuelve el servidor y la URL.
//   - Sesión caducada: el legacy buscaba el texto "_Logon_" en una respuesta 200. Aquí el servidor responde 401
//     porque enviamos la cabecera X-Requested-With (jQuery la añadía sola; fetch NO).
"use strict";

(function () {
    const contenedor = document.getElementById("lotes");
    const formulario = document.getElementById("filtro-lotes");
    if (!contenedor || !formulario) {
        return;
    }

    const urlTabla = contenedor.dataset.urlTabla;
    const urlIndex = contenedor.dataset.urlIndex;

    async function cargarTabla(parametros) {
        contenedor.classList.add("cargando");
        try {
            const respuesta = await fetch(`${urlTabla}?${parametros}`, {
                headers: { "X-Requested-With": "XMLHttpRequest" }   // sin esto, una sesión caducada devolvería el HTML del login (302 → 200)
            });

            if (respuesta.status === 401) {
                // Sesión caducada: al login, volviendo después a ESTA página (no a /Tabla, que es solo un fragmento).
                const volver = encodeURIComponent(location.pathname + location.search);
                location.href = `/Cuenta/Login?ReturnUrl=${volver}`;
                return;
            }
            if (!respuesta.ok) {
                throw new Error(`HTTP ${respuesta.status}`);
            }

            contenedor.innerHTML = await respuesta.text();   // HTML generado por Razor: ya viene codificado (sin XSS)
            history.replaceState(null, "", `${urlIndex}?${parametros}`);   // la URL refleja la búsqueda (F5, marcadores, atrás)
        } catch (error) {
            // Si algo falla, se hace lo mismo que sin JavaScript: navegar a la página completa.
            console.error("No se pudo cargar la tabla de lotes", error);
            location.href = `${urlIndex}?${parametros}`;
        } finally {
            contenedor.classList.remove("cargando");
        }
    }

    // Buscar: en lugar de navegar, pedir solo la tabla.
    formulario.addEventListener("submit", (evento) => {
        evento.preventDefault();
        const parametros = new URLSearchParams(new FormData(formulario));
        for (const [clave, valor] of [...parametros]) {
            if (valor === "") parametros.delete(clave);   // URLs limpias: sin "proceso=&ejercicio="
        }
        cargarTabla(parametros);
    });

    // Delegación de eventos en el contenedor: sigue funcionando después de sustituir su contenido.
    contenedor.addEventListener("click", (evento) => {
        // Paginación
        const enlace = evento.target.closest("a[data-pagina]");
        if (enlace) {
            evento.preventDefault();
            cargarTabla(new URL(enlace.href).searchParams);
            return;
        }

        // Casilla "seleccionar todos"
        const todos = evento.target.closest("input[data-seleccionar-todos]");
        if (todos) {
            contenedor.querySelectorAll("input[name='LoteIds']").forEach((casilla) => {
                casilla.checked = todos.checked;
            });
        }
    });

    // Confirmaciones antes de enviar (antes: modalConfirmacion(...) llamado desde onclick, que la CSP bloquea).
    contenedor.addEventListener("submit", (evento) => {
        const form = evento.target;

        if (form.dataset.confirmar !== undefined && !confirm(form.dataset.confirmar)) {
            evento.preventDefault();
            return;
        }

        if (form.dataset.confirmarRemesa !== undefined) {
            const marcados = contenedor.querySelectorAll("input[name='LoteIds']:checked").length;
            if (marcados === 0) {
                // Aviso inmediato. El servidor lo comprueba igualmente: esto es comodidad, no seguridad.
                evento.preventDefault();
                alert("No se ha seleccionado ningún lote.");
                return;
            }
            const texto = marcados === 1 ? "1 lote" : `${marcados} lotes`;
            if (!confirm(`Se va a crear una remesa a partir de ${texto}. ¿Continuar?`)) {
                evento.preventDefault();
            }
        }
    });
})();
