# Meu Pet

Um bichinho que vive na sua área de trabalho do Windows: anda pelas janelas, cai na barra de tarefas,
dorme, faz bagunça e pode ser arrastado e arremessado com o mouse.

## Download

Baixe o `DesktopPet.exe` (e a pasta `lang`, se quiser o app em português) na página de
[releases](https://github.com/lfeliipeadv-stack/meu-pet/releases/latest).

Precisa do [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) instalado.

## O que tem

- Janela com tema claro, escuro ou automático e suporte a vários monitores
- Interface em português (arquivos de tradução em `src/lang`, fáceis de editar)
- O **eSheep** vem dentro do programa e funciona sem internet
- Galeria com dezenas de pets, baixados deste repositório (pasta `Pets`), incluindo os novos
  **Capi** (capivara), **Mago** (mago de barba branca), **Nuvi** (nuvem-terminal), **Boo** (fantasminha), **Pingo** (pinguim), **Rex** (dinossauro)
  e **Claude** (homenagem não oficial ao mascote do Claude Code, sem vínculo com a Anthropic)
- Os pets da galeria são atualizados direto pelo repositório, sem precisar de um exe novo
- Atualização automática do programa com verificação SHA-256
- Executável de menos de 1 MB

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
