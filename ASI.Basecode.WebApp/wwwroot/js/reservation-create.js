(() => {
    "use strict";
    const root = document.querySelector("[data-reservation-create]");
    if (!root) return;
    const form = root.querySelector("form");

    const pickup = form.querySelector("[name=Pickup]");
    const returned = form.querySelector("[name=ExpectedReturn]");

    const rows = [...root.querySelectorAll("[data-equipment-row]")];

    const status = root.querySelector("[data-preview-status]");
    const duration = root.querySelector("[data-duration]");
    const controls = root.querySelector("[data-view-controls]");
    const toggle = root.querySelector("[data-view-toggle]");
    const dayControl = root.querySelector("[data-day-control]");
    const dayInput = root.querySelector("[data-timeline-day]");
    const refresh = root.querySelector("[data-check-availability]");
    const submit = root.querySelector("[data-submit-request]");

    const hour = 60 * 60 * 1000,
          day = 24 * hour,
          offset = 8 * hour;

    const dateFormat = new Intl.DateTimeFormat("en-PH", { timeZone: "Asia/Manila", month: "short", day: "numeric", year: "numeric" });
    const shortDate = new Intl.DateTimeFormat("en-PH", { timeZone: "Asia/Manila", month: "short", day: "numeric" });
    const timeFormat = new Intl.DateTimeFormat("en-PH", { timeZone: "Asia/Manila", hour: "numeric", minute: "2-digit" });

    // Treat datetime-local input and the existing endpoints' naive timestamps as Philippine time.
    const parse = value => value ? Date.parse(value + "+08:00") : NaN;
    const floorDay = value => Math.floor((value + offset) / day) * day - offset;
    const localIso = value => new Date(value + offset).toISOString().slice(0, 19);
    const dateValue = value => localIso(value).slice(0, 10);
    const moment = value => `${dateFormat.format(value)} · ${timeFormat.format(value)}`;
    const text = (tag, value, className) => {
        const node = document.createElement(tag);
        node.textContent = value;
        if (className) node.className = className;
        return node;
    };
    let timer, request, generation = 0, cache = [], selection, bounds, detailed = false;
    refresh.hidden = false;

    function readSchedule() { // Validate date inputs
        const start = parse(pickup.value);
        const end = parse(returned.value);

        returned.setCustomValidity("");
        duration.classList.remove("is-error");
        if (!Number.isFinite(start) || !Number.isFinite(end)) {
            duration.textContent = "Choose pickup and return dates to preview your schedule.";
            return null;
        }
        if (end <= start) {
            duration.textContent = "Return must be after pickup.";
            duration.classList.add("is-error");
            returned.setCustomValidity("Return must be after pickup.");
            return null;
        }
        const minutes = Math.round((end - start) / 60000);
        const days = Math.floor(minutes / 1440), hours = Math.floor((minutes % 1440) / 60), mins = minutes % 60;
        const parts = [];
            
        if (days) parts.push(`${days} ${days === 1 ? "day" : "days"}`);
        if (hours) parts.push(`${hours} ${hours === 1 ? "hour" : "hours"}`);
        if (mins) parts.push(`${mins} ${mins === 1 ? "minute" : "minutes"}`);
        duration.textContent = `Borrowing duration: ${parts.join(", ") || "less than a minute"}.`;
        return { start, end };
    }

    function resetPreview() {
        cache = []; bounds = null;
        root.querySelector("[data-availability]").removeAttribute("aria-busy");
        status.className = "rc-preview-status";
        controls.hidden = true;
        root.querySelector("[data-timeline-legend]").hidden = true;
        root.querySelector("[data-range-label]").textContent = "Choose your schedule, then check existing bookings.";
        status.textContent = "";
        rows.forEach(row => {
            row.querySelector("[data-axis]").replaceChildren();
            row.querySelector("[data-track]").hidden = true;
            row.querySelector("[data-booking-list]").replaceChildren();
            const message = row.querySelector("[data-row-state]");
            message.className = "rc-row-state";
            message.textContent = "Choose valid pickup and return dates to preview bookings.";
        });
    }

    function draw() {
        if (!bounds || !selection) return;
        const start = detailed ? parse(`${dayInput.value}T00:00`) : bounds.start;
        const end = detailed ? start + day : bounds.end;
        const span = end - start;
        const days = Math.round(span / day);
        root.querySelector("[data-range-label]").textContent = `${dateFormat.format(start)}${days > 1 ? `–${dateFormat.format(end - 1)}` : ""} · ${days > 1 ? "Multi-day overview" : "Hourly detail"}`;
        cache.forEach(({ row, periods, error, available }) => {
            const axis = row.querySelector("[data-axis]");
            const track = row.querySelector("[data-track]");
            const list = row.querySelector("[data-booking-list]");
            const message = row.querySelector("[data-row-state]");
            axis.replaceChildren(); track.replaceChildren(); list.replaceChildren();
            track.hidden = Boolean(error);
            message.className = `rc-row-state${error ? " is-error" : ""}`;
            if (error) { message.textContent = "Preview unavailable for this item. Try checking again."; return; }
            const columns = days > 1 ? days : 4;
            axis.style.display = "grid";
            axis.style.gridTemplateColumns = `repeat(${columns}, minmax(0, 1fr))`;
            for (let i = 0; i < columns; i++) axis.append(text("span", days > 1 ? shortDate.format(start + i * day) : timeFormat.format(start + i * 6 * hour)));
            function block(from, to, className) {
                const left = Math.max(start, from), right = Math.min(end, to);
                if (right <= left) return;
                const node = text("div", "", className);

                node.style.left = `${100 * (left - start) / span}%`;
                node.style.width = `${100 * (right - left) / span}%`;

                track.append(node);
            }
            block(selection.start, selection.end, "rc-selection");
            let visible = 0;
            periods.forEach(period => {
                if (period.start >= end || period.end <= start) return;
                visible++;
                const overlaps = period.start < selection.end && period.end > selection.start;
                block(period.start, period.end, `rc-period${overlaps ? " is-overlap" : ""}`);
                const label = String(period.type).toLowerCase().includes("maintenance") ? "Maintenance" : "Existing booking";
                list.append(text("li", `${label}: ${moment(period.start)}–${moment(period.end)}${overlaps ? " · Overlaps your schedule" : ""}`, overlaps ? "is-overlap" : ""));
            });
            message.textContent = available.isAvailable
                ? (visible ? "No conflict with your selected schedule." : "No bookings in this view. Your selected schedule is available.")
                : available.reason || "This item is unavailable for your selected schedule.";
            if (!available.isAvailable) message.classList.add("is-error");
            if (list.querySelector(".is-overlap")) message.textContent = "";
        });
    }

    async function getJson(url, signal) {
        const response = await fetch(url, { signal, headers: { Accept: "application/json" } });
        if (!response.ok || !response.headers.get("content-type")?.includes("application/json")) throw new Error("Availability unavailable");
        return response.json();
    }

    async function update() {
        const version = ++generation;
        request?.abort();
        selection = readSchedule();
        if (!selection) { resetPreview(); return; }
        const rangeStart = floorDay(selection.start), rangeEnd = floorDay(selection.end - 1) + day;
        if (rangeEnd - rangeStart > 62 * day) {
            resetPreview();
            status.textContent = "The preview supports ranges of up to 62 days. Availability will be checked when you submit.";
            return;
        }
        if (!rows.length) { resetPreview(); status.textContent = "Select equipment before checking availability."; return; }
        bounds = { start: rangeStart, end: rangeEnd };
        dayInput.min = dateValue(rangeStart); dayInput.max = dateValue(rangeEnd - 1);
        if (!dayInput.value || dayInput.value < dayInput.min || dayInput.value > dayInput.max) dayInput.value = dateValue(selection.start);
        controls.hidden = rangeEnd - rangeStart <= day;
        if (controls.hidden) detailed = false;
        toggle.textContent = detailed ? "Full period" : "Day detail";
        dayControl.hidden = !detailed;
        root.querySelector("[data-timeline-legend]").hidden = false;
        status.className = "rc-preview-status";
        status.textContent = "Checking selected equipment…";
        root.querySelector("[data-availability]").setAttribute("aria-busy", "true");
        // Remove previous results immediately so stale availability never looks current.
        rows.forEach(row => {
            row.querySelector("[data-track]").hidden = true;
            row.querySelector("[data-booking-list]").replaceChildren();
            row.querySelector("[data-row-state]").textContent = "Checking bookings…";
        });
        request = new AbortController();
        const signal = request.signal;
        cache = [];
        const results = await Promise.all(rows.map(async row => {
            try {
                const scheduleUrl = new URL(row.dataset.scheduleUrl, location.origin);
                scheduleUrl.searchParams.set("from", localIso(rangeStart)); scheduleUrl.searchParams.set("to", localIso(rangeEnd));
                const checkUrl = new URL(row.dataset.availabilityUrl, location.origin);
                checkUrl.searchParams.set("from", localIso(selection.start)); checkUrl.searchParams.set("to", localIso(selection.end));

                const [schedule, available] = await Promise.all([
                    getJson(scheduleUrl, signal),
                    getJson(checkUrl, signal)
                ]);

                if (!Array.isArray(schedule.periods) || typeof available.isAvailable !== "boolean") throw new Error("Invalid preview");
                const periods = schedule.periods.map(p => ({ start: parse(p.startIso), end: parse(p.endIso), type: p.type }));
                if (periods.some(p => !Number.isFinite(p.start) || !Number.isFinite(p.end) || p.end <= p.start)) throw new Error("Invalid booking dates");
                return { row, periods, available };
            } catch (error) { return { row, error: true }; }
        }));
        if (version !== generation) return;
        cache = results;
        root.querySelector("[data-availability]").removeAttribute("aria-busy");
        const failed = results.some(r => r.error), conflicts = results.filter(r => !r.error && !r.available.isAvailable);
        status.className = `rc-preview-status ${failed || conflicts.length ? "is-error" : "is-success"}`;
        status.textContent = failed ? "Some availability could not be checked. Try again; submission will still recheck all items."
            : conflicts.length ? `${conflicts.length} selected ${conflicts.length === 1 ? "item is" : "items are"} unavailable. Adjust your schedule or change equipment.`
                : "All selected items are available for this schedule. This preview does not reserve them.";
        draw();
    }

    [pickup, returned].forEach(input => input.addEventListener("input", () => {
        clearTimeout(timer);
        // Abort immediately on a new edit, rather than allowing an old response during debounce.
        generation++; request?.abort();
        readSchedule();
        rows.forEach(row => { row.querySelector("[data-track]").hidden = true; row.querySelector("[data-booking-list]").replaceChildren(); row.querySelector("[data-row-state]").textContent = "Schedule changed. Updating…"; });
        cache = [];
        status.textContent = "Schedule changed. Updating availability…";
        timer = setTimeout(update, 350);
    }));
    refresh.addEventListener("click", () => { clearTimeout(timer); update(); });
    toggle.addEventListener("click", () => { detailed = !detailed; dayControl.hidden = !detailed; toggle.textContent = detailed ? "Full period" : "Day detail"; draw(); });
    dayInput.addEventListener("change", () => { if (dayInput.value && dayInput.checkValidity()) draw(); });
    let submitting = false;
    form.addEventListener("submit", event => {
        if (event.submitter?.hasAttribute("formnovalidate")) return;
        if (submitting) { event.preventDefault(); return; }
        if (!form.checkValidity()) return;
        submitting = true; submit.disabled = true; submit.textContent = "Submitting…"; form.setAttribute("aria-busy", "true");
    });
    window.addEventListener("pageshow", () => {
        submitting = false; submit.disabled = false; submit.textContent = "Submit request"; form.removeAttribute("aria-busy");
    });
    update();
})();
