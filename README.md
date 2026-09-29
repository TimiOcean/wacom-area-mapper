# Ausschnitt

Ausschnitt legt die Stiftfläche eines **Wacom One** auf ein Rechteck, das du auf dem Bildschirm aufziehst. Danach steuert der ganze Stift nur noch diesen Bereich, nicht mehr den kompletten Monitor.

Gedacht für das günstigste Modell, das **Wacom One Small (CTL-472)**: ein Tablett ohne Bildschirm, USB, aktive Fläche 152 × 95 mm (16:10), 2048 Druckstufen, zwei Seitentasten, kein Radiergummi und keine Neigung. Das Medium **CTL-672** (216 × 135 mm) wird mit erkannt.

## Bedienung

1. Tablett per USB anschließen. Der Wacom-Treiber darf installiert bleiben.
2. Ausschnitt starten.
3. `Strg+Alt+A` drücken oder **Bereich aufziehen** wählen.
4. Mit der Maus ein Rechteck ziehen und loslassen. `Shift` hält das Seitenverhältnis 16:10. `Esc` bricht ab.
5. Die ganze Stiftfläche steuert jetzt nur dieses Rechteck. Ein roter Rahmen zeigt es an.
6. **Ganzer Bildschirm** hebt den Ausschnitt auf.
7. Das Fenster zu schließen legt Ausschnitt in die Taskleiste. Beenden steht im Menü dort.

Die rechte Seite zeigt deine Monitore. Auf der gezeichneten Stiftfläche kannst du mit der Maus prüfen, wo ein Punkt landet.

## Zwei Wege, den Stift umzulegen

**Wacom-Treiber.** Wenn die Einstellungsdatei `%APPDATA%\WTablet\Wacom_Tablet.dat` den Bildschirmbereich als Text enthält (`SystemOriginX/Y`, `SystemExtentX/Y`), schreibt Ausschnitt das Rechteck dort hinein und startet den Wacom-Dienst neu. Windows Ink, die vollen 2048 Druckstufen und die Tasten bleiben beim Originaltreiber. Dafür ist einmal eine Administratorabfrage nötig. Der Bereich bleibt gespeichert, auch wenn du Ausschnitt schließt.

**Direkt.** Liegt der Bereich nicht in dieser Datei, oder du hältst Proportionen bzw. drehst die Ausrichtung, liest Ausschnitt das Tablett selbst (dieselben HID-Berichte wie der Treiber: Position 15200 × 9500, Druck 0–2047, zwei Tasten) und setzt einen Windows-Stift nur in das Rechteck. Damit der originale Treiber nicht parallel den ganzen Bildschirm bewegt, wird sein Dienst pausiert. Beim Beenden oder bei **Ganzer Bildschirm** startet er wieder. Windows nimmt bei so eingespritztem Stiftdruck höchstens 1024 Stufen an; die 2048 des Tabletts werden darauf abgebildet. Die untere Seitentaste ist der Stiftknopf (Rechtsklick), die obere ein Mittelklick.

Im Modus **Automatisch** nimmt Ausschnitt den Treiber, solange Proportionen aus sind und die Ausrichtung normal bleibt. Sonst den Direktmodus.

## Voraussetzungen

- Windows 11
- .NET 10 SDK zum Bauen, oder die veröffentlichte `Ausschnitt.exe` ohne extra Runtime
- Wacom-Treiber von [wacom.com/start/one](https://www.wacom.com/start/one), falls du ihn ohnehin nutzt

## Starten

```bash
dotnet run --project src/Ausschnitt.App
```

Tests:

```bash
dotnet test
```

Eine einzelne Windows-Datei bauen:

```bash
dotnet publish src/Ausschnitt.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o dist
```

`dist/Ausschnitt.exe` braucht dann keine installierte Runtime.

## Wenn der Stift nach dem Beenden tot wirkt

Im Direktmodus ist der Wacom-Dienst pausiert. Ausschnitt startet ihn beim Beenden wieder. Wurde der Prozess hart beendet, in einer Eingabeaufforderung als Administrator:

```bat
net start WTabletServicePro
net start WTabletServiceCon
net start TabletServiceWacom
```

Es reicht der Dienst, den Windows tatsächlich kennt. Ein Neustart des PCs holt ihn ebenfalls zurück.

Die allererste lesbare Treiberdatei liegt als `Wacom_Tablet.dat.ausschnitt-original` neben dem Original, falls Ausschnitt sie beschrieben hat.
