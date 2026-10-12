# 03 – Technisch ontwerp

**Werkproces:** B1-K1-W2 Maakt een technisch ontwerp voor software

Dit document vertaalt de [user stories](02-functioneel-ontwerp.md) naar een technisch ontwerp. Gebruikte schematechnieken:
architectuurdiagram, **klassendiagram**, **ERD**, **activiteitendiagram**, **sequentiediagram**, **toestandsdiagram**
(en in het functioneel ontwerp: use-casediagram en navigatiestroom). Alle diagrammen zijn in Mermaid geschreven, zodat
ze in GitHub worden weergegeven en onder versiebeheer staan.

## 1. Architectuur

```mermaid
flowchart LR
    subgraph Client["Browser (telefoon / scherm in taxi)"]
        V["Razor-pagina's + Bootstrap"]
        JS["plan.js / trip.js<br/>Leaflet-kaart, polling elke 2 s"]
    end

    subgraph Server["ASP.NET Core 10 (HitchTracker.Web)"]
        MW["Middleware<br/>security headers · rate limiting · foutpagina's"]
        HC["HomeController"]
        TC["TripsController<br/>(MVC, anti-forgery)"]
        API["TripsApiController<br/>TaxiApiController (API-sleutel)"]
        TS["TripService<br/>(bedrijfslogica)"]
        TM["TripMonitor<br/>(routebewaking)"]
        RS["IRoutingService<br/>Osrm / Offline"]
        SIM["TripSimulator +<br/>SimulationBackgroundService"]
        RET["DataRetentionService<br/>(AVG-opschoning)"]
        DB[("SQLite<br/>EF Core")]
    end

    OSRM["OSRM-routeplanner<br/>(OpenStreetMap)"]
    TILES["OpenStreetMap<br/>kaarttegels"]
    TAXI["Taxikastje /<br/>chauffeursapp"]

    V --> HC & TC
    JS -->|"GET /api/trips/{id}/status"| API
    JS --> TILES
    TC & API --> TS
    TS --> TM
    TS --> RS
    RS -->|HTTPS| OSRM
    SIM --> TS
    TAXI -.->|"POST posities (X-Taxi-Key)"| API
    TS & SIM & RET --> DB
```

**Lagen.** Controllers zijn dun: ze valideren invoer, roepen een service aan en kiezen een view. Alle regels
(prijs, status-overgangen, routebewaking) zitten in `Services` en `Models`, zodat ze los te testen zijn. De
simulator gebruikt **exact dezelfde** methode (`TripService.RecordPositionsAsync`) als de API voor een echt
taxikastje; de simulatie kan dus later zonder wijzigingen in de rest van het systeem vervangen worden.

## 2. Klassendiagram

