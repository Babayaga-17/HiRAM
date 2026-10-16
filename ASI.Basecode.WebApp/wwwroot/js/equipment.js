(function () {
    const PAGE_SIZE = 10;
    const search = document.getElementById("equipmentSearch");
    const categoryFilter = document.getElementById("categoryFilter");
    const statusFilter = document.getElementById("statusFilter");
    const rows = Array.from(document.querySelectorAll("#equipmentRows tr"));
    const tableScroll = document.getElementById("equipmentTable");
    const emptyResults = document.getElementById("emptyResults");
    const resultCount = document.getElementById("resultCount");
    const pager = document.getElementById("pager");
    const detailsDialog = document.getElementById("detailsDialog");
    const deleteDialog = document.getElementById("deleteEquipmentDialog");
    let page = 1;

    // Keep the current table view across action POSTs and their redirects.
    const stateKey = "hiram.equipment.pendingTableState";
    const filters = { search, statusFilter, categoryFilter };
    try {
        const saved = JSON.parse(sessionStorage.getItem(stateKey) || "null");
        sessionStorage.removeItem(stateKey);
        if (saved) {
            if (Number.isSafeInteger(saved.page) && saved.page > 0) page = saved.page;
            Object.entries(filters).forEach(([key, field]) => {
                const value = saved[key];
                if (typeof value !== "string") return;
                if (field.tagName === "SELECT" &&
                    !Array.from(field.options).some(option => option.value === value)) return;
                field.value = value;
            });
        }
    } catch {
        // The table remains usable when browser storage is unavailable.
    }

    document.querySelectorAll("#equipmentRows form[method='post'], #deleteEquipmentDialog form[method='post']").forEach(form => {
        form.addEventListener("submit", () => {
            try {
                const state = { page };
                Object.entries(filters).forEach(([key, field]) => { state[key] = field.value; });
                sessionStorage.setItem(stateKey, JSON.stringify(state));
            } catch {
                // Let the action proceed even when browser storage is unavailable.
            }
        });
    });

    const show = value => (value && value.trim()) ? value : "—";


    // Search and filtering
    function matchingRows() {
        const term = search.value.trim().toLowerCase();
        return rows.filter(row => {
            const matchesText = [row.dataset.name, row.dataset.code, row.dataset.brand, row.dataset.model]
                .some(value => (value || "").toLowerCase().includes(term));
            const matchesCategory = categoryFilter.value === "all" || row.dataset.category === categoryFilter.value;
            const matchesStatus = statusFilter.value === "all" || row.dataset.status === statusFilter.value;
            return matchesText && matchesCategory && matchesStatus;
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
            button.addEventListener("click", () => { page = target; render(); });
            pager.appendChild(button);
        };

        if (page > 1) addButton("‹", page - 1, false);
        for (let p = 1; p <= pages; p++) addButton(String(p), p, p === page);
        if (page < pages) addButton("›", page + 1, false);
    }

    function render() {
        const found = matchingRows();
        const pages = Math.max(1, Math.ceil(found.length / PAGE_SIZE));
        if (page > pages) page = pages;

        const start = (page - 1) * PAGE_SIZE;
        const visible = new Set(found.slice(start, start + PAGE_SIZE));
        rows.forEach(row => { row.hidden = !visible.has(row); });

        tableScroll.hidden = found.length === 0;
        emptyResults.hidden = found.length !== 0;
        resultCount.textContent = found.length === 0
            ? "Showing 0 of " + rows.length + " records"
            : "Showing " + (start + 1) + "–" + (start + visible.size) + " of " + found.length + " records";
        renderPager(pages);
    }

    function filterChanged() {
        page = 1;
        render();
    }


    // Search and filter events
    search.addEventListener("input", filterChanged);
    categoryFilter.addEventListener("change", filterChanged);
    statusFilter.addEventListener("change", filterChanged);

    document.getElementById("clearFilters").addEventListener("click", () => {
        search.value = "";
        categoryFilter.value = "all";
        statusFilter.value = "all";
        filterChanged();
        search.focus();
    });

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

    render();
})();
