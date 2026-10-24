function setLanguage(language) {
  document.documentElement.lang = language;
  document.getElementById('content-en').classList.toggle('hidden', language !== 'en');
  document.getElementById('content-ru').classList.toggle('hidden', language !== 'ru');
  document.getElementById('btn-en').classList.toggle('active', language === 'en');
  document.getElementById('btn-ru').classList.toggle('active', language === 'ru');
  try { localStorage.setItem('musclecuties_lang', language); } catch { /* optional preference */ }
}

window.addEventListener('DOMContentLoaded', () => {
  const requested = new URLSearchParams(window.location.search).get('lang');
  let saved;
  try { saved = localStorage.getItem('musclecuties_lang'); } catch { /* use browser language */ }
  const browserLanguage = (navigator.language || '').toLowerCase().startsWith('ru') ? 'ru' : 'en';
  setLanguage(requested === 'ru' || requested === 'en' ? requested : (saved || browserLanguage));
});
