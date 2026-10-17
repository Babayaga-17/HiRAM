(function () {
    const rows = Array.from(
        document.querySelectorAll("#equipmentRows tr")
    );

    const detailsDialog = document.getElementById("detailsDialog");
    const deleteDialog = document.getElementById("deleteEquipmentDialog");

    const show = value => value && value.trim() ? value : "—";

    rows.forEach(row => {
        row.querySelector(".view-equipment").addEventListener("click", () => {
            const data = row.dataset;
            document.getElementById("detailsTitle").textContent = data.name;
            document.getElementById("detailsDescription").textContent = data.description || "No description provided.";
            document.getElementById("detailsCode").textContent = data.code;
            document.getElementById("detailsCategory").textContent = show(data.categoryName);
            document.getElementById("detailsBrandModel").textContent = show([data.brand, data.model].filter(Boolean).join(" "));
            document.getElementById("detailsSerial").textContent = show(data.serial);
            document.getElementById("detailsAsset").textContent = show(data.asset);
            document.getElementById("detailsCondition").textContent = data.condition;
            document.getElementById("detailsStatus").textContent = data.statusLabel;
            document.getElementById("detailsLocation").textContent = show(data.location);
            document.getElementById("detailsAcquired").textContent = show(data.acquired);
            document.getElementById("detailsCost").textContent = show(data.cost);
            document.getElementById("detailsNext").textContent = data.next || "None scheduled";
            document.getElementById("detailsEdit").href = row.querySelector(".edit-equipment").href;
            detailsDialog.showModal();
        });

        // Delete equipment
        const deleteButton = row.querySelector(".delete-equipment");

        if (deleteButton && deleteDialog) {
            deleteButton.addEventListener("click", () => {
                document.getElementById("deleteEquipmentId").value =
                    row.dataset.id;

                document.getElementById("deleteEquipmentName").textContent =
                    row.dataset.name;

                deleteDialog.showModal();
            });
        }
    });

    document.querySelectorAll(".dialog-close").forEach(button => {
        button.addEventListener("click", () => {
            const dialog = button.closest("dialog");

            if (dialog) {
                dialog.close();
            }
        });
    });

    // Close a dialog by clicking its backdrop
    [detailsDialog, deleteDialog].forEach(dialog => {
        if (!dialog) return;

        dialog.addEventListener("click", event => {
            if (event.target === dialog) {
                dialog.close();
            }
        });
    });
})();
