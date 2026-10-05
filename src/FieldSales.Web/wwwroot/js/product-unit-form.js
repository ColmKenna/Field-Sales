(() => {
    // This updates presentation only. Exact quantity/price validation stays on the server.
    for (const form of document.querySelectorAll('[data-product-unit-form]')) {
        const fields = form.querySelector('[data-unit-fields]');
        const unit = fields.querySelector('[data-unit-select]');
        const measure = fields.querySelector('[data-measure-fields]');
        const step = fields.querySelector('[data-quantity-step]');
        const preview = fields.querySelector('[data-resulting-price]');
        const flag = fields.querySelector('[data-price-basis-change]');
        const price = form.querySelector('[data-base-price]');
        const priceLabel = form.querySelector('[data-base-price-label]');
        function update() {
            measure.hidden = measure.disabled = unit.value === 'Each';
            step.required = !measure.hidden;
            if (priceLabel) priceLabel.textContent = `Base price (€ per ${unit.value})`;
            let amount = fields.dataset.priceAmount;
            if (price) {
                // Format the entered decimal as text; never round or convert it to a binary number.
                const match = /^(\d+)(?:\.(\d{0,2}))?$/.exec(price.value);
                amount = match ? `€${match[1]}.${(match[2] || '').padEnd(2, '0')}` : '';
            }
            preview.textContent = amount ? `Resulting price: ${amount} per ${unit.value}` : `Price per ${unit.value}`;
            flag.hidden = !fields.dataset.originalUnit || fields.dataset.originalUnit === unit.value;
            flag.textContent = amount
                ? `Price basis changes from ${amount} per ${fields.dataset.originalUnit} to ${amount} per ${unit.value}. The amount stays the same.`
                : `Price basis changes from per ${fields.dataset.originalUnit} to per ${unit.value}.`;
        }
        unit.addEventListener('change', update);
        if (price) price.addEventListener('input', update);
        update();
    }
})();
