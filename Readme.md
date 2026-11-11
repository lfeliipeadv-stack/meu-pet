# Meu Pet

Um bichinho que vive na sua área de trabalho do Windows: anda pelas janelas, cai na barra de tarefas,
dorme, faz bagunça e pode ser arrastado e arremessado com o mouse.

## Download

Baixe o `DesktopPet.exe` (e a pasta `lang`, se quiser o app em português) na página de
[releases](https://github.com/lfeliipeadv-stack/meu-pet/releases/latest).

Precisa do [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) instalado.

## O que tem

- Janela de configurações nova, com tema claro/escuro e suporte a vários monitores
- Interface em português (arquivos de tradução em `src/lang`, fáceis de editar)
- Pets embutidos que funcionam sem internet: **Capi** (capivara), **Mago** (mago de barba branca), **Nuvi** (nuvem-terminal) e **Claude** (homenagem não oficial ao mascote do Claude Code, sem vínculo com a Anthropic)
- Galeria com dezenas de outros pets, baixados deste repositório
- Atualização automática com verificação SHA-256
- Executável de cerca de 1 MB

## Compilar

```bash
dotnet build src/DesktopPet.Net.csproj -c Release
```

Para gerar um release (versão, exe, `lang.zip` e `SHA256SUMS`):

```bash
pwsh Tools/release.ps1 -Version 2.1.0 -NoUpload
```

Sem `-NoUpload`, o script também publica o release no GitHub.

## Licença

MIT, veja [LICENSE.md](LICENSE.md).
