/* ==========================================================================
   Delivery console — work-item status store
   Generic. The plan data file's workItemStatus map is authoritative; a status
   changed in the browser is only a local draft until the file is updated.
   Each draft remembers the file value it was made against, so once the file
   changes that item the draft is stale and is dropped. Pure functions, no DOM:
   loaded by the console with a classic script tag and by Node for tests.
   ========================================================================== */
var DeliveryStatus = (function () {
  'use strict';

  var STATUSES = ['todo', 'active', 'done', 'blocked'];
  var DEFAULT_STATUS = STATUSES[0];

  function isStatus(v) { return STATUSES.indexOf(v) !== -1; }
  function has(map, k) { return Object.prototype.hasOwnProperty.call(map, k); }

  /* The file map, keeping only known work items with a recognised status.
     Rejected entries are reported so the console can surface them. */
  function readFile(workItemStatus, knownIds) {
    var statuses = {}, problems = [];
    var source = workItemStatus && typeof workItemStatus === 'object' ? workItemStatus : {};
    Object.keys(source).forEach(function (id) {
      var v = source[id];
      if (!has(knownIds, id)) problems.push('workItemStatus lists ' + id + ', which is not in this plan.');
      else if (!isStatus(v)) problems.push('workItemStatus gives ' + id + ' the unknown status "' + v + '".');
      else statuses[id] = v;
    });
    return { statuses: statuses, problems: problems };
  }

  function fileValue(fileStatuses, id) { return has(fileStatuses, id) ? fileStatuses[id] : DEFAULT_STATUS; }

  /* Browser drafts, reconciled against the current file.
     Stored shape: { id: { status, base } }. A bare string is the pre-file-status
     format, where the browser was the only store; it is kept as a draft against
     the current file value so that progress is not silently lost. */
  function reconcile(stored, fileStatuses, knownIds) {
    var drafts = {}, unknown = 0, superseded = 0;
    var source = stored && typeof stored === 'object' ? stored : {};
    Object.keys(source).forEach(function (id) {
      if (!has(knownIds, id)) { unknown++; return; }
      var entry = source[id];
      var current = fileValue(fileStatuses, id);
      var draft = typeof entry === 'string'
        ? { status: entry, base: current }
        : entry && typeof entry === 'object' ? { status: entry.status, base: entry.base } : null;
      if (!draft || !isStatus(draft.status)) { unknown++; return; }
      if (draft.base !== current) { superseded++; return; }
      if (draft.status === current) return;
      drafts[id] = draft;
    });
    return { drafts: drafts, unknown: unknown, superseded: superseded };
  }

  function effective(fileStatuses, drafts, id) {
    return has(drafts, id) ? drafts[id].status : fileValue(fileStatuses, id);
  }

  function isDraft(drafts, id) { return has(drafts, id); }

  /* Records a browser-side change. Returning to the file's value clears the draft. */
  function setDraft(fileStatuses, drafts, id, status) {
    var current = fileValue(fileStatuses, id);
    if (status === current) delete drafts[id];
    else drafts[id] = { status: status, base: current };
    return drafts;
  }

  function next(status) {
    var i = STATUSES.indexOf(status);
    return STATUSES[(i + 1) % STATUSES.length];
  }

  /* The workItemStatus map to paste into the plan data file: file values with
     drafts applied, in plan order, omitting the default status. */
  function toWorkItemStatus(fileStatuses, drafts, orderedIds) {
    var out = {};
    orderedIds.forEach(function (id) {
      var v = effective(fileStatuses, drafts, id);
      if (v !== DEFAULT_STATUS) out[id] = v;
    });
    return out;
  }

  return {
    STATUSES: STATUSES,
    DEFAULT_STATUS: DEFAULT_STATUS,
    readFile: readFile,
    reconcile: reconcile,
    effective: effective,
    isDraft: isDraft,
    setDraft: setDraft,
    next: next,
    toWorkItemStatus: toWorkItemStatus
  };
})();

if (typeof module !== 'undefined' && module.exports) module.exports = DeliveryStatus;
