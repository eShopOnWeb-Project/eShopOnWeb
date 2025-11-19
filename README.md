# eShopOnWeb - Kørsel med Docker Compose

## Sådan kører du projektet

1. Naviger til rodmappen `eShopOnWeb/`, hvor filen `all-services.yaml` ligger.

2. Åbn PowerShell i den mappe.

3. Kør følgende kommando for at bygge og starte alle services:

```powershell
docker compose -f all-services.yaml up --build
