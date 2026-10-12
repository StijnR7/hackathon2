# 01 – Plan van aanpak en planning

**Project:** HitchTracker (Hackathon 2) · **Werkvorm:** individueel, Scrum · **Duur:** 3 weken × 21 lesuren
**Werkproces:** B1-K1-W1 Stemt opdracht af, plant werkzaamheden en bewaakt de voortgang

## 1. Aanleiding

Toeristen betalen in onbekende steden regelmatig te veel voor een taxirit: de chauffeur rijdt om of rekent een
fantasieprijs ("je dacht dat het een klein stukje was, en je betaalt ineens 45 euro"). Een investeerder wil met
**HitchTracker** de veiligheid en transparantie van taxigebruik in het buitenland vergroten.

## 2. Opdracht en doelen

De klant verwacht een **concept voor een oplossing** en een **MVP** dat drie epics afdekt:

| Epic | Als gebruiker wil ik… | zodat… |
|---|---|---|
| E1 Voor de reis | op mijn telefoon een bestemming invoeren en een schatting van reistijd en kosten krijgen | ik weet wat een eerlijke prijs is |
| E2 Tijdens de reis | op mijn telefoon (of scherm in de taxi) de gereden route, de nog te rijden route, de huidige prijs en de geschatte eindprijs zien | ik de chauffeur kan aanspreken of de reis voortijdig kan beëindigen |
| E3 Na de reis | de eindprijs zien | ik weet dat de chauffeur mij niet te veel laat betalen |

**Projectdoelen (SMART)**

1. Aan het eind van week 1 ligt er een goedgekeurd ontwerp met minimaal vier schematechnieken, wireframes en een onderbouwing van de keuzes (incl. privacy, ethiek en security).
2. Aan het eind van week 2 staat er een werkend MVP met dummydata in een Git-repository waarin alle Must-haves van de drie epics werken.
3. Aan het eind van week 3 zijn er een integratietest, een acceptatietest, een testrapport, verbetervoorstellen met planning en een productvideo.

## 3. Afbakening

**Binnen scope:** webapplicatie die op telefoon en taxischerm werkt; schatting, live volgen, waarschuwingen, berichten aan de chauffeur, voortijdig stoppen, eindprijs, beoordeling/melding; dummydata voor drie steden; gesimuleerde taxi.

**Buiten scope (MVP):** echte koppeling met taximeters, betalen in de app, accounts, meertaligheid, native apps, juridische afhandeling van klachten. Deze punten staan in de [verbetervoorstellen](08-verbetervoorstellen.md).

## 4. Prioritering (MoSCoW)

| Prio | User story (zie [functioneel ontwerp](02-functioneel-ontwerp.md)) |
|---|---|
| **Must** | US-01 bestemming kiezen · US-02 reistijd en prijs vooraf · US-04 gereden en nog te rijden route · US-05 huidige en verwachte eindprijs · US-07 rit voortijdig beëindigen · US-09 eindprijs met opbouw |
| **Should** | US-03 tarief per stad/tijdstip · US-06 waarschuwing bij omrijden of te hoge prijs · US-08 bericht aan chauffeur · US-10 vergelijking met schatting · US-11 beoordelen en melden |
| **Could** | US-12 chauffeursscherm · US-13 rit delen via link · automatisch opschonen van oude gegevens |
| **Won't (nu)** | Betalen in de app, accounts, meertaligheid, push-notificaties |

## 5. Aanpak en planning

Scrum met drie sprints van één week (21 lesuren). Elke sprint start met een sprintplanning en eindigt met een
**product review** (demo aan de opdrachtgever/docent) en een **retrospective**. De voortgang staat in het
[logboek](09-logboek.md).

### Sprint 1 – Ontwerpen (week 1, B1-K1-W2)

| Taak | Uren | Resultaat |
|---|---:|---|
| Casus analyseren, vragen opstellen en afstemmen met opdrachtgever | 3 | Afgestemde opdracht en planning |
| Concurrentieanalyse (Uber, Bolt, FREENOW, Google Maps) | 2 | Paragraaf in functioneel ontwerp |
| User stories + acceptatiecriteria uitwerken | 3 | Functioneel ontwerp §2 |
| Use-casediagram, wireframes, navigatiestroom | 4 | Functioneel ontwerp §3–5 |
| Klassendiagram, ERD, activiteiten-, sequentie- en toestandsdiagram | 5 | Technisch ontwerp |
| Technische keuzes onderbouwen, privacy/ethiek/security | 3 | Technisch ontwerp §6–7 |
| Review + akkoord leidinggevende | 1 | Getekend akkoord |
| **Totaal** | **21** | |

