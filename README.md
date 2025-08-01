# IndoorNavigator – Indoor-Navigation mit handelsüblichen Smartphones

Dieses Repository enthält die im Rahmen der Bachelorarbeit von Nico-Kevin Jacobi entwickelte Lösung zur Indoor-Positionierung und Navigation in Gebäuden der Philipps-Universität Marburg. Ziel ist eine skalierbare, wartbare und genaue Anwendung, die Orientierung in komplexen Innenräumen ermöglicht.

## 🗂 Projektübersicht

```text
├── geoJsonParser/        → Python-Tool zur Umwandlung von GeoJSON in 3D-Modelle, Navigationsgraphen und Config Dateien
├── navigatorAppUnity/    → Unity-App für die Indoor-Navigation mit WLAN & IMU-Sensorfusion
├── posDataInspector/     → Auswertung und Visualisierung gesammelter Positionsdaten
├── IndoorNavigator.apk   → Installierbare Android-Anwendung
```

## 📍 geoJsonParser

Python-Modul zur automatisierten Verarbeitung von GeoJSON-Raumdaten:

- Parsen der GeoJSON in sinvolle Datenstrukturen
- Optimieren und Korrigieren der Daten
- Erzeugung eines 2 stufigen Navigationsgraphen (gitter- und türbasiert)
- Erzeugung von 3D Modellen für jede Etage (als .obj)
- Export von Modellen, Graph und Config-Datei

Siehe main.py für Details


## 🧭 navigatorAppUnity

Unity-Projekt für Android zur Positionsbestimmung und Navigation:

- Darstellung der Nutzerposition auf einer interaktiven 3D-Karte
- Sensorfusion: WLAN-Fingerprinting + IMU + Kalman-Filter (alt. eigener Filter)
- Navigation zu beliebigem Raum
- Gebäude einfach hinzufügbar mit generierten Daten aus geoJsonParser
- Sammeln von WLAN-Fingerprint daten in der App
- Verschiedene Einstellungen, import und export von Daten



## 📊 posDataInspector

Python-Skript zur Analyse und Visualisierung von Positionsdaten:

- Enthält alle im Rahmen des Projektes gesammelte Daten
- Berechnen von statistischen Kennzahlen
- Erstellen von Grafiken aus den gesammelten Daten

Die wichtigsten Visualisierungen sind unter posDataInspector/resources/Graphics/interesting zu finden, durch ausführen von Main.py werden noch weit mehr generiert.
Siehe main.py für Details



## 📱 IndoorNavigator.apk

Die finale Android-App zur Nutzung ohne Unity-Editor.

### Installation:

1. APK auf ein Android-Gerät übertragen und installieren
2. WLAN-Drosselung deaktivieren ("Wi-Fi scan throttling" -> off)
3. Beim ersten Start initialisiert sich die Datenbank
4. App fragt nach berechtigungen für Standort
5. wenn positon ermittelt werden kann mit diese auf der karte angezeigt, sonst ein dialog fester
6. oben kann ein geböude und stocwerk ausgwählt werden, unten rechts, "springe zur aktuellen position" und "navigation"


## 🧪 Hinweise

- Die App wurde für ausgewählte Gebäude der Philipps-Universität vorkonfiguriert
- Neue Gebäude lassen sich einzufügen indem unter Ressources/Buildings die Config.json und graph.json hinzugefügt werden und unter Ressources/Prefabs der in geoJsonParser generierte ordner mit allen obj Geböudemodellen
- über den Setup-Modus das geböude in der app einrichten. (daten können exportiert werden und über das Database skript zu intallation automatisch in die Datenbank initialisiert werden)


## 🖼 Screenshots

### Standardansicht mit Nutzerposition und aktuellem Raum auf der 3D-Karte
![Standardansicht](images/StandartView.jpg)

### Aktive Navigation, zeigt nutzer wo er lang gehen soll
![Navigationsdialog](images/ActiveNavigationRounded.jpg)

### Einstellungen in der App
![Aktive Navigation](images/SettingsMenu.jpg)

### Datenanalyse: Geschätze Wege vs tatsächlicher weg (6 Messungen, Kalman Filter, Accuracy-Wert 2)
![Grafik](images/Kalman2WalkedPaths.png)

### Datenanalyse: Tabelle mit statistischen Ergebnissen der Gesammtauswertung, angegeben sind Abweichungen zum nächsten Punkt auf der Tatsächlichen Strecke in Metern.
![Ungenauigkeitstabelle](images/Statistics.png)


## 📝 Weitere Informationen

Eine ausführliche Beschreibung der Umsetzung, Methodik und Evaluation befindet sich in der zugehörigen Bachelorarbeit.


## 👤 Autor

**Nico-Kevin Jacobi**  
Informatikstudent an der Philipps-Universität Marburg  
Matrikelnummer: 3663174

---

© 2025 – Alle Rechte vorbehalten.
