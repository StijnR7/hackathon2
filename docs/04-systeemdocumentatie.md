# 04 – Systeemdocumentatie

Voor ontwikkelaars en beheerders: installeren, configureren, de opbouw van de code en de koppelingen.

## 1. Vereisten

| Onderdeel | Versie |
|---|---|
| .NET SDK | 10.0 of hoger (`dotnet --version`) |
| Browser | Recente Chrome, Edge, Firefox of Safari |
| Internet (optioneel) | Voor kaarttegels (OpenStreetMap) en echte routes (OSRM). Zonder internet werkt de app met een offline routebenadering. |

Er is **geen** aparte database-server nodig (SQLite).

## 2. Installeren en starten

```bash
git clone https://github.com/StijnR7/hackathon2.git
cd hackathon2
dotnet run --project src/HitchTracker.Web --launch-profile http
```

- Open <http://localhost:5189> (of <https://localhost:7288> met profiel `https`).
- Bij de eerste start voert de app de EF Core-migraties uit en vult hij de database met dummydata (`DbSeeder`).
- Opnieuw beginnen met schone data: stop de app en verwijder `src/HitchTracker.Web/hitchtracker.db`.
- In Visual Studio / Rider: open `HitchTracker.sln` en start het project `HitchTracker.Web`.

### Tests

```bash
dotnet test                                   # alle 87 tests
dotnet test --filter "FullyQualifiedName~Unit"         # alleen unittests
dotnet test --filter "FullyQualifiedName~Integration"  # alleen integratietests
```

### Database-migraties

Het schema staat onder versiebeheer als EF Core-migratie (`Data/Migrations`). Na een wijziging in het model:

```bash
dotnet tool restore
dotnet ef migrations add <NaamVanDeWijziging> --project src/HitchTracker.Web --output-dir Data/Migrations
```

De migratie wordt bij de volgende start automatisch uitgevoerd.

## 3. Configuratie (`appsettings.json`)

Alle instellingen worden bij het opstarten gevalideerd (`ValidateDataAnnotations` + `ValidateOnStart`); een ongeldige waarde stopt de app direct met een duidelijke fout.

| Sectie / sleutel | Standaard | Betekenis |
|---|---|---|
| `ConnectionStrings:HitchTracker` | `Data Source=hitchtracker.db` | SQLite-database |
| `Routing:Provider` | `Osrm` | `Osrm` (online, met offline terugval) of `Offline` |
| `Routing:OsrmBaseUrl` | `https://router.project-osrm.org/` | OSRM-server. Voor productie een eigen server gebruiken (de demo-server heeft een gebruikslimiet). |
| `Routing:TimeoutSeconds` | `8` | Daarna valt de app terug op de offline benadering |
| `Routing:OfflineAverageSpeedKmh` | `25` | Gemiddelde snelheid voor de offline benadering |
| `Monitoring:OffRouteThresholdMeters` | `100` | Waarschuwing "wijkt af van route" |
| `Monitoring:BackOnRouteThresholdMeters` | `50` | "Weer op route" (hysterese) |
| `Monitoring:TrafficMarginFactor` | `1.4` | Bovengrens schatting = rijtijd × factor |
| `Monitoring:RerouteIntervalSeconds` | `15` | Min. tijd tussen herberekeningen van de route na afwijking |
| `Monitoring:MaxPlausibleSpeedKmh` | `200` | Snellere GPS-sprongen worden genegeerd |
| `Monitoring:MinimumTripDistanceMeters` | `200` | Minimale ritafstand |
| `Simulation:Enabled` | `true` | Demo-taxi's rijden automatisch |
| `Simulation:TickMilliseconds` | `1000` | Interval van de simulator |
| `Simulation:SpeedFactor` | `10` | Versnelling (10 = rit van 10 min duurt 1 min) |
| `DataRetention:Enabled` | `true` | Automatisch opschonen (AVG) |
| `DataRetention:PositionRetentionDays` | `30` | Bewaartermijn GPS-posities |
| `DataRetention:TripRetentionDays` | `90` | Bewaartermijn afgeronde ritten |
| `DataRetention:PlannedTripRetentionHours` | `24` | Bewaartermijn niet-gestarte schattingen |
| `DataRetention:IntervalHours` | `6` | Hoe vaak er opgeschoond wordt |

Instellingen kunnen ook via omgevingsvariabelen, bijv. `Routing__Provider=Offline`.

