/* ---------------------------------------------------------------
   demo.js — a página de demonstração.

   Aqui ficam as três coisas que o motor não faz sozinho: o mundo
   (a área de trabalho da página), o desenho no canvas e as conversas
   com o pet.

   O mundo é uma mini área de trabalho: uma janela de cada vez pode
   ser arrastada, maximizada, minimizada ou fechada, e o pet anda
   em cima delas, perde o chão quando elas somem e acompanha a
   janela que se move — como no Windows.
   --------------------------------------------------------------- */

import { Pet, World } from "./pet-engine.js";

/* Os pets que têm as animações publicadas na página. */
const PETS = [
  { folder: "esheep64", name: "eSheep 64", note: "a ovelha clássica" },
  { folder: "claude", name: "Claude", note: "o mascote" },
  { folder: "rex", name: "Rex", note: "o dinossauro" },
  { folder: "boo", name: "Boo", note: "" },
  { folder: "pingo", name: "Pingo", note: "" },
  { folder: "mago", name: "Mago", note: "" },
  { folder: "nuvi", name: "Nuvi", note: "" }
];

/* As falas do pet: as mesmas do programa (src/lang/pt-BR.json). */
const FORTUNES = [
  "Um passo pequeno hoje é um passo grande amanhã.",
  "Algo bom está para acontecer.",
  "Faça uma pausa: sua melhor ideia pode vir nela.",
  "Sua gentileza vai voltar para você.",
  "Hoje é um bom dia para aprender algo novo.",
  "Beba água. Sério.",
  "Uma tarefa terminada vale mais que um plano perfeito.",
  "Alguém está feliz por ter você por perto.",
  "Paciência também é progresso.",
  "O erro está na linha que você tem certeza que está certa."
];

const JOKES = [
  "Por que o computador foi ao médico? Porque estava com vírus!",
  "O que o zero disse para o oito? Que cinto bonito!",
  "Qual é o café mais perigoso do mundo? O ex-presso.",
  "Por que o livro de matemática estava triste? Tinha muitos problemas.",
  "O que o pato disse para a pata? Vem quá!",
  "Qual é o cúmulo da rapidez? Fechar a gaveta e trancar a mão lá dentro… antes de abrir."
];

const BALL_ASK = "Pense numa pergunta de sim ou não… e chacoalhe a bola!";
const BALL_SHAKE = "Chacoalhar";
const BALL_ANSWERS = [
  "Sim!", "Não.", "Com certeza.", "Melhor não.", "Pergunte de novo mais tarde.",
  "Sem dúvida.", "Hmm… pouco provável.", "Os sinais dizem que sim.",
  "Não dá para saber agora.", "Pode contar com isso.", "Minha resposta é não.", "Muito provável."
];

const MOOD_ASK = "Como você está hoje?";
const MOOD_FACES = ["😄", "🙂", "😐", "😕", "😢"];
const MOOD_REPLIES = [
  "Que maravilha! Vamos manter essa energia! ✨",
  "Que bom! Tenha um ótimo dia.",
  "Ok. Que tal uma voltinha ou um copo d'água?",
  "Sinto muito. Vai com calma, uma coisa de cada vez. Estou aqui.",
  "Sinto muito que você esteja mal. Converse com alguém de confiança e seja gentil com você. ❤️"
];

/* O que o pet responde quando não tem IA por perto. */
const ANSWERS = [
  "Estou andando pela janela, como sempre.",
  "Meu dia está bom! E o seu?",
  "Já pensou em dar uma pausa para beber água?",
  "Me arraste e me solte: eu sei quicar.",
  "Para me usar de verdade, baixe o programa para o Windows.",
  "Meu nome é {0} e eu moro nesta página.",
  "Shhh… deixa eu escalar mais um pouquinho.",
  "Aqui no navegador eu não leio e-mail, mas na versão para Windows eu leio.",
  "Clique com o botão direito em mim para ver o que eu sei fazer."
];

/* --------------------------------------------------------------- o mundo */
const EMBED = new URLSearchParams(window.location.search).has("embed");
// Modo foto: usado para tirar a imagem da documentação. A simulação roda
// adiantada e o quadro é desenhado uma vez só, com o pet já em cima da janela.
const SHOT = new URLSearchParams(window.location.search).has("shot");
if (EMBED) document.body.classList.add("embed");

