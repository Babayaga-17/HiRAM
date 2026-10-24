/*
 * User and Role Management (WBS 5.1) page behaviour.
 * Same style as category.js: list filtering, <dialog> confirmations, copy button.
 * All rules are enforced on the server; this script only improves the UI.
 */
(function () {
    // ----- Users list: search + role + status filters -----
    const search = document.getElementById("userSearch");
    const roleFilter = document.getElementById("roleFilter");
    const statusFilter = document.getElementById("statusFilter");
    const rows = Array.from(document.querySelectorAll("#userRows tr"));

    function applyFilters() {
        const term = search.value.trim().toLowerCase();
        const role = roleFilter.value;
        const status = statusFilter.value;
        let visible = 0;
        rows.forEach(row => {
            const matches = row.dataset.search.includes(term)
                && (role === "all" || row.dataset.role === role)
                && (status === "all" || row.dataset.status === status);
            row.hidden = !matches;
            if (matches) visible++;
        });
        document.getElementById("emptyResults").hidden = visible !== 0;
        document.getElementById("resultCount").textContent = `Showing ${visible} of ${rows.length} users`;
    }

    if (search && roleFilter && statusFilter) {
        search.addEventListener("input", applyFilters);
        roleFilter.addEventListener("change", applyFilters);
        statusFilter.addEventListener("change", applyFilters);
        document.getElementById("clearFilters").addEventListener("click", () => {
            search.value = "";
            roleFilter.value = "all";
            statusFilter.value = "all";
            applyFilters();
            search.focus();
        });
    }

    // ----- Confirmation dialogs (deactivate / reset access) -----
    document.querySelectorAll("[data-open-dialog]").forEach(button => {
        button.addEventListener("click", () => document.getElementById(button.dataset.openDialog)?.showModal());
    });
    document.querySelectorAll(".dialog-close").forEach(button => {
        button.addEventListener("click", () => button.closest("dialog").close());
    });

    // ----- Prevent double submission of any form on these pages -----
    document.querySelectorAll("form").forEach(form => {
        form.addEventListener("submit", event => {
            if (window.jQuery && jQuery(form).data("validator") && !jQuery(form).valid()) return;
            if (form.dataset.submitted === "true") {
                event.preventDefault();
                return;
            }
            form.dataset.submitted = "true";
            form.querySelectorAll("button[type=submit]").forEach(button => button.disabled = true);
        });
    });

    // ----- Temporary password copy -----
    const copyButton = document.getElementById("copyPassword");
    if (copyButton) {
        copyButton.addEventListener("click", () => {
            const text = document.getElementById("temporaryPassword").textContent;
            navigator.clipboard?.writeText(text).then(
                () => copyButton.lastChild.textContent = "Copied",
                () => copyButton.lastChild.textContent = "Copy failed");
        });
    }
})();