## 4. Projectstructuur

```
HitchTracker.sln
src/HitchTracker.Web/
├── Program.cs                     opstarten: services, middleware, routes
├── Controllers/
│   ├── HomeController.cs          rit plannen, info, privacy, foutpagina's
│   ├── TripsController.cs         schatting, start, live, bericht, beëindigen, eindprijs, beoordeling, chauffeur
│   └── Api/TripsApiController.cs  JSON-status (browser) en taxi-API (taxikastje)
├── Models/                        domein: City, Tariff, FareRates, Place, Driver, Taxi, Trip, TripPosition, ...
├── Data/
│   ├── HitchTrackerDbContext.cs   EF Core-model (relaties, conversies, indexen)
│   ├── DbSeeder.cs                dummydata
│   ├── DatabaseInitializer.cs     migreren + seeden bij opstarten
│   └── Migrations/                databaseschema onder versiebeheer
├── Services/
│   ├── GeoMath.cs                 afstanden, projectie op route, deelroutes
│   ├── Routing/                   IRoutingService, OsrmRoutingService, OfflineRoutingService
│   ├── Trips/                     TripService (bedrijfslogica), TripMonitor, TariffSelector, DTO's, fouttypes
│   ├── Simulation/                TripSimulator + achtergrondservice
│   ├── DataRetention.cs           bewaartermijnen (AVG)
│   └── PlanTripViewModelFactory.cs
├── Infrastructure/                security headers, exceptionfilter, opmaak (€, km, min), API-sleutels
├── ViewModels/                    modellen en formulieren voor de views
├── Views/                         Razor-views (Home, Trips, Shared)
└── wwwroot/                       css, js (site, plan, trip), lib (Bootstrap, Leaflet)
tests/HitchTracker.Tests/
├── Unit/                          GeoMath, prijs/tarief, TripMonitor, TripService, simulatie, opschonen
├── Integration/                   volledige app via HTTP (WebApplicationFactory)
└── TestSupport/                   testdatabase, vaste klok, scenario's
```

## 5. Routes (URL's)

| Methode | URL | Actie |
|---|---|---|
| GET | `/` `?cityId=` | Rit plannen |
| POST | `/ritten/plannen` | Route + schatting berekenen → redirect naar schatting |
| GET | `/ritten/{id}/schatting` | Schatting + taxi kiezen |
| POST | `/ritten/{id}/start` | Rit starten |
| GET | `/ritten/{id}` | Live volgen |
| POST | `/ritten/{id}/bericht` | Bericht aan chauffeur |
| POST | `/ritten/{id}/beeindigen` | Rit voortijdig beëindigen |
| GET | `/ritten/{id}/eindprijs` | Eindprijs |
| POST | `/ritten/{id}/beoordeling` | Beoordeling / melding |
| GET | `/ritten/{id}/chauffeur` | Chauffeursscherm (demo) |
| GET | `/api/trips/{id}/status` | Live status (JSON) |
| POST | `/api/taxi/trips/{id}/positions` | GPS-posities (taxi-API) |
| POST | `/api/taxi/trips/{id}/complete` | Aankomst melden (taxi-API) |

Alle POST-acties van de website vereisen een anti-forgery token. Een URL van een rit in een andere fase stuurt door naar de juiste pagina.

## 6. Status-API

`GET /api/trips/{id}/status` → `200 OK` (of `404` voor een onbekende rit). Velden (camelCase):

| Veld | Betekenis |
|---|---|
| `status`, `statusText` | `Planned` / `InProgress` / `Completed` / `EndedEarly` en de Nederlandse weergave |
| `isFinished` | Rit afgelopen → browser gaat naar de eindprijs |
| `currencyCode` | `EUR`, `GBP` |
| `currentPrice`, `estimatedFinalPrice`, `estimatedPrice`, `estimatedPriceHigh`, `finalPrice` | Bedragen |
| `drivenDistanceMeters`, `remainingDistanceMeters`, `elapsedSeconds`, `remainingDurationSeconds`, `progressPercent` | Voortgang |
| `isOffRoute`, `isPriceAboveEstimate` | Waarschuwingsvlaggen |
| `taxiPosition` | `[lat, lng]` |
| `driven`, `remaining` | Routes als lijst `[lat, lng]` |
| `alerts` | `[{ id, type, severity, message, createdAt, fromPassenger }]` |

