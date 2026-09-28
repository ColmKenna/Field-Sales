/* ==========================================================================
   Delivery console — renderer
   Generic. Renders entirely from the loaded plan object; contains no project
   vocabulary. Loaded with a classic script tag so it works from file://.
   ========================================================================== */
(function () {
  'use strict';

  var EXPECTED_SCHEMA = 1;
  var COMPLEXITY_ORDER = ['simple', 'moderate', 'complex'];
  var STORE_PREFIX = 'delivery-console:';

  var state = {
    plan: null,
    fileStatuses: {},
    drafts: {},
    storeKey: null,
    tab: 'plan',
    filters: { tier: [], complexity: [], modelTier: [], status: [], q: '' }
  };

  /* ------------------------------------------------------------- helpers */

  function h(tag, props, kids) {
    var node = document.createElement(tag);
    props = props || {};
    Object.keys(props).forEach(function (k) {
      var v = props[k];
      if (v === null || v === undefined || v === false) return;
      if (k === 'text') { node.textContent = String(v); }
      else if (k === 'cls') { node.className = v; }
      else if (k === 'html') { node.innerHTML = v; }
      else { node.setAttribute(k, v === true ? '' : String(v)); }
    });
    (kids || []).forEach(function (kid) {
      if (kid === null || kid === undefined || kid === false) return;
      node.appendChild(typeof kid === 'string' ? document.createTextNode(kid) : kid);
    });
    return node;
  }

  function clear(node) { while (node.firstChild) node.removeChild(node.firstChild); }
  function byId(id) { return document.getElementById(id); }
  function arr(v) { return Array.isArray(v) ? v : []; }
  function slug(v) { return String(v).toLowerCase().replace(/[^a-z0-9]+/g, '-'); }
  function itemDomId(id) { return 'item-' + slug(id); }
  function storyDomId(id) { return 'story-' + slug(id); }

  function tierOf(plan, id) {
    var found = arr(plan.tiers).filter(function (t) { return t.id === id; })[0];
    return found || { id: id, label: id || 'Unassigned', cssVar: '--tier-blocked' };
  }

  /* ------------------------------------------------------------- banners */

  function banner(kind, title, lines, dismissible) {
    var body = [h('h4', { text: title })];
    if (lines && lines.length) {
      body.push(h('ul', {}, lines.map(function (l) { return h('li', { text: l }); })));
    }
    var box = h('div', { cls: 'banner' + (kind === 'note' ? ' banner-note' : '') }, [h('div', {}, body)]);
    if (dismissible !== false) {
      var btn = h('button', { type: 'button', cls: 'btn btn-mono banner-dismiss', text: 'Dismiss' });
      btn.addEventListener('click', function () { box.parentNode.removeChild(box); });
      box.appendChild(btn);
    }
    byId('banners').appendChild(box);
  }

  /* ---------------------------------------------------------- validation */

  function validate(plan) {
    var problems = [];
    var storyIds = {}, itemIds = {}, phaseIds = {};
    arr(plan.stories).forEach(function (s) { storyIds[s.id] = true; });
    arr(plan.workItems).forEach(function (w) { itemIds[w.id] = true; });
    arr(plan.phases).forEach(function (p) { phaseIds[p.id] = true; });

    arr(plan.workItems).forEach(function (w) {
      arr(w.storyRefs).forEach(function (r) {
        if (!storyIds[r]) problems.push(w.id + ' references story ' + r + ', which is not in this plan.');
      });
      arr(w.dependsOn).forEach(function (d) {
        if (!itemIds[d]) problems.push(w.id + ' depends on ' + d + ', which is not in this plan.');
      });
      arr(w.blocks).forEach(function (b) {
        if (!itemIds[b]) problems.push(w.id + ' blocks ' + b + ', which is not in this plan.');
      });
      if (w.phaseId && !phaseIds[w.phaseId]) {
        problems.push(w.id + ' sits in phase ' + w.phaseId + ', which is not in this plan.');
      }
    });
    arr(plan.stories).forEach(function (s) {
      arr(s.implementedBy).forEach(function (r) {
        if (!itemIds[r]) problems.push(s.id + ' says it is implemented by ' + r + ', which is not in this plan.');
      });
    });
    return problems;
  }

  /* --------------------------------------------------------- persistence */

  /* The plan file's workItemStatus map is authoritative. Status changes made in
     the browser are drafts, kept in localStorage until the file catches up. */

  function knownItemIds() {
    var known = {};
    arr(state.plan.workItems).forEach(function (w) { known[w.id] = true; });
    return known;
  }

  function loadStatuses() {
    var known = knownItemIds();
    var file = DeliveryStatus.readFile(state.plan.workItemStatus, known);
    state.fileStatuses = file.statuses;
    state.drafts = {};
    if (file.problems.length) {
      banner('error', 'The plan file\'s workItemStatus has ' + file.problems.length + ' unusable entr' +
        (file.problems.length === 1 ? 'y' : 'ies') + '.', file.problems);
    }
    if (!state.storeKey) return;
    var raw = null;
    try { raw = window.localStorage.getItem(state.storeKey); } catch (e) { return; }
    if (!raw) return;
    var parsed;
    try { parsed = JSON.parse(raw); } catch (e) { return; }
    var r = DeliveryStatus.reconcile(parsed, state.fileStatuses, known);
    state.drafts = r.drafts;
    var notes = [];
    if (r.superseded > 0) notes.push(r.superseded + ' unsaved change' + (r.superseded === 1 ? ' was' : 's were') +
      ' discarded because the plan file has since updated ' + (r.superseded === 1 ? 'that item' : 'those items') + '.');
    if (r.unknown > 0) notes.push(r.unknown + ' unsaved change' + (r.unknown === 1 ? ' was' : 's were') +
      ' discarded because ' + (r.unknown === 1 ? 'it names a work item' : 'they name work items') + ' no longer in this plan.');
    if (notes.length) banner('note', 'Some unsaved status changes no longer apply.', notes);
    saveStatuses();
  }

  function saveStatuses() {
    if (!state.storeKey) return;
    try { window.localStorage.setItem(state.storeKey, JSON.stringify(state.drafts)); } catch (e) { /* storage unavailable */ }
  }

  function statusOf(id) { return DeliveryStatus.effective(state.fileStatuses, state.drafts, id); }
  function isDraftStatus(id) { return DeliveryStatus.isDraft(state.drafts, id); }

  function workItemStatusMap() {
    var ids = arr(state.plan.workItems).map(function (w) { return w.id; });
    return DeliveryStatus.toWorkItemStatus(state.fileStatuses, state.drafts, ids);
  }

  function paintChip(chip, w) {
    var st = statusOf(w.id);
    var draft = isDraftStatus(w.id);
    chip.textContent = st;
    chip.setAttribute('data-status', st);
    chip.classList.toggle('is-draft', draft);
    chip.setAttribute('aria-label', 'Status of ' + w.id + ': ' + st +
      (draft ? ', unsaved change not yet in the plan file' : '') + '. Activate to change.');
    chip.title = draft ? 'Unsaved: the plan file says ' + DeliveryStatus.effective(state.fileStatuses, {}, w.id) : '';
  }

  function paintDraftCount() {
    var el = byId('draft-count');
    if (!el) return;
    var n = Object.keys(state.drafts).length;
    el.hidden = n === 0;
    el.textContent = n + ' unsaved status change' + (n === 1 ? '' : 's');
  }

  /* -------------------------------------------------------------- copying */

  function copyText(text, btn) {
    var label = btn.textContent;
    function done() {
      btn.textContent = 'Copied';
      btn.classList.add('is-done');
      window.setTimeout(function () { btn.textContent = label; btn.classList.remove('is-done'); }, 1600);
    }
    function fallback() {
      var ta = document.createElement('textarea');
      ta.value = text;
      ta.setAttribute('readonly', '');
      ta.style.position = 'fixed';
      ta.style.opacity = '0';
      document.body.appendChild(ta);
      ta.select();
      try { document.execCommand('copy'); done(); } catch (e) { btn.textContent = 'Copy failed'; }
      document.body.removeChild(ta);
    }
    if (navigator.clipboard && navigator.clipboard.writeText) {
      navigator.clipboard.writeText(text).then(done, fallback);
    } else {
      fallback();
    }
  }

  function copyButton(text, label) {
    var btn = h('button', { type: 'button', cls: 'btn-copy', text: label || 'Copy' });
    btn.addEventListener('click', function (ev) { ev.preventDefault(); ev.stopPropagation(); copyText(text, btn); });
    return btn;
  }

  /* ----------------------------------------------------------------- tabs */

  function switchTab(name) {
    state.tab = name;
    ['plan', 'stories', 'contexts', 'decisions', 'architecture'].forEach(function (t) {
      var tab = byId('tab-' + t), panel = byId('panel-' + t);
      var on = t === name;
      tab.setAttribute('aria-selected', on ? 'true' : 'false');
      tab.tabIndex = on ? 0 : -1;
      if (on) panel.removeAttribute('hidden'); else panel.setAttribute('hidden', '');
    });
  }

  function wireTabs() {
    var tabs = [].slice.call(document.querySelectorAll('.tab'));
    tabs.forEach(function (tab, i) {
      tab.addEventListener('click', function () { switchTab(tab.getAttribute('data-tab')); });
      tab.addEventListener('keydown', function (ev) {
        var next = null;
        if (ev.key === 'ArrowRight') next = tabs[(i + 1) % tabs.length];
        if (ev.key === 'ArrowLeft') next = tabs[(i - 1 + tabs.length) % tabs.length];
        if (ev.key === 'Home') next = tabs[0];
        if (ev.key === 'End') next = tabs[tabs.length - 1];
        if (next) { ev.preventDefault(); next.focus(); next.click(); }
      });
    });
  }

  /* ------------------------------------------------------------ deep links */

  function jumpTo(kind, id) {
    switchTab(kind === 'story' ? 'stories' : 'plan');
    var node = byId(kind === 'story' ? storyDomId(id) : itemDomId(id));
    if (!node) return;
    node.open = true;
    node.scrollIntoView({ block: 'start' });
    var summary = node.querySelector('summary');
    if (summary) summary.focus();
  }

  function jumpChip(kind, id, label) {
    var btn = h('button', { type: 'button', cls: 'id-chip', text: label || id, title: 'Go to ' + id });
    btn.addEventListener('click', function (ev) { ev.preventDefault(); ev.stopPropagation(); jumpTo(kind, id); });
    return btn;
  }

  /* ------------------------------------------------------------- filtering */

  function searchBlob(plan, w) {
    var parts = [w.id, w.title, w.rationale, w.complexity, w.confidence, w.testOwnership, w.storyRefLabel];
    parts = parts.concat(arr(w.storyRefs), arr(w.dependsOn), arr(w.blocks));
    if (w.risk) parts.push(w.risk.level, w.risk.reason);
    if (w.guidance) {
      parts = parts.concat(arr(w.guidance.tasks), arr(w.guidance.outOfScope));
      parts.push(w.guidance.checkpoint);
      if (w.guidance.contract) {
        ['services', 'entities', 'endpoints', 'configuration'].forEach(function (k) {
          parts = parts.concat(arr(w.guidance.contract[k]));
        });
      }
    }
    if (w.model) {
      if (w.model.implementation) parts.push(w.model.implementation.tier);
      if (w.model.testWriting) parts.push(w.model.testWriting.tier);
    }
    if (w.git) parts.push(w.git.branch);
    if (w.tests) {
      ['unit', 'integration', 'component', 'worker'].forEach(function (k) {
        arr(w.tests[k]).forEach(function (t) { parts.push(t.name, t.intent); });
      });
    }
    arr(w.storyRefs).forEach(function (sid) {
      var s = arr(plan.stories).filter(function (x) { return x.id === sid; })[0];
      if (s) parts.push(s.id, s.title, s.asA, s.iWant, s.soThat, arr(s.acceptanceCriteria).join(' '));
    });
    return parts.filter(Boolean).join(' ').toLowerCase();
  }

  function matches(w) {
    var f = state.filters;
    if (f.tier.length && f.tier.indexOf(w.tier) === -1) return false;
    if (f.complexity.length && f.complexity.indexOf(w.complexity) === -1) return false;
    if (f.status.length && f.status.indexOf(statusOf(w.id)) === -1) return false;
    if (f.modelTier.length) {
      var mt = w.model && w.model.implementation ? w.model.implementation.tier : null;
      if (!mt || f.modelTier.indexOf(mt) === -1) return false;
    }
    if (f.q && w._blob.indexOf(f.q) === -1) return false;
    return true;
  }

  function applyFilters() {
    var plan = state.plan;
    var shown = 0, total = 0;
    var perPhase = {};

    arr(plan.workItems).forEach(function (w) {
      var card = byId(itemDomId(w.id));
      var tile = document.querySelector('[data-tile="' + w.id + '"]');
      var on = matches(w);
      if (!w.deferred) { total++; if (on) shown++; }
      if (card) { if (on) card.removeAttribute('hidden'); else card.setAttribute('hidden', ''); }
      if (tile && !w.deferred) { tile.classList.toggle('is-dim', !on); }

      var key = w.deferred ? '__deferred' : w.phaseId;
      perPhase[key] = perPhase[key] || { done: 0, all: 0, visible: 0 };
      perPhase[key].all++;
      if (statusOf(w.id) === 'done') perPhase[key].done++;
      if (on) perPhase[key].visible++;
    });

    Object.keys(perPhase).forEach(function (key) {
      var counter = document.querySelector('[data-phase-count="' + key + '"]');
      if (counter) counter.textContent = perPhase[key].done + '/' + perPhase[key].all + ' done';
      var group = document.querySelector('[data-phase-group="' + key + '"]');
      if (group) { if (perPhase[key].visible === 0) group.setAttribute('hidden', ''); else group.removeAttribute('hidden'); }
    });

    var showing = byId('showing-count');
    if (showing) showing.textContent = 'Showing ' + shown + ' of ' + total;

    var empty = byId('filter-empty');
    if (empty) { if (shown === 0) empty.removeAttribute('hidden'); else empty.setAttribute('hidden', ''); }

    var completed = byId('stat-completed');
    if (completed) {
      completed.textContent = String(arr(plan.workItems).filter(function (w) {
        return !w.deferred && statusOf(w.id) === 'done';
      }).length);
    }

    arr(plan.workItems).forEach(function (w) {
      var tile = document.querySelector('[data-tile="' + w.id + '"]');
      if (!tile || w.deferred) return;
      paintTile(tile, w);
    });
  }

  function paintTile(tile, w) {
    var t = tierOf(state.plan, w.tier);
    var st = statusOf(w.id);
    var base = st === 'blocked' ? '--tier-blocked' : t.cssVar;
    var isDone = st === 'done';
    tile.style.background = 'var(' + base + (isDone ? '-done' : '') + ')';
    tile.classList.toggle('is-done', isDone);
    tile.setAttribute('aria-label', w.id + ' — ' + w.title + ' — ' + t.label + ' — ' + st);
    tile.title = w.id + ' — ' + w.title + ' (' + t.label + ', ' + st + ')';
  }

  /* --------------------------------------------------------------- status */

  function cycleStatus(w, chip) {
    var next = DeliveryStatus.next(statusOf(w.id));
    DeliveryStatus.setDraft(state.fileStatuses, state.drafts, w.id, next);
    saveStatuses();
    paintChip(chip, w);
    paintDraftCount();
    var card = byId(itemDomId(w.id));
    if (card) {
      var spine = next === 'blocked' ? '--tier-blocked' : tierOf(state.plan, w.tier).cssVar;
      card.style.borderLeftColor = 'var(' + spine + ')';
    }
    applyFilters();
  }

  /* -------------------------------------------------------- plan header */

  function renderPlanHeader() {
    var plan = state.plan;
    var host = byId('plan-header');
    clear(host);

    var stats = [
      { label: 'WORK ITEMS', value: arr(plan.workItems).filter(function (w) { return !w.deferred; }).length },
      { label: 'USER STORIES', value: arr(plan.stories).length }
    ];
    if (plan.domainMetric && plan.domainMetric.label) {
      stats.push({ label: plan.domainMetric.label, value: plan.domainMetric.value });
    }
    stats.push({ label: 'COMPLETED', value: 0, id: 'stat-completed' });

    var statsGrid = h('div', { cls: 'stats' }, stats.map(function (s) {
      return h('div', { cls: 'stat' }, [
        h('div', { cls: 'stat-value', text: s.value, id: s.id || null }),
        h('div', { cls: 'stat-label', text: s.label })
      ]);
    }));
    if (stats.length === 3) statsGrid.style.gridTemplateColumns = 'repeat(3, 1fr)';

    var legend = h('div', { cls: 'legend' }, arr(plan.tiers).map(function (t) {
      var sw = h('span', { cls: 'legend-swatch' });
      sw.style.background = 'var(' + t.cssVar + ')';
      return h('span', { cls: 'legend-item' }, [sw, t.label]);
    }));

    host.appendChild(h('div', { cls: 'masthead-block' }, [
      h('h1', { cls: 'plan-title', text: plan.title || 'Delivery plan' }),
      plan.subtitle ? h('p', { cls: 'plan-subtitle', text: plan.subtitle }) : null,
      h('div', { cls: 'plan-generated', text: 'Generated ' + (plan.generatedUtc || 'unknown') + (plan.modelTableDate ? ' · model table ' + plan.modelTableDate : '') }),
      statsGrid,
      legend,
      renderMinimap(),
      renderFilters()
    ]));
  }

  function renderMinimap() {
    var plan = state.plan;
    var cols = arr(plan.phases).map(function (p) {
      var items = arr(plan.workItems).filter(function (w) { return w.phaseId === p.id && !w.deferred; });
      return { key: p.id, label: p.shortName || p.name, items: items, deferred: false };
    });
    var deferred = arr(plan.workItems).filter(function (w) { return w.deferred; });
    if (deferred.length) cols.push({ key: '__deferred', label: 'Deferred', items: deferred, deferred: true });

    return h('div', { cls: 'minimap' }, cols.map(function (col) {
      return h('div', { cls: 'minimap-col' + (col.deferred ? ' is-deferred' : '') }, [
        h('div', { cls: 'minimap-head' }, [
          h('span', { text: col.label }),
          h('span', { text: '(' + col.items.length + ')' })
        ]),
        h('div', { cls: 'minimap-tiles' }, col.items.map(function (w) {
          var num = String(w.id).replace(/^\D+/, '');
          if (col.deferred) {
            return h('span', { cls: 'tile is-deferred', text: num, title: w.id + ' — ' + w.title + ' (deferred)' });
          }
          var tile = h('button', { type: 'button', cls: 'tile', text: num, 'data-tile': w.id });
          paintTile(tile, w);
          tile.addEventListener('click', function () { jumpTo('workitem', w.id); });
          return tile;
        }))
      ]);
    }));
  }

  function chipGroup(label, values, filterKey, labeller) {
    if (!values.length) return null;
    return h('div', { cls: 'filter-group' }, [h('span', { cls: 'filter-label', text: label })].concat(
      values.map(function (v) {
        var chip = h('button', { type: 'button', cls: 'chip', 'aria-pressed': 'false', text: labeller ? labeller(v) : v });
        chip.addEventListener('click', function () {
          var list = state.filters[filterKey];
          var i = list.indexOf(v);
          if (i === -1) { list.push(v); chip.setAttribute('aria-pressed', 'true'); }
          else { list.splice(i, 1); chip.setAttribute('aria-pressed', 'false'); }
          applyFilters();
        });
        return chip;
      })
    ));
  }

  function renderFilters() {
    var plan = state.plan;

    var complexities = arr(plan.complexities);
    if (!complexities.length) {
      var seen = {};
      arr(plan.workItems).forEach(function (w) { if (w.complexity) seen[w.complexity] = true; });
      complexities = Object.keys(seen).sort(function (a, b) {
        var ia = COMPLEXITY_ORDER.indexOf(a.toLowerCase()), ib = COMPLEXITY_ORDER.indexOf(b.toLowerCase());
        return (ia === -1 ? 99 : ia) - (ib === -1 ? 99 : ib);
      });
    }

    var modelTiers = [];
    arr(plan.workItems).forEach(function (w) {
      var t = w.model && w.model.implementation ? w.model.implementation.tier : null;
      if (t && modelTiers.indexOf(t) === -1) modelTiers.push(t);
    });

    var search = h('input', { cls: 'search', type: 'search', id: 'filter-search', placeholder: 'Search', 'aria-label': 'Search work items' });
    search.addEventListener('input', function () { state.filters.q = search.value.trim().toLowerCase(); applyFilters(); });

    var reset = h('button', { type: 'button', cls: 'btn btn-mono', text: 'Reset filters' });
    reset.addEventListener('click', function () {
      state.filters = { tier: [], complexity: [], modelTier: [], status: [], q: '' };
      search.value = '';
      [].slice.call(document.querySelectorAll('.filters .chip')).forEach(function (c) { c.setAttribute('aria-pressed', 'false'); });
      applyFilters();
    });

    var copyStatus = h('button', { type: 'button', cls: 'btn btn-mono', text: 'Copy workItemStatus',
      title: 'Copies the statuses, including unsaved changes, as the workItemStatus value for the plan file' });
    copyStatus.addEventListener('click', function () {
      copyText(JSON.stringify(workItemStatusMap(), null, 2), copyStatus);
    });
    var draftCount = h('span', { cls: 'draft-count', id: 'draft-count', hidden: true });

    return h('div', { cls: 'filters' }, [
      chipGroup('Tier:', arr(plan.tiers).map(function (t) { return t.id; }), 'tier', function (id) { return tierOf(plan, id).label; }),
      chipGroup('Complexity:', complexities, 'complexity'),
      chipGroup('Model:', modelTiers, 'modelTier'),
      chipGroup('Status:', arr(plan.statuses), 'status'),
      search,
      reset,
      copyStatus,
      draftCount,
      h('span', { cls: 'showing', id: 'showing-count', text: 'Showing 0 of 0' })
    ]);
  }

  /* ---------------------------------------------------------- work items */

  function renderWorkItemCard(w) {
    var plan = state.plan;
    var tier = tierOf(plan, w.tier);
    var st = statusOf(w.id);

    var card = h('details', { cls: 'card', id: itemDomId(w.id) });
    card.style.borderLeftColor = 'var(' + (st === 'blocked' ? '--tier-blocked' : tier.cssVar) + ')';

    /* summary row */
    var summaryKids = [h('span', { cls: 'id-chip', text: w.id })];
    if (arr(w.storyRefs).length) {
      arr(w.storyRefs).forEach(function (r) { summaryKids.push(jumpChip('story', r)); });
    } else if (w.storyRefLabel) {
      summaryKids.push(h('span', { cls: 'id-chip', text: w.storyRefLabel }));
    }
    summaryKids.push(h('span', { cls: 'card-title', text: w.title }));

    if (!w.deferred) {
      var tierTag = h('span', { cls: 'tag tag-tier', text: tier.label });
      tierTag.style.background = 'var(' + tier.cssVar + ')';
      summaryKids.push(tierTag);
      if (w.complexity) summaryKids.push(h('span', { cls: 'tag', text: w.complexity }));
      var chip = h('button', { type: 'button', cls: 'status-chip' });
      paintChip(chip, w);
      chip.addEventListener('click', function (ev) { ev.preventDefault(); ev.stopPropagation(); cycleStatus(w, chip); });
      summaryKids.push(chip);
    } else {
      summaryKids.push(h('span', { cls: 'tag', text: 'Deferred' }));
    }
    card.appendChild(h('summary', {}, summaryKids));

    /* body */
    var body = h('div', { cls: 'card-body' });

    /* 1. meta strip */
    var phase = arr(plan.phases).filter(function (p) { return p.id === w.phaseId; })[0];
    var meta = h('div', { cls: 'meta' });
    function metaItem(label, kids) { meta.appendChild(h('div', { cls: 'meta-item' }, [h('b', { text: label })].concat(kids))); }
    if (phase) metaItem('Phase', [phase.name]);
    metaItem('Story ref', arr(w.storyRefs).length
      ? arr(w.storyRefs).map(function (r) { return jumpChip('story', r); })
      : [w.storyRefLabel || 'N/A']);
    if (w.risk) metaItem('Risk', [w.risk.level + ' (' + w.risk.reason + ')']);
    if (w.confidence) metaItem('Confidence', [w.confidence]);
    metaItem('Depends on', arr(w.dependsOn).length ? arr(w.dependsOn).map(function (d) { return jumpChip('workitem', d); }) : ['None']);
    metaItem('Blocks', arr(w.blocks).length ? arr(w.blocks).map(function (b) { return jumpChip('workitem', b); }) : ['Nothing']);
    body.appendChild(meta);

    /* 2. rationale */
    if (w.rationale) {
      body.appendChild(h('div', { cls: 'section' }, [
        h('div', { cls: 'section-label', text: 'Rationale & assessment' }),
        h('div', { cls: 'prose' }, [h('p', { text: w.rationale })])
      ]));
    }

    /* 3. model recommendation */
    if (w.model) {
      var rows = [];
      [['Implementation', w.model.implementation], ['Test writing', w.model.testWriting]].forEach(function (pair) {
        if (!pair[1]) return;
        rows.push(h('dt', { text: pair[0] }));
        rows.push(h('dd', {}, [
          h('div', { text: pair[1].tier }),
          h('div', { cls: 'test-intent', text: [pair[1].anthropic, pair[1].openai, pair[1].google].filter(Boolean).join('  ·  ') })
        ]));
      });
      body.appendChild(h('div', { cls: 'section' }, [
        h('div', { cls: 'section-label', text: 'Model recommendation' }),
        h('dl', { cls: 'kv' }, rows),
        w.model.footnote ? h('div', { cls: 'footnote', text: w.model.footnote }) : null
      ]));
    }

    /* 4. implementation guidance */
    if (w.guidance) {
      var g = w.guidance;
      var kids = [h('div', { cls: 'section-label', text: 'Implementation guidance' })];
      if (arr(g.tasks).length) {
        kids.push(h('ul', { cls: 'list' }, g.tasks.map(function (t) { return h('li', { text: t }); })));
      }
      if (g.contract) {
        var crows = [];
        [['Services', 'services'], ['Entities', 'entities'], ['Endpoints', 'endpoints'], ['Configuration', 'configuration']].forEach(function (pair) {
          var vals = arr(g.contract[pair[1]]);
          if (!vals.length) return;
          crows.push(h('dt', { text: pair[0] }));
          crows.push(h('dd', {}, [h('span', { cls: 'mono', text: vals.join(' · ') })]));
        });
        if (crows.length) kids.push(h('dl', { cls: 'kv', style: 'margin-top:12px' }, crows));
      }
      if (g.checkpoint) {
        kids.push(h('div', { cls: 'callout' }, [h('b', { text: 'Checkpoint' }), g.checkpoint]));
      }
      if (arr(g.outOfScope).length) {
        kids.push(h('div', { cls: 'section', style: 'margin-top:12px' }, [
          h('div', { cls: 'section-label', text: 'Out of scope' }),
          h('ul', { cls: 'list' }, g.outOfScope.map(function (t) { return h('li', { text: t }); }))
        ]));
      }
      body.appendChild(h('div', { cls: 'section' }, kids));
    }

    /* 5. recommended tests */
    if (w.tests) {
      var tkids = [h('div', { cls: 'section-label', text: 'Recommended tests' })];
      var any = false;
      [['Unit', 'unit'], ['Integration', 'integration'], ['Component', 'component'], ['Worker', 'worker']].forEach(function (pair) {
        var list = arr(w.tests[pair[1]]);
        if (!list.length) return;
        any = true;
        tkids.push(h('div', { cls: 'test-group' }, [
          h('h5', { text: pair[0] }),
          h('ul', { cls: 'list' }, list.map(function (t) {
            return h('li', {}, [h('span', { cls: 'test-name', text: t.name }), h('span', { cls: 'test-intent', text: ' — ' + t.intent })]);
          }))
        ]));
      });
      if (!any) tkids.push(h('div', { cls: 'prose' }, [h('p', { text: w.tests.noTestSurface || 'No test surface.' })]));
      body.appendChild(h('div', { cls: 'section' }, tkids));
    }

    /* 6. test ownership */
    if (w.testOwnership) {
      body.appendChild(h('div', { cls: 'section' }, [
        h('div', { cls: 'section-label', text: 'Test ownership' }),
        h('div', { cls: 'prose' }, [h('p', { text: w.testOwnership })])
      ]));
    }

    /* 7. git workflow */
    if (w.git) {
      var gkids = [h('div', { cls: 'section-label', text: 'Git workflow' })];
      if (w.git.branch) {
        gkids.push(h('div', { cls: 'section-head' }, [
          h('span', { cls: 'branch', text: w.git.branch }),
          copyButton(w.git.branch, 'Copy branch')
        ]));
      }
      if (arr(w.git.commitPoints).length) {
        gkids.push(h('div', { style: 'margin-top:10px' }, [
          h('div', { cls: 'test-group' }, [
            h('h5', { text: 'Commit points' }),
            h('ul', { cls: 'list' }, w.git.commitPoints.map(function (c) { return h('li', { text: c }); }))
          ])
        ]));
      }
      if (w.git.commitFormat) {
        gkids.push(h('div', { cls: 'test-group' }, [h('h5', { text: 'Commit format' }), h('pre', { cls: 'prompt', text: w.git.commitFormat })]));
      }
      gkids.push(h('div', { cls: 'prose' }, [h('p', { text: 'No attribution: commit messages name no agent, model, or tooling — no co-author trailers, no generated-with footers, no model names, no badges. Suppress any your tooling adds by default.' })]));
      gkids.push(h('div', { cls: 'callout callout-stop' }, [
        h('b', { text: 'Merge' }),
        'Tests green, then the developer merges. The agent stops at a pushed branch — it does not merge, rebase onto the integration branch, fast-forward, force-push, or delete the branch.'
      ]));
      body.appendChild(h('div', { cls: 'section' }, gkids));
    }

    /* 8 + 9. prompts */
    if (w.prompts && w.prompts.short) {
      body.appendChild(h('div', { cls: 'section' }, [
        h('div', { cls: 'section-head' }, [
          h('div', { cls: 'section-label', style: 'flex:1', text: 'Starter prompt' }),
          copyButton(w.prompts.short, 'Copy prompt')
        ]),
        h('pre', { cls: 'prompt', text: w.prompts.short })
      ]));
    }
    if (w.prompts && w.prompts.full) {
      var full = h('details', { cls: 'sub-details' }, [
        h('summary', { text: 'Full agent briefing' }),
        h('div', {}, [
          h('div', { style: 'padding:8px 0' }, [copyButton(w.prompts.full, 'Copy briefing')]),
          h('pre', { cls: 'prompt', text: w.prompts.full })
        ])
      ]);
      body.appendChild(h('div', { cls: 'section' }, [full]));
    }

    /* 10. source story */
    var refs = arr(w.storyRefs);
    if (refs.length) {
      var storyKids = [];
      refs.forEach(function (rid) {
        var s = arr(plan.stories).filter(function (x) { return x.id === rid; })[0];
        if (!s) return;
        storyKids.push(h('h5', { cls: 'test-name', style: 'margin:12px 0 4px', text: s.id + ' — ' + s.title }));
        storyKids.push(h('p', { cls: 'story-line' }, [
          h('b', { text: 'As a ' }), s.asA + ', ', h('b', { text: 'I want ' }), s.iWant + ', ', h('b', { text: 'so that ' }), s.soThat + '.'
        ]));
        storyKids.push(h('ul', { cls: 'list' }, arr(s.acceptanceCriteria).map(function (c) { return h('li', { text: c }); })));
      });
      body.appendChild(h('div', { cls: 'section' }, [
        h('details', { cls: 'sub-details' }, [h('summary', { text: 'Source story' }), h('div', {}, storyKids)])
      ]));
    } else {
      body.appendChild(h('div', { cls: 'section' }, [
        h('div', { cls: 'footnote', text: 'No source story: this is infrastructure work — ' + (w.storyRefLabel || 'N/A') + '.' })
      ]));
    }

    card.appendChild(body);
    return card;
  }

  /* ------------------------------------------------------------ plan body */

  function renderPlanBody() {
    var plan = state.plan;
    var host = byId('plan-body');
    clear(host);

    arr(plan.phases).forEach(function (p) {
      var items = arr(plan.workItems).filter(function (w) { return w.phaseId === p.id && !w.deferred; });
      if (!items.length) return;
      var group = h('section', { cls: 'phase', 'data-phase-group': p.id }, [
        h('div', { cls: 'phase-head' }, [
          h('div', {}, [h('h2', { cls: 'phase-name', text: p.name }), p.delivers ? h('div', { cls: 'phase-delivers', text: p.delivers }) : null]),
          h('div', { cls: 'phase-count', 'data-phase-count': p.id, text: '0/0 done' })
        ])
      ]);
      items.forEach(function (w) { group.appendChild(renderWorkItemCard(w)); });
      host.appendChild(group);
    });

    var deferred = arr(plan.workItems).filter(function (w) { return w.deferred; });
    if (deferred.length) {
      var dgroup = h('section', { cls: 'phase is-deferred', 'data-phase-group': '__deferred' }, [
        h('div', { cls: 'phase-head' }, [
          h('div', {}, [
            h('h2', { cls: 'phase-name', text: 'Deferred — out of scope' }),
            h('div', { cls: 'phase-delivers', text: 'Visible so nothing is lost, but not in the active queue: no tier, no model recommendation, no starter prompt.' })
          ]),
          h('div', { cls: 'phase-count', 'data-phase-count': '__deferred', text: '0/0 done' })
        ])
      ]);
      deferred.forEach(function (w) { dgroup.appendChild(renderWorkItemCard(w)); });
      host.appendChild(dgroup);
    }

    host.appendChild(h('div', { cls: 'empty', id: 'filter-empty', hidden: true }, [
      h('h2', { text: 'Nothing matches those filters' }),
      h('p', { text: 'Loosen a filter or clear the search box to bring work items back.' })
    ]));

    host.appendChild(renderHowTo());
  }

  function renderHowTo() {
    function para(t) { return h('p', { text: t }); }
    return h('details', { cls: 'sub-details', style: 'margin-top:28px' }, [
      h('summary', { text: 'How to use this console' }),
      h('div', { cls: 'prose' }, [
        h('h3', { text: 'Working an item' }),
        para('Open a card, read the rationale, then copy either the starter prompt (for a session that already has the repo conventions loaded) or the full briefing (for an agent with no other context). Click the status chip to cycle it: to do, active, done, blocked. A clicked chip is an unsaved change until the plan file records it.'),
        h('h3', { text: 'The merge boundary' }),
        para('No tier authorises an agent to merge. Every work item ends at a green, committed, pushed branch and a report — the branch name, the commits made, the test run result, and anything worth a second look. The developer decides what enters the integration branch. Agents do not merge, rebase onto the integration branch, fast-forward, force-push, or delete branches, and that holds on autonomous items too.'),
        h('h3', { text: 'Commit messages' }),
        para('Commit messages describe the change, not what produced it: no co-author trailers naming a model, no generated-with footers, no model names, no AI badges. Several agent tools add these by default, so they have to be suppressed rather than merely omitted.'),
        h('h3', { text: 'Where your progress lives' }),
        para('The plan data file is the record of progress: its workItemStatus map lists every work item that is not to do, and an agent working the plan updates it there. A page opened from disk cannot write that file, so a status you change here is kept in this browser as an unsaved change, shown with a dashed chip. Copy workItemStatus copies the full map with your changes applied, ready to paste over the value in the file. Once the file changes an item, any unsaved change you made to it is discarded in favour of the file. Export progress and Import progress move unsaved changes between browsers.'),
        para('Regenerating the data file must carry the workItemStatus map forward; it is the only copy of recorded progress.'),
        h('h3', { text: 'Swapping the plan' }),
        para('Load plan file swaps in a different plan at runtime without touching the folder. The shell carries no project content, so a different plan is a different data file and nothing else.')
      ])
    ]);
  }

  /* ---------------------------------------------------------- stories tab */

  function renderStories() {
    var plan = state.plan;
    var host = byId('stories-body');
    clear(host);

    var groups = [], seen = {};
    arr(plan.stories).forEach(function (s) {
      var key = s.role || 'Stories';
      if (!seen[key]) { seen[key] = []; groups.push(key); }
      seen[key].push(s);
    });

    groups.forEach(function (key) {
      host.appendChild(h('h2', { cls: 'group-head', text: key }));
      seen[key].forEach(function (s) {
        var card = h('details', { cls: 'card', id: storyDomId(s.id) });
        var sum = [h('span', { cls: 'id-chip', text: s.id }), h('span', { cls: 'card-title', text: s.title })];
        if (s.moscow) sum.push(h('span', { cls: 'tag', text: s.moscow }));
        if (s.points || s.points === 0) sum.push(h('span', { cls: 'tag', text: s.points + ' pts' }));
        if (!arr(s.implementedBy).length) sum.push(h('span', { cls: 'tag tag-warn', text: 'No work item' }));
        card.appendChild(h('summary', {}, sum));

        var body = h('div', { cls: 'card-body' });
        body.appendChild(h('div', { cls: 'section' }, [
          h('div', { cls: 'section-label', text: 'Implemented by' }),
          h('div', {}, arr(s.implementedBy).length
            ? arr(s.implementedBy).map(function (r) { return jumpChip('workitem', r); })
            : [h('span', { cls: 'tag tag-warn', text: 'Nothing implements this story — that is a defect in the plan.' })])
        ]));
        body.appendChild(h('p', { cls: 'story-line' }, [
          h('b', { text: 'As a ' }), s.asA + ', ', h('b', { text: 'I want ' }), s.iWant + ', ', h('b', { text: 'so that ' }), s.soThat + '.'
        ]));
        body.appendChild(h('div', { cls: 'section' }, [
          h('div', { cls: 'section-label', text: 'Acceptance criteria' }),
          h('ul', { cls: 'list' }, arr(s.acceptanceCriteria).map(function (c) { return h('li', { text: c }); }))
        ]));
        arr(s.extra).forEach(function (x) {
          body.appendChild(h('div', { cls: 'section' }, [
            h('div', { cls: 'section-label', text: x.label }),
            Array.isArray(x.value)
              ? h('ul', { cls: 'list' }, x.value.map(function (v) { return h('li', { text: v }); }))
              : h('div', { cls: 'prose' }, [h('p', { text: x.value })])
          ]));
        });
        card.appendChild(body);
        host.appendChild(card);
      });
    });
  }

  /* ------------------------------------------------------- contexts tab */

  function renderContexts() {
    var plan = state.plan;
    var host = byId('contexts-body');
    clear(host);

    var contextMap = {};
    arr(plan.stories).forEach(function (s) {
      arr(s.extra).forEach(function (x) {
        if (x.label === 'Bounded context') {
          var val = x.value;
          if (!contextMap[val]) contextMap[val] = [];
          contextMap[val].push(s);
        }
      });
    });

    var keys = Object.keys(contextMap).sort();
    keys.forEach(function (ctx) {
      host.appendChild(h('h2', { cls: 'group-head', text: ctx }));
      var list = h('ul', { cls: 'list' });
      contextMap[ctx].forEach(function(s) {
        var li = h('li', { cls: 'story-line' });
        li.appendChild(jumpChip('story', s.id));
        li.appendChild(document.createTextNode(' — ' + s.title));
        list.appendChild(li);
      });
      host.appendChild(list);
    });
  }

  /* -------------------------------------------------------- decisions tab */

  function renderDecisions() {
    var plan = state.plan;
    var host = byId('decisions-body');
    clear(host);
    var d = plan.decisions || {};

    var mi = arr(d.missingInformation);
    var miBlock = h('div', { cls: 'panel-block' }, [
      h('h2', { text: 'Missing information' }),
      h('p', { cls: 'plan-subtitle', text: 'Every placeholder in the data file, and what has to be decided before the work items that carry it can be finished.' })
    ]);
    if (mi.length) {
      miBlock.appendChild(h('table', { cls: 'table' }, [
        h('thead', {}, [h('tr', {}, ['Ref', 'Item', 'What I assumed', 'Why', 'Confirm by'].map(function (t) { return h('th', { text: t }); }))]),
        h('tbody', {}, mi.map(function (r) {
          return h('tr', {}, [
            h('td', {}, [h('span', { cls: 'mono', text: r.ref })]),
            h('td', { text: r.item }), h('td', { text: r.assumed }), h('td', { text: r.why }), h('td', { text: r.confirmBy })
          ]);
        }))
      ]));
    }
    host.appendChild(miBlock);

    var inf = arr(d.inferences);
    if (inf.length) {
      host.appendChild(h('div', { cls: 'panel-block' }, [
        h('h2', { text: 'Inferences' }),
        h('p', { cls: 'plan-subtitle', text: 'Decided rather than read. Each line says what changes if the inference is wrong.' }),
        h('table', { cls: 'table' }, [
          h('thead', {}, [h('tr', {}, ['Ref', 'Decision', 'If it is wrong'].map(function (t) { return h('th', { text: t }); }))]),
          h('tbody', {}, inf.map(function (r) {
            return h('tr', {}, [
              h('td', {}, [h('span', { cls: 'mono', text: r.ref || '' })]),
              h('td', { text: r.decision }), h('td', { text: r.ifWrong })
            ]);
          }))
        ])
      ]));
    }

    var co = arr(d.carriedOver);
    if (co.length) {
      var block = h('div', { cls: 'panel-block' }, [
        h('h2', { text: 'Open items carried over' }),
        h('p', { cls: 'plan-subtitle', text: 'Raised by the source stories, reproduced with their original identifiers.' })
      ]);
      co.forEach(function (r) {
        block.appendChild(h('div', { cls: 'section' }, [
          h('div', { cls: 'section-label', text: r.id + (r.affects ? ' · affects ' + r.affects : '') }),
          h('div', { cls: 'prose' }, [h('p', { text: r.text })])
        ]));
      });
      host.appendChild(block);
    }
  }

  /* ----------------------------------------------------- architecture tab */

  function renderArchitecture() {
    var plan = state.plan;
    var host = byId('architecture-body');
    clear(host);
    var a = plan.architecture || {};

    if (arr(a.stack).length) {
      host.appendChild(h('div', { cls: 'panel-block' }, [
        h('h2', { text: 'Stack' }),
        h('table', { cls: 'table' }, [
          h('thead', {}, [h('tr', {}, ['Concern', 'Value'].map(function (t) { return h('th', { text: t }); }))]),
          h('tbody', {}, a.stack.map(function (r) {
            return h('tr', {}, [h('td', { text: r.concern }), h('td', {}, [h('span', { cls: 'mono', text: r.value })])]);
          }))
        ])
      ]));
    }

    [['Solution layout', 'solutionLayout'], ['API surface', 'apiSurface'], ['Authentication topology', 'authTopology'], ['Deferred front ends', 'deferredFrontEnds']].forEach(function (pair) {
      var v = a[pair[1]];
      if (!v) return;
      var block = h('div', { cls: 'panel-block' }, [h('h2', { text: pair[0] })]);
      String(v).split('\n\n').forEach(function (para) {
        if (/^\s*[-•]/.test(para)) {
          block.appendChild(h('ul', { cls: 'list' }, para.split('\n').map(function (line) {
            return h('li', { text: line.replace(/^\s*[-•]\s*/, '') });
          })));
        } else if (/^\s{2,}|\|/.test(para) && para.indexOf('\n') !== -1) {
          block.appendChild(h('pre', { cls: 'prompt', text: para }));
        } else {
          block.appendChild(h('p', { cls: 'prose', text: para }));
        }
      });
      host.appendChild(block);
    });
  }

  /* ------------------------------------------------------------ file I/O */

  function parsePlanText(text) {
    var t = String(text);
    var marker = t.indexOf('DELIVERY_PLAN');
    var start, end;
    if (marker !== -1) {
      start = t.indexOf('{', marker);
      end = t.lastIndexOf('}');
    } else {
      start = t.indexOf('{');
      end = t.lastIndexOf('}');
    }
    if (start === -1 || end === -1 || end < start) throw new Error('No plan object found in that file.');
    return JSON.parse(t.slice(start, end + 1));
  }

  function wireToolbar() {
    var planInput = byId('input-plan');
    byId('btn-load-plan').addEventListener('click', function () { planInput.click(); });
    planInput.addEventListener('change', function () {
      var f = planInput.files && planInput.files[0];
      if (!f) return;
      var reader = new FileReader();
      reader.onload = function () {
        try {
          var plan = parsePlanText(reader.result);
          clear(byId('banners'));
          boot(plan, f.name);
        } catch (e) {
          banner('error', 'That file could not be read as a plan.', [String(e.message || e), 'Expected a JavaScript file assigning a plain JSON object, or a bare .json file.']);
        }
      };
      reader.readAsText(f);
      planInput.value = '';
    });

    byId('btn-export').addEventListener('click', function () {
      if (!state.plan) return;
      var payload = { consoleId: state.plan.consoleId, exportedUtc: new Date().toISOString(), statuses: workItemStatusMap() };
      var blob = new Blob([JSON.stringify(payload, null, 2)], { type: 'application/json' });
      var url = URL.createObjectURL(blob);
      var a = h('a', { href: url, download: (state.plan.consoleId || 'plan') + '-progress.json' });
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      window.setTimeout(function () { URL.revokeObjectURL(url); }, 1000);
    });

    var progInput = byId('input-progress');
    byId('btn-import').addEventListener('click', function () { progInput.click(); });
    progInput.addEventListener('change', function () {
      var f = progInput.files && progInput.files[0];
      if (!f) return;
      var reader = new FileReader();
      reader.onload = function () {
        var payload;
        try { payload = JSON.parse(reader.result); } catch (e) {
          banner('error', 'That progress file could not be read.', [String(e.message || e)]);
          return;
        }
        if (payload.consoleId && state.plan && payload.consoleId !== state.plan.consoleId) {
          var ok = window.confirm('That progress file was exported from a different plan (' + payload.consoleId +
            '). Import it into ' + state.plan.consoleId + ' anyway?');
          if (!ok) return;
        }
        var known = knownItemIds(), merged = 0;
        Object.keys(payload.statuses || {}).forEach(function (k) {
          var v = payload.statuses[k];
          if (known[k] && DeliveryStatus.STATUSES.indexOf(v) !== -1) {
            DeliveryStatus.setDraft(state.fileStatuses, state.drafts, k, v);
            merged++;
          }
        });
        saveStatuses();
        render();
        banner('note', 'Imported ' + merged + ' statuses.', [
          'Any that differ from the plan file are unsaved changes until the file\'s workItemStatus is updated.'
        ]);
      };
      reader.readAsText(f);
      progInput.value = '';
    });

    byId('btn-print').addEventListener('click', function () { window.print(); });

    window.addEventListener('beforeprint', function () {
      [].slice.call(document.querySelectorAll('details')).forEach(function (d) {
        if (!d.open) { d.open = true; d.setAttribute('data-was-closed', ''); }
      });
    });
    window.addEventListener('afterprint', function () {
      [].slice.call(document.querySelectorAll('details[data-was-closed]')).forEach(function (d) {
        d.open = false; d.removeAttribute('data-was-closed');
      });
    });
  }

  /* ---------------------------------------------------------------- boot */

  function renderEmpty() {
    clear(byId('plan-header'));
    clear(byId('stories-body'));
    clear(byId('contexts-body'));
    clear(byId('decisions-body'));
    clear(byId('architecture-body'));
    var host = byId('plan-body');
    clear(host);
    host.appendChild(h('div', { cls: 'empty' }, [
      h('h2', { text: 'No plan loaded' }),
      h('p', { text: 'This console is a shell — it carries no project content. Put a plan data file beside it named plan-data.js, or load one now.' }),
      (function () {
        var b = h('button', { type: 'button', cls: 'btn', text: 'Load plan file' });
        b.addEventListener('click', function () { byId('input-plan').click(); });
        return b;
      })()
    ]));
    byId('site-footer').textContent = '';
  }

  function render() {
    renderPlanHeader();
    renderPlanBody();
    renderStories();
    renderContexts();
    renderDecisions();
    renderArchitecture();
    applyFilters();
    paintDraftCount();
  }

  function boot(plan, sourceLabel) {
    if (!plan) { renderEmpty(); return; }
    state.plan = plan;
    state.filters = { tier: [], complexity: [], modelTier: [], status: [], q: '' };

    if (plan.schemaVersion !== EXPECTED_SCHEMA) {
      banner('error', 'Schema version mismatch.', [
        'This console expects schemaVersion ' + EXPECTED_SCHEMA + '. The loaded plan declares ' +
        (plan.schemaVersion === undefined ? 'none' : plan.schemaVersion) + '.',
        'Rendering continues, but fields may be missing or interpreted wrongly.'
      ]);
    }

    var problems = validate(plan);
    if (problems.length) {
      banner('error', 'This plan has ' + problems.length + ' broken reference' + (problems.length === 1 ? '' : 's') + '.', problems);
    }

    arr(plan.workItems).forEach(function (w) { w._blob = searchBlob(plan, w); });

    state.storeKey = plan.consoleId ? STORE_PREFIX + plan.consoleId : null;
    loadStatuses();

    document.title = plan.title || 'Delivery Console';
    byId('brand').textContent = plan.consoleId || 'Delivery Console';
    byId('site-footer').textContent = 'Loaded from ' + (sourceLabel || 'plan-data.js') +
      '. Statuses come from its workItemStatus map; changes made here stay in this browser until the file records them.';

    render();
    switchTab('plan');
  }

  document.addEventListener('DOMContentLoaded', function () {
    wireTabs();
    wireToolbar();
    boot(window.DELIVERY_PLAN || null, 'plan-data.js');
  });
})();
