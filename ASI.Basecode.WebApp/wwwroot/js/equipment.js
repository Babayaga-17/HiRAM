(function () {
    const rows = Array.from(
        document.querySelectorAll("#equipmentRows tr")
    );

    const deleteDialog = document.getElementById("deleteEquipmentDialog");

    rows.forEach(row => {

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
    [deleteDialog].forEach(dialog => {
        if (!dialog) return;

        dialog.addEventListener("click", event => {
            if (event.target === dialog) {
                dialog.close();
            }
        });
    });
})();
