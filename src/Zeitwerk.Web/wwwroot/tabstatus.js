// Zeigt den Timer-Zustand im Browser-Tab. Der Seitentitel aus <PageTitle> bleibt unangetastet:
// Ein eigenes <title> vorn im <head> hat Vorrang (es gilt das erste <title> im Dokument)
// und wird im Leerlauf wieder entfernt. Die Favicon-Links bekommen ihre Original-URL zurück.

const TITLE_ID = "zw-tab-title";

function icons() {
    return document.querySelectorAll('link[rel="icon"]');
}

function setIcon(link, href) {
    if (link.dataset.zwHref === undefined)
        link.dataset.zwHref = link.getAttribute("href");
    if (link.getAttribute("href") !== href)
        link.setAttribute("href", href);
}

export function show(title, icon) {
    let el = document.getElementById(TITLE_ID);
    if (!el) {
        el = document.createElement("title");
        el.id = TITLE_ID;
        document.head.prepend(el);
    }
    if (el.textContent !== title)
        el.textContent = title;
    icons().forEach(link => setIcon(link, icon));
}

export function clear() {
    document.getElementById(TITLE_ID)?.remove();
    icons().forEach(link => {
        if (link.dataset.zwHref !== undefined) {
            link.setAttribute("href", link.dataset.zwHref);
            delete link.dataset.zwHref;
        }
    });
}
