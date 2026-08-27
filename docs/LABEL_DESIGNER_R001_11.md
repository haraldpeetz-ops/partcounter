# Partcounter R001.11 – Etiketteneditor mit Bild-/Logo-Elementen

## Ausgangsbasis

R001.11 baut ausschließlich auf dem funktionsfähigen Stand `r001.10-label-editor` auf. Die LOGO!-Schnittstelle und das Modbus-V2-Protokoll bleiben unverändert.

## Neue Funktion

Der Etiketteneditor unterstützt zusätzlich den Elementtyp **Bild / Logo**.

Unterstützte Dateiformate:

- PNG
- JPG / JPEG
- BMP
- GIF
- TIFF

Empfehlung für Firmenlogos: PNG mit transparentem Hintergrund.

## Bedienung

1. Im Etiketteneditor links **Bild / Logo** wählen.
2. Rechts unter **Bild / Logo** auf **Bilddatei auswählen …** klicken.
3. Datei auswählen.
4. Das Bild erscheint sofort in der WYSIWYG-Vorschau.
5. Position und Größe wie bei allen anderen Elementen per Maus bzw. exakt über X/Y/Breite/Höhe in Millimetern einstellen.
6. Optional **Seitenverhältnis beibehalten** aktivieren oder deaktivieren.
7. Vorlage speichern.

## Speicherung

Die Bilddaten werden Base64-codiert direkt in `DefinitionJson` der Etikettenvorlage gespeichert. Dadurch ist die Vorlage unabhängig vom ursprünglichen Dateipfad. Wird die Quelldatei später verschoben oder gelöscht, bleibt das Bild im Etikett erhalten.

Maximale Bildgröße beim Import: **10 MB je Bild**.

Eine Datenbankmigration ist nicht erforderlich, da die zusätzlichen Bildinformationen Bestandteil der bestehenden JSON-Definition sind.

## Vorschau und Druck

Vorschau, Testdruck und automatischer VE-Etikettendruck verwenden weiterhin denselben `LabelRenderService`. Bild-Elemente werden daher im Editor und im tatsächlichen Druck mit derselben Position, Größe und Skalierungsart gerendert.

## Kompatibilität

Ältere R001.10-Vorlagen bleiben vollständig kompatibel. Die neuen Felder besitzen Defaultwerte und werden beim Deserialisieren alter Vorlagen automatisch leer bzw. mit aktiviertem Seitenverhältnis initialisiert.

## LOGO!/Modbus

Keine Änderung an:

- ProtocolVersion 2
- HR1–HR12 PC → LOGO!
- HR20–HR37 LOGO! → PC
- Zykluszählung
- CompletionSequence
- CommandSequence/AckSequence
- Ventilimpuls
- Heartbeats

Die R001.11-Erweiterung betrifft ausschließlich die PC-seitige Etikettengestaltung und den Etikettendruck.
