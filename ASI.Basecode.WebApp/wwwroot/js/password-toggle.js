(function () {
    document.querySelectorAll(".js-password-toggle").forEach(function (button) {
        const input = document.getElementById(button.dataset.target);
        const openEye = button.querySelector(".eye-open");
        const closedEye = button.querySelector(".eye-closed");

        if (!input || !openEye || !closedEye) return;

        const fieldName = button.dataset.passwordLabel || "password";

        function updateToggle() {
            const visible = input.type === "text";

            openEye.toggleAttribute("hidden", !visible);
            closedEye.toggleAttribute("hidden", visible);

            button.setAttribute(
                "aria-label",
                (visible ? "Hide " : "Show ") + fieldName
            );
        }

        updateToggle();

        button.addEventListener("click", function () {
            input.type = input.type === "password" ? "text" : "password";
            updateToggle();
        });
    });
})();