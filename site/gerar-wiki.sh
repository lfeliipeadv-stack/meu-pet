#!/usr/bin/env bash
# Gera as páginas HTML do Wiki a partir dos arquivos .md da pasta wiki/.
#
# O Wiki nativo do GitHub (github.com/OWNER/REPO/wiki) só passa a existir
# depois que a primeira página é criada pela interface do site. Até lá, esta
# gera uma cópia navegável em /wiki/ dentro da página do projeto, a partir
# exatamente da mesma fonte (a pasta wiki/).
#
# Usa a API de markdown do GitHub, que devolve o HTML com o mesmo visual do
# Wiki (tabelas, blocos de código, listas de tarefas).
#
# Uso:  bash site/gerar-wiki.sh
set -euo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WIKI="$RAIZ/wiki"
SAIDA="$RAIZ/site/wiki"

command -v gh >/dev/null || { echo "ERRO: precisa do gh CLI." >&2; exit 1; }

# Gera numa pasta temporaria e so troca no final: se uma pagina falhar, a
# copia que ja estava no ar continua de pe.
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT
SAIDA="$TMP/wiki"
mkdir -p "$SAIDA"
# As imagens nao sao copiadas aqui: o workflow pega a pasta wiki/images e
# coloca em _site/wiki/images, que e o mesmo lugar.

# Páginas, na ordem do menu.
PAGINAS=(
  "Home:Home"
  "Instalacao:Instalação"
  "Primeiros-passos:Primeiros passos"
  "Demo-no-navegador:Demo no navegador"
  "Painel-de-pets:Painel de pets"
  "Pergunte-ao-pet:Pergunte ao pet"
  "Lembretes-e-Pomodoro:Lembretes e Pomodoro"
  "Agenda-e-e-mails:Agenda e e-mails"
  "Pet-virtual-e-brinquedos:Pet virtual e brincadeiras"
  "Plugins-e-integracoes:Plugins e integrações"
  "Crie-o-seu-pet:Crie o seu pet"
  "Atalhos-e-comandos:Atalhos e comandos"
  "Para-desenvolvedores:Para desenvolvedores"
  "Perguntas-frequentes:Perguntas frequentes"
  "Changelog:Changelog"
)

menu() {
  local atual="$1" saida="" par item rotulo
  for par in "${PAGINAS[@]}"; do
    item="${par%%:*}"; rotulo="${par#*:}"
    if [ "$item" = "$atual" ]; then
      saida+="<li class=\"on\"><a href=\"$item.html\">$rotulo</a></li>"
    else
      saida+="<li><a href=\"$item.html\">$rotulo</a></li>"
    fi
  done
  printf '%s' "$saida"
}

pagina() {
  local arquivo="$1" destino="$2" titulo="$3" atual="$4" corpo

  # -F lê o conteúdo do arquivo e o escapa como JSON; a API devolve o HTML.
  corpo="$(gh api markdown -X POST \
    -F text=@"$arquivo" -F mode=gfm \
    -H "Accept: application/vnd.github.raw")"

  # No HTML as páginas dentro do Wiki precisam apontar para os arquivos .html gerados.
  corpo="$(printf '%s' "$corpo" | sed -E 's/href="([A-Za-z0-9_-]+)\.md"/href="\1.html"/g')"

  # Escapa o título para o HTML.
  titulo="${titulo//&/&amp;}"; titulo="${titulo//</&lt;}"; titulo="${titulo//>/&gt;}"

  cat > "$destino" <<HTML
<!DOCTYPE html>
<html lang="pt-BR">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>$titulo — Wiki do Meu Pet</title>
<link rel="icon" href="../img/icon.png">
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link href="https://fonts.googleapis.com/css2?family=Silkscreen:wght@400;700&family=Space+Grotesk:wght@400;500;700&display=swap" rel="stylesheet">
<link rel="stylesheet" href="../style.css">
</head>
<body class="wiki">
<header class="topo">
  <a class="volta" href="../">&larr; Voltar para a página do Meu Pet</a>
  <span class="marca">Wiki do Meu Pet</span>
</header>
<div class="layout">
  <nav class="lateral">
    <p class="lateral-titulo">Páginas</p>
    <ul>
$(menu "$atual")
    </ul>
  </nav>
  <main class="conteudo wiki-corpo">
$corpo
  </main>
</div>
<footer class="rodape">
  <p>O <strong>Meu Pet</strong> é um bichinho de área de trabalho para Windows 10 e 11.
  <a href="https://github.com/lfeliipeadv-stack/meu-pet">Ver no GitHub</a></p>
</footer>
</body>
</html>
HTML
}

for par in "${PAGINAS[@]}"; do
  item="${par%%:*}"; rotulo="${par#*:}"
  src="$WIKI/$item.md"
  [ -f "$src" ] || { echo "faltando: $src" >&2; exit 1; }
  pagina "$src" "$SAIDA/$item.html" "$rotulo" "$item"
  echo "gerada: $item.html"
done

# index.html aponta para a Home, para /wiki/ abrir direto no manual.
cp "$SAIDA/Home.html" "$SAIDA/index.html"

# Só agora troca a cópia antiga pela nova.
rm -rf "$RAIZ/site/wiki"
mv "$SAIDA" "$RAIZ/site/wiki"

echo "Wiki gerado em site/wiki/ ($(ls -1 "$RAIZ/site/wiki"/*.html | wc -l) paginas)"
