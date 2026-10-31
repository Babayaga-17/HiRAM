(function () {
    const root = document.getElementById("calendarRoot");
    if (!root) return;

    const eventsUrl = root.dataset.eventsUrl;
    const detailsUrl = root.dataset.detailsUrl || "";

    const search = document.getElementById("calendarSearch");
    const equipmentFilter = document.getElementById("equipmentFilter");
    const categoryFilter = document.getElementById("categoryFilter");
    const body = document.getElementById("calendarBody");
    const titleLabel = document.getElementById("calendarTitle");
    const statusLabel = document.getElementById("calendarStatus");
    const viewButtons = Array.from(document.querySelectorAll(".calendar-views button"));
    const legendButtons = Array.from(document.querySelectorAll(".calendar-legend button"));

    const availableDate = document.getElementById("availableDate");
    const availableCount = document.getElementById("availableCount");
    const availableNote = document.getElementById("availableNote");
    const availableList = document.getElementById("availableList");

    const dialog = document.getElementById("eventDialog");
    const eventSchedule = document.getElementById("eventSchedule");
    const eventDetailsLink = document.getElementById("eventDetailsLink");

    const typeLabels = { Reserved: "Reserved", Borrowed: "Borrowed", Maintenance: "Under maintenance" };
    const statusLabels = {
        Pending: "Pending approval",
        Approved: "Approved",
        Borrowed: "Borrowed",
        Overdue: "Overdue",
        Scheduled: "Maintenance scheduled",
        InProgress: "Maintenance in progress"
    };

    const equipment = Array.from(document.querySelectorAll("#calendarEquipment li")).map(item => ({
        id: Number(item.dataset.id),
        code: item.dataset.code,
        name: item.dataset.name,
        categoryId: item.dataset.category ? Number(item.dataset.category) : null,
        categoryName: item.dataset.categoryName || "",
        borrowable: item.dataset.borrowable === "true"
    }));
    const byId = new Map(equipment.map(item => [item.id, item]));

    /* ---------- Date helpers ---------- */

    const pad = number => String(number).padStart(2, "0");
    const startOfDay = date => new Date(date.getFullYear(), date.getMonth(), date.getDate());
    const addDays = (date, days) => new Date(date.getFullYear(), date.getMonth(), date.getDate() + days);
    const isSameDay = (a, b) => a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate();
    const toParam = date => date.getFullYear() + "-" + pad(date.getMonth() + 1) + "-" + pad(date.getDate()) +
        "T" + pad(date.getHours()) + ":" + pad(date.getMinutes()) + ":" + pad(date.getSeconds());
    const parseLocal = value => {
        const parts = value.split("T");
        const date = parts[0].split("-").map(Number);
        const time = (parts[1] || "00:00:00").split(":").map(Number);
        return new Date(date[0], date[1] - 1, date[2], time[0], time[1], time[2] || 0);
    };
    const read = (data, key) => data[key] !== undefined ? data[key] : data[key.charAt(0).toUpperCase() + key.slice(1)];

    const fmtTime = date => date.toLocaleTimeString("en-US", { hour: "numeric", minute: "2-digit" });
    const fmtDate = date => date.toLocaleDateString("en-US", { month: "short", day: "numeric", year: "numeric" });
    const fmtDateTime = date => fmtDate(date) + " " + fmtTime(date);

    const el = (tag, className, text) => {
        const node = document.createElement(tag);
        if (className) node.className = className;
        if (text !== undefined) node.textContent = text;
        return node;
    };

    /* ---------- State ---------- */

    let view = "month";
    let cursor = startOfDay(new Date());
    let selectedDay = startOfDay(new Date());
    let events = [];
    let requestId = 0;
    const hiddenTypes = new Set();

    function monthWeeks(date) {
        const first = new Date(date.getFullYear(), date.getMonth(), 1);
        const days = new Date(date.getFullYear(), date.getMonth() + 1, 0).getDate();
        return Math.ceil((first.getDay() + days) / 7);
    }

    // The range of dates the current view needs from the server.
    function viewRange() {
        if (view === "day") return [cursor, addDays(cursor, 1)];

        if (view === "week") {
            const start = addDays(cursor, -cursor.getDay());
            return [start, addDays(start, 7)];
        }

        const first = new Date(cursor.getFullYear(), cursor.getMonth(), 1);
        const start = addDays(first, -first.getDay());
        return [start, addDays(start, monthWeeks(cursor) * 7)];
    }

    function ensureSelectedDay() {
        const today = startOfDay(new Date());
        const range = viewRange();

        if (view === "day") {
            selectedDay = cursor;
        } else if (selectedDay < range[0] || selectedDay >= range[1]) {
            selectedDay = (today >= range[0] && today < range[1]) ? today : cursor;
        }
    }

    /* ---------- Loading ---------- */

    async function load() {
        const range = viewRange();
        const ticket = ++requestId;

        statusLabel.className = "calendar-status";
        statusLabel.textContent = "Loading...";

        try {
            const url = new URL(eventsUrl, window.location.origin);
            url.searchParams.set("start", toParam(range[0]));
            url.searchParams.set("end", toParam(range[1]));

            const response = await fetch(url);
            const data = await response.json().catch(() => null);
            if (!response.ok) throw new Error((data && data.message) || "Unable to load the calendar.");
            if (ticket !== requestId) return;

            events = data.map(item => ({
                equipmentId: Number(read(item, "equipmentId")),
                code: read(item, "code"),
                name: read(item, "name"),
                categoryId: read(item, "categoryId"),
                type: read(item, "type"),
                status: read(item, "status"),
                start: parseLocal(read(item, "start")),
                end: parseLocal(read(item, "end"))
            }));
            statusLabel.textContent = "";
        } catch (error) {
            if (ticket !== requestId) return;
            events = [];
            statusLabel.className = "calendar-status is-error";
            statusLabel.textContent = error.message || "Unable to load the calendar.";
        }

        ensureSelectedDay();
        render();
    }

    /* ---------- Filters ---------- */

    function matchesFilters(item) {
        const term = search.value.trim().toLowerCase();
        if (term && !(item.code + " " + item.name).toLowerCase().includes(term)) return false;
        if (equipmentFilter.value !== "all" && String(item.id) !== equipmentFilter.value) return false;
        if (categoryFilter.value !== "all" && String(item.categoryId) !== categoryFilter.value) return false;
        return true;
    }

    function filteredEvents() {
        return events.filter(event => {
            const item = byId.get(event.equipmentId);
            return item && matchesFilters(item) && !hiddenTypes.has(event.type);
        });
    }

    const eventsOn = (day, list) => list
        .filter(event => event.start < addDays(day, 1) && event.end > day)
        .sort((a, b) => a.start - b.start);

    /* ---------- Text for entries ---------- */

    function spanText(event, day) {
        const startsToday = event.start >= day;
        const endsToday = event.end <= addDays(day, 1);

        if (startsToday && endsToday) return fmtTime(event.start) + "–" + fmtTime(event.end);
        if (startsToday) return "from " + fmtTime(event.start);
        if (endsToday) return "until " + fmtTime(event.end);
        return "all day";
    }

    const segmentText = (event, day) => event.type === "Maintenance"
        ? event.code + " · Maintenance"
        : event.code + " · " + spanText(event, day);

    function chip(event, day, detailed) {
        const button = el("button", "calendar-chip chip-" + event.type.toLowerCase());
        button.type = "button";
        button.title = event.code + " · " + event.name;

        if (detailed) {
            button.appendChild(el("strong", "", segmentText(event, day)));
            button.appendChild(el("small", "", event.name));
        } else {
            button.textContent = segmentText(event, day);
        }

        button.addEventListener("click", clickEvent => {
            clickEvent.stopPropagation();
            openEvent(event);
        });
        return button;
    }

    /* ---------- Views ---------- */

    function renderMonth(list) {
        const today = startOfDay(new Date());
        const start = viewRange()[0];
        const weeks = monthWeeks(cursor);
        const wrap = el("div", "calendar-scroll");
        const grid = el("div", "calendar-month");

        ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"].forEach(name => grid.appendChild(el("div", "calendar-weekday", name)));

        for (let index = 0; index < weeks * 7; index++) {
            const day = addDays(start, index);
            const cell = el("div", "calendar-cell" +
                (day.getMonth() !== cursor.getMonth() ? " is-outside" : "") +
                (isSameDay(day, selectedDay) ? " is-selected" : ""));

            const number = el("button", "calendar-day-number" + (isSameDay(day, today) ? " is-today" : ""), String(day.getDate()));
            number.type = "button";
            number.setAttribute("aria-label", day.toLocaleDateString("en-US", { weekday: "long", month: "long", day: "numeric" }));
            cell.appendChild(number);

            const dayEvents = eventsOn(day, list);
            dayEvents.slice(0, 3).forEach(event => cell.appendChild(chip(event, day, false)));

            if (dayEvents.length > 3) {
                const more = el("button", "calendar-more", "+" + (dayEvents.length - 3) + " more");
                more.type = "button";
                more.addEventListener("click", clickEvent => {
                    clickEvent.stopPropagation();
                    cursor = day;
                    setView("day");
                });
                cell.appendChild(more);
            }

            cell.addEventListener("click", () => selectDay(day));
            grid.appendChild(cell);
        }

        wrap.appendChild(grid);
        return wrap;
    }

    function renderWeek(list) {
        const today = startOfDay(new Date());
        const start = viewRange()[0];
        const wrap = el("div", "calendar-scroll");
        const grid = el("div", "calendar-week");

        for (let index = 0; index < 7; index++) {
            const day = addDays(start, index);
            const column = el("div", "calendar-week-day" +
                (isSameDay(day, today) ? " is-today" : "") +
                (isSameDay(day, selectedDay) ? " is-selected" : ""));

            const head = el("button", "calendar-week-head");
            head.type = "button";
            head.appendChild(el("span", "", day.toLocaleDateString("en-US", { weekday: "short" })));
            head.appendChild(el("strong", "", String(day.getDate())));
            head.addEventListener("click", () => selectDay(day));
            column.appendChild(head);

            const dayEvents = eventsOn(day, list);
            if (dayEvents.length === 0) column.appendChild(el("p", "calendar-empty", "Nothing scheduled"));
            dayEvents.forEach(event => column.appendChild(chip(event, day, true)));

            grid.appendChild(column);
        }

        wrap.appendChild(grid);
        return wrap;
    }

    function renderDay(list) {
        const wrap = el("div", "calendar-day");
        const dayEvents = eventsOn(cursor, list);

        if (dayEvents.length === 0) {
            wrap.appendChild(el("p", "calendar-empty calendar-empty-large", "No reservations or maintenance on this day."));
            return wrap;
        }

        dayEvents.forEach(event => {
            const row = el("button", "calendar-day-row chip-" + event.type.toLowerCase());
            row.type = "button";

            const info = el("span");
            info.appendChild(el("strong", "", event.code + " · " + event.name));
            info.appendChild(el("small", "", typeLabels[event.type] + " · " + (statusLabels[event.status] || event.status)));

            row.appendChild(info);
            row.appendChild(el("span", "calendar-day-time", spanText(event, cursor)));
            row.addEventListener("click", () => openEvent(event));
            wrap.appendChild(row);
        });

        return wrap;
    }

    function titleText() {
        if (view === "day") {
            return cursor.toLocaleDateString("en-US", { weekday: "long", month: "long", day: "numeric", year: "numeric" });
        }

        if (view === "week") {
            const start = viewRange()[0];
            const last = addDays(start, 6);
            return fmtDate(start).replace(/, \d{4}$/, "") + " – " + fmtDate(last);
        }

        return cursor.toLocaleDateString("en-US", { month: "long", year: "numeric" });
    }

    /* ---------- Available equipment ---------- */

    function renderAvailable() {
        const today = startOfDay(new Date());
        const day = view === "day" ? cursor : selectedDay;

        availableDate.textContent = day.toLocaleDateString("en-US", { weekday: "long", month: "long", day: "numeric", year: "numeric" });
        availableList.replaceChildren();

        if (day < today) {
            availableCount.textContent = "—";
            availableNote.textContent = "Availability is shown for today and future dates.";
            return;
        }

        const busyIds = new Set(eventsOn(day, events).map(event => event.equipmentId));
        const free = equipment.filter(item => item.borrowable && matchesFilters(item) && !busyIds.has(item.id));

        availableCount.textContent = free.length + (free.length === 1 ? " item" : " items");
        availableNote.textContent = free.length === 0
            ? "Nothing is free all day. Try another date or clear a filter."
            : "Not reserved, borrowed, or under maintenance at any time on this day.";

        free.forEach(item => {
            const row = el("li", "available-item");
            const info = el("span");
            info.appendChild(el("strong", "", item.code + " · " + item.name));
            info.appendChild(el("small", "", item.categoryName));
            row.appendChild(info);

            if (detailsUrl) {
                const link = el("a", "", "View details");
                link.href = detailsUrl + "?id=" + item.id;
                row.appendChild(link);
            }

            availableList.appendChild(row);
        });
    }

    /* ---------- Reservation schedule dialog ---------- */

    function openEvent(event) {
        const item = byId.get(event.equipmentId);

        document.getElementById("eventTitle").textContent = event.code;
        document.getElementById("eventSubtitle").textContent = event.name;
        document.getElementById("eventType").textContent = typeLabels[event.type] || event.type;
        document.getElementById("eventStatus").textContent = statusLabels[event.status] || event.status;
        document.getElementById("eventCategory").textContent = (item && item.categoryName) || "—";
        document.getElementById("eventStart").textContent = fmtDateTime(event.start);
        document.getElementById("eventEnd").textContent = fmtDateTime(event.end);

        eventSchedule.replaceChildren();
        events
            .filter(other => other.equipmentId === event.equipmentId)
            .sort((a, b) => a.start - b.start)
            .forEach(other => {
                const row = el("li", other === event ? "is-current" : "");
                row.appendChild(el("span", "", fmtDateTime(other.start) + " – " + fmtDateTime(other.end)));
                row.appendChild(el("span", "calendar-schedule-tag", typeLabels[other.type] || other.type));
                eventSchedule.appendChild(row);
            });

        if (detailsUrl) {
            eventDetailsLink.href = detailsUrl + "?id=" + event.equipmentId;
            eventDetailsLink.hidden = false;
        }

        dialog.showModal();
    }

    /* ---------- Rendering and navigation ---------- */

    function render() {
        const list = filteredEvents();

        titleLabel.textContent = titleText();
        body.replaceChildren();

        if (view === "month") body.appendChild(renderMonth(list));
        else if (view === "week") body.appendChild(renderWeek(list));
        else body.appendChild(renderDay(list));

        renderAvailable();
    }

    function selectDay(day) {
        selectedDay = day;
        render();
    }

    function setView(nextView) {
        view = nextView;
        viewButtons.forEach(button => button.classList.toggle("is-active", button.dataset.view === view));
        ensureSelectedDay();
        load();
    }

    function step(direction) {
        if (view === "day") {
            cursor = addDays(cursor, direction);
        } else if (view === "week") {
            cursor = addDays(cursor, direction * 7);
        } else {
            const target = new Date(cursor.getFullYear(), cursor.getMonth() + direction, 1);
            const lastDay = new Date(target.getFullYear(), target.getMonth() + 1, 0).getDate();
            cursor = new Date(target.getFullYear(), target.getMonth(), Math.min(cursor.getDate(), lastDay));
        }

        ensureSelectedDay();
        load();
    }

    /* ---------- Events ---------- */

    document.getElementById("calendarPrev").addEventListener("click", () => step(-1));
    document.getElementById("calendarNext").addEventListener("click", () => step(1));
    document.getElementById("calendarToday").addEventListener("click", () => {
        cursor = startOfDay(new Date());
        selectedDay = cursor;
        load();
    });

    viewButtons.forEach(button => button.addEventListener("click", () => setView(button.dataset.view)));

    legendButtons.forEach(button => button.addEventListener("click", () => {
        const type = button.dataset.type;

        if (hiddenTypes.has(type)) hiddenTypes.delete(type);
        else hiddenTypes.add(type);

        button.classList.toggle("is-off", hiddenTypes.has(type));
        button.setAttribute("aria-pressed", hiddenTypes.has(type) ? "false" : "true");
        render();
    }));

    search.addEventListener("input", render);
    equipmentFilter.addEventListener("change", render);
    categoryFilter.addEventListener("change", render);

    dialog.addEventListener("click", clickEvent => {
        if (clickEvent.target.closest(".dialog-close")) dialog.close();
    });

    setView("month");
})();