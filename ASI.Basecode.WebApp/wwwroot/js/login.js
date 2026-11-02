const loginPage = document.getElementById("loginPage");
const themeToggle = document.getElementById("themeToggle");
const themeText = themeToggle.querySelector(".theme-toggle-text");

function applyTheme(theme) {
    const isDark = theme === "dark";

    loginPage.dataset.theme = theme;
    document.body.classList.toggle("login-dark-mode", isDark);
    themeToggle.setAttribute("aria-pressed", isDark.toString());
    themeToggle.setAttribute("aria-label", isDark ? "Switch to light mode" : "Switch to dark mode");
    themeText.textContent = isDark ? "Light mode" : "Dark mode";
}

const savedTheme = localStorage.getItem("hiram-theme");
const preferredTheme = window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
applyTheme(savedTheme || preferredTheme);

themeToggle.addEventListener("click", function () {
    const nextTheme = loginPage.dataset.theme === "dark" ? "light" : "dark";
    applyTheme(nextTheme);
    localStorage.setItem("hiram-theme", nextTheme);
});