```mermaid
classDiagram
    direction LR
    class City {
        +int Id
        +string Name
        +string Country
        +string CurrencyCode
        +string TimeZoneId
        +double CenterLat, CenterLng
        +double ServiceRadiusKm
        +ToLocalTime(utc) DateTime
    }
    class Tariff {
        +int Id
        +string Name
        +int StartHour
        +int EndHour
        +FareRates Rates
        +AppliesAt(localHour) bool
    }
    class FareRates {
        <<owned>>
        +decimal BaseFare
        +decimal PricePerKm
        +decimal PricePerMinute
        +decimal MinimumFare
        +Calculate(meters, seconds) FareBreakdown
        +Copy() FareRates
    }
    class FareBreakdown {
        <<record>>
        +decimal BaseFare
        +decimal DistanceCost
        +decimal TimeCost
        +decimal MinimumFareSurcharge
        +decimal Total
    }
    class Place {
        +int Id
        +string Name
        +string Category
        +double Lat, Lng
    }
    class Driver {
        +int Id
        +string FirstName
        +string LicenseNumber
        +double Rating
    }
    class Taxi {
        +int Id
        +string LicensePlate
        +string VehicleDescription
        +bool IsAvailable
        +string ApiKeyHash
    }
    class Trip {
        +Guid Id
        +TripStatus Status
        +string PickupName / DestinationName
        +List~GeoPoint~ PlannedRoute
        +double PlannedDistanceMeters
        +double PlannedDurationSeconds
        +FareRates Rates
        +decimal EstimatedPrice
        +decimal EstimatedPriceHigh
        +double DrivenDistanceMeters
        +double ElapsedSeconds
        +List~GeoPoint~ RemainingRoute
        +decimal CurrentPrice
        +decimal EstimatedFinalPrice
        +bool IsOffRoute
        +decimal? FinalPrice
        +int Version
        +IsFinished bool
        +IsPriceAboveEstimate bool
    }
    class TripStatus {
        <<enumeration>>
        Planned
        InProgress
        Completed
        EndedEarly
    }
    class TripPosition {
        +long Id
        +double Lat, Lng
        +DateTime RecordedAt
        +double DistanceFromRouteMeters
    }
    class TripAlert {
        +int Id
        +TripAlertType Type
        +string Message
        +DateTime CreatedAt
        +Severity string
    }
    class TripFeedback {
        +int Stars
        +string? Comment
        +bool ReportedOvercharge
    }
    class TripSimulation {
        +List~GeoPoint~ Path
        +double DistanceTravelledMeters
        +double SpeedMetersPerSecond
        +bool WithDetour
    }
    class GeoPoint {
        <<record struct>>
        +double Lat
        +double Lng
        +IsValid bool
    }

    class TripService {
        +PlanTripAsync(cityId, pickup, destination) Trip
        +StartTripAsync(tripId, taxiId) Trip
        +RecordPositionsAsync(tripId, reports) int
        +SendPassengerMessageAsync(tripId, text)
        +CompleteTripAsync(tripId) Trip
        +EndTripEarlyAsync(tripId) Trip
        +SubmitFeedbackAsync(tripId, stars, comment, report)
        +GetStatusAsync(tripId) TripStatusDto
    }
    class TripMonitor {
        +CheckPosition(trip, position) RouteCheck
    }
    class IRoutingService {
        <<interface>>
        +GetRouteAsync(waypoints) RouteResult
    }
    class OsrmRoutingService
    class OfflineRoutingService
    class TripSimulator {
        +CreateAsync(trip, withDetour)
        +StepAllAsync(elapsed) int
    }
    class GeoMath {
        <<static>>
        +Distance(a, b) double
        +PathLength(path) double
        +ProjectOnPath(point, path, min) PathProjection
        +SubPath(path, from, to) List~GeoPoint~
    }

    City "1" *-- "1..*" Tariff
    City "1" *-- "*" Place
    City "1" *-- "*" Taxi
    Taxi "*" --> "1" Driver
    Tariff *-- FareRates
    Trip *-- FareRates : kopie
    FareRates ..> FareBreakdown : maakt
    Trip "*" --> "1" City
    Trip "*" --> "0..1" Taxi
    Trip --> TripStatus
    Trip "1" *-- "*" TripPosition
    Trip "1" *-- "*" TripAlert
    Trip "1" *-- "0..1" TripFeedback
    Trip "1" -- "0..1" TripSimulation
    TripService ..> TripMonitor
    TripService ..> IRoutingService
    IRoutingService <|.. OsrmRoutingService
    IRoutingService <|.. OfflineRoutingService
    OsrmRoutingService ..> OfflineRoutingService : terugval
    TripSimulator ..> TripService
    TripMonitor ..> GeoMath
```

## 3. Datamodel (ERD)

