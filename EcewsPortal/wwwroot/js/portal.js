(function () {
  'use strict';

  var isCard = true;
  var activeFilter = 'all';
  var pinnedIds = [];
  var recentIds = [];
  var cmdActiveIndex = 0;
  var heroSlideIndex = 0;
  var heroCarouselTimer = null;

  /* ── Utilities ── */
  function $(sel, ctx) { return (ctx || document).querySelector(sel); }
  function $$(sel, ctx) { return Array.from((ctx || document).querySelectorAll(sel)); }

  function showToast(message, icon) {
    var container = $('#toastContainer');
    if (!container) return;
    var toast = document.createElement('div');
    toast.className = 'toast';
    toast.innerHTML = '<i class="ti ' + (icon || 'ti-check') + '"></i><span>' + message + '</span>';
    container.appendChild(toast);
    setTimeout(function () {
      toast.classList.add('out');
      setTimeout(function () { toast.remove(); }, 300);
    }, 3200);
  }

  function createRipple(e, el) {
    var rect = el.getBoundingClientRect();
    var size = Math.max(rect.width, rect.height);
    var ripple = document.createElement('span');
    ripple.className = 'ripple';
    ripple.style.width = ripple.style.height = size + 'px';
    ripple.style.left = (e.clientX - rect.left - size / 2) + 'px';
    ripple.style.top = (e.clientY - rect.top - size / 2) + 'px';
    el.style.position = 'relative';
    el.style.overflow = 'hidden';
    el.appendChild(ripple);
    setTimeout(function () { ripple.remove(); }, 600);
  }

  /* ── Weather (dynamic, location-aware) ── */
  var WX_PLACE_KEY = 'ecews-weather-place';

  function applyWeather(data) {
    if (!data) return;

    var topLoc = (data.location || '').split(',')[0].trim();
    var locShortText = topLoc ? ('Weather · ' + topLoc) : 'Weather';

    var locShortEl = $('#wxLocShortText');
    if (locShortEl) locShortEl.textContent = locShortText;

    var tempEl = $('#wxTemp');
    if (tempEl && data.temperature) tempEl.textContent = data.temperature;

    var locEl = $('#wxLoc');
    if (locEl && data.location) locEl.textContent = data.location;

    var descEl = $('#wxDesc');
    if (descEl && data.description) descEl.textContent = data.description;

    var iconEl = $('#wxIcon');
    if (iconEl && data.icon) iconEl.textContent = data.icon;

    var forecast = data.forecast || [];
    for (var i = 0; i < forecast.length && i < 5; i++) {
      var day = forecast[i];
      if (!day) continue;
      var dayName = $('#wxDayName-' + i);
      var dayIcon = $('#wxDayIcon-' + i);
      var highEl = $('#wxHigh-' + i);
      var lowEl = $('#wxLow-' + i);
      if (dayName && day.day) dayName.textContent = day.day;
      if (dayIcon && day.icon) dayIcon.textContent = day.icon;
      if (highEl && day.high) highEl.textContent = day.high;
      if (lowEl && day.low) lowEl.textContent = day.low;
    }
  }

  function saveWxPlace(place) {
    try { localStorage.setItem(WX_PLACE_KEY, JSON.stringify(place)); } catch (e) {}
  }

  function loadWxPlace() {
    try { return JSON.parse(localStorage.getItem(WX_PLACE_KEY) || 'null'); } catch (e) { return null; }
  }

  function fillHomeStateSelect() {
    var select = $('#wxHomeStateSelect');
    if (!select || !window.EcewsWeather) return;
    var places = EcewsWeather.places.slice().sort(function (a, b) {
      return a.state.localeCompare(b.state);
    });
    places.forEach(function (p) {
      var opt = document.createElement('option');
      opt.value = p.id;
      opt.textContent = p.label;
      select.appendChild(opt);
    });
  }

  async function fetchWeatherQuery(query, days) {
    var res = await fetch('/api/portal/weather?' + query + '&days=' + (days || 5), {
      credentials: 'same-origin',
      headers: { 'Accept': 'application/json' }
    });
    if (res.ok) return res.json();
    if (window.EcewsWeather) return EcewsWeather.fetchDirect(query, days || 5);
    throw new Error('Weather unavailable');
  }

  async function weatherFromCurrent() {
    var coords = await EcewsWeather.getCurrentCoords();
    var nearest = EcewsWeather.nearestPlace(coords.lat, coords.lon);
    var label = 'Near ' + nearest.label;
    saveWxPlace({ mode: 'current', lat: coords.lat, lon: coords.lon, name: label });
    var select = $('#wxHomeStateSelect');
    if (select) select.value = '__current__';
    return fetchWeatherQuery(
      'lat=' + coords.lat + '&lon=' + coords.lon + '&name=' + encodeURIComponent(label),
      5
    );
  }

  async function weatherFromState(id) {
    saveWxPlace({ mode: 'state', id: id });
    var select = $('#wxHomeStateSelect');
    if (select) select.value = id;
    return fetchWeatherQuery('state=' + encodeURIComponent(id), 5);
  }

  async function updateWeather() {
    if (updateWeather._inFlight) return;
    updateWeather._inFlight = true;
    try {
      if (!window.EcewsWeather) {
        applyWeather(await fetchWeatherQuery('state=lagos', 5));
        return;
      }
      var saved = loadWxPlace();
      var data;
      if (saved && saved.mode === 'state' && saved.id) {
        data = await weatherFromState(saved.id);
      } else {
        try {
          data = await weatherFromCurrent();
        } catch (geoErr) {
          data = await weatherFromState('lagos');
        }
      }
      applyWeather(data);
    } catch (e) {
      console.warn('Weather unavailable', e);
    } finally {
      updateWeather._inFlight = false;
    }
  }

  function initWeatherControls() {
    fillHomeStateSelect();
    var select = $('#wxHomeStateSelect');
    var locBtn = $('#wxHomeLocBtn');
    var saved = loadWxPlace();
    if (select && saved && saved.mode === 'state' && saved.id) select.value = saved.id;
    if (select && saved && saved.mode === 'current') select.value = '__current__';

    if (select) {
      select.addEventListener('change', function () {
        var val = select.value;
        var run = val === '__current__' ? weatherFromCurrent() : weatherFromState(val);
        run.then(applyWeather).catch(function () {
          showToast('Could not load weather', 'ti-cloud-off');
        });
      });
    }
    if (locBtn) {
      locBtn.addEventListener('click', function () {
        weatherFromCurrent().then(applyWeather).catch(function () {
          showToast('Allow location access or pick a state', 'ti-current-location');
        });
      });
    }
  }

  /* ── Page loader ── */
  function initLoader() {
    var loader = $('#pageLoader');
    if (!loader) return;

    var SPLASH_MS = 1200;
    var startedAt = Date.now();

    function hideLoader() {
      var elapsed = Date.now() - startedAt;
      var wait = Math.max(0, SPLASH_MS - elapsed);
      setTimeout(function () {
        loader.classList.add('done');
        document.body.style.overflow = '';
      }, wait);
    }

    if (document.readyState === 'complete') {
      hideLoader();
    } else {
      window.addEventListener('load', hideLoader);
    }
    document.body.style.overflow = 'hidden';
  }

  /* ── Layout ── */
  function applyLayout() {
    var grid = $('#appsGrid');
    if (grid) grid.classList.toggle('compact', !isCard);
    var ic = $('#layoutIc');
    var txt = $('#layoutTxt');
    var btn = $('#layoutBtn');
    if (ic) ic.className = isCard ? 'ti ti-layout-grid' : 'ti ti-layout-list';
    if (txt) txt.textContent = isCard ? 'Compact' : 'Card';
    if (btn) btn.classList.toggle('active-mode', !isCard);
  }

  function toggleLayout() {
    isCard = !isCard;
    applyLayout();
    localStorage.setItem('ecews-layout', isCard ? 'card' : 'compact');
    showToast(isCard ? 'Card layout restored' : 'Compact layout enabled', 'ti-layout-grid');
  }

  /* ── Preferences ── */
  function initPrefs() {
    if (localStorage.getItem('ecews-layout') === 'compact') { isCard = false; applyLayout(); }
    // Collapsed by default; expand only if the user explicitly expanded it before
    if (localStorage.getItem('ecews-sidebar-collapsed') !== '0') {
      var portal = $('#portal');
      if (portal) portal.classList.add('sidebar-collapsed');
    }
    try {
      pinnedIds = JSON.parse(localStorage.getItem('ecews-pinned') || '[]');
    } catch (e) { pinnedIds = []; }
    try {
      recentIds = JSON.parse(localStorage.getItem('ecews-recent') || '[]');
    } catch (e) { recentIds = []; }
  }

  /* ── Scroll reveal ── */
  function initReveal() {
    if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
      $$('.reveal').forEach(function (el) { el.classList.add('visible'); });
      return;
    }
    var observer = new IntersectionObserver(function (entries) {
      entries.forEach(function (entry) {
        if (entry.isIntersecting) {
          entry.target.classList.add('visible');
          observer.unobserve(entry.target);
        }
      });
    }, { threshold: 0.1, rootMargin: '0px 0px -40px 0px' });
    $$('.reveal').forEach(function (el) { observer.observe(el); });
  }

  /* ── Animated counters ── */
  function initCounters() {
    $$('[data-count]').forEach(function (el) {
      var target = parseInt(el.dataset.count, 10);
      if (isNaN(target)) return;
      var duration = 800;
      var start = performance.now();
      function step(now) {
        var progress = Math.min((now - start) / duration, 1);
        var eased = 1 - Math.pow(1 - progress, 3);
        el.textContent = Math.round(target * eased);
        if (progress < 1) requestAnimationFrame(step);
      }
      requestAnimationFrame(step);
    });
  }

  /* ── Back to top ── */
  function initBackToTop() {
    var btn = $('#backToTop');
    if (!btn) return;
    window.addEventListener('scroll', function () {
      btn.classList.toggle('show', window.scrollY > 400);
    }, { passive: true });
    btn.addEventListener('click', function () {
      window.scrollTo({ top: 0, behavior: 'smooth' });
    });
  }

  /* ── Announcement ── */
  function initAnnouncement() {
    var bar = $('#announcementBar');
    var close = $('#announcementClose');
    if (!bar || !close) return;
    if (localStorage.getItem('ecews-announcement-dismissed') === '1') {
      bar.remove();
      return;
    }
    close.addEventListener('click', function () {
      bar.style.animation = 'fadeOut 0.3s ease forwards';
      setTimeout(function () { bar.remove(); }, 280);
      localStorage.setItem('ecews-announcement-dismissed', '1');
    });
  }

  /* ── Notifications panel ── */
  function initNotifications() {
    var btn = $('#notifBtn');
    var panel = $('#notifPanel');
    if (!btn || !panel) return;
    btn.addEventListener('click', function (e) {
      e.stopPropagation();
      var open = panel.hidden;
      panel.hidden = !open;
      btn.setAttribute('aria-expanded', open ? 'true' : 'false');
    });
    document.addEventListener('click', function () {
      panel.hidden = true;
      btn.setAttribute('aria-expanded', 'false');
    });
    panel.addEventListener('click', function (e) { e.stopPropagation(); });
  }

  /* ── Filter apps ── */
  function filterApps(query) {
    query = (query || '').toLowerCase().trim();
    var cards = $$('.app-card:not(.no-results)', $('#appsGrid'));
    var visible = 0;
    cards.forEach(function (card) {
      var name = card.dataset.name || '';
      var cats = (card.dataset.cat || '').split(' ');
      var appName = card.querySelector('.app-name');
      var matchSearch = !query || name.indexOf(query) !== -1 ||
        (appName && appName.textContent.toLowerCase().indexOf(query) !== -1);
      var matchFilter = activeFilter === 'all' || cats.indexOf(activeFilter) !== -1;
      var show = matchSearch && matchFilter;
      card.classList.toggle('hidden', !show);
      if (show) visible++;
    });
    var noResults = $('#noResults');
    var appsCount = $('#appsCount');
    if (noResults) noResults.classList.toggle('show', visible === 0);
    if (appsCount) appsCount.textContent = visible + ' app' + (visible !== 1 ? 's' : '');
  }

  function initNavFilter() {
    $$('.nav-btn').forEach(function (btn) {
      btn.addEventListener('click', function () {
        $$('.nav-btn').forEach(function (x) { x.classList.remove('active'); });
        btn.classList.add('active');
        activeFilter = btn.dataset.filter || 'all';
        filterApps($('#searchInput') ? $('#searchInput').value : '');
      });
    });
  }

  /* ── Pin apps ── */
  function renderPinned() {
    var section = $('#pinnedSection');
    var grid = $('#pinnedGrid');
    var count = $('#pinnedCount');
    if (!section || !grid) return;

    grid.innerHTML = '';
    pinnedIds.forEach(function (id) {
      var card = $('.app-card[data-id="' + id + '"]', $('#appsGrid'));
      if (!card) return;
      var clone = card.cloneNode(true);
      clone.classList.add('pinned-clone');
      clone.addEventListener('click', function (e) {
        if (e.target.closest('.pin-btn')) return;
        openApp(card);
      });
      var pinBtn = clone.querySelector('.pin-btn');
      if (pinBtn) {
        pinBtn.addEventListener('click', function (e) {
          e.stopPropagation();
          togglePin(id);
        });
      }
      grid.appendChild(clone);
    });

    $$('.app-card[data-id]', $('#appsGrid')).forEach(function (card) {
      card.classList.toggle('pinned', pinnedIds.indexOf(card.dataset.id) !== -1);
    });

    section.hidden = pinnedIds.length === 0;
    if (count) count.textContent = pinnedIds.length + ' pinned';
  }

  function togglePin(id) {
    var idx = pinnedIds.indexOf(id);
    if (idx === -1) {
      pinnedIds.push(id);
      var card = $('.app-card[data-id="' + id + '"]');
      showToast('Pinned ' + (card ? card.querySelector('.app-name').textContent : 'app'), 'ti-pin');
    } else {
      pinnedIds.splice(idx, 1);
      showToast('App unpinned', 'ti-pin-off');
    }
    localStorage.setItem('ecews-pinned', JSON.stringify(pinnedIds));
    renderPinned();
  }

  function trackRecent(id) {
    id = String(id);
    recentIds = recentIds.filter(function (x) { return x !== id; });
    recentIds.unshift(id);
    if (recentIds.length > 6) recentIds = recentIds.slice(0, 6);
    localStorage.setItem('ecews-recent', JSON.stringify(recentIds));
    renderRecent();
  }

  function renderRecent() {
    var list = $('#quickAccessList');
    var empty = $('#quickAccessEmpty');
    var countLabel = $('#recentCountLabel');
    if (!list) return;

    $$('.ql.ql-recent', list).forEach(function (el) { el.remove(); });

    var shown = 0;
    recentIds.forEach(function (id) {
      var card = $('.app-card[data-id="' + id + '"]', $('#appsGrid'));
      if (!card) return;

      var nameEl = card.querySelector('.app-name');
      var tagEl = card.querySelector('.app-tag');
      var iconEl = card.querySelector('.app-icon');
      if (!nameEl || !iconEl) return;

      var row = document.createElement('button');
      row.type = 'button';
      row.className = 'ql ql-recent';
      row.setAttribute('data-id', id);

      var ic = document.createElement('div');
      ic.className = 'ql-ic ' + (iconEl.className.replace('app-icon', '').trim());
      ic.innerHTML = iconEl.innerHTML;

      var text = document.createElement('div');
      text.innerHTML =
        '<div class="ql-nm"></div><div class="ql-ds"></div>';
      text.querySelector('.ql-nm').textContent = nameEl.textContent;
      text.querySelector('.ql-ds').textContent = tagEl ? tagEl.textContent : 'Recently visited';

      var chevron = document.createElement('i');
      chevron.className = 'ti ti-chevron-right ql-ext';

      row.appendChild(ic);
      row.appendChild(text);
      row.appendChild(chevron);
      row.addEventListener('click', function () {
        openApp(card, false);
      });

      list.appendChild(row);
      shown++;
    });

    if (empty) empty.hidden = shown > 0;
    if (countLabel) {
      countLabel.textContent = shown > 0
        ? shown + ' recent'
        : 'Recently visited';
    }
  }

    function openApp(card, track) {
    if (track !== false && card.dataset.id) trackRecent(card.dataset.id);
    if (track !== false && card.dataset.id) trackAppVisit(card);
    var url = card.dataset.url;
    var external = card.dataset.external === 'true';
    if (url) {
      if (external) window.open(url, '_blank', 'noopener');
      else window.location.href = url;
    } else {
      showToast('Opening ' + card.querySelector('.app-name').textContent + '…', 'ti-external-link');
    }
  }

  function trackAppVisit(card) {
    try {
      var id = parseInt(card.dataset.id, 10);
      if (!id) return;
      var nameEl = card.querySelector('.app-name');
      var payload = JSON.stringify({
        appId: id,
        appName: nameEl ? nameEl.textContent.trim() : ''
      });
      fetch('/api/portal/track-visit', {
        method: 'POST',
        credentials: 'same-origin',
        headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' },
        body: payload,
        keepalive: true
      }).catch(function () {});
    } catch (e) { /* ignore */ }
  }

  function initAppCards() {
    $$('.app-card[data-id]', $('#appsGrid')).forEach(function (card) {
      card.addEventListener('click', function (e) {
        if (e.target.closest('.pin-btn')) return;
        createRipple(e, card);
        setTimeout(function () { openApp(card); }, 150);
      });
      card.addEventListener('keydown', function (e) {
        if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); openApp(card); }
      });
      var pinBtn = card.querySelector('.pin-btn');
      if (pinBtn) {
        pinBtn.addEventListener('click', function (e) {
          e.stopPropagation();
          togglePin(card.dataset.id);
        });
      }
    });
    renderPinned();
    renderRecent();
  }

  /* ── Command palette ── */
  function getCmdItems() {
    return $$('.app-card[data-id]', $('#appsGrid')).map(function (card) {
      return {
        id: card.dataset.id,
        name: card.querySelector('.app-name').textContent,
        tag: card.querySelector('.app-tag').textContent,
        iconEl: card.querySelector('.app-icon'),
        card: card
      };
    });
  }

  function renderCmdResults(query) {
    var results = $('#cmdResults');
    if (!results) return;
    query = (query || '').toLowerCase().trim();
    var items = getCmdItems().filter(function (item) {
      return !query || item.name.toLowerCase().indexOf(query) !== -1 ||
        item.tag.toLowerCase().indexOf(query) !== -1;
    });
    cmdActiveIndex = 0;
    results.innerHTML = items.length === 0
      ? '<div style="padding:20px;text-align:center;color:var(--muted);font-size:13px">No results found</div>'
      : items.map(function (item, i) {
          var ic = item.iconEl.cloneNode(true);
          ic.classList.add('cmd-item-ic');
          return '<div class="cmd-item' + (i === 0 ? ' active' : '') + '" data-id="' + item.id + '">' +
            ic.outerHTML +
            '<div><div class="cmd-item-name">' + item.name + '</div><div class="cmd-item-tag">' + item.tag + '</div></div></div>';
        }).join('');

    $$('.cmd-item', results).forEach(function (el, i) {
      el.addEventListener('click', function () {
        var card = $('.app-card[data-id="' + el.dataset.id + '"]');
        closeCmd();
        if (card) openApp(card);
      });
      el.addEventListener('mouseenter', function () {
        $$('.cmd-item', results).forEach(function (x) { x.classList.remove('active'); });
        el.classList.add('active');
        cmdActiveIndex = i;
      });
    });
  }

  function openCmd() {
    var overlay = $('#cmdOverlay');
    var input = $('#cmdInput');
    if (!overlay || !input) return;
    overlay.hidden = false;
    input.value = '';
    renderCmdResults('');
    setTimeout(function () { input.focus(); }, 50);
    document.body.style.overflow = 'hidden';
  }

  function closeCmd() {
    var overlay = $('#cmdOverlay');
    if (overlay) overlay.hidden = true;
    document.body.style.overflow = '';
  }

  function initCommandPalette() {
    var trigger = $('#searchTrigger');
    var mobileTrigger = $('#mobileSearchBtn');
    var overlay = $('#cmdOverlay');
    var input = $('#cmdInput');
    if (trigger) trigger.addEventListener('click', openCmd);
    if (mobileTrigger) mobileTrigger.addEventListener('click', openCmd);
    if (overlay) {
      overlay.addEventListener('click', function (e) {
        if (e.target === overlay) closeCmd();
      });
    }
    if (input) {
      input.addEventListener('input', function () {
        renderCmdResults(input.value);
        var hidden = $('#searchInput');
        if (hidden) hidden.value = input.value;
        filterApps(input.value);
      });
      input.addEventListener('keydown', function (e) {
        var items = $$('.cmd-item', $('#cmdResults'));
        if (e.key === 'ArrowDown') {
          e.preventDefault();
          cmdActiveIndex = Math.min(cmdActiveIndex + 1, items.length - 1);
          items.forEach(function (x, i) { x.classList.toggle('active', i === cmdActiveIndex); });
        } else if (e.key === 'ArrowUp') {
          e.preventDefault();
          cmdActiveIndex = Math.max(cmdActiveIndex - 1, 0);
          items.forEach(function (x, i) { x.classList.toggle('active', i === cmdActiveIndex); });
        } else if (e.key === 'Enter' && items[cmdActiveIndex]) {
          items[cmdActiveIndex].click();
        }
      });
    }
    document.addEventListener('keydown', function (e) {
      if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
        e.preventDefault();
        var overlay = $('#cmdOverlay');
        if (overlay && overlay.hidden) openCmd();
        else closeCmd();
      }
      if (e.key === 'Escape') closeCmd();
    });
  }

  /* ── Hero carousel ── */
  function goToHeroSlide(index) {
    var carousel = $('#heroCarousel');
    if (!carousel) return;
    var slides = $$('.hero-slide', carousel);
    var dots = $$('.hero-dot', $('#heroDots'));
    if (!slides.length) return;

    heroSlideIndex = ((index % slides.length) + slides.length) % slides.length;
    slides.forEach(function (s, i) {
      s.classList.toggle('active', i === heroSlideIndex);
    });
    dots.forEach(function (d, i) {
      d.classList.toggle('active', i === heroSlideIndex);
      d.setAttribute('aria-selected', i === heroSlideIndex ? 'true' : 'false');
    });
  }

  function startHeroCarousel() {
    if (heroCarouselTimer) clearInterval(heroCarouselTimer);
    if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) return;
    var slides = $$('.hero-slide', $('#heroCarousel'));
    if (slides.length < 2) return;
    heroCarouselTimer = setInterval(function () {
      goToHeroSlide(heroSlideIndex + 1);
    }, 5500);
  }

  function initHeroCarousel() {
    var carousel = $('#heroCarousel');
    if (!carousel) return;

    $$('.hero-dot', $('#heroDots')).forEach(function (dot) {
      dot.addEventListener('click', function (e) {
        e.stopPropagation();
        goToHeroSlide(parseInt(dot.dataset.slide, 10));
        startHeroCarousel();
      });
    });

    carousel.addEventListener('mouseenter', function () {
      if (heroCarouselTimer) clearInterval(heroCarouselTimer);
    });
    carousel.addEventListener('mouseleave', startHeroCarousel);

    $$('.hero-slide', carousel).forEach(function (slide) {
      slide.addEventListener('click', function (e) {
        if (e.target.closest('.hero-dot')) return;
        if (e.target.closest('.hero-link')) return;
        var link = slide.querySelector('.hero-link');
        if (link && link.getAttribute('href') && link.getAttribute('href') !== '#') {
          var href = link.getAttribute('href');
          if (href.indexOf('/Home/Article/') === 0 || href.indexOf('/newsletters/') === 0 && href.indexOf('.pdf') < 0) {
            window.location.href = href;
          } else if (href.indexOf('.pdf') >= 0) {
            window.open(href, '_blank', 'noopener');
          } else {
            window.location.href = href;
          }
          return;
        }
        showToast('Opening featured article…', 'ti-news');
      });
    });

    $$('.hero-side-card').forEach(function (card) {
      card.addEventListener('click', function (e) {
        if (e.target.closest('.hero-link')) return;
        var link = card.querySelector('.hero-link');
        if (link && link.getAttribute('href') && link.getAttribute('href') !== '#') {
          window.location.href = link.getAttribute('href');
          return;
        }
        showToast('Opening featured article…', 'ti-news');
      });
    });

    startHeroCarousel();
  }

  /* ── System status ── */
  function applySystemStatus(status) {
    var el = $('#systemStatus');
    var dot = $('#systemStatusDot');
    var text = $('#systemStatusText');
    if (!el || !text) return;

    el.classList.remove('is-live', 'is-degraded', 'is-down');
    if (status === 'operational') {
      el.classList.add('is-live');
      text.textContent = 'All systems operational';
      el.title = 'All portal services are running normally';
    } else if (status === 'degraded') {
      el.classList.add('is-degraded');
      text.textContent = 'Degraded performance';
      el.title = 'Some services may be slow';
    } else {
      el.classList.add('is-down');
      text.textContent = 'Service unavailable';
      el.title = 'Unable to reach portal services';
    }
  }

  async function updateSystemStatus() {
    try {
      var controller = new AbortController();
      var timeout = setTimeout(function () { controller.abort(); }, 5000);
      var res = await fetch('/api/portal/health', { signal: controller.signal });
      clearTimeout(timeout);
      if (!res.ok) throw new Error('Health check failed');
      var data = await res.json();
      applySystemStatus(data.status || 'operational');
    } catch (e) {
      applySystemStatus('down');
    }
  }

  /* ── User menu ── */
  function initUserMenu() {
    var btn = $('#userMenuBtn');
    var panel = $('#userMenuPanel');
    if (!btn || !panel) return;

    btn.addEventListener('click', function (e) {
      e.stopPropagation();
      var notifPanel = $('#notifPanel');
      if (notifPanel) notifPanel.hidden = true;
      var open = panel.hidden;
      panel.hidden = !open;
      btn.setAttribute('aria-expanded', open ? 'true' : 'false');
    });

    var signOut = $('.pro-user-signout', panel);
    if (signOut && signOut.tagName === 'BUTTON' && !signOut.closest('form')) {
      signOut.addEventListener('click', function () {
        showToast('Signing out…', 'ti-logout');
        panel.hidden = true;
        btn.setAttribute('aria-expanded', 'false');
      });
    }

    document.addEventListener('click', function () {
      panel.hidden = true;
      btn.setAttribute('aria-expanded', 'false');
    });
    panel.addEventListener('click', function (e) { e.stopPropagation(); });
  }

  /* ── Sidebar toggle (mobile + collapse) ── */
  function initSidebar() {
    var toggle = $('#sidebarToggle');
    var collapseBtn = $('#sidebarCollapseBtn');
    var backdrop = $('#sidebarBackdrop');
    var portal = $('#portal');
    if (!portal) return;

    function closeSidebar() {
      portal.classList.remove('sidebar-open');
      if (toggle) toggle.setAttribute('aria-expanded', 'false');
    }

    function openSidebar() {
      portal.classList.add('sidebar-open');
      if (toggle) toggle.setAttribute('aria-expanded', 'true');
    }

    if (toggle) {
      toggle.setAttribute('aria-expanded', 'false');
      toggle.addEventListener('click', function (e) {
        e.stopPropagation();
        if (portal.classList.contains('sidebar-open')) closeSidebar();
        else openSidebar();
      });
    }

    if (backdrop) {
      backdrop.addEventListener('click', closeSidebar);
    }

    document.addEventListener('click', function (e) {
      if (!portal.classList.contains('sidebar-open')) return;
      if (e.target.closest('.pro-sidebar') || e.target.closest('.sidebar-toggle')) return;
      closeSidebar();
    });

    // Close drawer after choosing a nav item on mobile
    $$('.pro-nav-item', portal).forEach(function (item) {
      item.addEventListener('click', function () {
        if (window.matchMedia('(max-width: 1024px)').matches) closeSidebar();
      });
    });

    document.addEventListener('keydown', function (e) {
      if (e.key === 'Escape') closeSidebar();
    });

    if (collapseBtn) {
      collapseBtn.addEventListener('click', function () {
        portal.classList.toggle('sidebar-collapsed');
        var collapsed = portal.classList.contains('sidebar-collapsed');
        localStorage.setItem('ecews-sidebar-collapsed', collapsed ? '1' : '0');
        collapseBtn.setAttribute('aria-label', collapsed ? 'Expand sidebar' : 'Collapse sidebar');
        collapseBtn.title = collapsed ? 'Expand sidebar' : 'Collapse sidebar';
      });
      if (portal.classList.contains('sidebar-collapsed')) {
        collapseBtn.setAttribute('aria-label', 'Expand sidebar');
        collapseBtn.title = 'Expand sidebar';
      }
    }
  }

  /* ── Theme (light / dark) ── */
  function applyTheme(theme) {
    var dark = theme === 'dark';
    document.documentElement.classList.toggle('theme-dark', dark);
    var ic = $('#themeIc');
    var btn = $('#themeBtn');
    if (ic) ic.className = dark ? 'ti ti-sun' : 'ti ti-moon';
    if (btn) {
      btn.setAttribute('aria-label', dark ? 'Switch to light theme' : 'Switch to dark theme');
      btn.title = dark ? 'Light theme' : 'Dark theme';
    }
  }

  function toggleTheme() {
    var next = document.documentElement.classList.contains('theme-dark') ? 'light' : 'dark';
    localStorage.setItem('ecews-theme', next);
    applyTheme(next);
    showToast(next === 'dark' ? 'Dark theme enabled' : 'Light theme enabled', next === 'dark' ? 'ti-moon' : 'ti-sun');
  }

  /* ── Bind controls ── */
  function initControls() {
    var layoutBtn = $('#layoutBtn');
    if (layoutBtn) layoutBtn.addEventListener('click', toggleLayout);

    var themeBtn = $('#themeBtn');
    if (themeBtn) themeBtn.addEventListener('click', toggleTheme);

    var savedTheme = localStorage.getItem('ecews-theme') || 'light';
    applyTheme(savedTheme);
  }

  /* ── Init ── */
  document.addEventListener('DOMContentLoaded', function () {
    initLoader();
    initPrefs();
    initReveal();
    initCounters();
    initBackToTop();
    initAnnouncement();
    initNotifications();
    initNavFilter();
    initAppCards();
    initCommandPalette();
    initHeroCarousel();
    initUserMenu();
    initControls();
    initSidebar();
    filterApps('');

    initWeatherControls();
    updateWeather();
    updateSystemStatus();
    window.setInterval(updateWeather, 10 * 60 * 1000);
    window.setInterval(updateSystemStatus, 60 * 1000);
  });
})();
