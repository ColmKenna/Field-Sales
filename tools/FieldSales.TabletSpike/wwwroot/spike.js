let submitStarted;
document.addEventListener('submit', event => {
    if (event.target.id === 'search-form') submitStarted = performance.now();
}, true);
window.spike = {
    userAgent: () => navigator.userAgent,
    searchStart: () => { const start = submitStarted ?? performance.now(); submitStarted = undefined; return start; },
    afterPaintElapsed: start => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(() => resolve(performance.now() - start))))
};
