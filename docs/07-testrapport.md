# 07 – Testrapport

**Werkproces:** B1-K1-W4 Test software · **Testplan:** [06-testplan.md](06-testplan.md)

| | |
|---|---|
| Testobject | HitchTracker MVP, versie `v1.0.0` |
| Testdatum | 12-10-2026 |
| Uitgevoerd door | *(naam student)* |
| Omgeving | Windows 11, .NET SDK 10.0.400, Microsoft Edge (Chromium) desktop + mobiele weergave 375×812, SQLite |

## 1. Samenvatting

| Testsoort | Aantal | Geslaagd | Gefaald | Resultaat |
|---|---:|---:|---:|---|
| Unittests | 76 (55 testgevallen, deels met meerdere testwaarden) | 76 | 0 | ✅ |
| Integratietests | 11 | 11 | 0 | ✅ |
| Acceptatietests | 13 | 13 | 0 | ✅ (na herstel van B-01 en B-10) |
| **Totaal** | **100** | **100** | **0** | |

**Codedekking** (coverlet, alle geautomatiseerde tests): **93 % van de regels**, 77 % van de beslissingen (branches).
Bedrijfslogica (`Services`): 96–100 % per klasse. Niet gedekt zijn vooral de achtergrondlussen (worden indirect
via de simulator-tests geraakt) en enkele foutpaden in controllers.

**Conclusie:** alle Must- en Should-user stories voldoen aan hun acceptatiecriteria. Tijdens het testen zijn 10
bevindingen gedaan; 7 zijn hersteld (met een regressietest waar mogelijk), 3 kleine bevindingen staan als
[verbetervoorstel](08-verbetervoorstellen.md). Het product is geschikt om als MVP op te leveren.

## 2. Unittests

Uitgevoerd met `dotnet test --filter "FullyQualifiedName~Unit"`. Alle 76 testuitvoeringen geslaagd (duur ± 5 s).

| Groep | ID's | Resultaat | Conclusie |
|---|---|---|---|
| Geo-rekenwerk | UT-01 – UT-10 | 16/16 ✅ | Afstanden kloppen tot op 1 m met referentiewaarden; projectie, deelroutes en grenzen (begin/einde route, lege route) werken. |
| Prijs en tarief | UT-11 – UT-18 | 18/18 ✅ | De prijsformule, afronding en het minimumtarief kloppen. Tarieven over middernacht en de lokale tijd (incl. zomertijd) worden goed gekozen. |
| Routebewaking | UT-19 – UT-23 | 5/5 ✅ | Waarschuwing precies boven 100 m; hysterese voorkomt herhaalde meldingen bij GPS-ruis. |
| Ritlogica | UT-24 – UT-46 | 26/26 ✅ | Alle status-overgangen worden bewaakt, invoer wordt in de service zelf gevalideerd, GPS-fouten worden genegeerd. |
| Simulatie en privacy | UT-47 – UT-51 | 5/5 ✅ | Rijden over de geplande route geeft precies de geschatte prijs; omrijden geeft beide waarschuwingen en een hogere prijs. Bewaartermijnen worden correct toegepast. |
| Routeplanner | UT-52 – UT-55 | 6/6 ✅ | OSRM-antwoord wordt correct verwerkt; bij elke soort storing valt de app terug op de offline route (NF-05 aangetoond). |

## 3. Integratietest

Uitgevoerd met `dotnet test --filter "FullyQualifiedName~Integration"`: de complete applicatie draait in het geheugen
(echte middleware, controllers, Razor-views, EF Core en SQLite), aangestuurd met echte HTTP-verzoeken. De routeplanner
staat op offline en de simulator wordt door de test gestuurd, zodat de test herhaalbaar is en geen internet nodig heeft.

