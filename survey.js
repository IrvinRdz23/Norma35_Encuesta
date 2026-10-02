(() => {
  const form = document.getElementById('surveyForm');
  if (!form) return;

  const YES = +form.dataset.yes;
  const $ = (s, r = document) => r.querySelector(s);
  const $$ = (s, r = document) => [...r.querySelectorAll(s)];

  const numero = $('#NumeroReloj');
  const btnFind = $('#btnBuscar');
  const msg = $('#lookupMsg');
  const card = $('#employeeData');
  const submit = $('#btnSubmit');
  const bar = $('#progressBar');
  const progressText = $('#progressText');

  let found = !card.classList.contains('d-none');
  let foundFor = found ? numero.value.trim() : '';
  if (found) numero.classList.add('is-found');

  const valueOf = id => {
    const r = form.querySelector(`input[name="Answers[${id}]"]:checked`);
    return r ? +r.value : null;
  };

  const isEnabled = item => {
    const req = item.dataset.requires;
    if (!req) return true;
    const [mode, ids] = req.split(':');
    const list = ids.split(',').map(Number);
    return mode === 'yes' ? valueOf(list[0]) === YES : list.some(i => valueOf(i) === YES);
  };

  function refresh() {
    let total = 0, done = 0;
    $$('.q-item').forEach(item => {
      const on = isEnabled(item);
      item.classList.toggle('is-disabled', !on);
      $$('input', item).forEach(i => { i.disabled = !on; if (!on) i.checked = false; });
      if (on) {
        total++;
        if ($('input:checked', item)) { done++; item.classList.remove('missing'); }
      }
    });
    const pct = total ? Math.round(100 * done / total) : 0;
    bar.style.width = pct + '%';
    progressText.textContent = `${done} de ${total} preguntas respondidas`;
    submit.disabled = !found;
  }

  function setMsg(text, kind) {
    msg.textContent = text;
    msg.classList.remove('text-ok', 'text-warn');
    if (kind) msg.classList.add(kind === 'ok' ? 'text-ok' : 'text-warn');
  }

  function setEmployee(d) {
    $$('[data-f]', card).forEach(el => { el.textContent = d ? (d[el.dataset.f] ?? '') : ''; });
    card.classList.toggle('d-none', !d);
  }

  async function lookup() {
    const n = numero.value.trim();
    if (!/^\d{8}$/.test(n)) { setMsg('El número de reloj debe tener 8 dígitos.', 'warn'); return; }
    btnFind.disabled = true;
    setMsg('Buscando…');
    try {
      const res = await fetch(`/Employee/Lookup?numero=${encodeURIComponent(n)}`);
      const d = await res.json();
      found = !!d.found;
      foundFor = found ? n : '';
      numero.classList.toggle('is-found', found);
      numero.classList.toggle('is-missing', !found);
      setEmployee(found ? d : null);
      setMsg(found ? 'Empleado encontrado.' : d.message, found ? 'ok' : 'warn');
    } catch {
      found = false;
      setMsg('No se pudo consultar. Intente de nuevo.', 'warn');
    } finally {
      btnFind.disabled = false;
      refresh();
    }
  }

  btnFind.addEventListener('click', lookup);
  numero.addEventListener('keydown', e => { if (e.key === 'Enter') { e.preventDefault(); lookup(); } });
  numero.addEventListener('input', () => {
    numero.value = numero.value.replace(/\D/g, '').slice(0, 8);
    if (numero.value !== foundFor) {
      found = false;
      numero.classList.remove('is-found', 'is-missing');
      setEmployee(null);
      setMsg('');
      refresh();
    }
    if (/^\d{8}$/.test(numero.value) && numero.value !== foundFor) lookup();
  });

  form.addEventListener('change', e => { if (e.target.matches('input[type=radio]')) refresh(); });

  form.addEventListener('submit', e => {
    if (!found) {
      e.preventDefault();
      setMsg('Busque y confirme su número de reloj antes de terminar.', 'warn');
      numero.focus();
      return;
    }
    const missing = $$('.q-item:not(.is-disabled)').filter(i => !$('input:checked', i));
    if (missing.length) {
      e.preventDefault();
      missing.forEach(i => i.classList.add('missing'));
      missing[0].scrollIntoView({ behavior: 'smooth', block: 'center' });
      progressText.textContent = `Faltan ${missing.length} preguntas por responder.`;
    }
  });

  refresh();
})();
