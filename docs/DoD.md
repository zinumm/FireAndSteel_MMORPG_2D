# Definition of Done (DoD) — v1

Um item do backlog só é considerado **Done** quando:

1. **Compila em Release**
   - `dotnet build -c Release`

2. **CI verde**
   - Workflow `ci` passou em push/PR

3. **Sem lixo no Git**
   - `bin/` e `obj/` não entram no commit

4. **Incremento verificável**
   - Existe evidência objetiva: log, comportamento observável, ou teste

5. **Padrões do repo**
   - Estilo e organização respeitam `.editorconfig` e `Directory.Build.props`

6. **Documentação mínima**
   - Se mudou execução/config, atualizar README ou docs do módulo
