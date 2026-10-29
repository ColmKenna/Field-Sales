import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { runInNewContext } from 'node:vm';

const source = readFileSync(new URL('../../../src/FieldSales.Tablet/wwwroot/tablet-sign-out.js', import.meta.url), 'utf8');

function screen() {
    const document = { activeElement: null };
    const action = () => ({
        clicks: 0,
        click() { this.clicks++; },
        focus() { document.activeElement = this; }
    });
    const actions = [action(), action(), action()];
    const listeners = new Map();
    const dialog = {
        isConnected: true,
        open: false,
        showModal() { this.open = true; },
        close() { this.open = false; },
        querySelector() { return actions[0]; },
        querySelectorAll() { return actions; },
        addEventListener(name, callback) {
            if (!listeners.has(name)) listeners.set(name, []);
            listeners.get(name).push(callback);
        },
        emit(name, event) { for (const callback of listeners.get(name) ?? []) callback(event); }
    };
    const trigger = { ...action(), isConnected: true };
    const window = {};
    runInNewContext(source, { window, document });
    return { api: window.tabletSignOut, dialog, actions, trigger, document };
}

test('queued dialog calls tolerate elements removed by automatic logout', () => {
    const { api, dialog, trigger, document } = screen();
    assert.doesNotThrow(() => api.open(null));
    assert.doesNotThrow(() => api.close(null, null));
    dialog.isConnected = false;
    trigger.isConnected = false;
    api.open(dialog);
    api.close(null, trigger);
    assert.equal(dialog.open, false);
    assert.equal(document.activeElement, null);
});

test('opening focuses Stay and closing returns focus to the initiating action', () => {
    const { api, dialog, actions, trigger, document } = screen();
    api.open(dialog);
    assert.equal(dialog.open, true);
    assert.equal(document.activeElement, actions[0]);
    api.close(dialog, trigger);
    assert.equal(dialog.open, false);
    assert.equal(document.activeElement, trigger);
});

test('Escape follows Stay exactly once even after the warning is reopened', () => {
    const { api, dialog, actions, trigger } = screen();
    api.open(dialog);
    api.close(dialog, trigger);
    api.open(dialog);
    let prevented = false;
    dialog.emit('cancel', { preventDefault() { prevented = true; } });
    assert.equal(prevented, true);
    assert.equal(actions[0].clicks, 1);
    assert.equal(actions[1].clicks, 0);
});

test('Tab and Shift+Tab wrap at the warning boundaries without changing middle navigation', () => {
    const { api, dialog, actions, document } = screen();
    api.open(dialog);
    let prevented = false;
    const event = shiftKey => ({ key: 'Tab', shiftKey, preventDefault() { prevented = true; } });
    actions[2].focus();
    dialog.emit('keydown', event(false));
    assert.equal(prevented, true);
    assert.equal(document.activeElement, actions[0]);
    prevented = false;
    dialog.emit('keydown', event(true));
    assert.equal(prevented, true);
    assert.equal(document.activeElement, actions[2]);
    prevented = false;
    actions[1].focus();
    dialog.emit('keydown', event(false));
    assert.equal(prevented, false);
});
