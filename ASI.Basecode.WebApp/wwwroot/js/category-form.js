(function () {
    const active = document.getElementById("categoryActive");
    const statusLabel = document.getElementById("statusLabel");

    active.addEventListener("change", () => {
        statusLabel.textContent = active.checked ? "Active" : "Inactive";
    });
})();
