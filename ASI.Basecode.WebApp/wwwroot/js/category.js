(function () {
    const search = document.getElementById("categorySearch");
    const filter = document.getElementById("statusFilter");
    const rows = Array.from(document.querySelectorAll("#categoryRows tr"));
    const detailsDialog = document.getElementById("detailsDialog"); 
    const deleteDialog = document.getElementById("deleteCategoryDialog");

    function applyFilters() {
        const term = search.value.trim().toLowerCase();
        const status = filter.value;
        let visible = 0;
        rows.forEach(row => {
            const matchesText = [row.dataset.name, row.dataset.code, row.dataset.description].some(value => value.toLowerCase().includes(term));
            const matchesStatus = status === "all" || row.dataset.status === status;
            row.hidden = !(matchesText && matchesStatus);
            if (!row.hidden) visible++;
        });
        document.getElementById("emptyResults").hidden = visible !== 0;
        document.getElementById("resultCount").textContent = `Showing ${visible} of ${rows.length} categories`;
    }

    search.addEventListener("input", applyFilters);
    filter.addEventListener("change", applyFilters);
    document.getElementById("clearFilters").addEventListener("click", () => {
        search.value = "";
        filter.value = "all";
        applyFilters();
        search.focus();
    });

    rows.forEach(row => {
        row.querySelector(".view-category").addEventListener("click", () => {
            document.getElementById("detailsTitle").textContent = row.dataset.name;
            document.getElementById("detailsDescription").textContent = row.dataset.description;
            document.getElementById("detailsCode").textContent = row.dataset.code;
            document.getElementById("detailsEquipment").textContent = `${row.dataset.equipment} items`;
            document.getElementById("detailsStatus").textContent = row.dataset.status === "active" ? "Active" : "Inactive";
            document.getElementById("detailsEdit").href = row.querySelector(".edit-category").href;
            detailsDialog.showModal();
        });

        const deleteButton = row.querySelector(".delete-category");
        if (deleteButton) {
            deleteButton.addEventListener("click", () => {
                document.getElementById("deleteCategoryId").value = row.dataset.id;
                document.getElementById("deleteCategoryName").textContent = row.dataset.name;
                deleteDialog.showModal();
            });
        }
    });

    document.querySelectorAll(".dialog-close").forEach(button => {
        button.addEventListener("click", () => button.closest("dialog").close());
    });
})();
