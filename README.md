# Fire&Steel — Sprint 1 (Networking v0)

## Requisitos

- .NET SDK **10.0.101** (ver `global.json`)

## Build

```bash
dotnet build -c Release
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

### Server

```bash
dotnet run --project src/Server -c Release -- --config Config/runtime.json
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

- `Handshake` (obrigatório como 1ª mensagem)
- `Ping` / `Pong`
- `Disconnect` (com `DisconnectReason`)

O framing é por envelope fixo (`EnvelopeV1`) com `BodyLen` e leitura segura (`ReadExact`).
