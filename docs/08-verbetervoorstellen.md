# 08 – Verbetervoorstellen

**Werkproces:** B1-K1-W5 Doet verbetervoorstellen voor de software

## 1. Gebruikte informatiebronnen

De voorstellen zijn systematisch afgeleid uit alle beschikbare bronnen:

| Bron | Wat leverde het op |
|---|---|
| [Testrapport](07-testrapport.md) – open bevindingen B-08, B-09, B-11 | Concrete tekortkomingen in de huidige versie |
| Testrapport – codedekking | Minder gedekte delen (achtergrondlussen, foutpaden controllers) |
| Acceptatietest – eigen observaties tijdens gebruik | Bruikbaarheid op telefoon en in de taxi |
| [Concurrentieanalyse](02-functioneel-ontwerp.md#6-concurrentieanalyse) | Functies die concurrenten wel hebben |
| Afbakening "Won't (nu)" in het [plan van aanpak](01-plan-van-aanpak.md#4-prioritering-moscow) | Bewust uitgestelde wensen van de opdrachtgever |
| Product review / feedback docent *(aanvullen na de review)* | |

## 2. Voorstellen

Prioriteit: **H** = nodig voor productie, **M** = duidelijke meerwaarde, **L** = nice-to-have. Inspanning in uren.

| ID | Voorstel | Bron | Waarom | Werkzaamheden | Prio | Uren |
|---|---|---|---|---|---|---:|
| V-01 | **Koppeling met echte taximeter / chauffeursapp** | Afbakening, ontwerp | Het MVP gebruikt een simulator. De API (`/api/taxi/...`) bestaat al. | API-sleutels per taxi uitgeven en intrekbaar maken (beheerpagina), lichte chauffeursapp (PWA) die elke 3 s GPS doorgeeft, simulator uitzetten, testen met één echte taxi | H | 24 |
| V-02 | **Eigen OSRM-routeserver** | B-09 | De publieke demo-server heeft een gebruikslimiet; bij herrouteren viel de app soms terug op de offline benadering. | OSRM in Docker met OpenStreetMap-extract per land, `Routing:OsrmBaseUrl` aanpassen, cache voor identieke routeaanvragen | H | 8 |
| V-03 | **Live updates via SignalR** i.p.v. polling | Technisch ontwerp | Minder dataverbruik in het buitenland (roaming) en directe meldingen. | SignalR-hub per rit, `trip.js` aanpassen, polling als terugval houden, integratietest | M | 8 |
| V-04 | **Meertaligheid (EN, DE, FR, ES) en berichten vertaald voor de chauffeur** | Doelgroep, concurrentie | Toeristen spreken lang niet altijd Nederlands; de chauffeur moet het bericht in zijn of haar eigen taal zien. | ASP.NET Core-lokalisatie (resx), taalkeuze, standaardberichten per taal, chauffeurstaal op taxi | M | 12 |
| V-05 | **Adres zoeken** (geocoding) naast plekken en kaartklik | Acceptatietest (eigen observatie) | Hotels en restaurants staan niet in de lijst. | Zoekveld met Nominatim/Photon (eigen server i.v.m. gebruikslimiet), debounce, validatie werkgebied | M | 8 |
| V-06 | **Tijden overal in de tijdzone van de stad** | B-08 | Consistentie tussen live pagina en eindprijspagina. | Tijdzone van de stad meesturen in de status-API, `Intl.DateTimeFormat` met `timeZone` | L | 1 |
| V-07 | **Geplande route beter zichtbaar** | B-11 | Waar gereden en gepland samenvallen, is de geplande route onzichtbaar. | Geplande route als bredere lichte "rand" onder de gereden lijn tekenen | L | 1 |
| V-08 | **Beheeromgeving voor tarieven en klachten** | Afbakening | Tarieven wijzigen nu via seed-data; meldingen "te veel betaald" komen alleen in de log. | Inlog voor beheerders (ASP.NET Core Identity + 2FA), CRUD tarieven/steden, klachtenlijst met route als bewijs | M | 20 |
| V-09 | **Echte, officiële tarieven** | Testrapport (dummydata) | De schatting moet juridisch kloppen. | Tarieven van TfL (Londen), gemeente Amsterdam en Préfecture de Police (Parijs) opzoeken en invoeren, incl. toeslagen (bagage, luchthaven) | H | 4 |
| V-10 | **Verkeersafhankelijke schatting** | Acceptatietest | De rijtijd van OSRM is zonder verkeer; de bandbreedte vangt dat nu grof op. | Historische ritduur per uur/dag gebruiken om de bandbreedte te verfijnen | L | 12 |
| V-11 | **Toegankelijkheidsaudit (WCAG 2.2 AA)** | Ethiek | Ook voor reizigers met een beperking. | Audit met axe/Lighthouse + schermlezer, bevindingen oplossen | M | 6 |
| V-12 | **End-to-end-tests in de browser** | Codedekking (JavaScript niet automatisch getest) | `plan.js` en `trip.js` zijn nu alleen handmatig getest. | Playwright-tests voor kaartklik, live update en doorsturen naar eindprijs | M | 6 |

## 3. Planning

Uitgaande van dezelfde capaciteit als het project (21 uur per week), in volgorde van prioriteit en afhankelijkheden
(V-02 vóór V-01, omdat echte ritten veel meer routeaanvragen geven; V-08 vóór de klachtenafhandeling in productie).

| Sprint | Week | Voorstellen | Uren | Resultaat |
|---|---|---|---:|---|
| 4 | 1 | V-06, V-07, V-09, V-02 | 14 | Kleine bevindingen opgelost, officiële tarieven, eigen routeserver |
| 4 | 1 | V-12 | 6 | Browsertests voor de hoofdflow |
| 5 | 2 | V-01 | 21 | Echte taxi kan posities doorgeven (pilot met één taxi) |
| 6 | 3 | V-01 (afronden, 3 u), V-03, V-11 | 3 + 8 + 6 = 17 | Live updates via SignalR, toegankelijkheid verbeterd |
| 7 | 4 | V-08 | 20 | Beheeromgeving |
| 8 | 5 | V-04, V-05 | 20 | Meertalig, adres zoeken |
| later | – | V-10 | 12 | Nadat er genoeg echte ritdata is (≥ 3 maanden) |

**Totaal:** 110 uur (± 5,5 weken). De planning is haalbaar: elke sprint blijft onder 21 uur, met ruimte voor review
en retrospective. Risico: V-01 hangt af van medewerking van een taxibedrijf; als dat uitloopt, schuiven V-03/V-11 naar voren.