const world = new World();
const stage = document.getElementById("stage");
const canvas = document.getElementById("canvas");
const context = canvas.getContext("2d");
const bubble = document.getElementById("bubble");
const bubbleText = document.getElementById("bubble-text");
const bubbleChoices = document.getElementById("bubble-choices");
const menu = document.getElementById("menu");
const loading = document.getElementById("loading");
const clock = document.getElementById("clock");
const petList = document.getElementById("pet-list");
const askButton = document.getElementById("ask");
const soundButton = document.getElementById("sound");
const iconList = document.getElementById("icons");
const taskbarApps = document.getElementById("taskbar-apps");

let pet = null;
let currentFolder = null;
let soundOn = true;
let drag = null;           // o que está sendo arrastado: o pet ou uma janela
let topZ = 2;              // a ordem das janelas (maior = na frente)

/* ----------------------------------------------------------- as janelas
   Cada janela é um retângulo do mundo, como as janelas de verdade do
   Windows: o pet anda no título, cai quando elas somem e acompanha
   a que se move.                                                    */

const WINDOW_DEFS = [
  { id: "notas", title: "Bloco de Notas", kind: "notes", icon: "📝",
    x: 0.13, y: 0.52, w: 300, h: 190, z: 1 },
  { id: "painel", title: "Meu Pet — Painel", kind: "panel", icon: "🐑",
    x: 0.34, y: 0.07, w: 460, h: 250, z: 2 }
];

const windows = WINDOW_DEFS.map((def) => {
  const win = {
    id: def.id,
    title: def.title,
    kind: def.kind,
    icon: def.icon,
    def,
    x: 0, y: 0,              // posição normal, em pixels
    w: def.w, h: def.h,      // tamanho normal, em pixels
    left: 0, top: 0, right: 0, bottom: 0,
    z: def.z,
    maximized: false,
    minimized: false,
    closed: false,
    gone: false,             // para o motor: janela que não existe mais
    el: null
  };
  world.windows.push(win);
  return win;
});

function windowById(id) {
  return windows.find((win) => win.id === id) || null;
}

function windowOf(element) {
  const holder = element && element.closest ? element.closest(".demo-window") : null;
  return holder ? windowById(holder.dataset.window) : null;
}

/** O corpo de cada janela: o painel do programa e um bloco de notas. */
function windowBody(win) {
  if (win.kind === "notes") {
    return `<div class="demo-note">
      <p class="demo-note-title">Lista de compras</p>
      <p>· pão de mel<br>· café<br>· mais uma ovelha<br>· Windows 11</p>
    </div>`;
  }
  return `<div class="demo-line w60"></div>
    <div class="demo-line w85"></div>
    <div class="demo-line w40"></div>
    <div class="demo-blocks"><span></span><span></span><span></span></div>`;
}

/** Cria o elemento de cada janela na tela. */
function buildWindows() {
  for (const win of windows) {
    const el = document.createElement("div");
    el.className = "demo-window";
    el.dataset.window = win.id;
    el.innerHTML = `
      <div class="demo-window-bar">
        <span class="dot min" data-action="min" title="Minimizar"></span>
        <span class="dot max" data-action="max" title="Maximizar"></span>
        <span class="dot close" data-action="close" title="Fechar"></span>
        <span class="demo-window-title"></span>
      </div>
      <div class="demo-window-body">${windowBody(win)}</div>`;
    el.querySelector(".demo-window-title").textContent = win.title;
    stage.appendChild(el);
    win.el = el;
  }
}

/** Os ícones da área de trabalho: abrem as janelas de novo. */
function buildIcons() {
  iconList.innerHTML = "";
  for (const win of windows) {
    const icon = document.createElement("button");
    icon.type = "button";
    icon.className = "demo-icon";
    icon.dataset.window = win.id;
    icon.innerHTML = `<b>${win.icon}</b><span></span>`;
    icon.querySelector("span").textContent = win.title;
    icon.addEventListener("click", () => openWindow(win));
    iconList.appendChild(icon);
  }
}

function openWindow(win) {
  win.closed = false;
  win.minimized = false;
  raiseWindow(win);
}

/** As janelas da barra de tarefas: as que ainda existem. */
function buildTaskbar() {
  taskbarApps.innerHTML = "";
  for (const win of windows) {
    if (win.closed) continue;
    const button = document.createElement("button");
    button.type = "button";
    button.className = "demo-task";
    if (!win.minimized) button.classList.add("on");
    button.innerHTML = `<b>${win.icon}</b><span></span>`;
    button.querySelector("span").textContent = win.title;
    button.addEventListener("click", () => {
      if (win.minimized) {
        win.minimized = false;
        layoutWindows();
      }
      raiseWindow(win);
    });
    taskbarApps.appendChild(button);
  }
}

