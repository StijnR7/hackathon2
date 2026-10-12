# 06 – Testplan

**Werkproces:** B1-K1-W4 Test software

## 1. Doel en scope

Aantonen dat HitchTracker voldoet aan de [user stories en acceptatiecriteria](02-functioneel-ontwerp.md#2-user-stories-en-acceptatiecriteria)
en de niet-functionele eisen (privacy, security, mobiel gebruik). In scope: alle functionaliteit van het MVP.
Buiten scope: performance onder hoge belasting, echte taxikastjes (vervangen door simulator en API-tests).

## 2. Testaanpak

De aanpak volgt **TMap Next** (risicogebaseerd testen) met de **ISTQB**-testniveaus. Hoe groter het risico van een
onderdeel, hoe zwaarder het getest wordt:

| Onderdeel | Risico | Waarom | Testzwaarte |
|---|---|---|---|
| Prijsberekening, tarief per tijdstip | Hoog | Kern van het product: een fout bedrag ondermijnt het vertrouwen | Unittests met grenswaarden + controle in acceptatietest |
| Routebewaking (afwijking, hysterese) | Hoog | Valse of gemiste waarschuwingen | Unittests + simulatie + acceptatietest met omrijden |
| Status-overgangen rit | Middel | Dubbel starten/beëindigen, gelijktijdigheid | Unittests op elke ongeldige overgang |
| Security (CSRF, XSS, API-sleutel) | Hoog | Misbruik, privacy | Integratietests |
| Privacy (bewaartermijnen) | Middel | AVG | Unittest op opschoonservice |
| Routeplanner (extern) | Middel | Uitval tijdens gebruik | Unittests met nep-HTTP-antwoorden (fouten, ongeldige JSON) |
| Gebruikersinterface | Middel | Bruikbaarheid op telefoon | Acceptatietest in browser, ook op 375 px breed |

### Testsoorten en -technieken

| Testniveau | Wat | Hoe | Technieken |
|---|---|---|---|
| **Unittest** | Losse klassen: `GeoMath`, `FareRates`, `Tariff`, `TariffSelector`, `TripMonitor`, `TripService`, `TripSimulator`, `DataRetentionService`, routeplanners | xUnit, geautomatiseerd; `TripService` tegen een echte SQLite-database in het geheugen met een vaste klok | Equivalentieklassen, grenswaarden (bijv. 100 m / 50 m, minimumtarief, uur 20 en 5), foutgevallen |
| **Integratietest** | Samenwerking van middleware, controllers, views, services, database en API via echte HTTP-verzoeken | xUnit + `WebApplicationFactory`, offline routeplanner, simulator handmatig gestuurd | Proces-/scenariotest (volledige rit), security-tests |
| **Acceptatietest** | Voldoet het product aan de acceptatiecriteria van de opdrachtgever? | Handmatig in de browser volgens testscenario's (hieronder), met echte routeplanner en kaart | Use-case-test, foutraden |
| **Regressietest** | Blijft alles werken na een wijziging? | Volledige geautomatiseerde set bij elke wijziging (`dotnet test`) | – |

**Ingangscriteria:** de code compileert zonder waarschuwingen; de database-migratie is up-to-date.
**Uitgangscriteria:** alle geautomatiseerde tests slagen; alle acceptatietests van Must- en Should-stories zijn geslaagd; open bevindingen zijn beoordeeld en vertaald naar verbetervoorstellen.

**Testomgeving:** Windows 11, .NET SDK 10.0.400, Microsoft Edge (Chromium) desktop en mobiele weergave 375×812,
SQLite. Testdata: de dummydata uit `DbSeeder` (3 steden, 27 plaatsen, 9 taxi's).

## 3. Testgevallen – geautomatiseerd

De code van elk testgeval staat als commentaar bij de test (bijv. `// UT-24`).

### Unittests

| ID | Test | User story / eis | Testdata → verwacht resultaat |
|---|---|---|---|
| UT-01 | Afstand 1° lengtegraad op de evenaar | US-02 | (0,0)–(0,1) → 111,15–111,25 km |
| UT-02 | Afstand Big Ben – Tower of London | US-02 | → 3,3–3,5 km |
| UT-03 | Lengte van een route | US-02 | L-route 1 km + 1 km → 2 km |
| UT-04 | Punt op afstand langs route (4 waarden) | US-04 | 0, 500, 1500, 99.999 m → juiste plek, begrensd op einde |
| UT-05 | Loodrechte afstand tot route | US-06 | 150 m naast de route → 149–151 m |
| UT-06 | Geen terugsprong in voortgang | US-06 | punt bij start, al 1500 m gereden → voortgang ≥ 1500 m |
| UT-07 | Deelroute behoudt hoekpunten | US-04 | 500–1500 m → begin, hoek, eind; 1 km |
| UT-08 | Resterende route | US-04 | vanaf 1000 m → 1 km, eindigt op bestemming |
| UT-09 | Lege route geweigerd | robuustheid | → `ArgumentException` |
| UT-10 | Geldige coördinaten (4 waarden) | validatie | lat 91, lng −181, NaN → ongeldig |
| UT-11 | Prijs = instap + km + minuten | US-02 | 5 km, 12 min Londen dag → £ 21,40 |
| UT-12 | Minimumtarief | US-02 | Parijs 0,5 km 1 min → € 7,30 |
| UT-13 | Afronding op centen | US-02 | 1,234 km, 77 s → 2,71 en 0,71 |
| UT-14 | Negatieve afstand/duur (2 waarden) | validatie | → `ArgumentOutOfRangeException` |
| UT-15 | Tariefkopie onafhankelijk | US-03 AC3 | origineel wijzigen → kopie gelijk |
| UT-16 | Tarief geldt op uur (8 grenswaarden) | US-03 | 5–20 op 4/5/19/20; 20–5 op 23/2/12; hele dag |
| UT-17 | Tarief op lokale tijd (3 waarden) | US-03 AC1 | 12/21/03 UTC in Londen (zomertijd) → dag/nacht/nacht |
| UT-18 | Specifiek tarief vóór hele-dag-tarief | US-03 | 23:00 UTC Amsterdam → nachttarief |
| UT-19 | Op route: geen melding | US-06 | 20 m naast route → op route |
| UT-20 | Van route af | US-06 AC1 | 150 m → afgeweken + melding |
| UT-21 | Hysterese: blijft afgeweken | US-06 AC4 | was af, nu 75 m → nog steeds af, geen nieuwe melding |
| UT-22 | Terug op route | US-06 AC3 | was af, nu 10 m → terug + melding |
| UT-23 | Rit zonder route | robuustheid | → `InvalidOperationException` |
| UT-24 | Schatting = tarief over route | US-02 | Amsterdam Centraal → Rijksmuseum → prijs, bovengrens > prijs, EUR |
| UT-25 | Punt op de kaart | US-01 AC2 | (52.36, 4.88) → naam "Punt op de kaart…" |
| UT-26 | Buiten werkgebied | US-01 AC4 | Rotterdam in Amsterdam → geweigerd |
| UT-27 | Plaats uit andere stad | validatie | Big Ben in Amsterdam → geweigerd |
| UT-28 | Ophalen = bestemming | US-01 AC4 | De Dam → De Dam → geweigerd |
| UT-29 | Geen locatie | US-01 AC3 | → geweigerd |
| UT-30 | Rit starten | US-05 | meter = instaptarief, taxi bezet |
| UT-31 | Twee keer starten | status | → `TripStateException` |
| UT-32 | Bezette taxi | validatie | → geweigerd |
| UT-33 | Halve rit over de route | US-04/05 | 50 % gereden, verwachte eindprijs = schatting, geen meldingen |
| UT-34 | Omrijden | US-06 AC1/AC2 | 1,5 km verkeerde kant op → afwijking + prijsmelding |
| UT-35 | GPS-sprong | robuustheid | 10 km in 10 s → genegeerd |
| UT-36 | Verouderde positie | robuustheid | tijdstip vóór vorige → genegeerd |
| UT-37 | Positie bij geplande rit | status | → `TripStateException` |
| UT-38 | Leeg bericht (3 waarden) | US-08 AC2 | "", "   ", null → geweigerd |
| UT-39 | Te lang bericht | US-08 AC2 | 201 tekens → geweigerd |
| UT-40 | Bericht verstuurd | US-08 AC3 | wordt getrimd en als reizigersbericht getoond |
| UT-41 | Voortijdig beëindigen | US-07 | eindprijs = meterstand, taxi vrij, melding |
| UT-42 | Twee keer beëindigen | status | → `TripStateException` |
| UT-43 | Beoordeling | US-11 | 4 sterren opgeslagen; tweede keer geweigerd |
| UT-44 | Beoordelen tijdens rit | US-11 AC2 | → geweigerd |
| UT-45 | Ongeldige sterren (2 waarden) | US-11 AC1 | 0 en 6 → geweigerd |
| UT-46 | Onbekende rit | robuustheid | → `TripNotFoundException` |
| UT-47 | Simulatie zonder omweg | US-09/10 | eindprijs = schatting (± € 0,02), geen afwijking |
| UT-48 | Simulatie met omweg | US-06/10 | eindprijs > bovengrens, afwijking- en prijsmelding |
| UT-49 | Omwegpunt ligt naast route | simulator | > 500 m van de route |
| UT-50 | Bewaartermijnen | NF-03 | posities na 30 d weg, rit na 90 d weg, schatting na 24 u weg, recente data blijft |
| UT-51 | Route begint op de weg (regressie B-01) | US-10 | route 40 m verschoven → eindprijs = schatting |
| UT-52 | OSRM-antwoord verwerken | US-02 | JSON → punten (lat/lng omgedraaid), URL met punt als decimaalteken |
| UT-53 | OSRM-uitval (3 waarden) | NF-05 | HTTP 500, "NoRoute", ongeldige JSON → offline route |
| UT-54 | Offline route consistent | NF-05 | afstand = lengte polyline, duur = afstand / 25 km/u |
| UT-55 | Ongeldige routepunten | validatie | 1 punt of lat 95 → geweigerd |

### Integratietests

| ID | Scenario | Eis | Verwacht resultaat |
|---|---|---|---|
| IT-01 | Startpagina | US-01 | Steden en plaatsen zichtbaar |
| IT-02 | Volledige rit: plannen → schatting → starten → live status → aankomst → eindprijs | E1–E3 | Juiste redirects, status-API met prijs en routes, eindpagina "Eerlijke prijs" |
| IT-03 | Plannen zonder anti-forgery token | NF-04 CSRF | 400 Bad Request |
| IT-04 | Plannen zonder bestemming | US-01 AC3 | Foutmelding op de pagina |
| IT-05 | Kaartcoördinaten met punt als decimaalteken | US-01 AC2 | Correct verwerkt, ongeacht de landinstelling van de server |
| IT-06 | Rit voortijdig beëindigen via de website | US-07 | Redirect naar eindprijs met "voortijdig beëindigd" |
| IT-07 | Bericht met `<script>` | NF-04 XSS | Wordt ge-encodeerd weergegeven |
| IT-08 | Onbekende rit (pagina en API) | robuustheid | 404 |
| IT-09 | Taxi-API zonder sleutel, met sleutel van andere taxi, met juiste sleutel | NF-04 toegang | 401, 401, 200 + positie verwerkt; aankomst melden 200 |
| IT-10 | Beveiligingsheaders | NF-04 | CSP, nosniff, DENY, Referrer-Policy aanwezig |
| IT-11 | Beoordeling met melding te veel betaald | US-11 | Opgeslagen en getoond |

## 4. Testgevallen – acceptatietest (handmatig)

Uitvoeren in de browser met de echte routeplanner. Resultaten in het [testrapport](07-testrapport.md#4-acceptatietest).

| ID | Story | Stappen | Testdata | Verwacht resultaat |
|---|---|---|---|---|
| AT-01 | US-01 AC3 | Klik direct op "Bereken reistijd en prijs" | geen | Meldingen "Kies een ophaallocatie…" en "Kies een bestemming…" |
| AT-02 | US-01, US-02 | Kies Londen, Big Ben → Tower of London, bereken | – | Prijs, bovengrens, reistijd, afstand, opbouw, route op kaart; route via OSRM |
| AT-03 | US-01 AC2 | Kies Amsterdam, klik twee punten op de kaart, bereken | twee kaartpunten | Schatting met "Punt op de kaart (…)" als namen |
| AT-04 | US-03 | Controleer tariefnaam bij schatting in Londen en Parijs overdag | – | "Dagtarief" resp. "Tarif B (dag)" |
| AT-05 | US-04, US-05 | Start rit (taxi kiezen), volg 30 s | – | Taxi beweegt, gereden lijn groeit, gestippelde lijn krimpt, prijzen en voortgang lopen op zonder verversen |
| AT-06 | US-06 | Start rit met "laat de chauffeur omrijden" | omrijden aan | Rode banner + melding met afstand; melding prijs boven bovengrens; verwachte eindprijs rood |
| AT-07 | US-06 AC3 | Laat de omgereden rit doorlopen | – | Groene melding "weer over de geplande route" als de taxi terugkomt |
| AT-08 | US-08, US-12 | Stuur standaardbericht en eigen bericht; open chauffeursscherm | "Kunt u bij de hoofdingang stoppen?" | Bevestiging; bericht zichtbaar op chauffeursscherm |
| AT-09 | US-07 | Klik "Rit nu beëindigen", bevestig | – | Bevestigingsvraag; eindprijspagina met meterstand en "voortijdig beëindigd" |
| AT-10 | US-09, US-10 | Laat rit zonder omweg en rit met omweg tot het eind rijden | – | Automatisch naar eindprijs; zonder omweg "Eerlijke prijs" en eindprijs = schatting; met omweg "Hoger dan geschat" met verschil |
| AT-11 | US-11 | Geef 4 sterren met toelichting | "Vriendelijke chauffeur" | "Bedankt voor je beoordeling!", beoordeling getoond |
| AT-12 | US-13 | Open de live link in een andere browser | link | Zelfde rit live zichtbaar |
| AT-13 | NF-01 | Live pagina op 375 × 812 | – | Geen horizontaal scrollen, alles bruikbaar |
