import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { runInNewContext } from 'node:vm';

const script = readFileSync(new URL('../../src/FieldSales.Web/wwwroot/js/product-unit-form.js', import.meta.url), 'utf8');

// Only the browser surface used by the script is supplied; all presentation logic is the real script.
function page({ unit = 'Each', originalUnit = '', amount = '', enteredPrice } = {}) {
    const element = (value = '') => ({ value, hidden: false, disabled: false, required: false, textContent: '',
        listeners: {}, addEventListener(name, callback) { this.listeners[name] = callback; } });
    const controls = { unit: element(unit), measure: element(), step: element(), preview: element(),
        flag: element(), price: enteredPrice === undefined ? null : element(enteredPrice), label: element() };
    const selectors = { '[data-unit-select]': controls.unit, '[data-measure-fields]': controls.measure,
        '[data-quantity-step]': controls.step, '[data-resulting-price]': controls.preview, '[data-price-basis-change]': controls.flag };
    const fields = { dataset: { originalUnit, priceAmount: amount }, querySelector: selector => selectors[selector] };
    const form = { querySelector: selector => selector === '[data-unit-fields]' ? fields
        : selector === '[data-base-price]' ? controls.price : controls.label };
    runInNewContext(script, { document: { querySelectorAll: () => [form] } });
    controls.changeUnit = value => { controls.unit.value = value; controls.unit.listeners.change(); };
    return controls;
}

test('Each hides and disables measure inputs; switching to kg reveals required step', () => {
    const controls = page({ enteredPrice: '4.80' });
    assert.equal(controls.measure.hidden, true);
    assert.equal(controls.measure.disabled, true);
    assert.equal(controls.step.required, false);
    controls.changeUnit('kg');
    assert.equal(controls.measure.hidden, false);
    assert.equal(controls.measure.disabled, false);
    assert.equal(controls.step.required, true);
    assert.equal(controls.label.textContent, 'Base price (€ per kg)');
    assert.equal(controls.preview.textContent, 'Resulting price: €4.80 per kg');
    controls.changeUnit('Each');
    assert.equal(controls.measure.hidden, true);
    assert.equal(controls.measure.disabled, true);
    assert.equal(controls.step.required, false);
});

test('existing price is shown and flagged before a changed unit is saved', () => {
    const controls = page({ originalUnit: 'Each', amount: '€4.80' });
    assert.equal(controls.flag.hidden, true);
    controls.changeUnit('kg');
    assert.equal(controls.preview.textContent, 'Resulting price: €4.80 per kg');
    assert.equal(controls.flag.hidden, false);
    assert.equal(controls.flag.textContent, 'Price basis changes from €4.80 per Each to €4.80 per kg. The amount stays the same.');
    controls.changeUnit('Each');
    assert.equal(controls.flag.hidden, true);
});

test('price preview preserves decimal text and accepts a zero price', () => {
    const controls = page({ unit: 'kg', enteredPrice: '9999999999999999.99' });
    assert.equal(controls.preview.textContent, 'Resulting price: €9999999999999999.99 per kg');
    controls.price.value = '0';
    controls.price.listeners.input();
    assert.equal(controls.preview.textContent, 'Resulting price: €0.00 per kg');
    controls.price.value = '4.8';
    controls.price.listeners.input();
    assert.equal(controls.preview.textContent, 'Resulting price: €4.80 per kg');
    controls.price.value = '4.805';
    controls.price.listeners.input();
    assert.equal(controls.preview.textContent, 'Price per kg');
});

test('litre and metre keep the same price amount and use their selected unit', () => {
    const controls = page({ unit: 'kg', originalUnit: 'kg', amount: '€4.80' });
    for (const unit of ['litre', 'metre']) {
        controls.changeUnit(unit);
        assert.equal(controls.preview.textContent, `Resulting price: €4.80 per ${unit}`);
        assert.equal(controls.flag.hidden, false);
        assert.equal(controls.measure.hidden, false);
    }
});

test('new products show the price without an existing-price change flag', () => {
    const controls = page({ enteredPrice: '4.80' });
    controls.changeUnit('kg');
    assert.equal(controls.preview.textContent, 'Resulting price: €4.80 per kg');
    assert.equal(controls.flag.hidden, true);
});
