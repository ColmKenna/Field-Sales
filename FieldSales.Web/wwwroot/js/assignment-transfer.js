// The server independently applies exclusions and validates every selected scope.
for (const scope of document.querySelectorAll('.transfer-scope')) {
    const parent = scope.querySelector('[data-transfer-parent]');
    const children = [...scope.querySelectorAll('[data-transfer-child]')];
    if (!parent || children.length === 0) continue;
    const update = () => {
        const selected = children.filter(child => child.checked).length;
        parent.checked = selected === children.length;
        parent.indeterminate = selected > 0 && selected < children.length;
    };
    parent.addEventListener('change', () => {
        for (const child of children) child.checked = parent.checked;
        update();
    });
    for (const child of children) child.addEventListener('change', update);
    // Preserve a child-only selection as a roll-up request; only user interaction
    // promotes all children to an explicit whole-parent selection.
    parent.indeterminate = !parent.checked && children.some(child => child.checked);
}
