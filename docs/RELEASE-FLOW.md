# 🚀 Progesi Release Flow

Questo documento descrive la catena completa di rilascio di **Progesi**, dal commit locale fino alla pubblicazione automatica dei pacchetti su **NuGet.org** e **GitHub Packages**, con creazione della Release su GitHub.

---

## 🔄 Flusso generale (Mermaid)

```mermaid
flowchart TD
  A[Dev: commit con Conventional Commits] --> B[Push su main]
  B --> C{Release?}
  C -- No --> D[CI: build+test pull/branch]
  C -- Sì --> E[pwsh tools/End-to-End-Release.ps1]
  subgraph Local
    E --> E1[Build-Changelog.ps1\n genera .changelog-LATEST.md\n e aggiorna CHANGELOG.md]
    E1 --> E2[Commit & push changelog]
    E2 --> E3[Calcolo nuova versione\n da git log (feat/fix/breaking)]
    E3 --> E4[Tag annotato vX.Y.Z\n(+ opzionale -Pre)]
    E4 -->|push tag| F
  end

  subgraph GitHub Actions (release.yml)
    F[trigger: push tag v*] --> G[Setup .NET + fetch-depth:0]
    G --> H[Build + Pack (MinVer)]
    H --> I[Verify-Packages.ps1\n readme in nupkg, snupkg, sourcelink]
    I --> J[Build-Changelog.ps1\n per sicurezza lato CI]
    J --> K[Publish to NuGet.org\n --skip-duplicate]
    J --> L[Publish to GPR (GitHub Packages)\n --skip-duplicate]
    K --> M[Create GitHub Release\n files: nupkg\n body_path: .changelog-LATEST.md]
    L --> M
  end

  D --> A
  M --> N[Pacchetti su NuGet/GPR\n Release GitHub pronta]