### Sprint 2 – MVP realiseren (week 2, B1-K1-W3)

| Taak | Uren | User stories |
|---|---:|---|
| Projectopzet, Git-repository, database + dummydata | 3 | – |
| Routeplanner (OSRM + offline terugval) en prijsberekening | 3 | US-01, 02, 03 |
| Schattingspagina met kaart | 3 | US-01, 02 |
| Rit starten, simulator, live status-API | 4 | US-04, 05 |
| Routebewaking en waarschuwingen | 3 | US-06 |
| Berichten, voortijdig beëindigen, chauffeursscherm | 2 | US-07, 08, 12 |
| Eindprijs, beoordeling, melding | 2 | US-09, 10, 11 |
| Systeem- en gebruikersdocumentatie | 1 | – |
| **Totaal** | **21** | |

### Sprint 3 – Testen en verbeteren (week 3, B1-K1-W4/W5)

| Taak | Uren | Resultaat |
|---|---:|---|
| Testplan en testgevallen | 3 | [Testplan](06-testplan.md) |
| Unit- en integratietests schrijven en uitvoeren | 5 | Geautomatiseerde testset |
| Acceptatietest uitvoeren met testscenario's | 3 | Acceptatietestverslag |
| Testrapport met conclusies | 2 | [Testrapport](07-testrapport.md) |
| Bevindingen herstellen | 3 | Nieuwe versie |
| Verbetervoorstellen + planning | 2 | [Verbetervoorstellen](08-verbetervoorstellen.md) |
| Productvideo opnemen | 3 | Video |
| **Totaal** | **21** | |

### Mijlpalen

| Mijlpaal | Wanneer | Bewijs |
|---|---|---|
| M1 Ontwerp goedgekeurd | einde week 1 | Akkoord leidinggevende (§8) |
| M2 MVP werkend | einde week 2 | Git-tag `v0.2.0`, product review |
| M3 Getest en opgeleverd | einde week 3 | Git-tag `v1.0.0`, testrapport, video |

## 6. Afhankelijkheden en beperkingen

- De routeplanner (OSRM demo-server) en de kaarttegels (OpenStreetMap) zijn externe diensten met een gebruikslimiet. **Maatregel:** offline terugvaloptie, rate limiting per gebruiker.
- Geen toegang tot echte taximeters. **Maatregel:** simulator die via dezelfde code GPS-posities aanlevert als een echt taxikastje (API is wel gebouwd).
- Individueel project: geen taakverdeling nodig, wel vaste reviewmomenten met de docent.

## 7. Risico's

| Risico | Kans | Impact | Maatregel |
|---|---|---|---|
| Externe routeplanner onbereikbaar tijdens demo | Middel | Hoog | Offline routebenadering; tests draaien volledig offline |
| Te veel scope (nice-to-haves) | Hoog | Middel | MoSCoW; Musts eerst; Coulds alleen bij tijd over |
| GPS-ruis geeft valse waarschuwingen | Middel | Middel | Drempel van 100 m + hysterese (50 m), onrealistische sprongen negeren |
| Privacy (locatiegegevens) | Middel | Hoog | Geen accounts, bewaartermijnen, automatische verwijdering |
| Uitloop door ziekte/onvoorziene zaken | Laag | Hoog | Wekelijkse voortgangscheck in logboek; bijsturen op Should/Could |

## 8. Vragen aan de opdrachtgever (afstemming)

| # | Vraag | Antwoord / aanname |
|---|---|---|
| 1 | Is het een app voor de reiziger, een scherm in de taxi of beide? | Beide: responsive webapp; dezelfde live pagina werkt op telefoon en taxischerm. |
| 2 | Moeten reizigers een account aanmaken? | Nee (aanname): minder drempel en betere privacy. Rit is te volgen via een geheime link. |
| 3 | Welke steden voor de demo? | Londen (voorbeeld uit de casus), Amsterdam en Parijs met dummytarieven. |
| 4 | Hoe wordt de positie van de taxi bepaald? | Via een taxikastje/chauffeursapp (API); in het MVP gesimuleerd. |
| 5 | Wat is een "te hoge" prijs? | Boven de bovengrens van de schatting (rijtijd × 1,4 voor druk verkeer); instelbaar. |
| 6 | Moet de reiziger in de app kunnen betalen? | Niet in het MVP (Won't). |

**Akkoord opdrachtgever/leidinggevende op opdracht, doelen en planning**

| Naam | Datum | Handtekening |
|---|---|---|
| | | |
