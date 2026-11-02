(function () {
    const form = document.getElementById("forgotPasswordForm");
    const button = document.getElementById("sendCodeButton");
    const label = document.getElementById("sendCodeLabel");
    const spinner = button?.querySelector(".send-code-spinner");
    const status = document.getElementById("sendCodeStatus");

    if (!form || !button || !label || !spinner || !status) return;

    let submitting = false;

    function resetButton() {
        submitting = false;
        button.disabled = false;
        label.textContent = "Send code";
        spinner.hidden = true;
        status.textContent = "";
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
        label.textContent = "Sending code…";
        spinner.hidden = false;
        status.textContent = "Sending your verification code. Please wait.";
        form.setAttribute("aria-busy", "true");
    });

    window.addEventListener("pageshow", resetButton);
})();