```mermaid
erDiagram
    Cities ||--|{ Tariffs : "heeft"
    Cities ||--o{ Places : "heeft"
    Cities ||--o{ Taxis : "heeft"
    Drivers ||--o{ Taxis : "rijdt in"
    Cities ||--o{ Trips : "in"
    Taxis |o--o{ Trips : "rijdt"
    Trips ||--o{ TripPositions : "gereden route"
    Trips ||--o{ TripAlerts : "meldingen"
    Trips ||--o| TripFeedback : "beoordeling"
    Trips ||--o| TripSimulations : "demo"

    Cities {
        int Id PK
        text Name
        text Country
        text CurrencyCode "ISO 4217"
        text TimeZoneId "IANA"
        real CenterLat
        real CenterLng
        real ServiceRadiusKm
    }
    Tariffs {
        int Id PK
        int CityId FK
        text Name
        int StartHour
        int EndHour
        decimal Rates_BaseFare
        decimal Rates_PricePerKm
        decimal Rates_PricePerMinute
        decimal Rates_MinimumFare
    }
    Places {
        int Id PK
        int CityId FK
        text Name
        text Category
        real Lat
        real Lng
    }
    Drivers {
        int Id PK
        text FirstName
        text LicenseNumber
        real Rating
    }
    Taxis {
        int Id PK
        int CityId FK
        int DriverId FK
        text LicensePlate UK
        text VehicleDescription
        bool IsAvailable
        text ApiKeyHash "SHA-256"
    }
    Trips {
        guid Id PK "geheime sleutel in URL"
        int CityId FK
        int TaxiId FK "nullable"
        text Status
        text PickupName
        real PickupLat
        real PickupLng
        text DestinationName
        real DestinationLat
        real DestinationLng
        text PlannedRoute "JSON polyline"
        real PlannedDistanceMeters
        real PlannedDurationSeconds
        text RouteSource
        text TariffName
        decimal Rates_x "kopie tarief"
        text CurrencyCode
        decimal EstimatedPrice
        decimal EstimatedPriceHigh
        real CurrentLat
        real CurrentLng
        datetime LastPositionAt
        real DrivenDistanceMeters
        real ElapsedSeconds
        real ProgressAlongRouteMeters
        text RemainingRoute "JSON polyline"
        real RemainingDistanceMeters
        real RemainingDurationSeconds
        decimal CurrentPrice
        decimal EstimatedFinalPrice
        bool IsOffRoute
        bool PriceAlertSent
        decimal FinalPrice
        datetime CreatedAt
        datetime StartedAt
        datetime EndedAt
        int Version "concurrency token"
    }
    TripPositions {
        long Id PK
        guid TripId FK
        real Lat
        real Lng
        datetime RecordedAt
        real DistanceFromRouteMeters
    }
    TripAlerts {
        int Id PK
        guid TripId FK
        text Type
        text Message
        datetime CreatedAt
    }
    TripFeedback {
        int Id PK
        guid TripId FK "uniek"
        int Stars "1-5"
        text Comment
        bool ReportedOvercharge
        datetime CreatedAt
    }
    TripSimulations {
        guid TripId PK, FK
        text Path "JSON polyline"
        real PathLengthMeters
        real DistanceTravelledMeters
        real SpeedMetersPerSecond
        bool WithDetour
    }
```

**Keuzes in het datamodel**

- **Tarief wordt gekopieerd naar de rit** (`Rates_*` in `Trips`): wijzigt de stad haar tarief, dan verandert de prijs van een lopende rit niet (US-03 AC3).
- **Routes als JSON-polyline** in één kolom: een route wordt altijd als geheel gelezen en geschreven; een aparte tabel met duizenden punten zou alleen vertragen. De **gereden** route staat wél per punt in `TripPositions`, omdat die stap voor stap groeit en na 30 dagen apart verwijderd moet kunnen worden.
- **GUID als sleutel** van een rit: onvoorspelbaar (122 bits willekeur), zodat niemand ritten van anderen kan raden; geen account nodig.
- **Geen reizigerstabel**: er zijn geen persoonsgegevens van reizigers (privacy by design).
- **`ON DELETE CASCADE`** van rit naar posities, meldingen, beoordeling en simulatie: de opschoonservice verwijdert een rit in één keer volledig.
- **`Version`** als concurrency token: de simulator en de reiziger kunnen tegelijk dezelfde rit wijzigen (zie §5).

## 4. Activiteitendiagram – verwerken van een GPS-positie

Dit is de kern van epic 2: wat gebeurt er als het taxikastje (of de simulator) een positie doorgeeft?

