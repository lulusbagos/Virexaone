# Virexa One

This repository contains the Unity and mobile applications, plus a source-only Git mirror of the backend. The backend process used for local development still lives beside this workspace:

| Folder | Purpose |
| --- | --- |
| `Assets/`, `Packages/`, `ProjectSettings/` | Unity digital twin and its project settings |
| `../Backend/`, `../Backend.Tests/` | Canonical local API source and tests |
| `Backend/`, `Backend.Tests/` | Source-only GitHub mirror of the same API and tests |
| `virexa_in_cabin_flutter/` | Flutter in-cabin mobile app |
| `../Builds/` | Generated backend deployment packages; not source code |
| `Design/` | Design sources and references |

Edit the canonical local `D:\4. PROJECT\20. Astha\Backend`, then run `./sync_backend_source.ps1 -Apply` and `./sync_backend_source.ps1` before committing. The in-repo `Backend/` is a source mirror for GitHub, not a second running service. On a fresh clone, it can be built directly with `dotnet build Backend/Virexaone.FMS.Backend.csproj`. Run `../Backend/publish_backend_server.ps1` locally to produce a Windows server package under `../Builds/`; on a fresh clone, use `Backend/publish_backend_server.ps1`. See [backend deployment](Backend/DEPLOYMENT.md) before copying to a server. Never commit `appsettings.Local.json` or generated build output.

Unity runtime scripts are grouped under `Assets/Scripts/Core`, `Fleet`, `GIS`, `Presentation`, `Rendering`, and `InCabin`. Unity editor-only scripts remain in `Assets/Editor` and `Assets/Scripts/Editor`. Asset `.meta` files must travel with their assets so Unity scene references remain stable.

The mobile app has its own [README](virexa_in_cabin_flutter/README.md). Generated folders such as `Library`, `Temp`, `bin`, `obj`, and `.dart_tool` are not source code.

Architecture and source ownership are in the [system blueprint](Design/SYSTEM_BLUEPRINT.md). [FMS readiness](Design/FMS_READINESS.md) is a preparation checklist, not an enabled dispatch workflow. The [roadmap](FMS_ROADMAP_AND_DATABASE_SPECS.md) and [developer guide](VIREXAONE_FMS_DEVELOPMENT_GUIDE.md) distinguish source implementation, deployment, and device validation.
