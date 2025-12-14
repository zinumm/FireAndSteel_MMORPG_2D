# Contribuindo

Obrigado por contribuir.

## Requisitos
- .NET SDK conforme `global.json`
- Build: `dotnet build -c Release`
- Testes: `dotnet test -c Release`

## Fluxo de trabalho
1) Faça um fork
2) Crie branch a partir de `main`:
   - `feat/<assunto>`
   - `fix/<assunto>`
   - `docs/<assunto>`
3) Commits pequenos e objetivos
4) Abra PR com descrição clara e evidência (log/teste)

## Convenções
- Mudanças devem ser cirúrgicas: evitar refatoração “gratuita”.
- Não adicionar segredos/config local no repo (use seu `Config/runtime.json` local).
- Se mexer em protocolo/mensagens, atualize a seção "Protocolo" no README e/ou `docs/`.

## O que aceitamos agora (escopo atual)
- Networking v0: framing/envelope, leitura segura, handshake/ping/pong/disconnect
- Testes automatizados e hardening
- Logging/diagnóstico, rate limit, estabilidade
- Documentação curta e objetiva

## Antes de abrir PR
- `dotnet build -c Release`
- `dotnet test -c Release` (CI deve ficar verde)