/** Traz a janela para a frente, como no Windows. */
function raiseWindow(win) {
  if (win.closed) return;
  if (win.z < topZ) {
    topZ++;
    win.z = topZ;
  }
  layoutWindows();
  buildTaskbar();
}

/** O que acontece quando se clica nos botões da janela. */
function windowAction(win, action) {
  if (action === "close") {
    win.closed = true;
    win.minimized = false;
    win.gone = true;
    win.el.hidden = true;
  } else if (action === "min") {
    win.minimized = true;
    win.gone = true;
    win.el.hidden = true;
  } else if (action === "max") {
    win.maximized = !win.maximized;
  }
  layoutWindows();
  buildTaskbar();
}

/** O tamanho de cada janela: cabe na tela ou ocupa o que der. */
function windowSize(win) {
  const area = world.area;
  return {
    w: Math.max(120, Math.min(win.w, world.width - 16)),
    h: Math.max(80, Math.min(win.h, area.bottom - 12))
  };
}

/** Mede o palco, coloca as janelas no lugar e prepara o canvas. */
function measure() {
  const rect = stage.getBoundingClientRect();
  world.width = Math.max(320, Math.round(rect.width));
  world.height = Math.max(240, Math.round(rect.height));

  for (const win of windows) {
    if (!win.el) continue;
    const size = windowSize(win);
    // A posição normal só é calculada na primeira vez.
    if (!win.placed) {
      win.x = Math.round(win.def.x * world.width);
      win.y = Math.round(win.def.y * world.height);
      win.placed = true;
    }
    // A janela cabe inteira na tela quando dá; se não der, fica pelo menos
    // um pedaço visível, como no Windows.
    win.x = Math.round(Math.max(0, Math.min(world.width - size.w, win.x)));
    win.y = Math.round(Math.max(0, Math.min(world.area.bottom - 40, win.y)));
  }

  layoutWindows();

  const ratio = Math.min(2, window.devicePixelRatio || 1);
  canvas.width = Math.round(world.width * ratio);
  canvas.height = Math.round(world.height * ratio);
  context.setTransform(ratio, 0, 0, ratio, 0, 0);
  context.imageSmoothingEnabled = false;
}

/** Coloca cada janela no seu lugar (e avisa o motor). */
function layoutWindows() {
  const area = world.area;
  for (const win of windows) {
    if (!win.el) continue;

    if (win.closed) {
      win.gone = true;
      win.el.hidden = true;
      continue;
    }

    const { w, h } = windowSize(win);
    const full = win.maximized;

    const left = full ? area.left : Math.round(win.x);
    const top = full ? area.top : Math.round(win.y);

    win.left = left;
    win.top = top;
    win.right = left + (full ? area.right - area.left : w);
    win.bottom = top + (full ? area.bottom - area.top : h);

    win.gone = win.minimized;
    win.el.hidden = win.minimized;
    win.el.classList.toggle("maximized", full);
    win.el.style.left = left + "px";
    win.el.style.top = top + "px";
    win.el.style.width = (full ? area.right - area.left : w) + "px";
    win.el.style.height = (full ? area.bottom - area.top : h) + "px";
    win.el.style.zIndex = String(10 + win.z);
  }
}

/* ---------------------------------------------------------------- o desenho */
function drawPet(target) {
  if (!target.sheet) return;
  const state = target.renderState();
  const total = target.tilesX * target.tilesY;
  const frame = Math.max(0, Math.min(total - 1, state.frame));
  const column = frame % target.tilesX;
  const row = Math.floor(frame / target.tilesX);
  const sourceX = column * target.tileWidth;
  const sourceY = row * target.tileHeight;

  context.save();
  context.globalAlpha = Math.max(0, Math.min(1, state.opacity));
  if (state.flipped) {
    context.translate(state.x + state.width, state.y);
    context.scale(-1, 1);
    context.drawImage(target.sheet, sourceX, sourceY, target.tileWidth, target.tileHeight, 0, 0, state.width, state.height);
  } else {
    context.drawImage(target.sheet, sourceX, sourceY, target.tileWidth, target.tileHeight, state.x, state.y, state.width, state.height);
  }
  context.restore();
}

function render(now) {
  if (!pet) return;
  pet.update(now);
  context.clearRect(0, 0, world.width, world.height);
  drawPet(pet);
  for (const child of pet.children) drawPet(child);
  positionBubble();
}

