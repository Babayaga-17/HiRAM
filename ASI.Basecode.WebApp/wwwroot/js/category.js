(function () {
    const rows = Array.from(document.querySelectorAll("#categoryRows tr"));
    const detailsDialog = document.getElementById("detailsDialog");
    const deleteDialog = document.getElementById("deleteCategoryDialog");

    rows.forEach(row => {
        row.querySelector(".view-category").addEventListener("click", () => {
            document.getElementById("detailsTitle").textContent = row.dataset.name;
            document.getElementById("detailsDescription").textContent =
                row.dataset.description || "No description provided.";
            document.getElementById("detailsCode").textContent = row.dataset.code;
            document.getElementById("detailsEquipment").textContent =
                `${row.dataset.equipment} items`;
            document.getElementById("detailsStatus").textContent =
                row.dataset.status === "active" ? "Active" : "Inactive";
            document.getElementById("detailsEdit").href =
                row.querySelector(".edit-category").href;

            detailsDialog.showModal();
        });

        const deleteButton = row.querySelector(".delete-category");

        if (deleteButton) {
            deleteButton.addEventListener("click", () => {
                document.getElementById("deleteCategoryId").value = row.dataset.id;
                document.getElementById("deleteCategoryName").textContent =
                    row.dataset.name;
                deleteDialog.showModal();
            });
        }
    });

    document.querySelectorAll(".dialog-close").forEach(button => {
        button.addEventListener("click", () => {
            button.closest("dialog").close();
        });
    });
})();
