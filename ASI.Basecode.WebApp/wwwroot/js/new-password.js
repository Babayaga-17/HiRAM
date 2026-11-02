(function () {
    const form = document.getElementById("pwForm");
    const password = document.getElementById("newPassword");
    const confirmation = document.getElementById("confirmPassword");
    const mismatch = document.getElementById("mismatch");

    if (!form || !password || !confirmation || !mismatch) return;

    form.addEventListener("submit", function (event) {
        const matches = password.value === confirmation.value;

        mismatch.hidden = matches;

        if (!matches) {
            event.preventDefault();
        }
    });
})();