# Programátorská dokumentácia EVE Market Viewer

## Architektúra a opis tried
- Program má hub-and-spoke architektúru, kde jedna centrálna trieda (```MainEsiIntreface```) obsahuje odkazy na ostatné, a tie majú nastarosť spracovanie a výpis dát z ESI. Centrálna trieda má okrem toho nastarosť autorizáciu a nejaké pomocné funkcie. 
- Všetky periférne triedy obsahujú odkaz na ```MainEsiInterface```, vďaka čomu dokážu využívať verejné funkcie ostatných tried.
- Program je rozdelený do viacerých tried hlavne kvôli prehľadnosti, technicky by všetky funkcie mohli byť samostatné v jednej triede.
### ```MainEsiInterface```
- Centrálna trieda. Ako bolo vyššie povedané, má nastarosť autorizáciu a nejaké pomocné funkcie. Obsahuje inštancie všetkých ostatných tried ako verejné položky.

### ```UniverseInterface```
- 