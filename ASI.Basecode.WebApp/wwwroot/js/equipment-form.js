(function () {
    const active = document.getElementById("equipmentActive");
    const statusLabel = document.getElementById("statusLabel");

    active.addEventListener("change", () => {
        statusLabel.textContent = active.checked ? "Active" : "Inactive";
    });
})();

(() => {
    const input = document.getElementById("equipmentPhotos");
    const picker = document.getElementById("equipmentPhotoPicker");
    const status = document.getElementById("equipmentPhotoStatus");

    if (!input || !picker || !status) return;

    const maxPhotos = Number(input.dataset.maxPhotos);
    const photos = [];

    const savedCount = picker.parentElement
        .querySelectorAll("[data-saved-photo]").length;

    function render(message) {
        picker.parentElement
            .querySelectorAll(".equipment-photo-tile:not([data-saved-photo])")
            .forEach(tile => tile.remove());

        const transfer = new DataTransfer();

        photos.forEach(({ file, url }, index) => {
            transfer.items.add(file);

            const tile = document.createElement("figure");
            tile.className = "equipment-photo-tile";

            const image = document.createElement("img");
            image.src = url;
            image.alt = file.name;

            const remove = document.createElement("button");
            remove.type = "button";
            remove.textContent = "Remove";
            remove.setAttribute("aria-label", `Remove photo ${index + 1}: ${file.name}`);
            remove.addEventListener("click", () => {
                URL.revokeObjectURL(url);
                photos.splice(index, 1);
                render("Photo removed.");
                input.focus();
            });

            tile.append(image, remove);

            if (savedCount === 0 && index === 0) {
                const cover = document.createElement("figcaption");
                cover.textContent = "Cover";
                tile.append(cover);
            }

            picker.before(tile);
        });

        input.files = transfer.files;

        const full = savedCount + photos.length >= maxPhotos;
        picker.hidden = full;
        input.tabIndex = full ? -1 : 0;

        const count = `${savedCount + photos.length} of ${maxPhotos} photos.`;
        status.textContent = message ? `${message} ${count}` : count;

        if (full && document.activeElement === input) {
            picker.parentElement.querySelector(".equipment-photo-tile button")?.focus();
        }
    }

    input.addEventListener("change", () => {
        const batch = Array.from(input.files);
        const remaining = maxPhotos - savedCount - photos.length;

        if (batch.length > remaining) {
            const message = remaining === 0
                ? "Remove a photo before adding another."
                : `Choose ${remaining} or fewer additional photos.`;

            render(message);
            return;
        }

        batch.forEach(file => {
            photos.push({ file, url: URL.createObjectURL(file) });
        });

        render();
    });

    input.addEventListener("cancel", () => render());
    render();
})();