## 7. Taxi-API

Voor het taxikastje of de chauffeursapp. Authenticatie met header `X-Taxi-Key`; de sleutel moet horen bij **de taxi die deze rit rijdt** (anders `401`).

```http
POST /api/taxi/trips/{id}/positions
X-Taxi-Key: demo-lx19-txi
Content-Type: application/json

{ "positions": [ { "lat": 51.5010, "lng": -0.1230, "recordedAt": "2026-10-12T10:15:03Z" } ] }
```

Antwoord: `200 { "accepted": 1, "rejected": 0 }`. Ongeldige, verouderde of onrealistische posities worden geweigerd (geteld in `rejected`). Maximaal 500 posities per aanroep.

```http
POST /api/taxi/trips/{id}/complete
X-Taxi-Key: demo-lx19-txi
```

Antwoord: `200 { "finalPrice": 20.71, "currency": "GBP" }`.

**Demo-sleutels** (alleen dummydata; formule `demo-` + kenteken in kleine letters met streepjes):

| Stad | Kenteken | Sleutel |
|---|---|---|
| Londen | LX19 TXI · LT68 CAB · LD21 HCK | `demo-lx19-txi` · `demo-lt68-cab` · `demo-ld21-hck` |
| Amsterdam | TX-123-A · TX-456-B · TX-789-C | `demo-tx-123-a` · `demo-tx-456-b` · `demo-tx-789-c` |
| Parijs | GT-512-PA · FR-224-TX · EV-901-PR | `demo-gt-512-pa` · `demo-fr-224-tx` · `demo-ev-901-pr` |

In de database staat alleen de SHA-256-hash. In productie krijgt elk kastje een willekeurige sleutel die niet in de broncode staat.

> Let op: zolang `Simulation:Enabled` aan staat, rijdt de simulator ook gestarte ritten. Zet hem uit om de taxi-API handmatig te testen.

## 8. Codeconventies

- [Microsoft C#-codeconventies](https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/coding-conventions); `.editorconfig` in de root legt opmaak vast.
- **Namen:** Engels voor code (klassen, methodes, variabelen), Nederlands voor alles wat de gebruiker ziet en voor commentaar. `PascalCase` voor typen/methodes/properties, `camelCase` voor variabelen/parameters, `_camelCase` voor private velden, `Async`-suffix voor asynchrone methodes.
- **Nullable reference types** aan; compileren zonder waarschuwingen.
- **Dunne controllers**, logica in services; domeinfouten als `TripException`-subklassen met een nette melding.
- **Validatie** zowel in formulieren (data-annotaties) als in de service (de service vertrouwt de controller niet).
- **Geen inline JavaScript** (CSP); gegevens voor scripts via `<script type="application/json">`.
- **Commentaar** legt het *waarom* uit (XML-doc op publieke typen en methodes).
- **Tests** heten `Methode_Situatie_VerwachtResultaat` en hebben een testcode (UT-xx / IT-xx) die terugkomt in het testrapport.

## 9. Versiebeheer

- Git, repository op GitHub. Workflow: **GitHub Flow met een `develop`-branch**:
  - `main` bevat alleen opgeleverde versies (tags `v0.1.0` ontwerp, `v0.2.0` MVP, `v1.0.0` getest en verbeterd).
  - `develop` is de integratiebranch; elk onderdeel wordt op een eigen `feature/…`-branch gemaakt en met een merge-commit (`--no-ff`) samengevoegd, zodat in de historie zichtbaar blijft welke commits bij welk onderdeel horen.
- Commitberichten in de gebiedende wijs met een type-prefix (`feat:`, `fix:`, `test:`, `docs:`, `chore:`).
- Bekijk de historie met `git log --graph --oneline --all`.

## 10. Uitrollen (productie)

1. `dotnet publish src/HitchTracker.Web -c Release -o publish`
2. Draai achter een reverse proxy met HTTPS (HSTS staat aan buiten Development).
3. Gebruik een eigen OSRM-server (`Routing:OsrmBaseUrl`) en een eigen tegelserver of betaalde tegeldienst voor productieverkeer.
4. Zet `Simulation:Enabled` op `false` zodra echte taxikastjes posities aanleveren.
5. Voor meerdere serverinstanties: vervang SQLite door PostgreSQL/SQL Server (alleen `UseSqlite` aanpassen en een nieuwe migratie maken).
