// Pagina "Rit plannen": ophaallocatie en bestemming kiezen via de lijst of door op de kaart te klikken.
(function () {
    'use strict';

    const dataElement = document.getElementById('plan-data');
    if (!dataElement || !window.L) {
        return;
    }

    const data = JSON.parse(dataElement.textContent);
    const map = HitchMap.create('plan-map', data.center, 13);
    const kinds = ['pickup', 'destination'];
    const markers = {};
    const fields = {};

    kinds.forEach(function (kind) {
        fields[kind] = {
            select: document.querySelector('[data-location="' + kind + '"]'),
            lat: document.querySelector('[data-location-lat="' + kind + '"]'),
            lng: document.querySelector('[data-location-lng="' + kind + '"]')
        };
    });

    const placesById = new Map(data.places.map(function (place) { return [String(place.id), place]; }));

    data.places.forEach(function (place) {
        L.circleMarker(place.position, { radius: 7, color: '#8a6d00', weight: 2, fillColor: '#ffc107', fillOpacity: 0.9 })
            .bindTooltip(place.name)
            .on('click', function (event) {
                L.DomEvent.stopPropagation(event);
                choosePlace(currentTarget(), place);
            })
            .addTo(map);
    });

    function currentTarget() {
        const checked = document.querySelector('input[name="map-target"]:checked');
        return checked ? checked.value : 'pickup';
    }

    function setTarget(kind) {
        const radio = document.getElementById('target-' + kind);
        if (radio) {
            radio.checked = true;
        }
    }

    function setMarker(kind, position) {
        if (markers[kind]) {
            map.removeLayer(markers[kind]);
            delete markers[kind];
        }
        if (position) {
            const label = kind === 'pickup' ? 'Ophaallocatie' : 'Bestemming';
            markers[kind] = L.marker(position, { icon: HitchMap.pin(kind), title: label }).addTo(map);
        }
    }

    function choosePlace(kind, place) {
        fields[kind].select.value = String(place.id);
        fields[kind].lat.value = '';
        fields[kind].lng.value = '';
        setMarker(kind, place.position);
        afterChoice(kind);
    }

    function choosePoint(kind, latlng) {
        fields[kind].select.value = '';
        fields[kind].lat.value = latlng.lat.toFixed(6);
        fields[kind].lng.value = latlng.lng.toFixed(6);
        setMarker(kind, [latlng.lat, latlng.lng]);
        afterChoice(kind);
    }

    // Na het kiezen van de ophaallocatie is de bestemming aan de beurt.
    function afterChoice(kind) {
        if (kind === 'pickup') {
            setTarget('destination');
        }
    }

    function syncFromFields(kind) {
        const field = fields[kind];
        const place = placesById.get(field.select.value);
        if (place) {
            setMarker(kind, place.position);
        } else if (field.lat.value && field.lng.value) {
            setMarker(kind, [parseFloat(field.lat.value), parseFloat(field.lng.value)]);
        } else {
            setMarker(kind, null);
        }
    }

    map.on('click', function (event) {
        choosePoint(currentTarget(), event.latlng);
    });

    kinds.forEach(function (kind) {
        fields[kind].select.addEventListener('change', function () {
            setTarget(kind);
            if (fields[kind].select.value) {
                fields[kind].lat.value = '';
                fields[kind].lng.value = '';
            }
            syncFromFields(kind);
        });
        // Na een validatiefout staan de eerder gekozen locaties weer op de kaart.
        syncFromFields(kind);
    });
})();