function frame(now) {
  render(now);
  requestAnimationFrame(frame);
}

/* --------------------------------------------------------------- o balão */
function say(text, choices) {
  bubbleText.textContent = text;
  bubbleChoices.innerHTML = "";
  if (choices && choices.length) {
    for (const choice of choices) {
      const button = document.createElement("button");
      button.type = "button";
      button.textContent = choice.label;
      button.addEventListener("click", choice.run);
      bubbleChoices.appendChild(button);
    }
    bubbleChoices.hidden = false;
  } else {
    bubbleChoices.hidden = true;
  }
  bubble.hidden = false;
  positionBubble();
}

function positionBubble() {
  if (bubble.hidden || !pet) return;
  const state = pet.renderState();
  const width = bubble.offsetWidth;
  const height = bubble.offsetHeight;
  let left = state.x + state.width / 2 - width / 2;
  let top = state.y - height - 14;
  if (top < 6) top = state.y + state.height + 14;         // sem espaço em cima: o balão desce
  left = Math.max(6, Math.min(world.width - width - 6, left));
  top = Math.max(6, Math.min(world.height - height - 6, top));
  bubble.style.left = Math.round(left) + "px";
  bubble.style.top = Math.round(top) + "px";
}

function hideBubble() {
  bubble.hidden = true;
  bubbleChoices.innerHTML = "";
}

/* ----------------------------------------------------------------- o menu */
function openMenu(x, y) {
  menu.innerHTML = "";

  const head = document.createElement("div");
  head.className = "demo-menu-head";
  head.textContent = pet ? pet.header.name : "Pet";
  menu.appendChild(head);

  const item = (label, run) => {
    const button = document.createElement("button");
    button.type = "button";
    button.textContent = label;
    button.addEventListener("click", () => { closeMenu(); run(); });
    menu.appendChild(button);
  };
  const separator = () => {
    const line = document.createElement("hr");
    menu.appendChild(line);
  };

  item("💬 Perguntar ao pet…", ask);
  item("🥠 Biscoito da sorte", () => {
    pet.react(["eat", "happy"]);
    say("🥠 " + pick(FORTUNES));
  });
  item("🎱 Bola 8 mágica", () => {
    say(BALL_ASK, [{
      label: BALL_SHAKE,
      run: () => {
        pet.react(["think", "spin", "dizzy"]);
        say("🎱 " + pick(BALL_ANSWERS));
      }
    }]);
  });
  item("😄 Conte uma piada", () => say(pick(JOKES)));
  item("🙂 " + MOOD_ASK, askMood);

  separator();
  item(soundOn ? "🔊 Som: ligado" : "🔇 Som: desligado", toggleSound);
  item("🔄 Mandar este pet embora", () => {
    pet.arriveFromSky(arrivalSpot());
    say("Até logo! Volto em seguida.");
  });

  separator();
  const link = document.createElement("a");
  link.className = "demo-menu-item";
  link.href = "https://github.com/lfeliipeadv-stack/meu-pet/releases/latest/download/DesktopPet-Instalador.exe";
  link.textContent = "⬇ Baixar o programa para Windows";
  link.style.cssText = "display:block;padding:8px 10px;border-radius:5px;text-decoration:none;color:#16191c;font-size:.92rem;";
  link.addEventListener("click", closeMenu);
  menu.appendChild(link);

  menu.hidden = false;
  const width = menu.offsetWidth;
  const height = menu.offsetHeight;
  menu.style.left = Math.round(Math.max(6, Math.min(world.width - width - 6, x))) + "px";
  menu.style.top = Math.round(Math.max(6, Math.min(world.height - height - 6, y))) + "px";
}

function closeMenu() {
  menu.hidden = true;
}

function pick(list) {
  return list[Math.floor(Math.random() * list.length)];
}

/* --------------------------------------------------------------- as falas */
function ask() {
  if (!pet) return;
  pet.react(["wave", "hello", "happy", "look around"]);
  const answer = pick(ANSWERS).replace("{0}", pet.header.name);
  say(answer);
}

function askMood() {
  say(MOOD_ASK, MOOD_FACES.map((face, index) => ({
    label: face,
    run: () => {
      if (index <= 1) pet.react(["happy", "cheer", "dance", "jump"]);
      else if (index >= 3) pet.react(["love", "hearts", "hug", "wave"]);
      say(MOOD_REPLIES[index]);
    }
  })));
}

