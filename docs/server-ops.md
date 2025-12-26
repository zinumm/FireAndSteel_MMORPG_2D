# Server Ops (Fire&Steel)

## Como iniciar

Na raiz do repo:

### Windows (PowerShell)

```powershell
dotnet run -c Release --project src/Server -- --config Config/runtime.json --host 0.0.0.0 --port 7777
```

### Linux/macOS (bash)

```bash
dotnet run -c Release --project src/Server -- --config Config/runtime.json --host 0.0.0.0 --port 7777
```

## Logs principais (key=value)

O Server escreve logs no formato `chave=valor` com campo `evt=...`.

Eventos relevantes:
- `evt=server_start`
- `evt=server_stop_begin`
- `evt=server_stop_end`
- `evt=server_metrics` (Sprint 6)

## evt=server_metrics (snapshot periódico)

Campos (todos numéricos):
- `currentConnections`
- `totalConnections`
- `totalDisconnects`
- `messagesIn`
- `messagesOut`
- `ioErrors`
- `parseErrors`
- `unhandledErrors`

Exemplo:

```text
ts=2025-12-26T03:12:10.1234567+00:00 level=INFO evt=server_metrics currentConnections=4 totalConnections=17 totalDisconnects=13 messagesIn=900 messagesOut=880 ioErrors=0 parseErrors=2 unhandledErrors=0
```

### Como habilitar/desabilitar

Por padrão o snapshot **fica desligado**.

#### Habilitar

Windows (PowerShell):

```powershell
$env:FNS_SERVER_METRICS_SNAPSHOT_ENABLED = "true"
$env:FNS_SERVER_METRICS_SNAPSHOT_INTERVAL_SECONDS = "10"  # default: 10

dotnet run -c Release --project src/Server -- --config Config/runtime.json --host 0.0.0.0 --port 7777
```

Linux/macOS (bash):

```bash
export FNS_SERVER_METRICS_SNAPSHOT_ENABLED=true
export FNS_SERVER_METRICS_SNAPSHOT_INTERVAL_SECONDS=10

dotnet run -c Release --project src/Server -- --config Config/runtime.json --host 0.0.0.0 --port 7777
```

#### Desabilitar

- Remova a variável (`unset`) ou defina `false`.
