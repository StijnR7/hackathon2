# 10 – Script productvideo

**Doel:** in ± 4 minuten de MVP uitleggen aan de investeerder (de opdrachtgever). Doelgroep: geen ontwikkelaar,
dus weinig vakjargon; leg begrippen als "route-afwijking" of "bovengrens" kort uit.

**Voorbereiding**

- `dotnet run --project src/HitchTracker.Web --launch-profile http` en open <http://localhost:5189>.
- Browser op ± 1280 px breed (of telefoonweergave via F12 → apparaatwerkbalk voor het stuk "tijdens de reis").
- Zorg voor internet (kaart en echte routes). Verwijder eventueel `hitchtracker.db` voor een schone start.
- De simulator rijdt 10× versneld: een rit van 12 minuten duurt ruim een minuut.

| Tijd | Beeld | Tekst (ingesproken) |
|---|---|---|
| 0:00–0:25 | Titel + startpagina | "Je bent in Londen, neemt een taxi voor een kort stukje en betaalt ineens 45 euro. HitchTracker voorkomt dat: je weet vooraf wat een eerlijke prijs is, en tijdens de rit zie je of de chauffeur eerlijk rijdt. Je hebt er geen account voor nodig." |
| 0:25–1:00 | Stad Londen, Big Ben → Tower of London kiezen, ook één keer op de kaart klikken | "**Voor de reis** kies ik waar ik ben en waar ik heen wil: een bekende plek, of gewoon een punt op de kaart. Geen adres typen in een vreemde taal." |
| 1:00–1:35 | Schattingspagina | "Ik zie de route, de reistijd en de prijs volgens het officiële tarief van Londen: ongeveer 21 pond, bij druk verkeer maximaal 23. Ook de opbouw: instaptarief, kilometers en minuten. Bij het instappen controleer ik het kenteken en kies ik de taxi." |
| 1:35–2:35 | Start met "laat de chauffeur omrijden"; live pagina | "**Tijdens de reis** zie ik de gereden route, de nog te rijden route, de meterstand en de verwachte eindprijs. Deze chauffeur rijdt om… en direct krijg ik een waarschuwing: de taxi wijkt af van de route, en de verwachte prijs komt boven de schatting. Ik kan de chauffeur met één tik een bericht sturen – ook als we geen taal delen – en in het ergste geval de rit beëindigen." Toon kort het chauffeursscherm met het bericht. |
| 2:35–3:15 | Eindprijspagina (rood) | "**Na de reis** zie ik de eindprijs met de opbouw, en de vergelijking met de schatting: ik heb 9 pond te veel betaald, en de kaart laat zien waar is omgereden. Met één klik meld ik dat." |
| 3:15–3:35 | Eindprijspagina van een eerlijke rit (groen) | "Rijdt de chauffeur netjes de route, dan betaal je precies de schatting." |
| 3:35–4:00 | Privacy-pagina, daarna testresultaat (`dotnet test`: 87 geslaagd) | "Privacy staat voorop: geen persoonsgegevens, en locatiegegevens worden automatisch verwijderd. Het product is getest met 87 geautomatiseerde tests en een acceptatietest. Volgende stappen: koppeling met echte taximeters en meer talen." |

**Tips:** spreek rustig, zoom in op bedragen en meldingen, en houd de muis stil tijdens uitleg.
