(function () {
    const form = document.getElementById("forgotPasswordForm");
    const button = document.getElementById("sendCodeButton");
    const label = document.getElementById("sendCodeLabel");
    const spinner = button?.querySelector(".send-code-spinner");

    if (!form || !button || !label || !spinner) return;

    let submitting = false;

    function resetButton() {
        submitting = false;
        button.disabled = false;
        spinner.hidden = true;
        form.removeAttribute("aria-busy");
    }

    form.addEventListener("submit", function (event) {
        if (submitting) {
            event.preventDefault();
            return;
        }

        if (event.defaultPrevented || !form.checkValidity()) return;

        submitting = true;
        button.disabled = true;
        spinner.hidden = false;
        form.setAttribute("aria-busy", "true");
    });

    window.addEventListener("pageshow", resetButton);
})();
