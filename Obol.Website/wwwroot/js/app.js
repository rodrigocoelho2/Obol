/* Obol — the page script of the signed-in app: the chart cursor, finding and sorting holdings, the Sync screen and the
   Settings forms. No framework; every block only runs when its markup is on the page. */
'use strict';

(function () {
  const token = document.querySelector('meta[name="request-verification-token"]')?.content ?? '';

  /* ----- Talking to the server ----- */
  async function post(url, body) {
    const headers = { RequestVerificationToken: token };
    let payload = body;
    if (body && !(body instanceof FormData) && !(body instanceof URLSearchParams)) {
      headers['Content-Type'] = 'application/json';
      payload = JSON.stringify(body);
    }
    try {
      const response = await fetch(url, { method: 'POST', headers, body: payload, credentials: 'same-origin' });
      if (response.status === 401 || response.redirected) { location.href = '/sign-in?returnUrl=' + encodeURIComponent(location.pathname); return null; }
      if (!response.ok) return { isValid: false, validationErrors: [{ field: '', message: 'Something went wrong (' + response.status + '). Try again.' }] };
      return await response.json();
    } catch {
      return { isValid: false, validationErrors: [{ field: '', message: 'No connection to Obol. Check the network and try again.' }] };
    }
  }

  function errors(result) {
    return (result?.validationErrors ?? []).map(error => error.message).filter(Boolean).join(' ') || 'Something went wrong. Try again.';
  }

  function show(element, text, ok) {
    if (!element) return;
    element.hidden = !text;
    element.textContent = text ?? '';
    element.classList.toggle('err', ok === false);
    element.classList.toggle('ok', ok === true);
  }

  /* A destructive button asks twice instead of opening a dialog: the first tap arms it for a few seconds. */
  function armed(button, question) {
    if (button.dataset.armed === 'true') return true;
    const label = button.innerHTML;
    button.dataset.armed = 'true';
    button.classList.add('armed');
    button.textContent = question;
    setTimeout(() => { button.dataset.armed = 'false'; button.classList.remove('armed'); button.innerHTML = label; }, 4000);
    return false;
  }

  function busy(button, on) {
    if (!button) return;
    button.disabled = on;
    button.classList.toggle('busy', on);
  }

  /* ----- Chart cursor ----- */
  document.querySelectorAll('[data-chart]').forEach(chart => {
    let points;
    try { points = JSON.parse(chart.dataset.points || '[]'); } catch { points = []; }
    if (points.length < 2) return;
    const cross = chart.querySelector('.cross');
    const tip = chart.querySelector('.tip');
    const knob = chart.querySelector('.knob');
    const svg = chart.querySelector('svg');
    const last = points[points.length - 1];
    let shown = -1;

    function place(index) {
      if (index === shown) return;
      shown = index;
      const [x, y, label, value, note] = points[index];
      const box = svg.getBoundingClientRect();
      const top = y / 100 * box.height;
      cross.style.left = x + '%';
      cross.style.height = box.height + 'px';
      tip.querySelector('small').textContent = label ?? '';
      tip.querySelector('b').textContent = value ?? '';
      const extra = tip.querySelector('i');
      extra.textContent = note ?? '';
      extra.hidden = !note;
      // Keep the tip inside the card near the edges.
      const width = tip.offsetWidth || 120;
      const left = Math.min(Math.max(x / 100 * box.width, width / 2 + 4), box.width - width / 2 - 4);
      tip.style.left = left + 'px';
      tip.style.top = Math.max(top, 0) + 'px';
      if (knob) { knob.style.left = x + '%'; knob.style.top = (top / box.height * 100) + '%'; }
    }

    function nearest(clientX) {
      const box = svg.getBoundingClientRect();
      const x = (clientX - box.left) / box.width * 100;
      let best = 0;
      for (let i = 1; i < points.length; i++) if (Math.abs(points[i][0] - x) < Math.abs(points[best][0] - x)) best = i;
      return best;
    }

    function leave() {
      chart.classList.remove('live');
      shown = -1;
      if (knob) { knob.style.left = last[0] + '%'; knob.style.top = last[1] + '%'; }
    }

    chart.addEventListener('pointermove', event => { chart.classList.add('live'); place(nearest(event.clientX)); });
    chart.addEventListener('pointerdown', event => { chart.classList.add('live'); place(nearest(event.clientX)); });
    chart.addEventListener('pointerleave', leave);
    chart.addEventListener('pointercancel', leave);
    chart.addEventListener('pointerup', event => { if (event.pointerType !== 'mouse') setTimeout(leave, 1600); });
  });

  /* ----- Holdings: filter, find and sort ----- */
  const lists = [...document.querySelectorAll('[data-rows]')];
  if (lists.length > 0) {
    const finds = [...document.querySelectorAll('[data-find]')];
    const filters = [...document.querySelectorAll('[data-filter]')];
    const sortButton = document.querySelector('[data-sort]');
    const none = document.querySelector('[data-none]');
    const sorts = [
      { key: 'value', label: 'by value' },
      { key: 'return', label: 'by return' },
      { key: 'name', label: 'by name' },
    ];
    let group = '';
    let text = '';
    let sort = 0;

    function apply() {
      let visible = 0;
      lists.forEach(list => {
        const rows = [...list.querySelectorAll('[data-row]')];
        const parent = rows[0]?.parentElement;
        let count = 0;
        rows.forEach(row => {
          const on = (!group || row.dataset.group === group) && (!text || (row.dataset.name ?? '').includes(text));
          row.hidden = !on;
          if (on) count++;
        });
        visible = Math.max(visible, count);
        const key = sorts[sort].key;
        rows.sort((a, b) => key === 'name'
          ? (a.dataset.name ?? '').localeCompare(b.dataset.name ?? '')
          : parseFloat(b.dataset[key] ?? '0') - parseFloat(a.dataset[key] ?? '0'));
        rows.forEach(row => parent?.appendChild(row));
      });
      if (none) none.hidden = visible > 0;
    }

    filters.forEach(button => button.addEventListener('click', () => {
      group = button.dataset.filter ?? '';
      filters.forEach(other => other.setAttribute('aria-pressed', other === button ? 'true' : 'false'));
      apply();
    }));

    finds.forEach(input => input.addEventListener('input', () => {
      text = input.value.trim().toLowerCase();
      finds.forEach(other => { if (other !== input) other.value = input.value; });
      apply();
    }));

    document.querySelector('[data-find-open]')?.addEventListener('click', () => {
      const input = finds.find(element => element.classList.contains('phone-only'));
      if (!input) return;
      input.hidden = !input.hidden;
      if (!input.hidden) input.focus();
      else if (text) { input.value = ''; text = ''; apply(); }
    });

    sortButton?.addEventListener('click', () => {
      sort = (sort + 1) % sorts.length;
      sortButton.dataset.sort = sorts[sort].key;
      sortButton.setAttribute('aria-label', 'Sort: ' + sorts[sort].label);
      sortButton.title = 'Sorted ' + sorts[sort].label;
      apply();
    });
  }

  /* Table rows open their page; the link inside keeps working for the keyboard and middle-click. */
  document.querySelectorAll('[data-href]').forEach(row => row.addEventListener('click', event => {
    if (event.target.closest('a, button, input')) return;
    if (event.metaKey || event.ctrlKey) window.open(row.dataset.href, '_blank');
    else location.href = row.dataset.href;
  }));

  /* ----- Overview holdings: the Trading 212 pies, or one line per stock (the choice is remembered) ----- */
  const hviews = document.querySelector('[data-hviews]');
  if (hviews) {
    const buttons = [...hviews.querySelectorAll('[data-hview]')];
    const show = view => {
      buttons.forEach(button => button.setAttribute('aria-pressed', String(button.dataset.hview === view)));
      hviews.querySelectorAll('[data-hpane]').forEach(pane => { pane.hidden = pane.dataset.hpane !== view; });
    };
    let saved = null;
    try { saved = localStorage.getItem('obol.holdings-view'); } catch { /* private mode */ }
    if (buttons.length && buttons.some(button => button.dataset.hview === saved)) show(saved);
    buttons.forEach(button => button.addEventListener('click', () => {
      show(button.dataset.hview);
      try { localStorage.setItem('obol.holdings-view', button.dataset.hview); } catch { /* private mode */ }
    }));

    /* A pie opens to its holdings. */
    hviews.querySelectorAll('.pie-row[data-pie]').forEach(row => {
      const toggle = () => {
        const open = row.getAttribute('aria-expanded') !== 'true';
        row.setAttribute('aria-expanded', String(open));
        const pane = row.closest('[data-hpane]') || hviews;
        pane.querySelectorAll(`[data-pie-of="${row.dataset.pie}"]`).forEach(child => { child.hidden = !open; });
      };
      row.addEventListener('click', event => { if (!event.target.closest('a')) toggle(); });
      row.addEventListener('keydown', event => {
        if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); toggle(); }
      });
    });
  }

  /* ----- Sync ----- */
  const connectForm = document.querySelector('[data-connect-form]');
  if (connectForm) {
    const error = connectForm.querySelector('[data-connect-error]');
    connectForm.addEventListener('submit', async event => {
      event.preventDefault();
      const button = connectForm.querySelector('button[type=submit]');
      busy(button, true);
      show(error, '');
      const result = await post('/sync/connect', new URLSearchParams(new FormData(connectForm)));
      busy(button, false);
      if (!result) return;
      if (result.isValid) location.reload();
      else show(error, errors(result), false);
    });
  }

  document.querySelector('[data-show-connect]')?.addEventListener('click', () => {
    const card = document.querySelector('[data-connect-card]');
    if (!card) return;
    card.hidden = false;
    card.scrollIntoView({ behavior: 'smooth', block: 'start' });
    card.querySelector('input')?.focus({ preventScroll: true });
  });

  document.querySelector('[data-sync-now]')?.addEventListener('click', async event => {
    const button = event.currentTarget;
    busy(button, true);
    const result = await post('/sync/now');
    if (!result) return;
    if (result.isValid) { location.reload(); return; }
    busy(button, false);
    flash(errors(result));
  });

  document.querySelector('[data-disconnect]')?.addEventListener('click', async event => {
    const button = event.currentTarget;
    if (!armed(button, 'Tap again to disconnect')) return;
    busy(button, true);
    const result = await post('/sync/disconnect');
    if (result?.isValid) location.reload(); else { busy(button, false); if (result) flash(errors(result)); }
  });

  document.querySelector('[data-delete-data]')?.addEventListener('click', async event => {
    const button = event.currentTarget;
    if (!armed(button, 'Tap again to delete everything')) return;
    busy(button, true);
    const result = await post('/sync/delete-data');
    if (result?.isValid) location.reload(); else { busy(button, false); if (result) flash(errors(result)); }
  });

  document.querySelectorAll('[data-option]').forEach(toggle => toggle.addEventListener('click', async () => {
    const all = [...document.querySelectorAll('[data-option]')];
    const was = toggle.getAttribute('aria-checked') === 'true';
    toggle.setAttribute('aria-checked', was ? 'false' : 'true');
    const body = {};
    all.forEach(item => { body[item.dataset.option] = item.getAttribute('aria-checked') === 'true'; });
    const result = await post('/sync/options', body);
    if (!result?.isValid) { toggle.setAttribute('aria-checked', was ? 'true' : 'false'); if (result) flash(errors(result)); }
  }));

  /* While a sync runs, the page asks how it is going and reloads when it ends. */
  const connection = document.querySelector('[data-connection]');
  if (connection?.dataset.running === 'true') {
    const poll = async () => {
      try {
        const response = await fetch('/sync/status', { credentials: 'same-origin', headers: { Accept: 'application/json' } });
        const result = await response.json();
        if (result?.isValid && result.data && result.data.state !== 'Running') { location.reload(); return; }
      } catch { /* try again */ }
      setTimeout(poll, 4000);
    };
    setTimeout(poll, 4000);
  }

  /* CSV import: pick or drop the file. */
  const csv = document.querySelector('[data-csv]');
  if (csv) {
    const drop = document.querySelector('[data-drop]');
    const output = document.querySelector('[data-csv-result]');

    async function upload(file) {
      if (!file) return;
      const data = new FormData();
      data.append('file', file);
      drop.classList.add('busy');
      output.hidden = false;
      output.className = 'msg';
      output.textContent = 'Reading ' + file.name + '…';
      const result = await post('/sync/csv', data);
      drop.classList.remove('busy');
      csv.value = '';
      if (!result) return;
      if (!result.isValid) { output.className = 'msg err'; output.textContent = errors(result); return; }
      const summary = result.data ?? {};
      output.className = 'msg ok';
      output.replaceChildren();
      const head = document.createElement('b');
      head.textContent = summary.summary || 'Imported.';
      output.appendChild(head);
      if (summary.lines?.length) {
        const list = document.createElement('ul');
        list.style.margin = '6px 0 0 18px';
        summary.lines.forEach(line => { const item = document.createElement('li'); item.textContent = line; list.appendChild(item); });
        output.appendChild(list);
      }
      const again = document.createElement('a');
      again.href = '/';
      again.textContent = 'See the overview →';
      again.style.display = 'inline-block';
      again.style.marginTop = '8px';
      output.appendChild(again);
    }

    csv.addEventListener('change', () => upload(csv.files?.[0]));
    ['dragenter', 'dragover'].forEach(name => drop.addEventListener(name, event => { event.preventDefault(); drop.classList.add('over'); }));
    ['dragleave', 'drop'].forEach(name => drop.addEventListener(name, event => { event.preventDefault(); drop.classList.remove('over'); }));
    drop.addEventListener('drop', event => upload(event.dataTransfer?.files?.[0]));
  }

  /* ----- Settings ----- */
  const passwordForm = document.querySelector('[data-password-form]');
  if (passwordForm) {
    const output = passwordForm.querySelector('[data-password-result]');
    passwordForm.addEventListener('submit', async event => {
      event.preventDefault();
      const button = passwordForm.querySelector('button[type=submit]');
      busy(button, true);
      const result = await post('/settings/password', new URLSearchParams(new FormData(passwordForm)));
      busy(button, false);
      if (!result) return;
      if (result.isValid) { passwordForm.reset(); output.textContent = 'Changed. Other devices were signed out.'; output.style.color = 'var(--lime)'; }
      else { output.textContent = errors(result); output.style.color = 'var(--loss)'; }
    });
  }

  document.querySelectorAll('[data-delete-user]').forEach(button => button.addEventListener('click', async () => {
    const id = button.dataset.deleteUser ?? '';
    if (!armed(button, 'Tap again to delete ' + (button.dataset.name ?? ''))) return;
    busy(button, true);
    const result = await post('/settings/delete-user', new URLSearchParams({ id }));
    if (!result) return;
    if (!result.isValid) { busy(button, false); flash(errors(result)); return; }
    if (!id) location.href = '/about'; else location.reload();
  }));

  /* ----- A short message at the bottom of the screen ----- */
  function flash(text) {
    let toast = document.querySelector('.toast');
    if (!toast) { toast = document.createElement('div'); toast.className = 'toast'; toast.setAttribute('role', 'status'); document.body.appendChild(toast); }
    toast.textContent = text;
    toast.classList.add('on');
    clearTimeout(flash.timer);
    flash.timer = setTimeout(() => toast.classList.remove('on'), 5000);
  }

  /* ----- Insights › Goals: the projection follows the sliders ----- */
  const goals = document.querySelector('[data-goals]');
  if (goals) {
    const value = parseFloat(goals.dataset.value) || 0;
    const income = parseFloat(goals.dataset.income) || 0;
    const thisYear = parseInt(goals.dataset.year, 10) || new Date().getFullYear();
    let scenarios = [];
    try { scenarios = JSON.parse(goals.dataset.scenarios || '[]'); } catch { scenarios = []; }
    const rate = key => (scenarios.find(item => item.key === key) || { rate: key === 'low' ? 0.04 : key === 'high' ? 0.1 : 0.07 }).rate;
    const money = new Intl.NumberFormat('en-IE', { style: 'currency', currency: goals.dataset.currency || 'EUR', maximumFractionDigits: 0 });
    const inputs = {};
    goals.querySelectorAll('[data-in]').forEach(input => { inputs[input.dataset.in] = input; });
    const out = name => goals.querySelector(`[data-out="${name}"]`);
    const show = name => goals.querySelector(`[data-show="${name}"]`);
    const path = name => goals.querySelector(`[data-path="${name}"]`);

    function grow(months, monthly, yearly) {
      const step = Math.pow(1 + yearly, 1 / 12) - 1;
      const values = [value];
      let v = value;
      for (let i = 1; i <= months; i++) { v = v * (1 + step) + monthly; values.push(v); }
      return values;
    }

    function line(values, x, y) {
      return values.map((v, i) => `${i ? 'L' : 'M'}${x(i).toFixed(1)},${y(v).toFixed(1)}`).join(' ');
    }

    function fill(input) {
      const min = parseFloat(input.min), max = parseFloat(input.max), now = parseFloat(input.value);
      input.style.setProperty('--fill', ((now - min) / (max - min) * 100) + '%');
    }

    function draw() {
      const monthly = parseFloat(inputs.monthly.value) || 0;
      const years = parseInt(inputs.years.value, 10) || 1;
      const goal = parseFloat(inputs.goal.value) || 0;
      const yieldRate = (parseFloat(inputs.yield.value) || 0) / 100;
      Object.values(inputs).forEach(fill);
      show('monthly').textContent = money.format(monthly);
      show('years').textContent = years + (years === 1 ? ' year' : ' years');
      show('goal').textContent = money.format(goal);
      show('yield').textContent = (yieldRate * 100).toFixed(1) + ' %';

      const months = years * 12;
      const low = grow(months, monthly, rate('low'));
      const mid = grow(months, monthly, rate('mid'));
      const high = grow(months, monthly, rate('high'));
      const put = Array.from({ length: months + 1 }, (_, i) => value + monthly * i);
      const top = Math.max(high[months], put[months], 1) * 1.06;
      const x = i => i / months * 1000;
      const y = v => 250 - v / top * 238;
      path('low').setAttribute('d', line(low, x, y));
      path('mid').setAttribute('d', line(mid, x, y));
      path('high').setAttribute('d', line(high, x, y));
      path('in').setAttribute('d', line(put, x, y));
      path('band').setAttribute('d', line(high, x, y) + ' ' + low.slice().reverse().map((v, i) => `L${x(months - i).toFixed(1)},${y(v).toFixed(1)}`).join(' ') + ' Z');

      out('years').textContent = years;
      out('mid').textContent = money.format(mid[months]);
      out('low').textContent = money.format(low[months]);
      out('high').textContent = money.format(high[months]);
      out('in').textContent = money.format(put[months]);

      const axis = goals.querySelector('[data-axis]');
      const marks = [0, 0.25, 0.5, 0.75, 1].map(share => thisYear + Math.round(years * share));
      axis.replaceChildren(...marks.map(year => { const span = document.createElement('span'); span.textContent = year; return span; }));

      // The income goal: how far today's income gets, and when the steady path gets there.
      const nowMonthly = income / 12;
      const share = goal > 0 ? Math.min(1, nowMonthly / goal) : 0;
      const ring = goals.querySelector('[data-ring]');
      ring.setAttribute('stroke-dasharray', `${(share * 276.46).toFixed(1)} 276.46`);
      out('goal-pct').textContent = Math.round(share * 100) + ' %';
      out('goal-label').textContent = money.format(goal) + ' a month';
      const text = out('goal-text');
      const far = grow(60 * 12, monthly, rate('mid'));
      const reach = far.findIndex(v => v * yieldRate / 12 >= goal);
      text.replaceChildren();
      if (goal <= 0) {
        text.textContent = 'Set an income you would like a month.';
      } else if (reach === 0) {
        text.append('Your portfolio already yields ', bold(money.format(value * yieldRate / 12)), ' a month at this yield.');
      } else if (reach > 0) {
        const when = thisYear + Math.ceil(reach / 12);
        text.append('At a steady 7 % and ', bold(money.format(monthly)), ' a month, you reach ', bold(money.format(goal)), ' a month around ', bold(String(when)), ` (${Math.ceil(reach / 12)} years).`);
      } else {
        text.textContent = 'Not within 60 years at this pace: add a little more each month or aim lower.';
      }
    }

    function bold(textValue) { const b = document.createElement('b'); b.textContent = textValue; return b; }

    Object.values(inputs).forEach(input => input.addEventListener('input', draw));
    draw();
  }

  /* ----- Insights › Coach: ask for a review and follow it ----- */
  const coach = document.querySelector('[data-coach]');
  if (coach) {
    const run = coach.querySelector('[data-coach-run]');
    const follow = async () => {
      try {
        const response = await fetch('/insights/coach/status', { credentials: 'same-origin', headers: { Accept: 'application/json' } });
        const result = await response.json();
        if (result?.isValid && result.data && !result.data.running) { location.reload(); return; }
      } catch { /* try again */ }
      setTimeout(follow, 3000);
    };
    run?.addEventListener('click', async () => {
      busy(run, true);
      run.lastChild.textContent = 'Reading your portfolio…';
      const result = await post('/insights/coach/run');
      if (!result) return;
      if (!result.isValid) { busy(run, false); flash(errors(result)); return; }
      if (result.data?.running) setTimeout(follow, 2500); else location.reload();
    });
    if (coach.dataset.running === 'true') setTimeout(follow, 2500);
  }

  /* ----- Installable app ----- */
  if ('serviceWorker' in navigator) {
    window.addEventListener('load', () => navigator.serviceWorker.register('/sw.js').catch(() => { /* not installable here */ }));
  }
})();
