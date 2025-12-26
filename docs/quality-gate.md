# Quality Gate (Server)

## Comandos
- Build (Release):
  - `dotnet build -c Release`
- Testes (Release):
  - `dotnet test -c Release`

## Política de warnings
- **Server**: warnings de nulabilidade (CS86xx) são tratados como **erro**.
- Demais projetos: warnings continuam como **warning** (sem travar evolução).

## Implementação
- `src/Server/Server.csproj`:
  - `<WarningsAsErrors>$(WarningsAsErrors);nullable</WarningsAsErrors>`
