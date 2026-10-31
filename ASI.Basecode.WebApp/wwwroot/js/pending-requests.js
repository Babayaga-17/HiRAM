(function () {
    const search = document.getElementById("pendingSearch");
    const rows = Array.from(document.querySelectorAll("#pendingRows tr"));
    const emptyResults = document.getElementById("emptyResults");
    const resultCount = document.getElementById("resultCount");
    const clearButton = document.getElementById("clearPendingSearch");

    function applySearch() {
        const term = (search.value || "").trim().toLowerCase();
        let visible = 0;
        rows.forEach(row => {
            const matches = (row.dataset.search || "").includes(term);
            row.hidden = !matches;
            if (matches) visible++;
        });
        if (emptyResults) emptyResults.hidden = visible !== 0;
        if (resultCount) resultCount.textContent = `Showing ${visible} of ${rows.length} requests`;
    }

    if (search) {
        search.addEventListener("input", applySearch);
    }

    if (clearButton) {
        clearButton.addEventListener("click", () => {
            search.value = "";
            applySearch();
            search.focus();
        });
    }
})();
