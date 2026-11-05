(function () {
    "use strict";
    const form = document.getElementById("registrationForm");
    if (!form) return;
    const get = id => document.getElementById(id);
    const type = get("BorrowerType");
    const fields = get("registrationFields");
    const review = get("registrationReview");
    const submit = get("registrationSubmit");
    const password = get("Password");
    const confirmation = get("ConfirmPassword");
    const required = ["FirstName", "LastName", "UserCode", "DepartmentId", "Email", "Password", "ConfirmPassword"];
    required.forEach(id => { get(id).required = true; });
    function updateFields() {
        const student = type.value === "Student";
        get("studentFields").hidden = !student;
        get("schoolIdLabel").textContent = student ? "Student ID" : "Employee ID";
        for (const id of ["Program", "YearLevel"]) {
            get(id).disabled = !student;
            get(id).required = student;
        }
    }
    function validatePasswords() {
        confirmation.setCustomValidity(confirmation.value && confirmation.value !== password.value ? "Passwords must match." : "");
    }
    function showFieldError(input) {
        const message = form.querySelector(`[data-valmsg-for="${input.name}"]`);
        if (message) {
            message.textContent = input.validationMessage;
            message.classList.toggle("field-validation-error", !input.validity.valid);
            message.classList.toggle("field-validation-valid", input.validity.valid);
        }
        input.setAttribute("aria-invalid", String(!input.validity.valid));
    }
    form.addEventListener("invalid", event => showFieldError(event.target), true);
    form.addEventListener("input", event => {
        validatePasswords();
        if (event.target instanceof HTMLInputElement || event.target instanceof HTMLSelectElement) showFieldError(event.target);
    });
    type.addEventListener("change", updateFields);
    password.addEventListener("input", validatePasswords);
    confirmation.addEventListener("input", validatePasswords);
    function edit() {
        review.hidden = true;
        fields.hidden = false;
        submit.textContent = "Review registration";
        type.focus();
    }
    get("editRegistration").addEventListener("click", edit);
    form.addEventListener("submit", event => {
        validatePasswords();
        const valid = form.checkValidity() && (!window.jQuery || window.jQuery(form).valid());
        if (!valid) {
            event.preventDefault();
            review.hidden = true;
            fields.hidden = false;
            submit.textContent = "Review registration";
            const first = form.querySelector(':invalid, .input-validation-error');
            if (first?.closest("details")) first.closest("details").open = true;
            first?.focus();
            return;
        }
        if (!review.hidden) return; // Confirm uses the existing protected POST.
        event.preventDefault();
        const validationSummary = form.querySelector("[data-valmsg-summary]");
        if (validationSummary) {
            validationSummary.classList.remove("validation-summary-errors");
            validationSummary.classList.add("validation-summary-valid");
            validationSummary.querySelector("ul")?.replaceChildren();
        }
        const summary = get("registrationSummary");
        summary.replaceChildren();
        const value = id => get(id).value.trim() || "—";
        const rows = [
            ["Borrower type", type.value],
            ["Name", [value("FirstName"), get("MiddleName").value.trim(), value("LastName"), get("Suffix").value.trim()].filter(Boolean).join(" ")],
            [type.value === "Student" ? "Student ID" : "Employee ID", value("UserCode")],
            ["Department", get("DepartmentId").selectedOptions[0]?.textContent || "—"],
            ["Email", value("Email")],
            ["Contact number", value("ContactNumber")]
        ];
        if (type.value === "Student") rows.push(["Program", value("Program")], ["Year level", get("YearLevel").selectedOptions[0].textContent]);
        for (const [label, text] of rows) {
            const dt = document.createElement("dt"); dt.textContent = label;
            const dd = document.createElement("dd"); dd.textContent = text;
            summary.append(dt, dd);
        }
        fields.hidden = true;
        review.hidden = false;
        submit.textContent = "Confirm registration";
        get("reviewHeading").focus();
        review.scrollIntoView({ block: "start" });
    });
    updateFields();
    const firstError = form.querySelector(".input-validation-error");
    if (firstError?.closest("details")) firstError.closest("details").open = true;
    firstError?.focus();
})();


