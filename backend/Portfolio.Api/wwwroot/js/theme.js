// Runs before first paint (classic script in <head>) to apply the saved or system colour theme without a flash.
(function () {
  var KEY = 'portfolio-theme';
  function stored() { try { return localStorage.getItem(KEY); } catch (e) { return null; } }
  function system() { return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'; }
  function apply(theme) {
    document.documentElement.setAttribute('data-bs-theme', theme);
    var buttons = document.querySelectorAll('[data-theme-toggle]');
    for (var i = 0; i < buttons.length; i++) {
      var dark = theme === 'dark';
      buttons[i].setAttribute('aria-label', dark ? 'Switch to light mode' : 'Switch to dark mode');
      buttons[i].setAttribute('aria-pressed', String(dark));
      var iconEl = buttons[i].querySelector('i');
      if (iconEl) iconEl.className = 'bi ' + (dark ? 'bi-sun' : 'bi-moon-stars');
    }
  }

  apply(stored() || system());

  window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', function () {
    if (!stored()) apply(system());
  });

  document.addEventListener('click', function (e) {
    var btn = e.target.closest && e.target.closest('[data-theme-toggle]');
    if (!btn) return;
    var next = document.documentElement.getAttribute('data-bs-theme') === 'dark' ? 'light' : 'dark';
    try { localStorage.setItem(KEY, next); } catch (err) { /* storage unavailable: theme still applies for this page */ }
    apply(next);
  });

  document.addEventListener('DOMContentLoaded', function () {
    apply(document.documentElement.getAttribute('data-bs-theme'));
  });
})();