```mermaid
flowchart TD
    S([Positie ontvangen]) --> A{Rit onderweg?}
    A -->|nee| X1([Fout: rit niet onderweg])
    A -->|ja| B{Coördinaat geldig en<br/>niet ouder dan vorige?}
    B -->|nee| N([Negeren])
    B -->|ja| C{Snelheid sinds vorige<br/>positie ≤ 200 km/u?}
    C -->|nee: GPS-fout| N
    C -->|ja| D[Gereden afstand += stap<br/>ritduur = tijdstip − start]
    D --> E[Meter = instap + km × tarief + min × tarief]
    E --> F[Afstand tot geplande route bepalen<br/>projectie op polyline]
    F --> G{Al van de route af?}
    G -->|nee| H{Afstand > 100 m?}
    G -->|ja| I{Afstand ≤ 50 m?}
    H -->|ja| J[Melding: wijkt af van route]
    H -->|nee| K[Voortgang op route bijwerken]
    I -->|ja| L[Melding: weer op route]
    I -->|nee| M[Blijft afgeweken]
    J --> M
    L --> K
    K --> O[Resterende route = rest van geplande route]
    M --> P{Laatste herberekening<br/>> 15 s geleden?}
    P -->|ja| Q[Nieuwe route huidige positie → bestemming<br/>via routeplanner]
    P -->|nee| R[Verreden stuk van vorige<br/>herberekende route afhalen]
    O & Q & R --> T[Verwachte eindprijs =<br/>tarief over gereden + resterende afstand en tijd]
    T --> U{Verwachte eindprijs > bovengrens<br/>én nog niet gemeld?}
    U -->|ja| V[Melding: prijs boven schatting]
    U -->|nee| W[Opslaan]
    V --> W
    W --> Z([Klaar])
```

## 5. Sequentiediagram – live volgen

```mermaid
sequenceDiagram
    autonumber
    actor R as Reiziger (browser)
    participant TC as TripsController
    participant TS as TripService
    participant SIM as Simulator (achtergrond)
    participant API as TripsApiController
    participant DB as SQLite

    R->>TC: POST /ritten/{id}/start (taxi, anti-forgery token)
    TC->>TS: StartTripAsync(id, taxiId)
    TS->>DB: status = InProgress, taxi bezet, startpositie
    TC->>SIM: CreateAsync(trip, omrijden?)
    TC-->>R: 302 → /ritten/{id}
    loop elke seconde (achtergrond)
        SIM->>TS: RecordPositionsAsync(id, posities)
        TS->>TS: meter, routebewaking, verwachte eindprijs
        TS->>DB: opslaan (Version + 1)
    end
    loop elke 2 seconden
        R->>API: GET /api/trips/{id}/status
        API->>TS: GetStatusAsync(id)
        TS->>DB: rit + posities + meldingen
        API-->>R: JSON (prijzen, routes, meldingen)
        R->>R: kaart en bedragen bijwerken
    end
    SIM->>TS: CompleteTripAsync(id) bij bestemming
    R->>API: GET status → isFinished = true
    R->>TC: GET /ritten/{id}/eindprijs
```

**Gelijktijdigheid.** Als de reiziger op "Rit beëindigen" drukt terwijl de simulator net een positie opslaat,
voorkomt het concurrency token (`Version`) dat de ene wijziging de andere overschrijft: de tweede schrijver krijgt
een `DbUpdateConcurrencyException`. `TripService` leest de rit dan opnieuw in en probeert het nog eens (max. 3×);
de simulator slaat de tick over.

## 6. Toestandsdiagram – status van een rit

```mermaid
stateDiagram-v2
    [*] --> Planned : schatting gemaakt
    Planned --> InProgress : reiziger kiest taxi en stapt in
    Planned --> [*] : niet gestart, na 24 uur verwijderd
    InProgress --> InProgress : GPS-positie / bericht
    InProgress --> Completed : bestemming bereikt (taxi meldt aankomst)
    InProgress --> EndedEarly : reiziger beëindigt rit
    Completed --> [*] : na 90 dagen verwijderd
    EndedEarly --> [*] : na 90 dagen verwijderd
```

Elke actie controleert de huidige status; een ongeldige overgang (bijv. een afgeronde rit opnieuw starten of
twee keer beoordelen) geeft een nette melding (`TripStateException`) in plaats van een fout.

## 7. Algoritmes

**Ritprijs** (zelfde formule voor schatting, meter en verwachte eindprijs):

```
prijs = max(minimumtarief, instaptarief + afrond(km × prijs per km) + afrond(minuten × prijs per minuut))
```

