# Demo no navegador

[Home](Home.md) · [Instalação](Instalacao.md) · [Primeiros passos](Primeiros-passos.md) · [Painel de pets](Painel-de-pets.md) · [Pergunte ao pet](Pergunte-ao-pet.md) · [Lembretes e Pomodoro](Lembretes-e-Pomodoro.md) · [Agenda e e-mails](Agenda-e-e-mails.md) · [Plugins e integrações](Plugins-e-integracoes.md) · [Crie o seu pet](Crie-o-seu-pet.md) · [Atalhos e comandos](Atalhos-e-comandos.md) · [Para desenvolvedores](Para-desenvolvedores.md) · [Perguntas frequentes](Perguntas-frequentes.md) · [Changelog](Changelog.md)

Existe uma versão do pet que roda **dentro do navegador**, na página do projeto:

**https://lfeliipeadv-stack.github.io/meu-pet/demo.html**

Não é vídeo nem imagem: é o pet de verdade, desenhado quadro a quadro num `canvas`, usando o mesmo arquivo `animations.xml` que o programa para Windows abre. Você vê o bicho chegar do céu, cair, pousar no topo da janela, andar por ela, descer para a barra de tarefas e virar de frente quando chega na borda.

![A demo no navegador: a ovelha andando no topo da janela azul, dentro de uma mini área de trabalho com papel de parede e barra de tarefas](images/demo.png)

*A demo no navegador: a ovelha andando no título da janela. Ao fundo, o papel de parede e, embaixo, a barra de tarefas.*

## O que dá para fazer na demo

| Ação | Como fazer |
| --- | --- |
| Conversar | Clique no pet (o balão responde) ou use o botão **Perguntar ao pet** |
| Brincar | Botão direito no pet: biscoito da sorte, bola 8 mágica, piada e "como você está hoje?" |
| Arremessar | Arraste o pet e solte com força: ele voa, bate na borda e quica |
| Trocar de pet | Os botões embaixo do palco: eSheep 64, Claude, Rex, Boo, Pingo, Mago e Nuvi |
| Ligar e desligar o som | O botão **Som**, ou o item do menu |
| Mexer na janela | Arraste a barra azul da janela: o pet acompanha o movimento, como no Windows |

## O que a demo **não** tem

A demo é o mesmo pet com o mesmo motor, mas o mundo em que ele anda é a página, não o Windows. Por isso ela **não** tem:

- IA (o "Perguntar ao pet" da demo responde com as falas guardadas no programa, sem chamar nenhum serviço);
- agenda, e-mails do Outlook, lembretes, Pomodoro e os outros plugins;
- atalhos de teclado e menu da bandeja;
- multi-monitor, tamanhos 1×/2×/4× e o painel de Configurações.

Tudo isso vive dentro do [programa para Windows](Instalacao.md), que é gratuito.

## Como ela foi feita

O motor do pet (`FormPet.cs`, `FormPet.Motion.cs`, `MotionTrack.cs` e `Xml.cs`) foi portado para JavaScript em `site/demo/pet-engine.js`. A porta segue o C# passo a passo:

- as expressões do XML (`screenW`, `areaH`, `random`, `imageX`…) são avaliadas em inteiros, como o `DataTable.Compute` do .NET;
- a escolha da próxima animação respeita o `only` e as probabilidades de cada `<next>`;
- a queda sobre as janelas, o acompanhamento da janela, a borda da tela e o arremesso com quique são os mesmos do programa;
- o `animations.xml` usado é o do próprio pet, sem alteração — é por isso que a demo tem as mesmas animações e os mesmos sons do programa.

A página (`site/demo.html`, `site/demo/demo.js` e `site/demo/demo.css`) desenha o palco, a janela de mentira e a barra de tarefas, e cuida do mouse: arrastar o pet, arrastar a janela, o menu e o balão de fala.

## Ver também

- [Instalação](Instalacao.md) — o programa para Windows, que é a versão completa.
- [Primeiros passos](Primeiros-passos.md) — o primeiro uso do programa e a tela de apresentação.
- [Painel de pets](Painel-de-pets.md) — a galeria de pets e o painel de detalhes de cada um.
- [Pergunte ao pet](Pergunte-ao-pet.md) — a conversa de verdade, com IA.
- [Para desenvolvedores](Para-desenvolvedores.md) — compilar, publicar e a estrutura do repositório.

## Onde fica no código

- `site/demo.html` — a página da demo (com o modo `?embed=1`, que a página principal usa dentro de um `iframe`).
- `site/demo/pet-engine.js` — o motor do pet portado do C# para o navegador.
- `site/demo/demo.js` — o palco, o desenho, o menu, o balão e as falas.
- `site/demo/demo.css` — o papel de parede, a janela de mentira, a barra de tarefas e o modo embutido.
- `site/index.html` — a seção "Demo" da página principal, com o `iframe` da demo.
- `.github/workflows/pages.yml` — publica a demo e copia os `animations.xml` dos pets que aparecem nela.
