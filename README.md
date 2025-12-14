# Fire&Steel — Sprint 2 (Networking v0.1)

## Requisitos

- .NET SDK **10.0.101** (ver `global.json`)

## Build

```bash
dotnet build -c Release
```

## Testes

```bash
dotnet test -c Release
```

## Rodar (demo)

Crie um arquivo `Config/runtime.json` (na raiz do repo) com o mínimo:

```json
{
  "network": {
    "host": "127.0.0.1",
    "port": 7777
  }
}
```

### Flags (Client/Server)

- `--config <path>`: caminho do runtime.json (mantido)
- `--host <host>`: override do host do config
- `--port <port>`: override da porta do config

**Regra:** `--host/--port` vencem o `runtime.json` quando informados.

### Server

```bash
dotnet run --project src/Server -c Release -- --config Config/runtime.json
```

Override:

```bash
dotnet run --project src/Server -c Release -- --config Config/runtime.json --host 127.0.0.1 --port 7777
```

### Client

```bash
dotnet run --project src/Client -c Release -- --config Config/runtime.json
```

Para forçar o rate limit (teste de desconexão):

```bash
dotnet run --project src/Client -c Release -- --config Config/runtime.json --spam
```

## Protocolo v0

- `Handshake` (obrigatório como 1ª mensagem; timeout no server)
- `Ping` / `Pong`
- `Disconnect` (com `DisconnectReason`)

O framing é por envelope fixo (`EnvelopeV1`) com `BodyLen` e leitura segura (`ReadExact`).

## Como contribuir
- Leia: CONTRIBUTING.md
- Código de conduta: CODE_OF_CONDUCT.md
- Segurança: SECURITY.md (não abra issue pública para vulnerabilidade)

### Boas “primeiras issues”
Procure labels:
- good first issue
- help wanted

## Comunicação
- Use Issues para trabalho e decisões técnicas.
- (Opcional) Discussions para perguntas gerais e ideias.