- *Schatting* = formule over de route-afstand en de verwachte rijtijd van de routeplanner.
- *Bovengrens* = zelfde afstand, rijtijd × 1,4 (druk verkeer). Instelbaar (`Monitoring:TrafficMarginFactor`).
- *Meter* = formule over de gereden afstand (som van GPS-stappen) en de verstreken tijd.
- *Verwachte eindprijs* = formule over (gereden + resterende afstand) en (verstreken + resterende tijd).

Omdat de schatting en de meter dezelfde afstandsmaat gebruiken (lengte van de polyline), betaalt de reiziger bij het
rijden van de geplande route precies de geschatte prijs (getest in UT-47 en UT-51).

**Afwijking van de route:** de positie wordt loodrecht geprojecteerd op elk lijnstuk van de geplande route
(lokale vlakke projectie, nauwkeurig genoeg binnen een stad); de kleinste afstand telt. Stukken die al verreden zijn
(meer dan 200 m terug) worden overgeslagen, zodat een route die vlak langs zichzelf terugloopt geen sprong in de
voortgang geeft. Drempel 100 m met **hysterese** (terug op route pas onder 50 m) tegen GPS-ruis.

## 8. Technische keuzes en onderbouwing

| Keuze | Alternatieven | Waarom deze keuze |
|---|---|---|
| **Webapplicatie (responsive)** i.p.v. native app | Native iOS/Android, MAUI | De toerist hoeft in het buitenland niets te installeren (dure roaming, geen app-store-account nodig); dezelfde pagina werkt op telefoon én scherm in de taxi; link delen werkt direct. |
| **ASP.NET Core 10 MVC (C#)** | Node.js, PHP/Laravel | Aanbevolen in de casus (EF Core-hulpbron), sterke typering, ingebouwde anti-forgery, validatie, rate limiting en DI; .NET 10 is een LTS-versie (ondersteund tot 2028). |
| **Entity Framework Core + migraties** | Dapper, ruwe SQL | Databaseschema onder versiebeheer (migraties), geparametriseerde queries (geen SQL-injectie), LINQ is goed leesbaar. |
| **SQLite** | SQL Server, PostgreSQL | Geen databaseserver nodig: de app start met één commando, ideaal voor een MVP en voor tests (in-memory). Via EF Core later eenvoudig te vervangen door PostgreSQL/SQL Server. |
| **Leaflet + OpenStreetMap** | Google Maps, Mapbox | Open source, geen API-sleutel of creditcard, geen tracking van gebruikers door een advertentiebedrijf (privacy). Leaflet staat lokaal in `wwwroot/lib`, geen CDN. |
| **OSRM-routeplanner + offline terugval** | Google Directions API | Gratis, OpenStreetMap-data, geeft afstand + rijtijd + route. Interface `IRoutingService` maakt vervangen eenvoudig; bij uitval wordt de offline benadering gebruikt (NF-05). |
| **Polling elke 2 s** | SignalR/WebSockets | Eenvoudig, werkt door elke proxy en bij wisselend mobiel bereik (automatisch opnieuw proberen); 2 s is ruim voldoende voor een taxi. SignalR staat als verbetervoorstel. |
| **Simulator via dezelfde service als de echte API** | Nepdata in de views | Het MVP test zo de echte keten (positie → meter → waarschuwing → scherm). De taxikastje-API (`/api/taxi/...`) is al gebouwd en getest. |
| **Geen account, GUID-link** | Inloggen met e-mail | Minder drempel voor toeristen, geen persoonsgegevens (AVG), rit delen is gewoon de link doorsturen. |
| **xUnit + WebApplicationFactory** | MSTest, Selenium | Unittests en integratietests in één project; de integratietests draaien de complete app in het geheugen zonder browser of internet. |

## 9. Privacy, ethiek en security

### Privacy (AVG)

| Principe | Uitwerking in het ontwerp |
|---|---|
| Dataminimalisatie | Geen naam, e-mail, telefoon of betaalgegevens van de reiziger. Van de chauffeur alleen voornaam + vergunningnummer (nodig om te herkennen en te melden). |
| Doelbinding | Locatiegegevens alleen voor route, prijs en bewijs bij klachten; nooit buiten een rit. |
| Opslagbeperking | `DataRetentionService` verwijdert automatisch: posities na 30 dagen, ritten na 90 dagen, ongebruikte schattingen na 24 uur (instelbaar). |
| Transparantie | Privacypagina in de app met per gegeven het doel en de bewaartermijn. |
| Derden | Routeplanner krijgt alleen begin- en eindpunt, geen gegevens over de persoon. `Referrer-Policy: strict-origin-when-cross-origin` voorkomt dat de geheime rit-link via kaarttegels naar OpenStreetMap lekt. |
| Logging | Logs bevatten rit-ID's en afstanden, geen coördinaten of berichten. |

### Ethiek

- **Eerlijk tegenover de chauffeur:** een afwijking is niet automatisch fraude (wegwerkzaamheden, afsluiting). De app formuleert daarom neutraal ("Vraag de chauffeur waarom") en de chauffeur ziet op het eigen scherm ook dat de taxi afwijkt. Beoordelingen zijn gekoppeld aan een echte rit, zodat er geen nepbeoordelingen gegeven kunnen worden.
- **Geen schijnzekerheid:** de schatting is een bandbreedte (normaal – druk verkeer) en de tarieven zijn duidelijk als dummydata gemarkeerd.
- **Toegankelijkheid:** labels, tekst bij elke kleur, bediening met toetsenbord, `aria-live` voor nieuwe meldingen.
- **Veiligheid boven alles:** voortijdig beëindigen is altijd met één knop (plus bevestiging) mogelijk; rit delen met een vertrouwd persoon.

### Security (OWASP Top 10)

| Risico | Maatregel |
|---|---|
| Injectie (SQL) | Alleen EF Core met geparametriseerde queries. |
| XSS | Razor encodeert alle uitvoer; JavaScript gebruikt `textContent`, nooit `innerHTML` voor berichten; strikte **Content-Security-Policy** (`script-src 'self'`, geen inline scripts). Getest in IT-07. |
| CSRF | Alle wijzigende acties zijn POST met `[ValidateAntiForgeryToken]`. Getest in IT-03. |
| Broken access control | Rit-ID is een onvoorspelbare GUID; de taxi-API vereist de API-sleutel **van de taxi die de rit rijdt**. Getest in IT-09. |
| Gevoelige gegevens | API-sleutels alleen als SHA-256-hash opgeslagen en in constante tijd vergeleken; HTTPS + HSTS in productie. |
| Misbruik / DoS | Rate limiting: max. 20 routeberekeningen per minuut per IP-adres; maximaal 500 posities per API-aanroep. |
| Clickjacking | `X-Frame-Options: DENY` en `frame-ancestors 'none'`. |
| Invoervalidatie | Data-annotaties + servervalidatie: coördinaten binnen bereik en binnen het werkgebied, berichtlengte, sterren 1–5, onrealistische GPS-sprongen worden genegeerd. |
| Foutafhandeling | Nette foutpagina's (404/429/500) zonder stacktrace; domeinfouten worden vertaald naar begrijpelijke meldingen. |

## 10. Controle: ontwerp t.o.v. eisen

| Eis | Waar in het ontwerp |
|---|---|
| E1 schatting reistijd en kosten | `TripService.PlanTripAsync`, `FareRates`, `TariffSelector`, schattingspagina |
| E2 gereden route / nog te rijden route | `TripPositions`, `RemainingRoute`, activiteitendiagram §4, live pagina |
| E2 huidige prijs / schatting eindprijs | `CurrentPrice`, `EstimatedFinalPrice`, §7 |
| E2 chauffeur aanspreken / voortijdig beëindigen | `SendPassengerMessageAsync`, `EndTripEarlyAsync`, toestandsdiagram |
| E3 eindprijs, niet te veel betalen | `FinalPrice`, vergelijking met `EstimatedPriceHigh`, `TripFeedback.ReportedOvercharge` |
| Privacy, ethiek, veiligheid | §9 |

**Voorstel tot aanpassing (na review van het ontwerp):** de oorspronkelijke opzet had één vaste schatting. Omdat
verkeer de rijtijd sterk beïnvloedt, is een **bandbreedte** toegevoegd (bovengrens bij druk verkeer); de waarschuwing
gaat pas af boven die bovengrens, zodat de reiziger niet bij elke file een onterechte melding krijgt.

**Akkoord leidinggevende op het ontwerp**

| Naam | Datum | Handtekening |
|---|---|---|
| | | |
