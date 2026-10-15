/* Ensure GitHub stars/forks render even if Material cached an empty __source. */
(() => {
  const REPO_API = "https://api.github.com/repos/Kerbalism/Kerbalism";

  function formatCount(n) {
    if (typeof n !== "number") return String(n);
    return n > 999 ? `${(n / 1000).toFixed(1).replace(/\.0$/, "")}k` : String(n);
  }

  function clearBadSourceCache() {
    try {
      const doomed = [];
      for (let i = 0; i < sessionStorage.length; i++) {
        const key = sessionStorage.key(i);
        if (!key || !key.endsWith(".__source")) continue;
        try {
          const data = JSON.parse(sessionStorage.getItem(key));
          if (!data || (data.stars == null && data.forks == null)) doomed.push(key);
        } catch {
          doomed.push(key);
        }
      }
      doomed.forEach((key) => sessionStorage.removeItem(key));
    } catch {
      /* ignore */
    }
  }

  function applyFacts(facts) {
    document.querySelectorAll('[data-md-component="source"]').forEach((source) => {
      const repo = source.querySelector(":scope > .md-source__repository");
      if (!repo || repo.querySelector(".md-source__facts")) return;

      const ul = document.createElement("ul");
      ul.className = "md-source__facts";
      for (const [key, value] of Object.entries(facts)) {
        if (value == null || value === "") continue;
        const li = document.createElement("li");
        li.className = `md-source__fact md-source__fact--${key}`;
        li.textContent = typeof value === "number" ? formatCount(value) : value;
        ul.appendChild(li);
      }
      if (!ul.children.length) return;
      repo.appendChild(ul);
      repo.classList.add("md-source__repository--active");
    });
  }

  function ensureStars() {
    if (document.querySelector('[data-md-component="source"] .md-source__facts')) return;

    clearBadSourceCache();

    const cached =
      typeof __md_get === "function" ? __md_get("__source", sessionStorage) : null;
    if (cached && (cached.stars != null || cached.forks != null)) {
      applyFacts(cached);
      return;
    }

    fetch(REPO_API)
      .then((response) => (response.ok ? response.json() : Promise.reject()))
      .then((repo) => {
        const facts = {
          stars: repo.stargazers_count,
          forks: repo.forks_count,
        };
        if (typeof __md_set === "function") {
          __md_set("__source", facts, sessionStorage);
        }
        applyFacts(facts);
      })
      .catch(() => {});
  }

  if (typeof document$ !== "undefined" && document$.subscribe) {
    document$.subscribe(() => {
      window.setTimeout(ensureStars, 400);
    });
  } else {
    window.addEventListener("DOMContentLoaded", () => {
      window.setTimeout(ensureStars, 400);
    });
  }
})();
