document.querySelectorAll('.my-profile-portrait img').forEach(function (image) {
    function showInitial() { image.hidden = true; }
    image.addEventListener('error', showInitial);
    if (image.complete && image.naturalWidth === 0) showInitial();
});
