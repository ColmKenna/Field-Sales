// Run with: node --test plan_docs/field-sales-delivery/tests/status-store.test.js
'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const S = require('../status-store.js');

const known = { 'WI-001': true, 'WI-002': true, 'WI-003': true };
const order = ['WI-001', 'WI-002', 'WI-003'];

test('readFile keeps known items with recognised statuses and reports the rest', () => {
  const r = S.readFile({ 'WI-001': 'active', 'WI-009': 'done', 'WI-002': 'finished' }, known);
  assert.deepEqual(r.statuses, { 'WI-001': 'active' });
  assert.equal(r.problems.length, 2);
});

test('readFile treats a missing map as all to-do', () => {
  const r = S.readFile(undefined, known);
  assert.deepEqual(r.statuses, {});
  assert.equal(S.effective(r.statuses, {}, 'WI-003'), 'todo');
});

test('the file value is used when there is no draft', () => {
  assert.equal(S.effective({ 'WI-001': 'done' }, {}, 'WI-001'), 'done');
});

test('a draft made against the current file value shadows it', () => {
  const file = { 'WI-001': 'active' };
  const r = S.reconcile({ 'WI-001': { status: 'done', base: 'active' } }, file, known);
  assert.equal(S.effective(file, r.drafts, 'WI-001'), 'done');
  assert.ok(S.isDraft(r.drafts, 'WI-001'));
});

test('a draft is dropped once the file changes that item', () => {
  const file = { 'WI-001': 'done' };
  const r = S.reconcile({ 'WI-001': { status: 'blocked', base: 'active' } }, file, known);
  assert.deepEqual(r.drafts, {});
  assert.equal(r.superseded, 1);
  assert.equal(S.effective(file, r.drafts, 'WI-001'), 'done');
});

test('a draft that now matches the file is dropped without counting as superseded', () => {
  const r = S.reconcile({ 'WI-002': { status: 'todo', base: 'todo' } }, {}, known);
  assert.deepEqual(r.drafts, {});
  assert.equal(r.superseded, 0);
});

test('legacy bare-string entries become drafts against the current file value', () => {
  const file = { 'WI-001': 'active' };
  const r = S.reconcile({ 'WI-001': 'done', 'WI-002': 'todo' }, file, known);
  assert.deepEqual(r.drafts, { 'WI-001': { status: 'done', base: 'active' } });
});

test('drafts for unknown items or with unknown statuses are dropped and counted', () => {
  const r = S.reconcile({ 'WI-999': { status: 'done', base: 'todo' }, 'WI-002': { status: 'nope', base: 'todo' } }, {}, known);
  assert.deepEqual(r.drafts, {});
  assert.equal(r.unknown, 2);
});

test('setDraft records the base and clears when returning to the file value', () => {
  const file = { 'WI-001': 'active' };
  const drafts = {};
  S.setDraft(file, drafts, 'WI-001', 'done');
  assert.deepEqual(drafts, { 'WI-001': { status: 'done', base: 'active' } });
  S.setDraft(file, drafts, 'WI-001', 'active');
  assert.deepEqual(drafts, {});
});

test('next cycles todo, active, done, blocked and wraps', () => {
  assert.deepEqual(['todo', 'active', 'done', 'blocked'].map(S.next), ['active', 'done', 'blocked', 'todo']);
});

test('toWorkItemStatus merges drafts in plan order and omits to-do', () => {
  const file = { 'WI-003': 'blocked', 'WI-001': 'active' };
  const drafts = { 'WI-001': { status: 'done', base: 'active' }, 'WI-003': { status: 'todo', base: 'blocked' } };
  const out = S.toWorkItemStatus(file, drafts, order);
  assert.deepEqual(out, { 'WI-001': 'done' });
  assert.deepEqual(Object.keys(S.toWorkItemStatus({ 'WI-003': 'done', 'WI-001': 'done' }, {}, order)), ['WI-001', 'WI-003']);
});
