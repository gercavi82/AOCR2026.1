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
    });
})();
