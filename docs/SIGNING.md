# StayGreen.exe signieren (optional, noch nicht eingerichtet)

Die veröffentlichte `StayGreen.exe` ist **nicht digital signiert**. Deshalb zeigt Windows (SmartScreen) bei einer neuen,
unbekannten Datei eine Warnung. Die Herkunft lässt sich trotzdem prüfen:

- `SHA256SUMS.txt` liegt bei jedem Release.
- Seit Version 1.2.0 erstellt die CI einen **Herkunftsnachweis** (Build-Provenance). Er belegt, dass die Datei in
  GitHub Actions aus diesem Quelltext gebaut wurde:

  ```text
  gh attestation verify StayGreen.exe -R tho19b-oss/Staygreen
  ```

Eine Signatur ersetzt das nicht, aber sie nimmt der Warnung den Schrecken. Dafür braucht es ein Konto bei einem
Signaturdienst, das **nur der Projektinhaber** beantragen kann. Deshalb ist es hier vorbereitet, aber nicht aktiv.

## Möglichkeiten

| Dienst | Kosten | Voraussetzung |
|---|---|---|
| [SignPath Foundation](https://signpath.org/) | kostenlos für Open-Source-Projekte | Antrag und Freigabe des Projekts; Signatur über eine GitHub-Action |
| [Azure Trusted Signing](https://learn.microsoft.com/azure/trusted-signing/) | kostenpflichtig (Azure-Abo) | Identitätsprüfung; je nach Land nur für Organisationen verfügbar |
| Eigenes Code-Signing-Zertifikat | ab etwa 100 € im Jahr | Zertifikat als Secret im Repository |

Auch mit Signatur verschwindet die SmartScreen-Warnung erst, wenn die Datei genug „Reputation“ gesammelt hat
(bei Zertifikaten mit erweiterter Prüfung sofort).

## So würde es in den Workflow passen

Der Windows-Job in `.github/workflows/ci.yml` baut die EXE, testet sie und legt sie in `dist/` ab. Eine Signatur gehört
**zwischen** „Paket schnüren“ und „Herkunftsnachweis“, damit Prüfsumme und Nachweis zur *signierten* Datei gehören.
Beispiel für SignPath (Platzhalter in `<…>` ersetzen, Organisation und Projekt kommen aus dem SignPath-Konto):

```yaml
      - uses: actions/upload-artifact@v5
        id: unsigned
        with:
          name: StayGreen-unsigned
          path: dist/StayGreen.exe

      - uses: signpath/github-action-submit-signing-request@v1
        with:
          api-token: ${{ secrets.SIGNPATH_API_TOKEN }}
          organization-id: '<organization-id>'
          project-slug: 'staygreen'
          signing-policy-slug: 'release-signing'
          github-artifact-id: ${{ steps.unsigned.outputs.artifact-id }}
          wait-for-completion: true
          output-artifact-directory: dist-signed
```

Danach `dist-signed/StayGreen.exe` nach `dist/` kopieren und erst dann die Prüfsummen berechnen. Das portable ZIP muss
mit der signierten EXE neu gepackt werden.

Wichtig: Die genauen Schritte und Namen legt SignPath bei der Freigabe des Projekts fest; das Beispiel ist eine
Orientierung und wurde nicht ausgeführt.
