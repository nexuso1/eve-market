# Programátorská dokumentácia EVE Market Viewer

## Architektúra a opis tried
- Program má hub-and-spoke architektúru, kde jedna centrálna trieda (```MainEsiIntreface```) obsahuje odkazy na ostatné, a tie majú nastarosť spracovanie a výpis dát z ESI. Centrálna trieda má okrem toho nastarosť autorizáciu a nejaké pomocné funkcie. 
- Všetky periférne triedy obsahujú odkaz na ```MainEsiInterface```, vďaka čomu dokážu využívať verejné funkcie ostatných tried.
- Program je rozdelený do viacerých tried hlavne kvôli prehľadnosti, technicky by všetky funkcie mohli byť samostatné v jednej triede.
- Každá trieda obsahuje funkcie typu ```HandleCommand```, ktoré sa starajú o spracovanie používateľských príkazov a rovno aj vypisujú výsledky do na output stream
- Pri niektorých funkciách je možné/potrebné využiť autorizovať postavu z hry, čo má
  vplyv na ich výstup. Pri týchto funkciách je vždy napísané v README a komentároch, aký efekt autorizácia bude mať.
- Program si pamätá poslednú autorizovanú postavu pri opakovanom spustení.

### ```MainEsiInterface```
- Centrálna trieda. Ako bolo vyššie povedané, má nastarosť autorizáciu a nejaké pomocné funkcie. Obsahuje inštancie všetkých ostatných tried ako verejné položky.
- TextWriter predaný konštruktorú tejto triedy je použitý na output z celého programu


### ```UniverseInterface```
- Má nastarosť "Universe" časť ESI, a je využívaná hlavne kvôli IdToName a NameToId
  funkciám, ktoré dokáažu pre dané ID nájsť názov zodpovedajúcej entity a naopak
  nájsť pre nejaký string ID entity, ak existuje.
- Všetky výsledky sú cached.

### ```MarketInterface```
- Má nastarosti veci zodpovedajúce "Market" časti, a taktiež "Assets", pretože nebolo potrebné pre túto druhú časť vytvárať samostatnú triedu a sémanticky spolu súvisia.

### ```ContractInterface```
- Má nastarosť kontrakty a súvisiace príkazy. Obsahuje niektoré špecializované funkcie pre spracovanie kontraktov, pretože interakcia s nimi je trochu zložitejšia ako s ostatnými časťami ESI.

### ```Printer```
- Zodpovedná za pekné vypisovanie informácií z ```Interface``` tried.
- Jedna vec čo robí naviac, je konverzia ID na mená pri výpise. Na toto využíva       funkcie z ```UniverseInterface```, a vie, že to treba spraviť, vo vačšine prípadoch vďaka funkcii ```IsIdField``` z ```MainEsiInterface```.

### ```Program```
- Trieda obsahujúca main funkciu, ktorá vytvorí inštanciu ```MainEsiInterface``` a zavolá ```ParseInput```. ```ParseInput``` podľa užívateľského vstupu zavolá funkciu na spracovanie tohoto príkazu.