function toggleSound() {
  soundOn = !soundOn;
  if (pet) pet.volume = soundOn ? 0.6 : 0;
  soundButton.textContent = soundOn ? "🔊 Som" : "🔇 Som";
  soundButton.setAttribute("aria-pressed", String(soundOn));
}

/* ------------------------------------------------------------- o mouse */
function localPoint(event) {
  const rect = stage.getBoundingClientRect();
  return { x: event.clientX - rect.left, y: event.clientY - rect.top };
}

function hitPet(point) {
  if (!pet) return false;
  const state = pet.renderState();
  return point.x >= state.x && point.x <= state.x + state.width &&
         point.y >= state.y && point.y <= state.y + state.height;
}

stage.addEventListener("pointerdown", (event) => {
  if (event.button === 2) return;
  const point = localPoint(event);

  // O pet vem primeiro: ele é desenhado por cima de tudo.
  if (hitPet(point)) {
    const state = pet.renderState();
    drag = {
      what: "pet",
      offsetX: point.x - state.x,
      offsetY: point.y - state.y,
      moved: false,
      startX: event.clientX,
      startY: event.clientY
    };
    pet.beginDrag();
    stage.classList.add("dragging-pet");
    stage.setPointerCapture(event.pointerId);
    hideBubble();
    return;
  }

  // Os botões da janela (minimizar, maximizar, fechar).
  const action = event.target.dataset ? event.target.dataset.action : null;
  if (action) {
    const win = windowOf(event.target);
    if (win) {
      windowAction(win, action);
      event.stopPropagation();
    }
    return;
  }

  // Os ícones da área de trabalho.
  const icon = event.target.closest ? event.target.closest(".demo-icon") : null;
  if (icon) {
    const win = windowById(icon.dataset.window);
    if (win) openWindow(win);
    return;
  }

  // A barra do título: arrasta a janela.
  const bar = event.target.closest ? event.target.closest(".demo-window-bar") : null;
  if (bar) {
    const win = windowOf(bar);
    if (!win) return;
    raiseWindow(win);
    drag = {
      what: "window",
      win,
      offsetX: point.x - win.left,
      offsetY: point.y - win.top,
      moved: false,
      startX: event.clientX,
      startY: event.clientY
    };
    stage.classList.add("dragging-window");
    stage.setPointerCapture(event.pointerId);
    return;
  }

  // O resto da janela: só traz para a frente.
  const holder = event.target.closest ? event.target.closest(".demo-window") : null;
  if (holder) {
    const win = windowOf(holder);
    if (win) raiseWindow(win);
  }
});

stage.addEventListener("pointermove", (event) => {
  if (!drag) return;
  const point = localPoint(event);

  if (drag.what === "pet") {
    pet.dragTo(point.x - drag.offsetX, point.y - drag.offsetY, performance.now());
    if (Math.abs(event.clientX - drag.startX) > 4 || Math.abs(event.clientY - drag.startY) > 4) {
      drag.moved = true;
    }
    return;
  }

  const win = drag.win;
  if (win.maximized) {
    // Arrastar uma janela maximizada a restaura, como no Windows.
    win.maximized = false;
    drag.offsetX = Math.min(drag.offsetX, win.w / 2);
    drag.offsetY = Math.min(drag.offsetY, 26);
  }
  win.x = point.x - drag.offsetX;
  win.y = point.y - drag.offsetY;
  layoutWindows();
});

function endPointer(event) {
  if (!drag) return;
  if (drag.what === "pet") {
    pet.endDrag(performance.now());
    if (!drag.moved) ask();
  }
  drag = null;
  stage.classList.remove("dragging-pet", "dragging-window");
  if (event.pointerId !== undefined && stage.hasPointerCapture(event.pointerId)) {
    stage.releasePointerCapture(event.pointerId);
  }
}

stage.addEventListener("pointerup", endPointer);
stage.addEventListener("pointercancel", endPointer);

stage.addEventListener("contextmenu", (event) => {
  event.preventDefault();
  const point = localPoint(event);
  if (hitPet(point)) {
    openMenu(point.x, point.y);
  } else {
    closeMenu();
  }
});

document.addEventListener("pointerdown", (event) => {
  if (!menu.hidden && !menu.contains(event.target)) closeMenu();
});

