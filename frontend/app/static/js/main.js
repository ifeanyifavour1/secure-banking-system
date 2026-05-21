document.addEventListener("DOMContentLoaded", () => {
  // Mobile navigation
  const navToggle = document.querySelector("[data-nav-toggle]");
  const siteNav = document.querySelector("[data-site-nav]");

  if (navToggle && siteNav) {
    navToggle.addEventListener("click", () => {
      const isOpen = siteNav.classList.toggle("is-open");
      navToggle.setAttribute("aria-expanded", String(isOpen));
    });

    document.addEventListener("click", (event) => {
      if (!siteNav.contains(event.target) && !navToggle.contains(event.target)) {
        siteNav.classList.remove("is-open");
        navToggle.setAttribute("aria-expanded", "false");
      }
    });
  }

  // Staff dropdown
  document.querySelectorAll("[data-nav-dropdown]").forEach((dropdown) => {
    const trigger = dropdown.querySelector(".nav-dropdown-trigger");
    if (!trigger) return;

    trigger.addEventListener("click", (event) => {
      event.stopPropagation();
      const isOpen = dropdown.classList.toggle("is-open");
      trigger.setAttribute("aria-expanded", String(isOpen));
    });
  });

  document.addEventListener("click", () => {
    document.querySelectorAll("[data-nav-dropdown].is-open").forEach((dropdown) => {
      dropdown.classList.remove("is-open");
      const trigger = dropdown.querySelector(".nav-dropdown-trigger");
      if (trigger) trigger.setAttribute("aria-expanded", "false");
    });
  });

  // Dismiss flash messages
  document.querySelectorAll("[data-flash-dismiss]").forEach((button) => {
    button.addEventListener("click", () => {
      const flash = button.closest("[data-flash]");
      if (flash) {
        flash.style.opacity = "0";
        flash.style.transform = "translateY(-8px)";
        setTimeout(() => flash.remove(), 200);
      }
    });
  });

  // Password visibility toggle
  document.querySelectorAll("[data-password-toggle]").forEach((button) => {
    const targetId = button.getAttribute("data-target");
    const input = targetId ? document.getElementById(targetId) : null;
    if (!input || input.type !== "password") {
      return;
    }

    button.addEventListener("click", () => {
      const isHidden = input.type === "password";
      input.type = isHidden ? "text" : "password";
      button.textContent = isHidden ? "Hide" : "Show";
      button.setAttribute("aria-label", isHidden ? "Hide password" : "Show password");
      button.setAttribute("aria-pressed", String(isHidden));
    });
  });
});
