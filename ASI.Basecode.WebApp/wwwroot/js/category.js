
(function () {
    const PAGE_SIZE = 10;
    const search = document.getElementById("categorySearch");
    const statusFilter = document.getElementById("statusFilter");
    const rows = Array.from(document.querySelectorAll("#categoryRows tr"));
    const tableScroll = document.querySelector(".table-scroll");
    const emptyResults = document.getElementById("emptyResults");
    const resultCount = document.getElementById("resultCount");
    const pager = document.getElementById("pager");
    const detailsDialog = document.getElementById("detailsDialog");
    const deleteDialog = document.getElementById("deleteCategoryDialog");
    let page = 1;

    function matchingRows() {
        const term = search.value.trim().toLowerCase();

        return rows.filter(row => {
            const matchesText = [
                row.dataset.name,
                row.dataset.code,
                row.dataset.description
            ].some(value => (value || "").toLowerCase().includes(term));

            const matchesStatus =
                statusFilter.value === "all" ||
                row.dataset.status === statusFilter.value;

            return matchesText && matchesStatus;
        });
    }

    function renderPager(pages) {
        pager.innerHTML = "";
        if (pages <= 1) return;

        const addButton = (label, target, isActive) => {
            const button = document.createElement("button");
            button.type = "button";
            button.className = "pager-link" + (isActive ? " pager-active" : "");
            button.textContent = label;
            button.addEventListener("click", () => {
                page = target;
                render();
            });
            pager.appendChild(button);
        };

        if (page > 1) addButton("‹", page - 1, false);
        for (let p = 1; p <= pages; p++) {
            addButton(String(p), p, p === page);
        }
        if (page < pages) addButton("›", page + 1, false);
    }

    function render() {
        const found = matchingRows();
        const pages = Math.max(1, Math.ceil(found.length / PAGE_SIZE));

        if (page > pages) page = pages;

        const start = (page - 1) * PAGE_SIZE;
        const visible = new Set(found.slice(start, start + PAGE_SIZE));

        rows.forEach(row => {
            row.hidden = !visible.has(row);
        });

        tableScroll.hidden = found.length === 0;
        emptyResults.hidden = found.length !== 0;

        resultCount.textContent = found.length === 0
            ? "Showing 0 of " + rows.length + " categories"
            : "Showing " + (start + 1) + "–" +
              (start + visible.size) + " of " + found.length + " categories";

        renderPager(pages);
    }

    function filterChanged() {
        page = 1;
        render();
    }

    search.addEventListener("input", filterChanged);
    statusFilter.addEventListener("change", filterChanged);

    document.getElementById("clearFilters").addEventListener("click", () => {
        search.value = "";
        statusFilter.value = "all";
        filterChanged();
        search.focus();
    });

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

    render();
})();