| ID | Scenario | Resultaat | Waarneming / conclusie |
|---|---|---|---|
| IT-01 | Startpagina | ✅ | Steden en plaatsen uit de database worden getoond. |
| IT-02 | Volledige rit | ✅ | Plannen → 302 naar schatting → start → 302 naar live → status-API geeft prijs, gereden en resterende route → na aankomst `isFinished` → eindprijs "Eerlijke prijs". Alle lagen werken samen. |
| IT-03 | Zonder anti-forgery token | ✅ | 400: CSRF-bescherming werkt. |
| IT-04 | Zonder bestemming | ✅ | Pagina toont de validatiemelding; geen rit aangemaakt. |
| IT-05 | Kaartcoördinaten | ✅ | `51.503300` wordt correct gelezen (invariante cultuur); zonder deze instelling zou een Nederlandse server er 51503300 van maken. |
| IT-06 | Voortijdig beëindigen | ✅ | Redirect naar eindprijs met melding. |
| IT-07 | XSS in bericht | ✅ | `<script>` wordt als `&lt;script&gt;` weergegeven. |
| IT-08 | Onbekende rit | ✅ | 404 voor pagina en API, nette foutpagina. |
| IT-09 | Taxi-API | ✅ | Zonder sleutel en met de sleutel van een andere taxi 401; met de juiste sleutel wordt de positie verwerkt. |
| IT-10 | Beveiligingsheaders | ✅ | CSP, `nosniff`, `DENY`, `Referrer-Policy` aanwezig. |
| IT-11 | Beoordeling | ✅ | Beoordeling met melding opgeslagen en getoond. |

**Conclusie integratietest:** de onderdelen werken als geheel samen en de beveiligingsmaatregelen uit het ontwerp
zijn aantoonbaar actief. Bij de eerste uitvoering faalde IT-02 (bevinding B-03, zie §5); na herstel slagen alle tests.

## 4. Acceptatietest

Handmatig uitgevoerd in Microsoft Edge met de echte routeplanner (OSRM) en kaart, volgens de scenario's in het testplan.

| ID | Story | Resultaat | Waarneming |
|---|---|---|---|
| AT-01 | US-01 AC3 | ✅ | Beide meldingen verschijnen boven het formulier. |
| AT-02 | US-01/02 | ✅ | Big Ben → Tower of London: **£ 20,71** (max. £ 23,45), 12 min, 4,6 km, opbouw instap £ 3,80 + afstand + tijd, route via OpenStreetMap (OSRM) op de kaart. |
| AT-03 | US-01 AC2 | ✅ | Twee klikken op de kaart in Amsterdam → schatting € 22,31 met "Punt op de kaart (52.3752, 4.9279)". De kaartklik schakelt na de ophaallocatie automatisch naar "Bestemming". |
| AT-04 | US-03 | ✅ | Londen 's middags "Dagtarief", Parijs "Tarif B (dag)". (Nachttarief aangetoond met UT-17.) |
| AT-05 | US-04/05 | ✅ | Taxi beweegt elke 2 s; gereden lijn groeit, gestippelde lijn krimpt; huidige prijs, verwachte eindprijs, km en voortgangsbalk lopen op zonder verversen. |
| AT-06 | US-06 | ✅ | Met omrijden: rode banner en melding "de taxi wijkt 114 meter af", daarna "verwachte eindprijs (£ 23,55) is hoger dan de bovengrens (£ 23,45)"; verwachte eindprijs rood. |
| AT-07 | US-06 AC3 | ✅ | Groene melding "De taxi rijdt weer over de geplande route" toen de taxi terugkwam. |
| AT-08 | US-08/12 | ✅ | Standaardbericht en eigen bericht "Kunt u bij de hoofdingang stoppen?" verschijnen met tijd op het chauffeursscherm. |
| AT-09 | US-07 | ✅ | Bevestigingsvraag "Weet je zeker…"; eindprijs € 7,32 (meterstand) met "Je hebt deze rit voortijdig beëindigd". |
| AT-10 | US-09/10 | ✅ *(na herstel B-01)* | Met omweg: **£ 29,91** "Hoger dan geschat … £ 9,20 meer", 7,1 km i.p.v. 4,6 km. Zonder omweg (Amsterdam Centraal → Rijksmuseum): eerst € 23,17 bij schatting € 23,07 ❌ (B-01); na herstel **€ 23,07 = € 23,07** "Eerlijke prijs" ✅. |
| AT-11 | US-11 | ✅ | 4 sterren + "Vriendelijke chauffeur" → "Bedankt voor je beoordeling!". Bij een te hoge prijs staat "te veel betaald" al aangevinkt. |
| AT-12 | US-13 | ✅ | Live link geopend in een tweede, losse browsersessie: zelfde rit live zichtbaar. |
| AT-13 | NF-01 | ✅ | Op 375 px breed geen horizontale scroll (`scrollWidth = 375`), knoppen en kaart bruikbaar. |

