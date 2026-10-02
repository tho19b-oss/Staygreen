#!/usr/bin/env python3
"""
Erzeugt aus Version und SHA256 der EXE die Paketbeschreibungen fuer die Verteilung:

  staygreen.json                 Scoop-Manifest. Direkt installierbar:
                                   scoop install <URL der staygreen.json im Release>
  winget/<id>.yaml (3 Dateien)   Vorlage fuer das winget-Repository (microsoft/winget-pkgs).
                                 Das Einreichen (Pull Request) geschieht dort; die Dateien hier sind fertig befuellt.

Nur Standardbibliothek, laeuft in der CI (ubuntu) und lokal:

  python3 tools/make_manifests.py --version 1.2.0 --sha256 <hash der StayGreen.exe> --out dist/manifests
"""
import argparse
import json
import os
import re
import sys

OWNER = "tho19b-oss"
REPO = "Staygreen"
PACKAGE_ID = "%s.StayGreen" % OWNER
MANIFEST_VERSION = "1.6.0"


def release_url(version):
    return "https://github.com/%s/%s/releases/download/v%s/StayGreen.exe" % (OWNER, REPO, version)


def scoop_manifest(version, sha256):
    return {
        "version": version,
        "description": "Haelt deinen Microsoft-Teams-Status auf Verfuegbar, wenn du kurz nicht am Platz bist. "
                       "(Keeps your Teams status green while you are briefly away.)",
        "homepage": "https://github.com/%s/%s" % (OWNER, REPO),
        "license": "MIT",
        "url": release_url(version),
        "hash": sha256.lower(),
        # "bin" legt eine Verknuepfung im PATH an: "StayGreen --toggle" funktioniert dann in jeder Konsole.
        "bin": "StayGreen.exe",
        "shortcuts": [["StayGreen.exe", "StayGreen"]],
        "checkver": "github",
        "autoupdate": {
            "url": "https://github.com/%s/%s/releases/download/v$version/StayGreen.exe" % (OWNER, REPO),
        },
    }


def winget_files(version, sha256):
    head = "# yaml-language-server: $schema=https://aka.ms/winget-manifest.%s.%s.schema.json\n"
    version_yaml = (
        head % ("version", MANIFEST_VERSION)
        + "PackageIdentifier: %s\n" % PACKAGE_ID
        + "PackageVersion: %s\n" % version
        + "DefaultLocale: en-US\n"
        + "ManifestType: version\n"
        + "ManifestVersion: %s\n" % MANIFEST_VERSION
    )
    installer_yaml = (
        head % ("installer", MANIFEST_VERSION)
        + "PackageIdentifier: %s\n" % PACKAGE_ID
        + "PackageVersion: %s\n" % version
        + "InstallerType: portable\n"
        + "Commands:\n"
        + "- StayGreen\n"
        + "Installers:\n"
        + "- Architecture: neutral\n"
        + "  InstallerUrl: %s\n" % release_url(version)
        + "  InstallerSha256: %s\n" % sha256.upper()
        + "ManifestType: installer\n"
        + "ManifestVersion: %s\n" % MANIFEST_VERSION
    )
    locale_yaml = (
        head % ("defaultLocale", MANIFEST_VERSION)
        + "PackageIdentifier: %s\n" % PACKAGE_ID
        + "PackageVersion: %s\n" % version
        + "PackageLocale: en-US\n"
        + "Publisher: %s\n" % OWNER
        + "PublisherUrl: https://github.com/%s\n" % OWNER
        + "PackageName: StayGreen\n"
        + "PackageUrl: https://github.com/%s/%s\n" % (OWNER, REPO)
        + "License: MIT\n"
        + "LicenseUrl: https://github.com/%s/%s/blob/main/LICENSE\n" % (OWNER, REPO)
        + "ShortDescription: Keeps your Microsoft Teams status green while you are briefly away.\n"
        + "Tags:\n"
        + "- teams\n"
        + "- presence\n"
        + "- keep-awake\n"
        + "ManifestType: defaultLocale\n"
        + "ManifestVersion: %s\n" % MANIFEST_VERSION
    )
    return {
        "%s.yaml" % PACKAGE_ID: version_yaml,
        "%s.installer.yaml" % PACKAGE_ID: installer_yaml,
        "%s.locale.en-US.yaml" % PACKAGE_ID: locale_yaml,
    }


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--version", required=True, help="Version ohne fuehrendes v, z. B. 1.2.0")
    parser.add_argument("--sha256", required=True, help="SHA256 der StayGreen.exe (64 Hex-Zeichen)")
    parser.add_argument("--out", required=True, help="Ausgabeordner")
    args = parser.parse_args(argv)

    if not re.fullmatch(r"\d+\.\d+\.\d+", args.version):
        parser.error("Version muss die Form 1.2.3 haben")
    if not re.fullmatch(r"[0-9a-fA-F]{64}", args.sha256):
        parser.error("SHA256 muss aus 64 Hex-Zeichen bestehen")

    winget_dir = os.path.join(args.out, "winget")
    os.makedirs(winget_dir, exist_ok=True)

    with open(os.path.join(args.out, "staygreen.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump(scoop_manifest(args.version, args.sha256), f, indent=4, ensure_ascii=False)
        f.write("\n")

    for name, text in winget_files(args.version, args.sha256).items():
        with open(os.path.join(winget_dir, name), "w", encoding="utf-8", newline="\n") as f:
            f.write(text)

    print("Manifeste fuer %s nach %s geschrieben" % (args.version, args.out))
    return 0


if __name__ == "__main__":
    sys.exit(main())
