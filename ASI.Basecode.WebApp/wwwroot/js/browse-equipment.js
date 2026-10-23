;(function () {
    const PAGE_SIZE = 12;
    const PLACEHOLDER_PHOTO_COUNT = 4;

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

    const dialog = document.getElementById("detailsDialog");
    const detailsSelect = document.getElementById("detailsSelect");
    const detailsStatusBadge = document.getElementById("detailsStatusBadge");
    const galleryMainImg = document.getElementById("galleryMainImg");
    const galleryPrev = document.getElementById("galleryPrev");
    const galleryNext = document.getElementById("galleryNext");
    const galleryCount = document.getElementById("galleryCount");
    const galleryThumbs = document.getElementById("galleryThumbs");
    const availabilityFrom = document.getElementById("availabilityFrom");
    const availabilityTo = document.getElementById("availabilityTo");
    const availabilityResult = document.getElementById("availabilityResult");
    const availabilityRange = document.getElementById("availabilityRange");
    const availabilityPeriods = document.getElementById("availabilityPeriods");
    const availabilitySlots = document.getElementById("availabilitySlots");

    const token = document.querySelector('input[name="__RequestVerificationToken"]').value;
    const selected = new Set((root.dataset.selected || "").split(",").filter(Boolean).map(Number));
    const placeholderUrl = root.dataset.placeholder;

    const conditionNotes = {
        Excellent: "In excellent working order.",
        Good: "In good working order.",
        Fair: "Usable, with some signs of wear.",
        Damaged: "Damaged. It cannot be borrowed.",
        Lost: "Reported lost. It cannot be borrowed."
    };

    let page = 1;
    let currentCard = null;
    let availabilityUrl = "";
    let galleryPhotos = [];
    let galleryIndex = 0;
    let placeholderMissing = false;

    const show = value => (value && value.trim()) ? value : "—";
    const pad = number => String(number).padStart(2, "0");
    const toInput = date => date.getFullYear() + "-" + pad(date.getMonth() + 1) + "-" + pad(date.getDate()) +
        "T" + pad(date.getHours()) + ":" + pad(date.getMinutes());
    const read = (data, key) => data[key] !== undefined ? data[key] : data[key.charAt(0).toUpperCase() + key.slice(1)];

    // If the placeholder file does not exist, the icon placeholder from the design shows instead.
    const probe = new Image();
    probe.onerror = () => { placeholderMissing = true; };
    probe.src = placeholderUrl;

    cards.forEach(card => {
        const image = card.querySelector(".equipment-photo-img");
        const hide = () => { image.hidden = true; };
        image.addEventListener("error", hide);
        if (image.complete && image.naturalWidth === 0 && image.getAttribute("src")) hide();
    });

    function photosFor(card) {
        const urls = (card.dataset.images || "").split("|").filter(Boolean);
        return urls.length > 0 ? urls : Array.from({ length: PLACEHOLDER_PHOTO_COUNT }, () => placeholderUrl);
    }

    function isMissing(source) {
        return source === placeholderUrl && placeholderMissing;
    }

    function renderGallery() {
        const total = galleryPhotos.length;
        const source = galleryPhotos[galleryIndex];
        const missing = isMissing(source);

        galleryMainImg.hidden = missing;
        if (!missing) {
            galleryMainImg.onerror = () => { galleryMainImg.hidden = true; };
            galleryMainImg.src = source;
        }

        galleryCount.textContent = (galleryIndex + 1) + " / " + total;
        galleryCount.hidden = total <= 1;
        galleryPrev.hidden = total <= 1;
        galleryNext.hidden = total <= 1;
        galleryThumbs.hidden = total <= 1;
        galleryThumbs.innerHTML = "";

        galleryPhotos.forEach((photo, index) => {
            const thumb = document.createElement("button");
            thumb.type = "button";
            thumb.className = "gallery-thumb" + (index === galleryIndex ? " is-active" : "");
            thumb.setAttribute("aria-label", "Show photo " + (index + 1));

            const icon = document.createElement("span");
            icon.className = "photo-icon";
            thumb.appendChild(icon);

            if (!isMissing(photo)) {
                const image = document.createElement("img");
                image.alt = "";
                image.loading = "lazy";
                image.addEventListener("error", () => { image.hidden = true; });
                image.src = photo;
                thumb.appendChild(image);
            }

            thumb.addEventListener("click", () => { galleryIndex = index; renderGallery(); });
            galleryThumbs.appendChild(thumb);
        });
    }

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

        if (currentCard) syncDialogSelectButton();
    }

    function syncDialogSelectButton() {
        const isSelected = selected.has(Number(currentCard.dataset.id));
        const selectable = currentCard.dataset.selectable === "true";

        detailsSelect.disabled = !selectable;
        detailsSelect.textContent = !selectable ? "Not available" : (isSelected ? "Remove from selection" : "Select this item");
    }

    // Details and availability

    function fillList(list, items, emptyText, describe) {
        list.innerHTML = "";

        if (items.length === 0) {
            const empty = document.createElement("li");
            empty.className = "busy-empty";
            empty.textContent = emptyText;
            list.appendChild(empty);
            return;
        }

        items.forEach(item => {
            const row = document.createElement("li");
            const left = document.createElement("span");
            const text = describe(item);

            left.textContent = text.when;
            row.appendChild(left);

            if (text.tag) {
                const right = document.createElement("span");
                right.className = "busy-tag";
                right.textContent = text.tag;
                row.appendChild(right);
            }

            list.appendChild(row);
        });
    }

    async function loadAvailability(from, to, showVerdict, rangeText) {
        availabilityRange.textContent = rangeText;

        if (showVerdict) {
            availabilityResult.className = "availability-result";
            availabilityResult.textContent = "Checking...";
        }

        try {
            const url = new URL(availabilityUrl, window.location.origin);
            url.searchParams.set("from", from);
            url.searchParams.set("to", to);

            const response = await fetch(url);
            if (!response.ok) throw new Error("Request failed");

            const data = await response.json();
            const isAvailable = read(data, "isAvailable");
            const reason = read(data, "reason");

            if (showVerdict) {
                availabilityResult.className = "availability-result " + (isAvailable ? "availability-ok" : "availability-no");
                availabilityResult.textContent = isAvailable ? "Available for the selected dates." : reason;
            } else {
                availabilityResult.textContent = "";
            }

            fillList(availabilityPeriods, read(data, "periods") || [],
                "No reservations or maintenance in this period.",
                period => ({ when: read(period, "start") + " – " + read(period, "end"), tag: read(period, "label") }));

            fillList(availabilitySlots, read(data, "slots") || [],
                reason || "No available dates in this period.",
                slot => ({ when: read(slot, "start") + " – " + read(slot, "end"), tag: "" }));
        } catch (error) {
            availabilityResult.className = "availability-result availability-no";
            availabilityResult.textContent = "Unable to load availability. Please try again.";
        }
    }

    function openDetails(card) {
        const data = card.dataset;
        currentCard = card;

        document.getElementById("detailsTitle").textContent = data.name;
        document.getElementById("detailsDescription").textContent = data.description || "No description provided.";
        document.getElementById("detailsCode").textContent = data.code;
        document.getElementById("detailsCategory").textContent = show(data.categoryName);
        document.getElementById("detailsBrandModel").textContent = show([data.brand, data.model].filter(Boolean).join(" "));
        document.getElementById("detailsCondition").textContent = data.condition;
        document.getElementById("detailsSerial").textContent = show(data.serial);
        document.getElementById("detailsLocation").textContent = show(data.location);
        document.getElementById("detailsNext").textContent = data.next || "None scheduled";

        const dot = document.createElement("span");
        detailsStatusBadge.className = "status-badge status-" + data.status;
        detailsStatusBadge.replaceChildren(dot, document.createTextNode(data.statusLabel));

        const isInactive = data.status === "unavailable" && ["Excellent", "Good", "Fair"].includes(data.condition);
        document.getElementById("detailsConditionNote").textContent = isInactive
            ? "This equipment is inactive and cannot be borrowed right now."
            : (conditionNotes[data.condition] || "");

        galleryMainImg.alt = data.name;
        galleryPhotos = photosFor(card);
        galleryIndex = 0;
        renderGallery();

        const now = new Date();
        const tomorrow = new Date(now.getTime() + 24 * 60 * 60 * 1000);
        const inFourteenDays = new Date(now.getTime() + 14 * 24 * 60 * 60 * 1000);

        availabilityUrl = data.availabilityUrl;
        availabilityFrom.value = toInput(now);
        availabilityTo.value = toInput(tomorrow);

        syncDialogSelectButton();
        loadAvailability(toInput(now), toInput(inFourteenDays), false, "(next 14 days)");
        dialog.showModal();
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

    document.getElementById("checkAvailability").addEventListener("click", () => {
        if (!availabilityFrom.value || !availabilityTo.value) {
            availabilityResult.className = "availability-result availability-no";
            availabilityResult.textContent = "Choose a start and end date and time.";
            return;
        }

        loadAvailability(availabilityFrom.value, availabilityTo.value, true, "(selected period)");
    });

    galleryPrev.addEventListener("click", () => {
        galleryIndex = (galleryIndex - 1 + galleryPhotos.length) % galleryPhotos.length;
        renderGallery();
    });
    galleryNext.addEventListener("click", () => {
        galleryIndex = (galleryIndex + 1) % galleryPhotos.length;
        renderGallery();
    });

    detailsSelect.addEventListener("click", () => { if (currentCard) toggleSelection(currentCard); });

    cards.forEach(card => {
        card.querySelector(".view-equipment").addEventListener("click", () => openDetails(card));
        card.querySelector(".select-equipment").addEventListener("click", () => toggleSelection(card));
    });

    dialog.addEventListener("click", event => {
        if (event.target.closest(".dialog-close")) dialog.close();
    });
    dialog.addEventListener("close", () => { currentCard = null; });

    let savedView = "grid";
    try { savedView = localStorage.getItem("hiramBrowseView") || "grid"; } catch (error) { /* storage unavailable */ }

    setView(savedView);
    renderSelection();
    render();
})();
