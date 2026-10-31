;(function () {
    "use strict";
    const root = document.getElementById("equipmentDetailsRoot");
    if (!root) return;
    const get = id => document.getElementById(id);
    const pad = value => String(value).padStart(2, "0");
    const inputValue = date => `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
    const dayStart = date => new Date(date.getFullYear(), date.getMonth(), date.getDate());
    const addDays = (date, count) => new Date(date.getFullYear(), date.getMonth(), date.getDate() + count, date.getHours(), date.getMinutes());
    const sameDay = (left, right) => dayStart(left).getTime() === dayStart(right).getTime();
    const time = date => date.toLocaleTimeString("en-US", { hour: "numeric", minute: "2-digit" });
    const dateText = date => date.toLocaleDateString("en-US", { month: "short", day: "numeric", year: "numeric" });
    const read = (data, key) => data[key] !== undefined ? data[key] : data[key[0].toUpperCase() + key.slice(1)];
    const grid = get("availabilityCalendar");
    const from = get("availabilityFrom");
    const to = get("availabilityTo");
    const verdict = get("availabilityResult");
    const select = get("detailsSelect");
    let selected = root.dataset.selected === "true";
    let anchor = dayStart(new Date());
    let selectedDate = null;
    let view = "month";
    let calendarRequest;
    let checkRequest;

    async function json(url, options) {
        const response = await fetch(url, options);
        const data = await response.json().catch(() => ({}));
        if (!response.ok || response.redirected) throw new Error(data.message || "Unable to load data. Your session may have expired; refresh and try again.");
        return data;
    }
    function rangeUrl(endpoint, start, end) {
        const url = new URL(endpoint, window.location.origin);
        url.searchParams.set("from", inputValue(start));
        url.searchParams.set("to", inputValue(end));
        return url;
    }
    async function schedule(start, end, signal) {
        const data = await json(rangeUrl(root.dataset.scheduleUrl, start, end), { signal });
        return (read(data, "periods") || []).map(period => ({
            type: read(period, "type"), label: read(period, "label"),
            start: new Date(read(period, "startIso")), end: new Date(read(period, "endIso"))
        })).filter(period => Number.isFinite(period.start.getTime()) && period.end > period.start);
    }

    // Existing photo URLs and four fallback slots are preserved.
    const thumbs = Array.from(get("galleryThumbs").querySelectorAll("button"));
    const mainImage = get("galleryMainImg");
    let photoIndex = 0;
    root.querySelectorAll("img").forEach(image => {
        image.addEventListener("error", () => { image.hidden = true; });
        if (image.complete && image.naturalWidth === 0) image.hidden = true;
    });
    function showPhoto(index) {
        photoIndex = (index + thumbs.length) % thumbs.length;
        mainImage.hidden = false;
        mainImage.src = thumbs[photoIndex].dataset.photo;
        if (mainImage.complete && mainImage.naturalWidth === 0) mainImage.hidden = true;
        thumbs.forEach((thumb, number) => {
            thumb.classList.toggle("is-active", number === photoIndex);
            thumb.setAttribute("aria-pressed", String(number === photoIndex));
        });
        get("galleryCount").textContent = `${photoIndex + 1} / ${thumbs.length}`;
    }
    thumbs.forEach((thumb, index) => thumb.addEventListener("click", () => showPhoto(index)));
    get("galleryPrev").addEventListener("click", () => showPhoto(photoIndex - 1));
    get("galleryNext").addEventListener("click", () => showPhoto(photoIndex + 1));
    [get("galleryPrev"), get("galleryNext"), get("galleryCount"), get("galleryThumbs")].forEach(element => { element.hidden = thumbs.length <= 1; });

    function syncSelect() {
        select.disabled = root.dataset.selectable !== "true";
        select.textContent = select.disabled ? "Not available" : selected ? "Remove from selection" : "Select this item";
    }
    select.addEventListener("click", async () => {
        select.disabled = true;
        get("selectionMessage").textContent = "Updating selection…";
        try {
            const body = new URLSearchParams({
                id: root.dataset.id,
                __RequestVerificationToken: root.querySelector('input[name="__RequestVerificationToken"]').value
            });
            const data = await json(selected ? root.dataset.deselectUrl : root.dataset.selectUrl, { method: "POST", body });
            selected = (read(data, "ids") || []).map(Number).includes(Number(root.dataset.id));
            get("selectionMessage").textContent = selected ? "Item selected. Use Back to continue your reservation." : "Item removed from your selection.";
        } catch (error) {
            get("selectionMessage").textContent = error.message;
        } finally { syncSelect(); }
    });

    function fillList(element, items, emptyText, describe) {
        element.replaceChildren();
        if (!items.length) {
            const row = document.createElement("li");
            row.className = "busy-empty";
            row.textContent = emptyText;
            element.appendChild(row);
        }
        items.forEach(item => {
            const row = document.createElement("li");
            const content = describe(item);
            if (content.label) {
                const label = document.createElement("span");
                label.className = "busy-tag";
                label.textContent = content.label;
                row.appendChild(label);
            }
            row.appendChild(document.createTextNode(content.text));
            element.appendChild(row);
        });
    }
    async function checkAvailability() {
        checkRequest?.abort();
        checkRequest = new AbortController();
        const signal = checkRequest.signal;
        get("availabilityPeriods").replaceChildren();
        get("availabilitySlots").replaceChildren();
        const start = new Date(from.value), end = new Date(to.value);
        verdict.className = "availability-result";
        if (!Number.isFinite(start.getTime()) || !Number.isFinite(end.getTime()) || end <= start) {
            verdict.classList.add("availability-no");
            verdict.textContent = "Choose an end date and time after the start.";
            return;
        }
        verdict.textContent = "Checking…";
        try {
            const data = await json(rangeUrl(root.dataset.availabilityUrl, start, end), { signal });
            const available = read(data, "isAvailable");
            const reason = read(data, "reason");
            verdict.classList.add(available ? "availability-ok" : "availability-no");
            verdict.textContent = available ? "Available for the selected period." : reason;
            fillList(get("availabilityPeriods"), read(data, "periods") || [], "No scheduled conflicts in this period.",
                period => ({ label: read(period, "label"), text: read(period, "start") + " – " + read(period, "end") }));
            fillList(get("availabilitySlots"), read(data, "slots") || [], reason || "No free time in this period.",
                slot => ({ text: read(slot, "start") + " – " + read(slot, "end") }));
        } catch (error) {
            if (error.name === "AbortError") return;
            verdict.classList.add("availability-no");
            verdict.textContent = error.message;
        }
    }
    get("availabilityForm").addEventListener("submit", event => { event.preventDefault(); checkAvailability(); });

    function visibleRange() {
        if (view === "day") return { start: dayStart(anchor), end: addDays(dayStart(anchor), 1) };
        if (view === "week") {
            const start = addDays(dayStart(anchor), -anchor.getDay());
            return { start, end: addDays(start, 7) };
        }
        const first = new Date(anchor.getFullYear(), anchor.getMonth(), 1);
        const start = addDays(first, -first.getDay());
        const nextMonth = new Date(anchor.getFullYear(), anchor.getMonth() + 1, 1);
        const days = (Date.UTC(nextMonth.getFullYear(), nextMonth.getMonth(), nextMonth.getDate()) -
            Date.UTC(start.getFullYear(), start.getMonth(), start.getDate())) / 86400000;
        const weeks = Math.ceil(days / 7);
        return { start, end: addDays(start, weeks * 7) };
    }
    function selectDate(date) {
        selectedDate = dayStart(date);
        const previous = new Date(from.value), previousEnd = new Date(to.value);
        const start = dayStart(date);
        if (Number.isFinite(previous.getTime())) start.setHours(previous.getHours(), previous.getMinutes());
        const duration = Number.isFinite(previousEnd.getTime()) && previousEnd > previous ? previousEnd - previous : 86400000;
        from.value = inputValue(start);
        to.value = inputValue(new Date(start.getTime() + duration));
        grid.querySelectorAll("[data-date]").forEach(element => {
            const active = sameDay(new Date(element.dataset.date), selectedDate);
            element.classList.toggle("is-selected", active);
            element.querySelector("button").setAttribute("aria-pressed", String(active));
        });
        checkAvailability();
    }
    function eventElement(period, day, nextDay) {
        const element = document.createElement("div");
        const kind = period.type === "Maintenance" ? "maintenance" : period.label === "In use" ? "borrowed" : "reserved";
        element.className = "calendar-event is-" + kind;
        const label = period.type === "Maintenance" ? "Maintenance" : period.label === "In use" ? "Borrowed" : period.label;
        const start = period.start > day ? period.start : day;
        const end = period.end < nextDay ? period.end : nextDay;
        const hours = period.start <= day && period.end >= nextDay ? "All day" : `${time(start)} – ${end.getTime() === nextDay.getTime() ? "midnight" : time(end)}`;
        element.title = `${label}: ${dateText(period.start)} ${time(period.start)} – ${dateText(period.end)} ${time(period.end)}`;
        element.appendChild(document.createTextNode(label));
        const when = document.createElement("time");
        when.textContent = hours;
        element.appendChild(when);
        return element;
    }
    function renderCalendar(periods, range) {
        const fragment = document.createDocumentFragment();
        for (let day = range.start; day < range.end; day = addDays(day, 1)) {
            const date = new Date(day), nextDay = addDays(day, 1);
            const events = periods.filter(period => period.start < nextDay && period.end > day);
            const cell = document.createElement("section");
            cell.className = view === "month" ? "calendar-day" : "calendar-agenda";
            if (!events.length) cell.classList.add("is-free");
            if (view === "month" && day.getMonth() !== anchor.getMonth()) cell.classList.add("is-outside");
            if (selectedDate && sameDay(day, selectedDate)) cell.classList.add("is-selected");
            cell.dataset.date = inputValue(day);
            const button = document.createElement("button");
            button.type = "button";
            button.className = "calendar-date";
            button.textContent = view === "month" ? day.getDate() : day.toLocaleDateString("en-US", { weekday: "long", month: "short", day: "numeric" });
            button.setAttribute("aria-label", `Check availability on ${dateText(day)}`);
            button.setAttribute("aria-pressed", String(!!selectedDate && sameDay(day, selectedDate)));
            if (sameDay(day, new Date())) button.setAttribute("aria-current", "date");
            button.addEventListener("click", () => selectDate(date));
            cell.appendChild(button);
            events.forEach(period => cell.appendChild(eventElement(period, day, nextDay)));
            if (view !== "month" && !events.length) {
                const empty = document.createElement("p");
                empty.className = "calendar-empty";
                empty.textContent = "No scheduled conflicts.";
                cell.appendChild(empty);
            }
            fragment.appendChild(cell);
        }
        grid.replaceChildren(fragment);
    }
    async function loadCalendar() {
        calendarRequest?.abort();
        calendarRequest = new AbortController();
        const signal = calendarRequest.signal;
        const range = visibleRange();
        grid.replaceChildren();
        grid.setAttribute("aria-busy", "true");
        get("calendarMessage").textContent = "Loading calendar…";
        get("calendarWeekdays").hidden = view !== "month";
        get("calendarTitle").textContent = view === "month"
            ? anchor.toLocaleDateString("en-US", { month: "long", year: "numeric" })
            : view === "day" ? dateText(anchor) : `${dateText(range.start)} – ${dateText(addDays(range.end, -1))}`;
        try {
            const periods = await schedule(range.start, range.end, signal);
            if (signal.aborted) return;
            renderCalendar(periods, range);
            get("calendarMessage").textContent = "";
        } catch (error) {
            if (error.name !== "AbortError") get("calendarMessage").textContent = error.message;
        } finally {
            if (!signal.aborted) grid.setAttribute("aria-busy", "false");
        }
    }
    function navigate(direction) {
        anchor = view === "month" ? new Date(anchor.getFullYear(), anchor.getMonth() + direction, 1) : addDays(anchor, direction * (view === "week" ? 7 : 1));
        loadCalendar();
    }
    get("calendarPrev").addEventListener("click", () => navigate(-1));
    get("calendarNext").addEventListener("click", () => navigate(1));
    get("calendarToday").addEventListener("click", () => { anchor = dayStart(new Date()); loadCalendar(); });
    root.querySelectorAll("[data-calendar-view]").forEach(button => button.addEventListener("click", () => {
        view = button.dataset.calendarView;
        root.querySelectorAll("[data-calendar-view]").forEach(control => {
            const active = control === button;
            control.classList.toggle("is-active", active);
            control.setAttribute("aria-pressed", String(active));
        });
        loadCalendar();
    }));

    const now = new Date();
    from.value = inputValue(now);
    to.value = inputValue(addDays(now, 1));
    syncSelect();
    loadCalendar();
    checkAvailability();
    schedule(now, addDays(now, 14)).then(periods => {
        fillList(get("upcomingPeriods"), periods, "No reservations or maintenance in the next 14 days.",
            period => ({ label: period.type === "Maintenance" ? "Maintenance" : period.label,
                text: `${dateText(period.start)} ${time(period.start)} – ${dateText(period.end)} ${time(period.end)}` }));
    }).catch(error => fillList(get("upcomingPeriods"), [], error.message, () => ({})));
})();
;(function () {
    "use strict";
    const root = document.getElementById("equipmentDetailsRoot");
    if (!root) return;
    const get = id => document.getElementById(id);
    const pad = value => String(value).padStart(2, "0");
    const inputValue = date => `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
    const dayStart = date => new Date(date.getFullYear(), date.getMonth(), date.getDate());
    const addDays = (date, count) => new Date(date.getFullYear(), date.getMonth(), date.getDate() + count, date.getHours(), date.getMinutes());
    const sameDay = (left, right) => dayStart(left).getTime() === dayStart(right).getTime();
    const time = date => date.toLocaleTimeString("en-US", { hour: "numeric", minute: "2-digit" });
    const dateText = date => date.toLocaleDateString("en-US", { month: "short", day: "numeric", year: "numeric" });
    const read = (data, key) => data[key] !== undefined ? data[key] : data[key[0].toUpperCase() + key.slice(1)];
    const grid = get("availabilityCalendar");
    const from = get("availabilityFrom");
    const to = get("availabilityTo");
    const verdict = get("availabilityResult");
    const select = get("detailsSelect");
    let selected = root.dataset.selected === "true";
    let anchor = dayStart(new Date());
    let selectedDate = null;
    let view = "month";
    let calendarRequest;
    let checkRequest;

    async function json(url, options) {
        const response = await fetch(url, options);
        const data = await response.json().catch(() => ({}));
        if (!response.ok || response.redirected) throw new Error(data.message || "Unable to load data. Your session may have expired; refresh and try again.");
        return data;
    }
    function rangeUrl(endpoint, start, end) {
        const url = new URL(endpoint, window.location.origin);
        url.searchParams.set("from", inputValue(start));
        url.searchParams.set("to", inputValue(end));
        return url;
    }
    async function schedule(start, end, signal) {
        const data = await json(rangeUrl(root.dataset.scheduleUrl, start, end), { signal });
        return (read(data, "periods") || []).map(period => ({
            type: read(period, "type"), label: read(period, "label"),
            start: new Date(read(period, "startIso")), end: new Date(read(period, "endIso"))
        })).filter(period => Number.isFinite(period.start.getTime()) && period.end > period.start);
    }

    // Existing photo URLs and four fallback slots are preserved.
    const thumbs = Array.from(get("galleryThumbs").querySelectorAll("button"));
    const mainImage = get("galleryMainImg");
    let photoIndex = 0;
    root.querySelectorAll("img").forEach(image => {
        image.addEventListener("error", () => { image.hidden = true; });
        if (image.complete && image.naturalWidth === 0) image.hidden = true;
    });
    function showPhoto(index) {
        photoIndex = (index + thumbs.length) % thumbs.length;
        mainImage.hidden = false;
        mainImage.src = thumbs[photoIndex].dataset.photo;
        if (mainImage.complete && mainImage.naturalWidth === 0) mainImage.hidden = true;
        thumbs.forEach((thumb, number) => {
            thumb.classList.toggle("is-active", number === photoIndex);
            thumb.setAttribute("aria-pressed", String(number === photoIndex));
        });
        get("galleryCount").textContent = `${photoIndex + 1} / ${thumbs.length}`;
    }
    thumbs.forEach((thumb, index) => thumb.addEventListener("click", () => showPhoto(index)));
    get("galleryPrev").addEventListener("click", () => showPhoto(photoIndex - 1));
    get("galleryNext").addEventListener("click", () => showPhoto(photoIndex + 1));
    [get("galleryPrev"), get("galleryNext"), get("galleryCount"), get("galleryThumbs")].forEach(element => { element.hidden = thumbs.length <= 1; });

    function syncSelect() {
        select.disabled = root.dataset.selectable !== "true";
        select.textContent = select.disabled ? "Not available" : selected ? "Remove from selection" : "Select this item";
    }
    select.addEventListener("click", async () => {
        select.disabled = true;
        get("selectionMessage").textContent = "Updating selection…";
        try {
            const body = new URLSearchParams({
                id: root.dataset.id,
                __RequestVerificationToken: root.querySelector('input[name="__RequestVerificationToken"]').value
            });
            const data = await json(selected ? root.dataset.deselectUrl : root.dataset.selectUrl, { method: "POST", body });
            selected = (read(data, "ids") || []).map(Number).includes(Number(root.dataset.id));
            get("selectionMessage").textContent = selected ? "Item selected. Use Back to continue your reservation." : "Item removed from your selection.";
        } catch (error) {
            get("selectionMessage").textContent = error.message;
        } finally { syncSelect(); }
    });

    function fillList(element, items, emptyText, describe) {
        element.replaceChildren();
        if (!items.length) {
            const row = document.createElement("li");
            row.className = "busy-empty";
            row.textContent = emptyText;
            element.appendChild(row);
        }
        items.forEach(item => {
            const row = document.createElement("li");
            const content = describe(item);
            if (content.label) {
                const label = document.createElement("span");
                label.className = "busy-tag";
                label.textContent = content.label;
                row.appendChild(label);
            }
            row.appendChild(document.createTextNode(content.text));
            element.appendChild(row);
        });
    }
    async function checkAvailability() {
        checkRequest?.abort();
        checkRequest = new AbortController();
        const signal = checkRequest.signal;
        get("availabilityPeriods").replaceChildren();
        get("availabilitySlots").replaceChildren();
        const start = new Date(from.value), end = new Date(to.value);
        verdict.className = "availability-result";
        if (!Number.isFinite(start.getTime()) || !Number.isFinite(end.getTime()) || end <= start) {
            verdict.classList.add("availability-no");
            verdict.textContent = "Choose an end date and time after the start.";
            return;
        }
        verdict.textContent = "Checking…";
        try {
            const data = await json(rangeUrl(root.dataset.availabilityUrl, start, end), { signal });
            const available = read(data, "isAvailable");
            const reason = read(data, "reason");
            verdict.classList.add(available ? "availability-ok" : "availability-no");
            verdict.textContent = available ? "Available for the selected period." : reason;
            fillList(get("availabilityPeriods"), read(data, "periods") || [], "No scheduled conflicts in this period.",
                period => ({ label: read(period, "label"), text: read(period, "start") + " – " + read(period, "end") }));
            fillList(get("availabilitySlots"), read(data, "slots") || [], reason || "No free time in this period.",
                slot => ({ text: read(slot, "start") + " – " + read(slot, "end") }));
        } catch (error) {
            if (error.name === "AbortError") return;
            verdict.classList.add("availability-no");
            verdict.textContent = error.message;
        }
    }
    get("availabilityForm").addEventListener("submit", event => { event.preventDefault(); checkAvailability(); });

    function visibleRange() {
        if (view === "day") return { start: dayStart(anchor), end: addDays(dayStart(anchor), 1) };
        if (view === "week") {
            const start = addDays(dayStart(anchor), -anchor.getDay());
            return { start, end: addDays(start, 7) };
        }
        const first = new Date(anchor.getFullYear(), anchor.getMonth(), 1);
        const start = addDays(first, -first.getDay());
        const nextMonth = new Date(anchor.getFullYear(), anchor.getMonth() + 1, 1);
        const days = (Date.UTC(nextMonth.getFullYear(), nextMonth.getMonth(), nextMonth.getDate()) -
            Date.UTC(start.getFullYear(), start.getMonth(), start.getDate())) / 86400000;
        const weeks = Math.ceil(days / 7);
        return { start, end: addDays(start, weeks * 7) };
    }
    function selectDate(date) {
        selectedDate = dayStart(date);
        const previous = new Date(from.value), previousEnd = new Date(to.value);
        const start = dayStart(date);
        if (Number.isFinite(previous.getTime())) start.setHours(previous.getHours(), previous.getMinutes());
        const duration = Number.isFinite(previousEnd.getTime()) && previousEnd > previous ? previousEnd - previous : 86400000;
        from.value = inputValue(start);
        to.value = inputValue(new Date(start.getTime() + duration));
        grid.querySelectorAll("[data-date]").forEach(element => {
            const active = sameDay(new Date(element.dataset.date), selectedDate);
            element.classList.toggle("is-selected", active);
            element.querySelector("button").setAttribute("aria-pressed", String(active));
        });
        checkAvailability();
    }
    function eventElement(period, day, nextDay) {
        const element = document.createElement("div");
        const kind = period.type === "Maintenance" ? "maintenance" : period.label === "In use" ? "borrowed" : "reserved";
        element.className = "calendar-event is-" + kind;
        const label = period.type === "Maintenance" ? "Maintenance" : period.label === "In use" ? "Borrowed" : period.label;
        const start = period.start > day ? period.start : day;
        const end = period.end < nextDay ? period.end : nextDay;
        const hours = period.start <= day && period.end >= nextDay ? "All day" : `${time(start)} – ${end.getTime() === nextDay.getTime() ? "midnight" : time(end)}`;
        element.title = `${label}: ${dateText(period.start)} ${time(period.start)} – ${dateText(period.end)} ${time(period.end)}`;
        element.appendChild(document.createTextNode(label));
        const when = document.createElement("time");
        when.textContent = hours;
        element.appendChild(when);
        return element;
    }
    function renderCalendar(periods, range) {
        const fragment = document.createDocumentFragment();
        for (let day = range.start; day < range.end; day = addDays(day, 1)) {
            const date = new Date(day), nextDay = addDays(day, 1);
            const events = periods.filter(period => period.start < nextDay && period.end > day);
            const cell = document.createElement("section");
            cell.className = view === "month" ? "calendar-day" : "calendar-agenda";
            if (!events.length) cell.classList.add("is-free");
            if (view === "month" && day.getMonth() !== anchor.getMonth()) cell.classList.add("is-outside");
            if (selectedDate && sameDay(day, selectedDate)) cell.classList.add("is-selected");
            cell.dataset.date = inputValue(day);
            const button = document.createElement("button");
            button.type = "button";
            button.className = "calendar-date";
            button.textContent = view === "month" ? day.getDate() : day.toLocaleDateString("en-US", { weekday: "long", month: "short", day: "numeric" });
            button.setAttribute("aria-label", `Check availability on ${dateText(day)}`);
            button.setAttribute("aria-pressed", String(!!selectedDate && sameDay(day, selectedDate)));
            if (sameDay(day, new Date())) button.setAttribute("aria-current", "date");
            button.addEventListener("click", () => selectDate(date));
            cell.appendChild(button);
            events.forEach(period => cell.appendChild(eventElement(period, day, nextDay)));
            if (view !== "month" && !events.length) {
                const empty = document.createElement("p");
                empty.className = "calendar-empty";
                empty.textContent = "No scheduled conflicts.";
                cell.appendChild(empty);
            }
            fragment.appendChild(cell);
        }
        grid.replaceChildren(fragment);
    }
    async function loadCalendar() {
        calendarRequest?.abort();
        calendarRequest = new AbortController();
        const signal = calendarRequest.signal;
        const range = visibleRange();
        grid.replaceChildren();
        grid.setAttribute("aria-busy", "true");
        get("calendarMessage").textContent = "Loading calendar…";
        get("calendarWeekdays").hidden = view !== "month";
        get("calendarTitle").textContent = view === "month"
            ? anchor.toLocaleDateString("en-US", { month: "long", year: "numeric" })
            : view === "day" ? dateText(anchor) : `${dateText(range.start)} – ${dateText(addDays(range.end, -1))}`;
        try {
            const periods = await schedule(range.start, range.end, signal);
            if (signal.aborted) return;
            renderCalendar(periods, range);
            get("calendarMessage").textContent = "";
        } catch (error) {
            if (error.name !== "AbortError") get("calendarMessage").textContent = error.message;
        } finally {
            if (!signal.aborted) grid.setAttribute("aria-busy", "false");
        }
    }
    function navigate(direction) {
        anchor = view === "month" ? new Date(anchor.getFullYear(), anchor.getMonth() + direction, 1) : addDays(anchor, direction * (view === "week" ? 7 : 1));
        loadCalendar();
    }
    get("calendarPrev").addEventListener("click", () => navigate(-1));
    get("calendarNext").addEventListener("click", () => navigate(1));
    get("calendarToday").addEventListener("click", () => { anchor = dayStart(new Date()); loadCalendar(); });
    root.querySelectorAll("[data-calendar-view]").forEach(button => button.addEventListener("click", () => {
        view = button.dataset.calendarView;
        root.querySelectorAll("[data-calendar-view]").forEach(control => {
            const active = control === button;
            control.classList.toggle("is-active", active);
            control.setAttribute("aria-pressed", String(active));
        });
        loadCalendar();
    }));

    const now = new Date();
    from.value = inputValue(now);
    to.value = inputValue(addDays(now, 1));
    syncSelect();
    loadCalendar();
    checkAvailability();
    schedule(now, addDays(now, 14)).then(periods => {
        fillList(get("upcomingPeriods"), periods, "No reservations or maintenance in the next 14 days.",
            period => ({ label: period.type === "Maintenance" ? "Maintenance" : period.label,
                text: `${dateText(period.start)} ${time(period.start)} – ${dateText(period.end)} ${time(period.end)}` }));
    }).catch(error => fillList(get("upcomingPeriods"), [], error.message, () => ({})));
})();