/* --------------------------------------------------------------- os pets */
function buildPetList() {
  petList.innerHTML = "";
  for (const info of PETS) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = "demo-pet";
    button.setAttribute("aria-pressed", String(info.folder === currentFolder));
    button.dataset.folder = info.folder;

    const image = document.createElement("img");
    image.src = "pets/" + info.folder + ".gif";
    image.alt = "";
    image.loading = "lazy";

    const label = document.createElement("span");
    label.textContent = info.name;
    label.title = info.note || info.name;

    button.append(image, label);
    button.addEventListener("click", () => loadPet(info.folder));
    petList.appendChild(button);
  }
}

/** Onde o pet deve cair: em cima de uma das janelas que estão abertas. */
function arrivalSpot() {
  const open = windows.filter((win) => !win.closed && !win.minimized);
  if (!open.length) return null;
  const win = open[Math.floor(Math.random() * open.length)];
  return (win.left + win.right) / 2 - (pet ? pet.width : 40) / 2;
}

async function loadPet(folder) {
  loading.hidden = false;
  closeMenu();
  hideBubble();

  const next = new Pet(world);
  next.volume = soundOn ? 0.6 : 0;
  next.onEvent = (event) => {
    if (event.type === "spawn") measure();
  };

  try {
    await next.load("pets/" + folder + "/animations.xml");
  } catch (error) {
    loading.querySelector("p").textContent = "não foi possível carregar este pet.";
    console.error(error);
    return;
  }

  pet = next;
  currentFolder = folder;
  measure();

  // Como no primeiro arranque do programa, o pet chega do céu: ele cai
  // e pousa no topo da janela — é assim que ele aparece na tela do Windows.
  pet.arriveFromSky(arrivalSpot());
  pet.start();
  loading.hidden = true;

  // Desenha o primeiro quadro na hora: o pet aparece sem esperar o
  // próximo quadro da animação.
  if (SHOT) {
    advanceSimulation(4500);
    // Na foto, o pet fica em cima da janela do bloco de notas, andando.
    // (Uma janela colada no topo da tela é baixa demais para o pet cair
    //  em cima dela — é o mesmo que acontece no Windows.)
    const notas = windows.find((win) => win.id === "notas");
    if (notas) {
      pet.beginDrag();
      pet.dragTo((notas.left + notas.right) / 2 - 20, notas.top - 42, performance.now());
      pet.endDrag(performance.now());
      advanceSimulation(6000);
    }
  } else {
    render(performance.now());
  }

  for (const button of petList.querySelectorAll(".demo-pet")) {
    button.setAttribute("aria-pressed", String(button.dataset.folder === folder));
  }
}

/* ------------------------------------------------------------- o relógio */
function tickClock() {
  const now = new Date();
  clock.textContent = String(now.getHours()).padStart(2, "0") + ":" + String(now.getMinutes()).padStart(2, "0");
}

/* -------------------------------------------------- a simulação adiantada
   Roda a simulação por alguns segundos, sem depender da animação do
   navegador: serve para testar e para a imagem da documentação.   */
let simulatedNow = 0;
function advanceSimulation(milliseconds) {
  const step = 40;
  let now = Math.max(performance.now(), simulatedNow);
  const end = now + milliseconds;
  while (now < end) {
    now += step;
    render(now);
  }
  simulatedNow = now;
}

/* ----------------------------------------------------------------- início */
askButton.addEventListener("click", ask);
soundButton.addEventListener("click", toggleSound);
window.addEventListener("resize", measure);

// Um gancho para quem quiser inspecionar a demo pelo console do navegador.
window.meuPetDemo = {
  get pet() { return pet; },
  world,
  windows,
  ask,
  render,
  /** Mexe nas janelas do desktop, como se fossem clicadas. */
  windowAction(id, action) {
    const win = windowById(id);
    if (win) windowAction(win, action);
  },
  raiseWindow(id) {
    const win = windowById(id);
    if (win) raiseWindow(win);
  },
  moveWindow(id, x, y) {
    const win = windowById(id);
    if (!win) return;
    win.x = x;
    win.y = y;
    layoutWindows();
  },
  /** Onde o pet está em relação às janelas (para conferir o movimento). */
  state() {
    return {
      animacao: pet ? pet.current.name : null,
      x: pet ? Math.round(pet.x) : null,
      y: pet ? Math.round(pet.y) : null,
      janela: pet && pet.window ? pet.window.id : null
    };
  },
  /** Roda a simulação por alguns segundos, para testar sem depender da animação. */
  advance: advanceSimulation
};

buildWindows();
buildIcons();
buildTaskbar();
buildPetList();
measure();
tickClock();
setInterval(tickClock, 15000);
requestAnimationFrame(frame);
loadPet(PETS[0].folder);
