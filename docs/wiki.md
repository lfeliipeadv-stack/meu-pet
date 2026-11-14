# O Wiki do Meu Pet

O manual do projeto tem duas cópias, geradas a partir da mesma fonte.

## A fonte: a pasta `wiki/`

Os quatorze arquivos `.md` em `wiki/` são a fonte da verdade. Editar lá e
enviar para o `master` atualiza as duas cópias. As imagens ficam em
`wiki/images/`.

| Página | O que tem |
| --- | --- |
| [Home](wiki/Home.md) | Abertura e índice de tudo |
| [Instalação](wiki/Instalacao.md) | Os três downloads, o que o instalador confere, como desinstalar |
| [Primeiros passos](wiki/Primeiros-passos.md) | O que fazer depois de instalar, em ordem |
| [Painel de pets](wiki/Painel-de-pets.md) | Baixar, adotar, remover, exportar e importar pets |
| [Pergunte ao pet](wiki/Pergunte-ao-pet.md) | A conversa, os provedores de IA e onde fica a chave |
| [Lembretes e Pomodoro](wiki/Lembretes-e-Pomodoro.md) | Os lembretes em português e o foco que silencia o pet |
| [Agenda e e-mails](wiki/Agenda-e-e-mails.md) | A agenda e a leitura do e-mail do Outlook |
| [Pet virtual e brincadeiras](wiki/Pet-virtual-e-brinquedos.md) | O humor, as brincadeiras e o menu do pet |
| [Plugins e integrações](wiki/Plugins-e-integracoes.md) | Os plugins e a ponte com o Claude Code |
| [Crie o seu pet](wiki/Crie-o-seu-pet.md) | Fazer um pet com as suas próprias imagens |
| [Atalhos e comandos](wiki/Atalhos-e-comandos.md) | Teclado, mouse e os argumentos do executável |
| [Para desenvolvedores](wiki/Para-desenvolvedores.md) | Compilar, testar e publicar uma versão |
| [Perguntas frequentes](wiki/Perguntas-frequentes.md) | Dúvidas comuns, respondidas |
| [Changelog](wiki/Changelog.md) | O que mudou em cada versão |

## A cópia no site: `site/wiki/`

Já está no ar em **<https://lfeliipeadv-stack.github.io/meu-pet/wiki/>**.

O `site/gerar-wiki.sh` transforma cada `.md` em uma página HTML com a mesma
aparência do Wiki do GitHub (usa a API de markdown do próprio GitHub) e monta
a coluna de páginas ao lado do texto. O workflow *Página de download* roda o
gerador antes de montar o site, então o manual no ar acompanha a pasta `wiki/`.

O CSS do site foi para `site/style.css`, para o site e o Wiki usarem a mesma
fonte de estilo.

## A cópia no Wiki do GitHub

O Wiki nativo do repositório fica em
**<https://github.com/lfeliipeadv-stack/meu-pet/wiki>**.

Ele ainda não existe. O GitHub cria o repositório do Wiki (o `meu-pet.wiki.git`)
**somente quando a primeira página é criada pela interface do site** — não há
API pública para isso, e o próprio `gh` retorna "the wiki has no pages yet;
create its first page on GitHub, then try again".

Para ligar:

1. Abra <https://github.com/lfeliipeadv-stack/meu-pet/wiki/_new>.
2. Escreva qualquer coisa e salve. Isso cria o repositório do Wiki.
3. Pronto: o workflow *Wiki* roda de hora em hora e publica as quatorze
   páginas com as imagens assim que o repositório existir.

Nenhuma linha de código precisa mudar. O workflow já está pronto em
`.github/workflows/wiki.yml`.

## Por que as duas cópias

O Wiki do GitHub é onde o manual deve morar: ele aparece na aba do
repositório, é indexado pelo Google e pode ser editado por qualquer pessoa com
acesso ao projeto. Mas ele depende de um clique na interface do site para
existir, e esse clique não pode ser dado por automação.

A cópia no site existe para o manual nunca estar fora do ar. As duas saem da
mesma pasta, então não divergem.
