(function () {
    const active = document.getElementById("equipmentActive");
    const statusLabel = document.getElementById("statusLabel");

    active.addEventListener("change", () => {
        statusLabel.textContent = active.checked ? "Active" : "Inactive";
    });
})();(function () {
    const active = document.getElementById("equipmentActive");
    const statusLabel = document.getElementById("statusLabel");

    active.addEventListener("change", () => {
        statusLabel.textContent = active.checked ? "Active" : "Inactive";
    });
})();