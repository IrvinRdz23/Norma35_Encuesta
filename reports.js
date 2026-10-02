(() => {
  const $ = s => document.querySelector(s);
  const fmt = n => Number(n).toLocaleString('es-MX');
  const charts = {};

  const ORDER = ['Siempre', 'Casi siempre', 'Algunas veces', 'Casi nunca', 'Nunca', 'Sí', 'No'];
  const LABEL_COLORS = { 'Siempre': '#0b2a4a', 'Casi siempre': '#1f5f99', 'Algunas veces': '#6aa3d5', 'Casi nunca': '#b5d0ea', 'Nunca': '#e1ebf5', 'Sí': '#d9731a', 'No': '#8fa3b5' };
  const RISK_COLORS = { 'Nulo': '#2e9e6e', 'Bajo': '#8fc96a', 'Medio': '#f2c94c', 'Alto': '#f08a3c', 'Muy alto': '#d64541' };
  const BLUES = ['#0b2a4a', '#0a5a9c', '#3c8bc9', '#7db3e0', '#b5d0ea', '#8fa3b5', '#5d6f82'];

  Chart.defaults.font.family = '"Segoe UI", system-ui, sans-serif';
  Chart.defaults.color = '#33475b';

  function draw(id, cfg) {
    charts[id]?.destroy();
    charts[id] = new Chart(document.getElementById(id), cfg);
  }

  function params() {
    const p = new URLSearchParams();
    const a = $('#fAnexo').value, d = $('#fDesde').value, h = $('#fHasta').value;
    if (a) p.set('anexo', a);
    if (d) p.set('desde', d);
    if (h) p.set('hasta', h);
    return p.toString();
  }

  async function load() {
    const qs = params();
    $('#btnExport').href = '/Reports/Export' + (qs ? '?' + qs : '');
    $('#status').textContent = 'Cargando datos…';
    try {
      const res = await fetch('/Reports/Data' + (qs ? '?' + qs : ''));
      if (!res.ok) throw new Error();
      render(await res.json());
      $('#status').textContent = '';
    } catch {
      $('#status').textContent = 'No se pudieron cargar los datos. Verifique la conexión a la base de datos.';
    }
  }

  function render(d) {
    $('#kEmpleados').textContent = fmt(d.totales.empleados);
    $('#kCobertura').textContent = d.cobertura.toFixed(1) + '%';
    $('#kRespuestas').textContent = fmt(d.totales.respuestas);
    $('#kUltima').textContent = d.totales.ultima
      ? new Date(d.totales.ultima).toLocaleString('es-MX', { dateStyle: 'short', timeStyle: 'short' }) : '—';

    // Cobertura por área (top 12)
    const areas = d.porArea.slice(0, 12);
    draw('chArea', {
      type: 'bar',
      data: {
        labels: areas.map(a => a.etiqueta),
        datasets: [
          { label: 'Plantilla', data: areas.map(a => a.plantilla), backgroundColor: '#c9d7e6' },
          { label: 'Encuestados', data: areas.map(a => a.encuestados), backgroundColor: '#0a5a9c' }
        ]
      },
      options: {
        indexAxis: 'y', maintainAspectRatio: false,
        plugins: { tooltip: { callbacks: { afterBody: ctx => `Cobertura: ${areas[ctx[0].dataIndex].pct}%` } } },
        scales: { x: { beginAtZero: true, grid: { color: '#eef2f6' } }, y: { grid: { display: false } } }
      }
    });

    // Turno
    draw('chTurno', {
      type: 'doughnut',
      data: { labels: d.porTurno.map(t => t.etiqueta), datasets: [{ data: d.porTurno.map(t => t.encuestados), backgroundColor: BLUES, borderWidth: 2 }] },
      options: { maintainAspectRatio: false, cutout: '58%', plugins: { legend: { position: 'bottom' } } }
    });

    // Por día
    draw('chDia', {
      type: 'line',
      data: {
        labels: d.porDia.map(x => new Date(x.dia).toLocaleDateString('es-MX', { day: '2-digit', month: 'short' })),
        datasets: [{ label: 'Empleados', data: d.porDia.map(x => x.valor), borderColor: '#0a5a9c', backgroundColor: 'rgba(10,90,156,.12)', fill: true, tension: .25, pointRadius: 3 }]
      },
      options: { maintainAspectRatio: false, plugins: { legend: { display: false } }, scales: { y: { beginAtZero: true, ticks: { precision: 0 }, grid: { color: '#eef2f6' } }, x: { grid: { display: false } } } }
    });

    // Distribución global
    const dist = d.distribucion;
    draw('chDist', {
      type: 'doughnut',
      data: { labels: dist.map(x => x.etiqueta), datasets: [{ data: dist.map(x => x.valor), backgroundColor: dist.map(x => LABEL_COLORS[x.etiqueta] || '#999'), borderWidth: 2 }] },
      options: { maintainAspectRatio: false, cutout: '58%', plugins: { legend: { position: 'bottom' } } }
    });

    // Riesgo (Anexo III)
    $('#riesgoCol').hidden = !d.riesgo.length;
    if (d.riesgo.length) {
      draw('chRiesgo', {
        type: 'bar',
        data: { labels: d.riesgo.map(x => x.etiqueta), datasets: [{ data: d.riesgo.map(x => x.valor), backgroundColor: d.riesgo.map(x => RISK_COLORS[x.etiqueta]) }] },
        options: { maintainAspectRatio: false, plugins: { legend: { display: false } }, scales: { y: { beginAtZero: true, ticks: { precision: 0 }, grid: { color: '#eef2f6' } }, x: { grid: { display: false } } } }
      });
    }

    // Anexo I
    $('#anexoICol').hidden = !d.anexoI;
    if (d.anexoI) {
      $('#anexoISummary').innerHTML =
        `<span class="big">${fmt(d.anexoI.conEvento)}</span><span class="of">de ${fmt(d.anexoI.total)} trabajadores (${d.anexoI.pct}%) reportaron al menos un acontecimiento traumático severo</span>`;
    }

    // Perfil por pregunta (sólo cuando hay un anexo seleccionado)
    const qs = $('#fAnexo').value ? d.preguntas : [];
    $('#preguntasCol').hidden = qs.length === 0;
    if (qs.length) {
      const present = ORDER.filter(l => qs.some(q => q.conteos[l]));
      $('#qWrap').style.height = Math.max(240, qs.length * 24 + 90) + 'px';
      draw('chPreg', {
        type: 'bar',
        data: {
          labels: qs.map(q => 'P' + q.numero),
          datasets: present.map(l => ({
            label: l,
            backgroundColor: LABEL_COLORS[l],
            data: qs.map(q => {
              const t = Object.values(q.conteos).reduce((a, b) => a + b, 0);
              return t ? +(100 * (q.conteos[l] || 0) / t).toFixed(1) : 0;
            })
          }))
        },
        options: {
          indexAxis: 'y', maintainAspectRatio: false,
          plugins: { tooltip: { callbacks: {
            title: ctx => { const q = qs[ctx[0].dataIndex]; return `P${q.numero}${q.texto ? ' · ' + (q.texto.length > 90 ? q.texto.slice(0, 90) + '…' : q.texto) : ''}`; },
            label: ctx => `${ctx.dataset.label}: ${ctx.parsed.x}%`
          } } },
          scales: { x: { stacked: true, max: 100, ticks: { callback: v => v + '%' }, grid: { color: '#eef2f6' } }, y: { stacked: true, grid: { display: false } } }
        }
      });
    }
  }

  document.querySelectorAll('.dl').forEach(b => b.addEventListener('click', () => {
    const c = charts[b.dataset.chart];
    if (!c) return;
    const a = document.createElement('a');
    a.href = c.toBase64Image('image/png', 1);
    a.download = b.dataset.chart + '.png';
    a.click();
  }));

  $('#btnApply').addEventListener('click', load);
  $('#fAnexo').addEventListener('change', load);
  load();
})();
