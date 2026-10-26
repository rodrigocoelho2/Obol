/* Obol — the landing page: the coin follows the pointer, the net worth counts up, the sections animate as they
   scroll in. Everything degrades to a still page with reduced motion or without JavaScript. */
'use strict';

(function () {
  const page = document.querySelector('[data-landing]');
  if (!page) return;
  const still = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  /* Sections animate the first time they scroll into view. */
  const sections = [...page.querySelectorAll('[data-reveal]')];
  sections.forEach((section, index) => section.style.setProperty('--stagger', (index % 4) * 90 + 'ms'));
  if (still || !('IntersectionObserver' in window)) {
    sections.forEach(section => section.classList.add('in'));
  } else {
    const observer = new IntersectionObserver(entries => {
      entries.forEach(entry => {
        if (entry.isIntersecting) {
          entry.target.classList.add('in');
          observer.unobserve(entry.target);
        }
      });
    }, { threshold: 0.25 });
    sections.forEach(section => observer.observe(section));
  }

  /* The net worth counts up to its value. */
  const counter = page.querySelector('[data-count]');
  if (counter && !still) {
    const target = parseFloat(counter.dataset.count) || 0;
    const money = new Intl.NumberFormat('en-IE', { style: 'currency', currency: 'EUR', minimumFractionDigits: 2, maximumFractionDigits: 2 });
    const render = value => {
      const text = money.format(value);
      const cut = text.lastIndexOf('.');
      counter.replaceChildren(text.slice(0, cut), Object.assign(document.createElement('span'), { className: 'c', textContent: text.slice(cut) }));
    };
    render(0);
    const start = performance.now() + 1200;
    const duration = 1800;
    const step = now => {
      const t = Math.min(1, Math.max(0, (now - start) / duration));
      const eased = 1 - Math.pow(1 - t, 4);
      render(target * eased);
      if (t < 1) requestAnimationFrame(step);
    };
    requestAnimationFrame(step);
  }

  /* The coin leans towards the pointer, and the glow follows it (computers only). */
  const stage = page.querySelector('[data-stage]');
  const tilt = page.querySelector('[data-tilt]');
  const glow = page.querySelector('.mint-glow');
  if (stage && tilt && !still && window.matchMedia('(pointer: fine)').matches) {
    let frame = 0;
    window.addEventListener('pointermove', event => {
      if (frame) return;
      frame = requestAnimationFrame(() => {
        frame = 0;
        const box = stage.getBoundingClientRect();
        const x = (event.clientX - (box.left + box.width / 2)) / window.innerWidth;
        const y = (event.clientY - (box.top + box.height / 2)) / window.innerHeight;
        tilt.style.setProperty('--ry', (-10 + x * 26).toFixed(2) + 'deg');
        tilt.style.setProperty('--rx', (8 - y * 20).toFixed(2) + 'deg');
        if (glow) {
          glow.style.setProperty('--gx', (x * 60).toFixed(1) + 'px');
          glow.style.setProperty('--gy', (y * 60).toFixed(1) + 'px');
        }
      });
    }, { passive: true });
  }
})();