**Conclusie acceptatietest:** alle acceptatiecriteria van de Must- en Should-stories worden gehaald. De belangrijkste
belofte aan de klant – "bij de geplande route betaal je precies de schatting, bij omrijden word je gewaarschuwd" –
is aangetoond met echte routes in Londen en Amsterdam.

## 5. Bevindingen

| ID | Gevonden bij | Omschrijving | Ernst | Oorzaak | Status |
|---|---|---|---|---|---|
| B-01 | AT-10 | Eindprijs zonder omweg € 0,10 hoger dan de schatting | Hoog (kernbelofte) | OSRM laat de route op de dichtstbijzijnde weg beginnen; de meter begon op het gebouw → ca. 40 m extra | ✅ Hersteld: taxi start op het eerste routepunt. Regressietest UT-51. |
| B-02 | Eigen test (ontwikkeling) | Na "Bereken" een 404-pagina | Hoog | Twee acties met dezelfde naam; de link werd naar de POST-route gemaakt | ✅ Hersteld: POST-actie hernoemd naar `Plan` (`/ritten/plannen`). Gedekt door IT-02. |
| B-03 | IT-02 | `€` stond als `&#x20AC;` in de HTML | Laag | Standaard-encoder van Razor encodeert alle niet-ASCII-tekens | ✅ Hersteld: encoder staat Unicode toe (`<`, `>`, `&`, quotes blijven ge-encodeerd). |
| B-04 | UT-48 | Met de offline routeplanner leverde "omrijden" geen langere rit op | Middel (demo) | Punt binnen het rechthoekige gebied tussen start en eind is bij een "Manhattan"-route geen omweg | ✅ Hersteld: beide kanten proberen, langste omweg kiezen. |
| B-05 | AT-11 | Label "1 sterren" | Laag | Meervoud vast in de tekst | ✅ Hersteld: "1 ster". |
| B-06 | AT-09 | Tariefnaam in kleine letters ("tarif b (dag)") | Laag | `ToLowerInvariant()` op de naam | ✅ Hersteld. |
| B-07 | Eigen test | In een verborgen browservenster werden routes op de kaart afgeknipt | Laag | Leaflet meet de kaart niet opnieuw als het formaat verandert | ✅ Hersteld: `ResizeObserver` + `invalidateSize()`. |
| B-10 | AT-03 | Opbouw "5,5 km × € 2,50 = € 13,79" lijkt niet te kloppen | Middel (vertrouwen) | Afstand afgerond op 1 decimaal, berekening met exacte afstand | ✅ Hersteld: opbouw toont 2 decimalen ("5,52 km") en minuten met 1 decimaal. |
| B-08 | AT-05/06 | Tijden van meldingen staan op de live pagina in de tijdzone van het apparaat, op de eindprijspagina in die van de stad | Laag | Live pagina formatteert in de browser | 🔶 Open → verbetervoorstel V-06 |
| B-09 | AT-06 | Na een afwijking was de "nog te rijden" route soms een rechte (offline) lijn | Middel | Publieke OSRM-demo-server heeft een gebruikslimiet; bij herhaalde aanvragen valt de app terug | 🔶 Open → verbetervoorstel V-02 |
| B-11 | AT-10 | Grijze geplande route is onzichtbaar waar de gereden route er precies overheen ligt | Laag | Lijnen liggen op elkaar | 🔶 Open → verbetervoorstel V-07 |

## 6. Eindconclusie

- **Functioneel:** de drie epics van de opdrachtgever zijn volledig gerealiseerd en aantoonbaar getest.
- **Kwaliteit:** 87 geautomatiseerde tests (100 % geslaagd, 93 % regeldekking) vormen een regressieset die bij elke wijziging draait.
- **Security en privacy:** CSRF, XSS, toegangscontrole op de taxi-API, beveiligingsheaders en bewaartermijnen zijn met tests aangetoond.
- **Aanbeveling:** oplevering als MVP. Voor productie eerst V-01 (koppeling echte taximeter) en V-02 (eigen routeserver) uitvoeren; zie [verbetervoorstellen](08-verbetervoorstellen.md).
