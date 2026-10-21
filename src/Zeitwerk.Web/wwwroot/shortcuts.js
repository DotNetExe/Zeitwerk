// Globale Tastenkürzel: leitet Tasten als Aktionsnamen an Blazor weiter (siehe Shortcuts.razor).
// Greift nicht, solange der Fokus in einem Eingabefeld liegt oder ein Dialog offen ist.
const plain = { s: "stop", t: "today", "?": "help", ArrowLeft: "prev", ArrowRight: "next" };
let handler;

const isBlocked = target =>
    (target instanceof Element && (target.isContentEditable || target.closest("input, textarea, select, [role=dialog]")))
    || document.querySelector(".mud-dialog-container") !== null;

export function register(dotnet) {
    unregister();
    handler = e => {
        if (e.defaultPrevented || e.altKey || e.repeat || isBlocked(e.target))
            return;
        const key = e.key.length === 1 ? e.key.toLowerCase() : e.key;
        const action = e.ctrlKey || e.metaKey ? (key === "k" ? "palette" : undefined) : plain[key];
        if (!action)
            return;
        e.preventDefault();
        dotnet.invokeMethodAsync("Run", action);
    };
    document.addEventListener("keydown", handler);
}

export function unregister() {
    if (handler)
        document.removeEventListener("keydown", handler);
    handler = undefined;
}
