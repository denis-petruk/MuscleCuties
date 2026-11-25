(function () {
  'use strict';

  var STORAGE_KEY = 'musclecuties_lang';

  function readSaved() {
    try {
      return localStorage.getItem(STORAGE_KEY);
    } catch (error) {
      return null;
    }
  }

  function save(lang) {
    try {
      localStorage.setItem(STORAGE_KEY, lang);
    } catch (error) {
      /* The preference is optional; private mode must not break the page. */
    }
  }

  function setLanguage(lang) {
    var active = lang === 'ru' ? 'ru' : 'en';

    document.documentElement.lang = active;

    var en = document.getElementById('content-en');
    var ru = document.getElementById('content-ru');
    if (en) en.classList.toggle('hidden', active !== 'en');
    if (ru) ru.classList.toggle('hidden', active !== 'ru');

    var buttons = [
      { element: document.getElementById('btn-en'), lang: 'en' },
      { element: document.getElementById('btn-ru'), lang: 'ru' }
    ];
    buttons.forEach(function (button) {
      if (!button.element) return;
      var selected = button.lang === active;
      button.element.classList.toggle('active', selected);
      button.element.setAttribute('aria-pressed', selected ? 'true' : 'false');
    });

    // Carry the selection between files even when browser storage is unavailable.
    document.querySelectorAll('a[href*=".html"]').forEach(function (link) {
      var destination = new URL(link.href);
      destination.searchParams.set('lang', active);
      link.href = destination.href;
    });

    save(active);
  }

  function initialLanguage() {
    var requested;
    try {
      requested = new URLSearchParams(window.location.search).get('lang');
    } catch (error) {
      requested = null;
    }
    if (requested === 'ru' || requested === 'en') return requested;

    var saved = readSaved();
    if (saved === 'ru' || saved === 'en') return saved;

    return (navigator.language || '').toLowerCase().indexOf('ru') === 0 ? 'ru' : 'en';
  }

  // Each illustration settles into its printed pose once, when it comes into view.
  if ('IntersectionObserver' in window) {
    var arrivals = new IntersectionObserver(function (entries) {
      entries.forEach(function (entry) {
        if (entry.isIntersecting) {
          entry.target.classList.add('arrived');
          arrivals.unobserve(entry.target);
        }
      });
    });
    document.querySelectorAll('.pose, .studio-art').forEach(function (art) {
      arrivals.observe(art);
    });
  }

  window.setLanguage = setLanguage;

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function () {
      setLanguage(initialLanguage());
    });
  } else {
    setLanguage(initialLanguage());
  }
})();
