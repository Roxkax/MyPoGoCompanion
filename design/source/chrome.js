// Shared chrome for the mock-ups: status bar, phone tab bar, desktop sidebar and top bar.
(function () {
  const I = {
    today: '<path d="M12 3v2M12 19v2M4.2 4.2l1.4 1.4M18.4 18.4l1.4 1.4M3 12h2M19 12h2M4.2 19.8l1.4-1.4M18.4 5.6l1.4-1.4"/><circle cx="12" cy="12" r="4.2"/>',
    pokedex: '<rect x="4" y="3" width="16" height="18" rx="3"/><path d="M8 8h8M8 12h8M8 16h5"/>',
    battle: '<path d="M14.5 17.5 3 6V3h3l11.5 11.5M13 19l6-6M16 16l4 4M19 21l2-2"/><path d="M9.5 6.5 14 2h3v3l-4.5 4.5"/>',
    filters: '<path d="M3 5h18l-7 8v6l-4 2v-8z"/>',
    collection: '<path d="M3 7.5 12 3l9 4.5v9L12 21l-9-4.5z"/><path d="M3 7.5 12 12l9-4.5M12 12v9"/>',
    ask: '<path d="M12 3l1.6 3.9L17.5 8.5l-3.9 1.6L12 14l-1.6-3.9L6.5 8.5l3.9-1.6z"/><path d="M18.5 14l.8 1.9 1.9.8-1.9.8-.8 1.9-.8-1.9-1.9-.8 1.9-.8z"/>',
    search: '<circle cx="11" cy="11" r="7"/><path d="m20 20-3.5-3.5"/>',
    calendar: '<rect x="3" y="5" width="18" height="16" rx="3"/><path d="M3 10h18M8 3v4M16 3v4"/>',
    egg: '<path d="M12 3c-3.5 0-6.5 5.5-6.5 10a6.5 6.5 0 0 0 13 0C18.5 8.5 15.5 3 12 3z"/>',
    moves: '<path d="M13 2 4 14h7l-1 8 9-12h-7z"/>',
    chart: '<path d="M4 20V10M10 20V4M16 20v-7M22 20H2"/>',
    settings: '<circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.7 1.7 0 0 0 .3 1.8l.1.1a2 2 0 1 1-2.8 2.8l-.1-.1a1.7 1.7 0 0 0-1.8-.3 1.7 1.7 0 0 0-1 1.5V21a2 2 0 1 1-4 0v-.1a1.7 1.7 0 0 0-1.1-1.5 1.7 1.7 0 0 0-1.8.3l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1a1.7 1.7 0 0 0 .3-1.8 1.7 1.7 0 0 0-1.5-1H3a2 2 0 1 1 0-4h.1a1.7 1.7 0 0 0 1.5-1.1 1.7 1.7 0 0 0-.3-1.8l-.1-.1a2 2 0 1 1 2.8-2.8l.1.1a1.7 1.7 0 0 0 1.8.3H9a1.7 1.7 0 0 0 1-1.5V3a2 2 0 1 1 4 0v.1a1.7 1.7 0 0 0 1 1.5 1.7 1.7 0 0 0 1.8-.3l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1.7 1.7 0 0 0-.3 1.8V9a1.7 1.7 0 0 0 1.5 1H21a2 2 0 1 1 0 4h-.1a1.7 1.7 0 0 0-1.5 1z"/>',
    admin: '<path d="M12 3 4 6v6c0 5 3.5 8 8 9 4.5-1 8-4 8-9V6z"/>'
  };
  const svg = (k, s = 22, w = 1.9) => `<svg width="${s}" height="${s}" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="${w}" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${I[k]}</svg>`;
  window.icon = svg;
  const TABS = [["today", "Today"], ["pokedex", "Pokédex"], ["battle", "Battle"], ["filters", "Filters"], ["collection", "Collection"]];
  const logo = `<svg width="30" height="30" viewBox="0 0 30 30" aria-hidden="true"><rect width="30" height="30" rx="9" fill="var(--accent)"/><path d="M8.5 20.5v-11l6.5 6.5 6.5-6.5v11" fill="none" stroke="#fff" stroke-width="2.6" stroke-linecap="round" stroke-linejoin="round"/></svg>`;

  document.querySelectorAll("[data-statusbar]").forEach(el => {
    el.outerHTML = `<div class="statusbar"><span class="num">18:30</span><span class="icons">
      <svg width="18" height="12" viewBox="0 0 18 12"><rect x="0" y="8" width="3" height="4" rx="1" fill="currentColor"/><rect x="5" y="5" width="3" height="7" rx="1" fill="currentColor"/><rect x="10" y="2" width="3" height="10" rx="1" fill="currentColor"/><rect x="15" y="0" width="3" height="12" rx="1" fill="currentColor"/></svg>
      <svg width="16" height="12" viewBox="0 0 16 12"><path d="M8 11.5 1 4.6a10 10 0 0 1 14 0z" fill="currentColor"/></svg>
      <svg width="27" height="13" viewBox="0 0 27 13"><rect x=".5" y=".5" width="23" height="12" rx="3.5" fill="none" stroke="currentColor" opacity=".4"/><rect x="2" y="2" width="16" height="9" rx="2" fill="currentColor"/><rect x="25" y="4" width="2" height="5" rx="1" fill="currentColor" opacity=".4"/></svg>
    </span></div><div class="island"></div>`;
  });
  document.querySelectorAll("[data-tabbar]").forEach(el => {
    const on = el.dataset.tabbar;
    el.outerHTML = `<nav class="tabbar">${TABS.map(([k, n]) => `<a class="${k === on ? "on" : ""}">${svg(k, 24, k === on ? 2.3 : 1.9)}${n}</a>`).join("")}</nav><div class="homebar"></div>`;
  });
  document.querySelectorAll("[data-sidebar]").forEach(el => {
    const on = el.dataset.sidebar;
    const sec = [["calendar", "Calendar"], ["egg", "Eggs & research"], ["moves", "Moves"], ["chart", "Tier lists"]];
    el.outerHTML = `<aside class="sidebar"><div class="brand">${logo}MyPoGoCompanion</div>
      ${TABS.map(([k, n]) => `<a class="navitem ${k === on ? "on" : ""}">${svg(k, 20)}${n}</a>`).join("")}
      <div class="navlabel">Quick links</div>
      ${sec.map(([k, n]) => `<a class="navitem ${k === on ? "on" : ""}">${svg(k, 20)}${n}</a>`).join("")}
      <div style="margin-top:auto;border-top:1px solid var(--line);padding-top:10px">
      <a class="navitem">${svg("settings", 20)}Settings</a><a class="navitem">${svg("admin", 20)}Admin</a></div></aside>`;
  });
  document.querySelectorAll("[data-topbar]").forEach(el => {
    el.outerHTML = `<header class="topbar"><div class="searchbox wide">${svg("search", 19)}Search Pokémon, moves, bosses, events<kbd>Ctrl K</kbd></div>
      <span style="flex:1"></span><button class="askbtn">${svg("ask", 18)}Ask</button><div class="avatar" title="Ash_PT">A</div></header>`;
  });
  document.querySelectorAll("[data-search]").forEach(el => {
    el.outerHTML = `<div class="searchbox">${svg("search", 19)}Search Pokémon, moves, bosses…</div>`;
  });
  document.querySelectorAll("[data-ask]").forEach(el => { el.outerHTML = `<button class="askbtn">${svg("ask", 18)}Ask</button>`; });
  if (new URLSearchParams(location.search).get("full")) document.documentElement.classList.add("full");
  if (new URLSearchParams(location.search).get("theme") === "dark") document.documentElement.dataset.theme = "dark";
})();
