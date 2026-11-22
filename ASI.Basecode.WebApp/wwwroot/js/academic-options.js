(() => {
    "use strict";
    document.querySelectorAll("[data-academic-fields]").forEach(form => {
        if (form.dataset.academicReady) return;
        form.dataset.academicReady = "true";
        const department = form.querySelector("[data-academic-department]");
        const program = form.querySelector("[data-academic-program]");
        if (!department || !program) return;
        const groups = Array.from(program.querySelectorAll("optgroup"), group => group.cloneNode(true));
        const type = form.querySelector('[name="BorrowerType"]');
        const role = form.querySelector('[name="RoleName"]');
        const borrowerDetails = form.querySelector("#borrowerDetails"); 
        const year = form.querySelector('[name="YearLevel"]');
        let borrowerType = type?.value || "";

        function refresh(selected = "") {
            const faculty = borrowerType === "Faculty";
            const borrower = !role || role.value === "Borrower";    
            if (borrowerDetails) {
                borrowerDetails.hidden = !borrower;
                borrowerDetails.disabled = !borrower;
            }
            program.replaceChildren(new Option("Select program", ""));
            groups.filter(group => group.dataset.department === department.value).forEach(group => {
                program.append(group.cloneNode(true));
            });
            program.value = Array.from(program.options).some(option => option.value === selected) ? selected : "";
            program.disabled = !borrower || faculty;
            program.required = borrower && borrowerType === "Student";
            if (year) {
                year.disabled = !borrower || faculty;
                year.required = borrower && borrowerType === "Student";
            }
        }

        department.addEventListener("change", () => refresh());
        type?.addEventListener("change", () => {
            borrowerType = type.value;
            refresh(program.value);
        });
        role?.addEventListener("change", () => refresh(program.value));
        // Restore only a predefined program when opening the borrower modal.
        program.addEventListener("academic:load", event => {
            borrowerType = event.detail.borrowerType;
            refresh(event.detail.program);
        });
        refresh(program.value);
    });
})();
