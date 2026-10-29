(() => {
    const prepared = new WeakSet();
    window.tabletSignOut = {
        open(dialog) {
            if (!dialog?.isConnected) return;
            if (!prepared.has(dialog)) {
                dialog.addEventListener("cancel", event => {
                    event.preventDefault();
                    // Escape follows the same Blazor event path as Stay signed in.
                    dialog.querySelector("[data-stay-signed-in]").click();
                });
                dialog.addEventListener("keydown", event => {
                    if (event.key !== "Tab") return;
                    const actions = [...dialog.querySelectorAll("button:not(:disabled)")];
                    const first = actions[0];
                    const last = actions[actions.length - 1];
                    if (event.shiftKey && document.activeElement === first) {
                        event.preventDefault();
                        last.focus();
                    } else if (!event.shiftKey && document.activeElement === last) {
                        event.preventDefault();
                        first.focus();
                    }
                });
                prepared.add(dialog);
            }
            if (!dialog.open) dialog.showModal();
            dialog.querySelector("[data-stay-signed-in]").focus();
        },
        close(dialog, trigger) {
            if (dialog?.open) dialog.close();
            if (trigger?.isConnected) trigger.focus({ preventScroll: true });
        }
    };
})();
