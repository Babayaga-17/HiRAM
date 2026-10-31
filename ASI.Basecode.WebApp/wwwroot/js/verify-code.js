(function () {
    const page = document.getElementById("verifyCodePage");
    const expiryText = document.getElementById("codeExpiryText");
    const cooldownText = document.getElementById("resendCooldownText");
    const form = document.getElementById("resendCodeForm");
    const button = document.getElementById("resendCodeButton");
    const status = document.getElementById("resendCodeStatus");

    if (!page || !expiryText || !cooldownText ||
        !form || !button || !status) return;

    const serverNow = Date.parse(page.dataset.serverNow);
    const expiresAt = Date.parse(page.dataset.expiresAt);
    const resendAvailableAt = Date.parse(page.dataset.resendAvailableAt);

    if (!Number.isFinite(serverNow)) return;

    const startedAt = performance.now();
    let submitting = false;

    function currentServerTime() {
        return serverNow + performance.now() - startedAt;
    }

    function secondsUntil(timestamp) {
        if (!Number.isFinite(timestamp)) return 0;

        return Math.max(
            0,
            Math.ceil((timestamp - currentServerTime()) / 1000)
        );
    }

    function formatTime(seconds) {
        const minutes = Math.floor(seconds / 60);
        const remainder = seconds % 60;

        return String(minutes).padStart(2, "0") +
            ":" + String(remainder).padStart(2, "0");
    }

    function updateTimers() {
        const expirySeconds = secondsUntil(expiresAt);
        const cooldownSeconds = secondsUntil(resendAvailableAt);

        if (!Number.isFinite(expiresAt)) {
            expiryText.textContent =
                "No active code. Request a new code.";
        } else if (expirySeconds === 0) {
            expiryText.textContent =
                "Code expired. Request a new code.";
        } else {
            expiryText.textContent =
                "Code expires in " + formatTime(expirySeconds);
        }

        cooldownText.textContent = cooldownSeconds > 0
            ? "Resend available in " + formatTime(cooldownSeconds)
            : "You can request a new code.";

        button.disabled = submitting || cooldownSeconds > 0;
    }

    form.addEventListener("submit", function (event) {
        if (submitting || secondsUntil(resendAvailableAt) > 0) {
            event.preventDefault();
            return;
        }

        if (event.defaultPrevented) return;

        submitting = true;
        button.disabled = true;
        button.textContent = "Sending…";
        status.textContent = "Sending a new verification code.";
        form.setAttribute("aria-busy", "true");
    });

    window.addEventListener("pageshow", function (event) {
        if (event.persisted) {
            // Fetch current timestamps after browser Back restores the page.
            window.location.reload();
        }
    });

    updateTimers();
    window.setInterval(updateTimers, 1000);
})();