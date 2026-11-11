# Klimaatatlas 2.0 – platformonafhankelijk (Windows, macOS, Linux)

Dit is de rekenkern van de Klimaatatlas, overgezet van VB.NET / .NET Framework 4.8 (WinForms, Windows-only)
naar C# / .NET 10. Het draait op Windows, macOS en Linux. Voor nu is er een command-line versie;
een grafische interface (Avalonia) kan later bovenop `Klimaatatlas.Core` worden gebouwd.

| Map | Inhoud |
| --- | --- |
| `src/Klimaatatlas.Core` | Configuratie lezen, maatlatten, rekenregels, schrijven naar de geopackage. Geen UI-code. |
| `src/Klimaatatlas.Cli` | Command-line programma `klimaatatlas`. |
| `tests/Klimaatatlas.Core.Tests` | Unit- en integratietests (o.a. op een test-geopackage). |

## Gebruik

```bash
klimaatatlas rekenregels.json
```

Dit doet hetzelfde als de knop *Uitvoeren* in de oude applicatie: de configuratie lezen, alle rekenregels
voor alle scenario's doorrekenen en de resultaten (`{scenario}_{regel}` en `{scenario}_{resultsfield}`)
in de tabel van de geopackage schrijven. Het diagnosebestand `klimaatatlas.dia` komt naast de config.

Start je het programma zonder configuratiebestand (dubbelklikken, F5 in Visual Studio, `dotnet run`),
dan vraagt het om het pad; je kunt het bestand ook in het venster slepen. Het laatst gebruikte bestand
wordt onthouden en met Enter opnieuw gekozen.

Handige opties (zie `klimaatatlas --help`):

```bash
klimaatatlas rekenregels.json --list
```

```bash
klimaatatlas rekenregels.json --gpkg ~/data/Watervlakken_Plus7.gpkg --rule kroos --scenario K2050
```

De configuratiebestanden bevatten vaak een Windows-pad (`c:\\SYNC\\...`). Op macOS/Linux geef je dan met
`--gpkg` het juiste pad op, of je zet in de config een **relatief** pad: dat wordt gelezen ten opzichte
van de map waarin het configuratiebestand staat.

Maak eerst een kopie van de geopackage: de resultaten worden er direct in geschreven.

### SpatiaLite (optioneel)

De rekenregels lezen en schrijven alleen attribuutkolommen, dus SpatiaLite is niet nodig. Geopackages
die met GDAL/QGIS zijn gemaakt bevatten wel triggers die bij elke wijziging `ST_IsEmpty` aanroepen; als
`mod_spatialite` niet gevonden wordt, levert het programma die functies (`ST_IsEmpty`, `ST_MinX`, ...)
zelf, op basis van de GeoPackage-geometrieheader. Wil je toch SpatiaLite gebruiken:

* Windows: `--spatialite pad\naar\dependencies\mod_spatialite-5.1.0-win-amd64\mod_spatialite.dll`
* macOS: `brew install libspatialite`
* Linux (Debian/Ubuntu): `sudo apt install libsqlite3-mod-spatialite`

## Bouwen

Benodigd: [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet test
```

```bash
dotnet run --project src/Klimaatatlas.Cli -- rekenregels.json
```

Een losstaand programma (geen .NET-installatie nodig op de doelcomputer) maak je per platform met
`-r win-x64`, `osx-arm64` (Apple Silicon), `osx-x64` (Intel Mac) of `linux-x64`:

```bash
dotnet publish src/Klimaatatlas.Cli -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish/osx-arm64
```

Op macOS moet een niet-ondertekend programma de eerste keer worden vrijgegeven:
`xattr -d com.apple.quarantine klimaatatlas`.

## Verschillen met de 4.8-versie

Overgezet is het pad dat de oude applicatie daadwerkelijk gebruikt (config → rekenregels → geopackage).
Bewust **niet** overgezet, omdat het vanuit de applicatie niet meer werd aangeroepen: de oude
regel-systematiek (`clsOldRule`), shapefile-export en -import en GeoTIFF-tegels (MapWinGIS),
het upgraden van de SOBEK-database en het stroomschema-tekenwerk.

Rekenkundige verschillen – bewust, maar controleer de uitkomsten op een kopie van de echte data:

1. **Getallen worden overal met een punt als decimaalteken gelezen**, onafhankelijk van de
   landinstellingen. De oude versie gebruikte de Windows-landinstelling; op een computer met
   Nederlandse instellingen werd bijvoorbeeld de transformatie `52.185x^2 - 524.98x + 1422.2` gelezen
   als `52185x^2 - 52498x + 14222`.
2. **`nullValue` werkt nu.** De oude versie controleerde op `nullValue` maar las daarna `nullvalue`
   (andere hoofdletter), waardoor altijd 0 werd gebruikt.
3. Transformaties als `-x^2 + ...` (alleen een teken als coëfficiënt) worden nu geaccepteerd.
4. Configuratiefouten (ontbrekende velden, dubbele namen, onbekende maatlat in `components`) stoppen
   het programma met een duidelijke melding, in plaats van stilzwijgend een deel van de regels te laden.
   Een kolom die niet in de geopackage staat, of een maatlat zonder veld of constante voor een
   scenario, geeft vóór het rekenen een foutmelding met de naam van de maatlat.

Lege waarden (NULL) zonder `nullValue` worden behandeld zoals in de oude versie, en per maatlat gemeld:

* **discrete maatlat** (bijv. `doodlopend`): oordeel 0, telt wél mee in de weging;
* **continue maatlat** (bijv. `K2023_Pt_50pct`): de uitkomst van de regel voor dat record wordt NULL.

Wil je ander gedrag, geef de maatlat dan een `"nullValue"`.

Ongewijzigd gebleven gedrag dat je misschien wilt herzien:

* Een filter staat per maatlat in de config, maar geldt voor de hele regel; het filter van de
  **laatste** maatlat telt.
* Regels worden in alfabetische volgorde doorgerekend.
* Records die buiten het filter vallen houden hun eerdere waarde in de resultaatkolommen.
