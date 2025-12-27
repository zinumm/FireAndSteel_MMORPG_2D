# Fire & Steel — 2D MMORPG (C# / .NET)

Projeto de estudo e construção incremental de um MMORPG 2D (estilo OT/Tibia-like), com arquitetura **Client/Server** e foco em engenharia: CI, testes, qualidade, incrementos pequenos e rastreáveis.

## Status atual (main)
- CI: **build + test** (GitHub Actions) ✅
- Qualidade (Server): **nullable warnings como erro** ✅
- Confiabilidade (Server Ops v0.4): **shutdown limpo e idempotente + metrics snapshot periódico + guardrails de lifecycle** ✅

---

## Stack
- C# / .NET 10
- GitHub Actions (CI)
- VS Code (dev)

---

## Operação (Server)
### Logs principais
- `evt=server_start`
- `evt=server_stop_begin`
- `evt=server_stop_end`
- `evt=server_metrics` (snapshot periódico de métricas)

### Habilitar snapshot periódico de métricas (Server-only)
Por padrão: **desligado**.

**Windows (PowerShell):**
```powershell
$env:FNS_SERVER_METRICS_SNAPSHOT_ENABLED="true"
$env:FNS_SERVER_METRICS_SNAPSHOT_INTERVAL_SECONDS="10"
