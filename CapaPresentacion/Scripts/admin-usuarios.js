(function () {
    function onReady(fn) {
        if (document.readyState === "loading") {
            document.addEventListener("DOMContentLoaded", fn);
        } else {
            fn();
        }
    }

    onReady(function () {
        document.addEventListener("submit", function (event) {
            var form = event.target;
            if (!form || !form.matches("form")) {
                return;
            }

            var confirmMessage = form.getAttribute("data-confirm");
            if (confirmMessage && !window.confirm(confirmMessage)) {
                event.preventDefault();
                return;
            }

            var submit = form.querySelector("button[type='submit']");
            if (submit) {
                submit.disabled = true;
                window.setTimeout(function () { submit.disabled = false; }, 1800);
            }
        });

        var chkGenerar = document.getElementById("GenerarPassword");
        var manualBlock = document.querySelector(".js-password-manual-block");
        if (chkGenerar && manualBlock) {
            var actualizar = function () {
                if (chkGenerar.checked) {
                    manualBlock.classList.add("d-none");
                } else {
                    manualBlock.classList.remove("d-none");
                }
            };

            chkGenerar.addEventListener("change", actualizar);
            actualizar();
        }

        var selectCompanias = document.querySelector(".js-companias-rt-select");
        var btnSeleccionarTodas = document.getElementById("btnSeleccionarTodasCompaniasAdmin");
        var btnLimpiar = document.getElementById("btnLimpiarCompaniasAdmin");
        var contador = document.getElementById("contadorCompaniasAdmin");

        var actualizarContadorCompanias = function () {
            if (!selectCompanias || !contador) {
                return;
            }

            var total = selectCompanias.options.length;
            var seleccionadas = 0;
            for (var i = 0; i < total; i++) {
                if (selectCompanias.options[i].selected) {
                    seleccionadas++;
                }
            }

            contador.textContent = seleccionadas + " seleccionada(s) de " + total;
        };

        if (selectCompanias) {
            selectCompanias.addEventListener("mousedown", function (event) {
                var option = event.target;
                if (!option || option.tagName !== "OPTION") {
                    return;
                }

                event.preventDefault();
                option.selected = !option.selected;
                actualizarContadorCompanias();
            });

            selectCompanias.addEventListener("change", actualizarContadorCompanias);
            actualizarContadorCompanias();
        }

        if (btnSeleccionarTodas && selectCompanias) {
            btnSeleccionarTodas.addEventListener("click", function () {
                for (var i = 0; i < selectCompanias.options.length; i++) {
                    selectCompanias.options[i].selected = true;
                }
                actualizarContadorCompanias();
            });
        }

        if (btnLimpiar && selectCompanias) {
            btnLimpiar.addEventListener("click", function () {
                for (var i = 0; i < selectCompanias.options.length; i++) {
                    selectCompanias.options[i].selected = false;
                }
                actualizarContadorCompanias();
            });
        }
    });
})();
