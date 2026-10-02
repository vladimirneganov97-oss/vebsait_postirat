document.addEventListener("DOMContentLoaded", () => {
    // Names and surnames: allow letters only. Server-side validation remains authoritative.
    document.querySelectorAll("[data-letters-only]").forEach((input) => {
        input.addEventListener("input", () => {
            input.value = Array.from(input.value)
                .filter((char) => /\p{L}/u.test(char))
                .slice(0, 30)
                .join("");
        });
    });

    // Room number: integer from 1 to 1000.
    document.querySelectorAll('input[name="roomNumber"]').forEach((input) => {
        input.addEventListener("input", () => {
            input.value = input.value.replace(/\D/g, "").slice(0, 4);
            if (input.value !== "") {
                const value = Number(input.value);
                if (value > 1000) input.value = "1000";
                if (value < 1) input.value = "";
            }
        });
    });

    document.querySelectorAll("[data-password-toggle]").forEach((button) => {
        button.addEventListener("click", () => {
            const input = document.getElementById(button.dataset.passwordToggle);
            if (!input) return;

            const showing = input.type === "password";
            input.type = showing ? "text" : "password";

            const icon = button.querySelector(".eye-icon");
            if (icon) icon.textContent = "👁";

            button.setAttribute("aria-label", showing ? "Скрыть пароль" : "Показать пароль");
            button.setAttribute("title", showing ? "Скрыть пароль" : "Показать пароль");
            button.setAttribute("aria-pressed", showing ? "true" : "false");
            button.classList.toggle("is-visible", showing);
        });
    });
});


    // Admin panel tabs.
    const adminTabs = document.querySelectorAll("[data-admin-tab]");
    const adminPanels = document.querySelectorAll("[data-admin-panel]");
    adminTabs.forEach((tab) => {
        tab.addEventListener("click", () => {
            const target = tab.dataset.adminTab;
            adminTabs.forEach((t) => t.classList.toggle("active", t === tab));
            adminPanels.forEach((panel) => panel.classList.toggle("active", panel.dataset.adminPanel === target));
            if (history.replaceState) history.replaceState(null, "", "#" + target);
        });
    });
    if (adminTabs.length) {
        const hash = window.location.hash.replace("#", "");
        const initial = [...adminTabs].find(t => t.dataset.adminTab === hash);
        if (initial) initial.click();
    }
