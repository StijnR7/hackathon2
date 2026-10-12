// Ritpagina's: kaart met geplande, gereden en nog te rijden route.
// Op het live scherm (reiziger of chauffeur) wordt de stand elke 2 seconden opgehaald.
(function () {
    'use strict';

    const dataElement = document.getElementById('trip-data');
    if (!dataElement || !window.L) {
        return;
    }

    const POLL_INTERVAL_MS = 2000;
    const MAX_POLL_INTERVAL_MS = 30000;

    const data = JSON.parse(dataElement.textContent);
    const map = HitchMap.create('trip-map', data.pickup, 14);

    const plannedLine = L.polyline(data.planned, { color: '#6c757d', weight: 5, opacity: 0.4 }).addTo(map);
    const remainingLine = L.polyline(data.remaining, { color: '#0d6efd', weight: 5, dashArray: '8 10' }).addTo(map);
    const drivenLine = L.polyline(data.driven, { color: '#0d6efd', weight: 6, opacity: 0.9 }).addTo(map);
    L.marker(data.pickup, { icon: HitchMap.pin('pickup'), title: 'Ophaallocatie' }).addTo(map);
    L.marker(data.destination, { icon: HitchMap.pin('destination'), title: 'Bestemming' }).addTo(map);
    const taxiMarker = data.taxi && data.status === 'InProgress'
        ? L.marker(data.taxi, { icon: HitchMap.taxiIcon(), title: 'Taxi', zIndexOffset: 1000 }).addTo(map)
        : null;

    const bounds = L.latLngBounds([data.pickup, data.destination]);
    [plannedLine, drivenLine].forEach(function (line) {
        if (line.getLatLngs().length > 0) {
            bounds.extend(line.getBounds());
        }
    });
    map.fitBounds(bounds, { padding: [30, 30], animate: false });

    const live = document.getElementById('live-trip');
    if (!live || data.status !== 'InProgress') {
        return;
    }

    const isDriverScreen = live.dataset.role === 'driver';
    const statusUrl = live.dataset.statusUrl;
    const summaryUrl = live.dataset.summaryUrl;
    const offRouteBanner = document.getElementById('off-route-banner');
    let pollInterval = POLL_INTERVAL_MS;

    function field(name) {
        return live.querySelector('[data-field="' + name + '"]');
    }

    function setText(name, text) {
        const element = field(name);
        if (element) {
            element.textContent = text;
        }
    }

    function renderAlerts(alerts) {
        const list = field('alerts');
        if (!list) {
            return;
        }
        const visible = list.dataset.onlyPassenger === 'true'
            ? alerts.filter(function (alert) { return alert.fromPassenger; })
            : alerts;
        if (visible.length === 0) {
            return;
        }

        // Nieuwste melding bovenaan. textContent (geen innerHTML) voorkomt XSS via berichten.
        list.replaceChildren.apply(list, visible.slice().reverse().map(function (alert) {
            const item = document.createElement('li');
            item.className = 'alert-item alert-item-' + alert.severity;
            const timeElement = document.createElement('span');
            timeElement.className = 'text-muted small me-1';
            timeElement.textContent = HitchMap.time(alert.createdAt);
            item.append(timeElement);
            if (alert.fromPassenger && !isDriverScreen) {
                const badge = document.createElement('span');
                badge.className = 'badge text-bg-secondary me-1';
                badge.textContent = 'Jouw bericht';
                item.append(badge);
            }
            item.append(document.createTextNode(alert.message));
            return item;
        }));
    }

    function render(status) {
        drivenLine.setLatLngs(status.driven);
        remainingLine.setLatLngs(status.remaining);
        if (taxiMarker && status.taxiPosition) {
            taxiMarker.setLatLng(status.taxiPosition);
            if (!map.getBounds().contains(status.taxiPosition)) {
                map.panTo(status.taxiPosition);
            }
        }

        setText('currentPrice', HitchMap.money(status.currentPrice, status.currencyCode));
        setText('estimatedFinalPrice', HitchMap.money(status.estimatedFinalPrice, status.currencyCode));
        setText('driven', HitchMap.kilometers(status.drivenDistanceMeters));
        setText('remaining', HitchMap.kilometers(status.remainingDistanceMeters));
        setText('elapsed', HitchMap.minutes(status.elapsedSeconds));
        setText('status', status.statusText);

        const progress = field('progress');
        if (progress) {
            progress.style.width = status.progressPercent + '%';
            progress.parentElement.setAttribute('aria-valuenow', String(Math.round(status.progressPercent)));
        }

        const estimate = field('estimatedFinalPrice');
        if (estimate) {
            estimate.classList.toggle('text-danger', status.isPriceAboveEstimate);
        }
        if (offRouteBanner) {
            offRouteBanner.classList.toggle('d-none', !status.isOffRoute);
        }
        renderAlerts(status.alerts);
    }

    async function poll() {
        try {
            const response = await fetch(statusUrl, { headers: { Accept: 'application/json' }, cache: 'no-store' });
            if (!response.ok) {
                throw new Error('HTTP ' + response.status);
            }
            const status = await response.json();
            render(status);
            pollInterval = POLL_INTERVAL_MS;

            if (status.isFinished) {
                if (summaryUrl) {
                    window.location.href = summaryUrl;
                }
                return; // klaar: niet meer verversen
            }
        } catch (error) {
            // Geen verbinding (bijv. tunnel): rustig opnieuw proberen in plaats van de server te bestoken.
            pollInterval = Math.min(pollInterval * 2, MAX_POLL_INTERVAL_MS);
            console.warn('Status ophalen mislukt, nieuwe poging over ' + pollInterval / 1000 + ' s.', error);
        }
        window.setTimeout(poll, pollInterval);
    }

    poll();
})();
