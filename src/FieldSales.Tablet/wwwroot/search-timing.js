// Include WebView event dispatch and rendering, not just the .NET filter. No query/location text is logged.
(() => {
    let inputAt = 0;
    document.addEventListener('input', event => {
        if (event.target.id === 'shop-query') inputAt = performance.now();
    }, true);
    window.fieldSalesSearchAfterPaint = matches => new Promise(resolve => {
        const started = inputAt;
        // Virtualize may render its first rows after the parent's render callback.
        const painted = () => {
            if (matches > 0 && !document.querySelector('.shop-result') && performance.now() - started < 5000) {
                requestAnimationFrame(painted);
                return;
            }
            requestAnimationFrame(() => requestAnimationFrame(() => resolve(performance.now() - started)));
        };
        requestAnimationFrame(painted);
    });
})();
