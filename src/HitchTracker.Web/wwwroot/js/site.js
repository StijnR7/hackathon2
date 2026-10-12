// Gedeelde hulpfuncties voor alle pagina's. Geen inline scripts: de Content-Security-Policy staat alleen
// scripts van onze eigen site toe.
(function () {
    'use strict';

    // Formulieren met data-confirm vragen eerst om bevestiging (bijv. "Rit nu beëindigen").
    document.addEventListener('submit', function (event) {
        const message = event.target.getAttribute('data-confirm');
        if (message && !window.confirm(message)) {
            event.preventDefault();
        }
    });

    // Een keuzelijst met data-auto-submit verstuurt het formulier direct (bijv. stad wisselen).
    document.querySelectorAll('select[data-auto-submit]').forEach(function (select) {
        select.addEventListener('change', function () {
            select.form.submit();
        });
    });
})();

/** Kaartfuncties op basis van Leaflet en OpenStreetMap. */
window.HitchMap = (function () {
    'use strict';

    const locale = 'nl-NL';

    function create(elementId, center, zoom) {
        const map = L.map(elementId, { scrollWheelZoom: true }).setView(center, zoom || 13);
        L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
            maxZoom: 19,
            attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
        }).addTo(map);

        // Verandert de kaart van formaat (bijv. doordat er een scrollbalk verschijnt), dan moet Leaflet
        // opnieuw meten; anders worden routes aan de rand afgeknipt.
        if ('ResizeObserver' in window) {
            new ResizeObserver(function () { map.invalidateSize(); }).observe(map.getContainer());
        }
        return map;
    }

    function pin(kind) {
        return L.divIcon({ className: 'pin-marker pin-' + kind, iconSize: [18, 18], iconAnchor: [9, 9] });
    }

    function taxiIcon() {
        return L.divIcon({ className: 'taxi-marker', html: 'TAXI', iconSize: [40, 22], iconAnchor: [20, 11] });
    }

    function money(amount, currency) {
        return new Intl.NumberFormat(locale, { style: 'currency', currency: currency }).format(amount);
    }

    function kilometers(meters) {
        return (meters / 1000).toLocaleString(locale, { minimumFractionDigits: 1, maximumFractionDigits: 1 }) + ' km';
    }

    function minutes(seconds) {
        const total = Math.round(seconds / 60);
        return total < 60 ? total + ' min' : Math.floor(total / 60) + ' u ' + String(total % 60).padStart(2, '0') + ' min';
    }

    function time(isoDate) {
        return new Date(isoDate).toLocaleTimeString(locale, { hour: '2-digit', minute: '2-digit' });
    }

    return { create, pin, taxiIcon, money, kilometers, minutes, time };
})();
