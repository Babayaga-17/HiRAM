;(function () {
    const PAGE_SIZE = 12;

    const root = document.getElementById("browseRoot");
    const search = document.getElementById("browseSearch");
    const categoryFilter = document.getElementById("categoryFilter");
    const statusFilter = document.getElementById("statusFilter");
    const grid = document.getElementById("browseGrid");
    const cards = Array.from(grid.querySelectorAll(".equipment-card"));
    const emptyResults = document.getElementById("emptyResults");
    const browseCount = document.getElementById("browseCount");
    const pager = document.getElementById("pager");
    const viewButtons = Array.from(document.querySelectorAll(".view-toggle button"));

    const tray = document.getElementById("selectionTray");
    const trayCount = document.getElementById("selectionCount");
    const trayList = document.getElementById("selectionChips");

    const token = document.querySelector('input[name="__RequestVerificationToken"]').value;
    const selected = new Set((root.dataset.selected || "").split(",").filter(Boolean).map(Number));
    let page = 1;

    cards.forEach(card => {
        const image = card.querySelector(".equipment-photo-img");
        const hide = () => { image.hidden = true; };
        image.addEventListener("error", hide);
        if (image.complete && image.naturalWidth === 0 && image.getAttribute("src")) hide();
    });

    // Search, filter, paging

    function matchingCards() {
        const term = search.value.trim().toLowerCase();

        return cards.filter(card => {
            const matchesText = [card.dataset.name, card.dataset.code, card.dataset.brand, card.dataset.model]
                .some(value => (value || "").toLowerCase().includes(term));
            const matchesCategory = categoryFilter.value === "all" || card.dataset.category === categoryFilter.value;
            const matchesStatus = statusFilter.value === "all" || card.dataset.status === statusFilter.value;
            return matchesText && matchesCategory && matchesStatus;
        });
    }

    function renderPager(pages) {
        pager.innerHTML = "";
        if (pages <= 1) return;

        const add = (label, target, isActive, isDisabled, ariaLabel) => {
            const button = document.createElement("button");
            button.type = "button";
            button.className = "pager-link" + (isActive ? " pager-active" : "");
            button.textContent = label;
            button.disabled = isDisabled;
            if (ariaLabel) button.setAttribute("aria-label", ariaLabel);
            button.addEventListener("click", () => { page = target; render(); });
            pager.appendChild(button);
        };

        add("‹", page - 1, false, page === 1, "Previous page");
        for (let p = 1; p <= pages; p++) add(String(p), p, p === page, false);
        add("›", page + 1, false, page === pages, "Next page");
    }

    function render() {
        const found = matchingCards();
        const pages = Math.max(1, Math.ceil(found.length / PAGE_SIZE));
        if (page > pages) page = pages;

        const start = (page - 1) * PAGE_SIZE;
        const visible = new Set(found.slice(start, start + PAGE_SIZE));
        cards.forEach(card => { card.hidden = !visible.has(card); });

        grid.hidden = found.length === 0;
        emptyResults.hidden = found.length !== 0;
        browseCount.textContent = found.length === 0
            ? "Showing 0 of " + cards.length + " equipment items"
            : "Showing " + (start + 1) + "–" + (start + visible.size) + " of " + found.length + " equipment items";
        renderPager(pages);
    }

    function filterChanged() {
        page = 1;
        render();
    }

    function setView(view) {
        grid.classList.toggle("is-list", view === "list");
        viewButtons.forEach(button => button.classList.toggle("is-active", button.dataset.view === view));
        try { localStorage.setItem("hiramBrowseView", view); } catch (error) { /* storage unavailable */ }
    }

    // Select equipment 

    async function postSelection(url, id) {
        const body = new URLSearchParams({ __RequestVerificationToken: token });
        if (id !== undefined) body.set("id", id);

        const response = await fetch(url, { method: "POST", body });
        const data = await response.json().catch(() => ({}));
        if (!response.ok) throw new Error(data.message || "Request failed.");

        selected.clear();
        (data.ids || []).forEach(value => selected.add(Number(value)));
        renderSelection();
    }

    async function toggleSelection(card) {
        const id = Number(card.dataset.id);
        const url = selected.has(id) ? root.dataset.deselectUrl : root.dataset.selectUrl;

        try {
            await postSelection(url, id);
        } catch (error) {
            alert(error.message || "Unable to update your selection. Please try again.");
        }
    }

    function renderSelection() {
        cards.forEach(card => {
            const button = card.querySelector(".select-equipment");
            const isSelected = selected.has(Number(card.dataset.id));
            const selectable = card.dataset.selectable === "true";

            button.disabled = !selectable;
            button.textContent = !selectable ? "Not available" : (isSelected ? "Selected" : "Select");
            button.classList.toggle("is-selected", isSelected);
            card.classList.toggle("is-selected", isSelected);
        });

        const chosen = cards.filter(card => selected.has(Number(card.dataset.id)));
        tray.hidden = chosen.length === 0;
        trayCount.textContent = chosen.length;
        trayList.innerHTML = "";

        chosen.forEach(card => {
            const chip = document.createElement("li");
            chip.className = "selection-chip";

            const name = document.createElement("span");
            name.textContent = card.dataset.name;

            const remove = document.createElement("button");
            remove.type = "button";
            remove.textContent = "×";
            remove.setAttribute("aria-label", "Remove " + card.dataset.name);
            remove.addEventListener("click", () => toggleSelection(card));

            chip.appendChild(name);
            chip.appendChild(remove);
            trayList.appendChild(chip);
        });

    }

    // Events 

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

    viewButtons.forEach(button => button.addEventListener("click", () => setView(button.dataset.view)));

    document.getElementById("clearSelection").addEventListener("click", async () => {
        try {
            await postSelection(root.dataset.clearUrl);
        } catch (error) {
            alert(error.message || "Unable to clear your selection. Please try again.");
        }
    });

    const stateKey = root.dataset.stateKey;
    cards.forEach(card => {
        card.querySelector(".select-equipment").addEventListener("click", () => toggleSelection(card));
        card.querySelector(".view-equipment").addEventListener("click", () => {
            try {
                sessionStorage.setItem(stateKey, JSON.stringify({
                    search: search.value, category: categoryFilter.value, status: statusFilter.value,
                    page, scroll: document.getElementById("mainContent").scrollTop
                }));
            } catch { /* Browsing still works when storage is blocked. */ }
        });
    });

    let restoreScroll = 0;
    try {
        const state = JSON.parse(sessionStorage.getItem(stateKey) || "null");
        sessionStorage.removeItem(stateKey);
        if (state) {
            if (typeof state.search === "string") search.value = state.search;
            if (Array.from(categoryFilter.options).some(option => option.value === state.category)) categoryFilter.value = state.category;
            if (Array.from(statusFilter.options).some(option => option.value === state.status)) statusFilter.value = state.status;
            if (Number.isSafeInteger(state.page) && state.page > 0) page = state.page;
            if (Number.isFinite(state.scroll)) restoreScroll = state.scroll;
        }
    } catch { /* Ignore invalid saved state. */ }

    // Browser Back may restore the page from memory; refresh session selection then.
    window.addEventListener("pageshow", event => { if (event.persisted) window.location.reload(); });

    let savedView = "grid";
    try { savedView = localStorage.getItem("hiramBrowseView") || "grid"; } catch (error) { /* storage unavailable */ }

    setView(savedView);
    renderSelection();
    render();
    requestAnimationFrame(() => { document.getElementById("mainContent").scrollTop = restoreScroll; });
})();
;(function () {
    const PAGE_SIZE = 12;

    const root = document.getElementById("browseRoot");
    const search = document.getElementById("browseSearch");
    const categoryFilter = document.getElementById("categoryFilter");
    const statusFilter = document.getElementById("statusFilter");
    const grid = document.getElementById("browseGrid");
    const cards = Array.from(grid.querySelectorAll(".equipment-card"));
    const emptyResults = document.getElementById("emptyResults");
    const browseCount = document.getElementById("browseCount");
    const pager = document.getElementById("pager");
    const viewButtons = Array.from(document.querySelectorAll(".view-toggle button"));

    const tray = document.getElementById("selectionTray");
    const trayCount = document.getElementById("selectionCount");
    const trayList = document.getElementById("selectionChips");

    const token = document.querySelector('input[name="__RequestVerificationToken"]').value;
    const selected = new Set((root.dataset.selected || "").split(",").filter(Boolean).map(Number));
    let page = 1;

    cards.forEach(card => {
        const image = card.querySelector(".equipment-photo-img");
        const hide = () => { image.hidden = true; };
        image.addEventListener("error", hide);
        if (image.complete && image.naturalWidth === 0 && image.getAttribute("src")) hide();
    });

    // Search, filter, paging

    function matchingCards() {
        const term = search.value.trim().toLowerCase();

        return cards.filter(card => {
            const matchesText = [card.dataset.name, card.dataset.code, card.dataset.brand, card.dataset.model]
                .some(value => (value || "").toLowerCase().includes(term));
            const matchesCategory = categoryFilter.value === "all" || card.dataset.category === categoryFilter.value;
            const matchesStatus = statusFilter.value === "all" || card.dataset.status === statusFilter.value;
            return matchesText && matchesCategory && matchesStatus;
        });
    }

    function renderPager(pages) {
        pager.innerHTML = "";
        if (pages <= 1) return;

        const add = (label, target, isActive, isDisabled, ariaLabel) => {
            const button = document.createElement("button");
            button.type = "button";
            button.className = "pager-link" + (isActive ? " pager-active" : "");
            button.textContent = label;
            button.disabled = isDisabled;
            if (ariaLabel) button.setAttribute("aria-label", ariaLabel);
            button.addEventListener("click", () => { page = target; render(); });
            pager.appendChild(button);
        };

        add("‹", page - 1, false, page === 1, "Previous page");
        for (let p = 1; p <= pages; p++) add(String(p), p, p === page, false);
        add("›", page + 1, false, page === pages, "Next page");
    }

    function render() {
        const found = matchingCards();
        const pages = Math.max(1, Math.ceil(found.length / PAGE_SIZE));
        if (page > pages) page = pages;

        const start = (page - 1) * PAGE_SIZE;
        const visible = new Set(found.slice(start, start + PAGE_SIZE));
        cards.forEach(card => { card.hidden = !visible.has(card); });

        grid.hidden = found.length === 0;
        emptyResults.hidden = found.length !== 0;
        browseCount.textContent = found.length === 0
            ? "Showing 0 of " + cards.length + " equipment items"
            : "Showing " + (start + 1) + "–" + (start + visible.size) + " of " + found.length + " equipment items";
        renderPager(pages);
    }

    function filterChanged() {
        page = 1;
        render();
    }

    function setView(view) {
        grid.classList.toggle("is-list", view === "list");
        viewButtons.forEach(button => button.classList.toggle("is-active", button.dataset.view === view));
        try { localStorage.setItem("hiramBrowseView", view); } catch (error) { /* storage unavailable */ }
    }

    // Select equipment 

    async function postSelection(url, id) {
        const body = new URLSearchParams({ __RequestVerificationToken: token });
        if (id !== undefined) body.set("id", id);

        const response = await fetch(url, { method: "POST", body });
        const data = await response.json().catch(() => ({}));
        if (!response.ok) throw new Error(data.message || "Request failed.");

        selected.clear();
        (data.ids || []).forEach(value => selected.add(Number(value)));
        renderSelection();
    }

    async function toggleSelection(card) {
        const id = Number(card.dataset.id);
        const url = selected.has(id) ? root.dataset.deselectUrl : root.dataset.selectUrl;

        try {
            await postSelection(url, id);
        } catch (error) {
            alert(error.message || "Unable to update your selection. Please try again.");
        }
    }

    function renderSelection() {
        cards.forEach(card => {
            const button = card.querySelector(".select-equipment");
            const isSelected = selected.has(Number(card.dataset.id));
            const selectable = card.dataset.selectable === "true";

            button.disabled = !selectable;
            button.textContent = !selectable ? "Not available" : (isSelected ? "Selected" : "Select");
            button.classList.toggle("is-selected", isSelected);
            card.classList.toggle("is-selected", isSelected);
        });

        const chosen = cards.filter(card => selected.has(Number(card.dataset.id)));
        tray.hidden = chosen.length === 0;
        trayCount.textContent = chosen.length;
        trayList.innerHTML = "";

        chosen.forEach(card => {
            const chip = document.createElement("li");
            chip.className = "selection-chip";

            const name = document.createElement("span");
            name.textContent = card.dataset.name;

            const remove = document.createElement("button");
            remove.type = "button";
            remove.textContent = "×";
            remove.setAttribute("aria-label", "Remove " + card.dataset.name);
            remove.addEventListener("click", () => toggleSelection(card));

            chip.appendChild(name);
            chip.appendChild(remove);
            trayList.appendChild(chip);
        });

    }

    // Events 

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

    viewButtons.forEach(button => button.addEventListener("click", () => setView(button.dataset.view)));

    document.getElementById("clearSelection").addEventListener("click", async () => {
        try {
            await postSelection(root.dataset.clearUrl);
        } catch (error) {
            alert(error.message || "Unable to clear your selection. Please try again.");
        }
    });

    const stateKey = root.dataset.stateKey;
    cards.forEach(card => {
        card.querySelector(".select-equipment").addEventListener("click", () => toggleSelection(card));
        card.querySelector(".view-equipment").addEventListener("click", () => {
            try {
                sessionStorage.setItem(stateKey, JSON.stringify({
                    search: search.value, category: categoryFilter.value, status: statusFilter.value,
                    page, scroll: document.getElementById("mainContent").scrollTop
                }));
            } catch { /* Browsing still works when storage is blocked. */ }
        });
    });

    let restoreScroll = 0;
    try {
        const state = JSON.parse(sessionStorage.getItem(stateKey) || "null");
        sessionStorage.removeItem(stateKey);
        if (state) {
            if (typeof state.search === "string") search.value = state.search;
            if (Array.from(categoryFilter.options).some(option => option.value === state.category)) categoryFilter.value = state.category;
            if (Array.from(statusFilter.options).some(option => option.value === state.status)) statusFilter.value = state.status;
            if (Number.isSafeInteger(state.page) && state.page > 0) page = state.page;
            if (Number.isFinite(state.scroll)) restoreScroll = state.scroll;
        }
    } catch { /* Ignore invalid saved state. */ }

    // Browser Back may restore the page from memory; refresh session selection then.
    window.addEventListener("pageshow", event => { if (event.persisted) window.location.reload(); });

    let savedView = "grid";
    try { savedView = localStorage.getItem("hiramBrowseView") || "grid"; } catch (error) { /* storage unavailable */ }

    setView(savedView);
    renderSelection();
    render();
    requestAnimationFrame(() => { document.getElementById("mainContent").scrollTop = restoreScroll; });
})();
