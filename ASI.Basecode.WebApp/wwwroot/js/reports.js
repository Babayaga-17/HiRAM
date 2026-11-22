;(function () {
    const root = document.getElementById("reportRoot");
    if (!root) return;

    const wrap = document.getElementById("recordPanelWrap");
    const panelTitle = document.getElementById("panelTitle");
    const panelBadge = document.getElementById("panelBadge");
    const panelBody = document.getElementById("panelBody");
    const panelPrint = document.getElementById("panelPrint");

    const exportDialog = document.getElementById("exportDialog");
    const exportFileName = document.getElementById("exportFileName");
    const exportSummary = document.getElementById("exportSummary");
    const exportFilters = document.getElementById("exportFilters");

    // The panel lives directly under <body> so "Print this record" can hide everything else.
    document.body.appendChild(wrap);

    const el = (tag, className, text) => {
        const node = document.createElement(tag);
        if (className) node.className = className;
        if (text !== undefined && text !== null) node.textContent = text;
        return node;
    };
    const dash = value => (value && String(value).trim()) ? value : "—";

    // View borrowing details (side panel)

    function infoCard(kicker, title, lines) {
        const card = el("div", "rep-info-card");
        card.appendChild(el("span", "rep-kicker", kicker));
        card.appendChild(el("strong", "", dash(title)));
        lines.filter(Boolean).forEach(line => card.appendChild(el("small", "", line)));
        return card;
    }

    function renderPanel(data) {
        panelTitle.textContent = data.reservationNumber;
        panelBadge.className = "status-badge status-" + data.statusClass;
        panelBadge.replaceChildren(el("span"), document.createTextNode(data.statusLabel));

        panelBody.replaceChildren();

        const cards = el("div", "rep-info-grid");
        const borrower = data.borrower || {};
        cards.appendChild(infoCard("BORROWER", borrower.name, [borrower.department, borrower.programYear, borrower.contact]));

        (data.items || []).forEach(item => {
            cards.appendChild(infoCard("EQUIPMENT", item.name, [
                [item.code, item.category].filter(Boolean).join(" · "),
                item.serial ? "Serial " + item.serial : "",
                item.location
            ]));
        });
        panelBody.appendChild(cards);

        panelBody.appendChild(el("span", "rep-kicker rep-kicker-block", "TIMELINE"));
        const timeline = el("ol", "rep-timeline");
        (data.timeline || []).forEach(step => {
            const row = el("li", "rep-step rep-step-" + step.state);
            row.appendChild(el("span", "rep-step-dot", step.state === "done" ? "✓" : (step.state === "alert" ? "!" : "·")));
            const text = el("div");
            text.appendChild(el("strong", "", step.title));
            text.appendChild(el("small", "", step.when));
            row.appendChild(text);
            timeline.appendChild(row);
        });
        panelBody.appendChild(timeline);

        const facts = el("dl", "rep-facts");
        [
            ["Purpose", data.purpose],
            ["Condition when borrowed", data.conditionBorrowed],
            ["Condition when returned", data.conditionReturned],
            ["Received by", data.receivedBy],
            ["Remarks", data.remarks]
        ].forEach(pair => {
            const row = el("div");
            row.appendChild(el("dt", "", pair[0]));
            row.appendChild(el("dd", "", dash(pair[1])));
            facts.appendChild(row);
        });
        panelBody.appendChild(facts);

        if (data.actionNeeded) {
            const note = el("p", "rep-action-note");
            note.appendChild(el("strong", "", "Action needed: "));
            note.appendChild(document.createTextNode("this item is past its due time and has not been returned. Contact the borrower or the custodian."));
            panelBody.appendChild(note);
        }
    }

    async function openRecord(id) {
        wrap.hidden = false;
        document.body.classList.add("rep-lock");
        panelTitle.textContent = "Loading...";
        panelBadge.className = "";
        panelBadge.replaceChildren();
        panelBody.replaceChildren();

        try {
            const response = await fetch(root.dataset.detailsUrl.replace("__ID__", encodeURIComponent(id)));
            if (!response.ok) throw new Error("Request failed");
            renderPanel(await response.json());
        } catch (error) {
            panelTitle.textContent = "Borrowing details";
            panelBody.replaceChildren(el("p", "rep-error", "Unable to load this record. Please try again."));
        }
    }

    function closePanel() {
        wrap.hidden = true;
        document.body.classList.remove("rep-lock");
    }

    document.querySelectorAll(".rep-view").forEach(button =>
        button.addEventListener("click", () => openRecord(button.dataset.id)));

    wrap.addEventListener("click", event => {
        if (event.target.closest("[data-close]")) closePanel();
    });
    document.addEventListener("keydown", event => {
        if (event.key === "Escape" && !wrap.hidden) closePanel();
    });

    panelPrint.addEventListener("click", () => {
        document.body.classList.add("printing-record");
        window.print();
    });
    window.addEventListener("afterprint", () => document.body.classList.remove("printing-record"));

    // Export report dialog

    const formats = {
        pdf: { extension: ".pdf", label: "Export PDF", urlKey: "exportUrl", note: "" },
        excel: {
            extension: ".xlsx",
            label: "Export Excel",
            urlKey: "exportExcelUrl",
            note: "The records are on the first sheet. The summary and applied filters are on a second sheet named Summary."
        },
        csv: {
            extension: ".csv",
            label: "Export CSV",
            urlKey: "exportCsvUrl",
            note: "CSV contains the records only, so it opens cleanly in other tools. The summary and filters are not included."
        }
    };

    const formatButtons = Array.from(exportDialog.querySelectorAll("[data-format]"));
    const pdfOptions = document.getElementById("exportPdfOptions");
    const exportSwitches = document.getElementById("exportSwitches");
    const formatNote = document.getElementById("exportFormatNote");
    const exportExtension = document.getElementById("exportExtension");
    const exportPreview = document.getElementById("exportPreview");
    const exportDownload = document.getElementById("exportDownload");
    const exportDownloadLabel = document.getElementById("exportDownloadLabel");
    let format = "pdf";

    function setFormat(next) {
        format = next;
        const settings = formats[format];

        formatButtons.forEach(button => {
            const active = button.dataset.format === format;
            button.classList.toggle("is-active", active);
            button.setAttribute("aria-checked", active ? "true" : "false");
        });

        pdfOptions.hidden = format !== "pdf";
        exportPreview.hidden = format !== "pdf";
        exportSwitches.hidden = format === "csv";
        formatNote.hidden = !settings.note;
        formatNote.textContent = settings.note;
        exportExtension.textContent = settings.extension;
        exportDownloadLabel.textContent = settings.label;
    }

    function reportParams() {
        // Uses the filters that are applied to the report on screen (the page address), not unsubmitted form edits.
        const params = new URLSearchParams(window.location.search);
        params.delete("page");
        return params;
    }

    function exportQuery() {
        const params = reportParams();
        const orientation = document.querySelector('input[name="exportOrientation"]:checked');
        params.set("Orientation", orientation ? orientation.value : "Landscape");
        params.set("IncludeSummary", exportSummary.checked);
        params.set("ShowFilters", exportFilters.checked);
        if (exportFileName.value.trim()) params.set("FileName", exportFileName.value.trim());
        return params.toString();
    }

    function openExport() {
        exportFileName.value = root.dataset.defaultFileName;
        setFormat(format);
        exportDialog.showModal();
    }

    const openButton = document.getElementById("openExport");
    if (openButton) openButton.addEventListener("click", openExport);

    formatButtons.forEach(button => button.addEventListener("click", () => setFormat(button.dataset.format)));

    exportDialog.addEventListener("click", event => {
        if (event.target.closest("[data-export-close]")) exportDialog.close();
    });

    exportPreview.addEventListener("click", () => {
        window.location.href = root.dataset.previewUrl + "?" + exportQuery();
    });

    exportDownload.addEventListener("click", () => {
        window.location.href = root.dataset[formats[format].urlKey] + "?" + exportQuery();
        exportDialog.close();
    });

    setFormat("pdf");
})();

