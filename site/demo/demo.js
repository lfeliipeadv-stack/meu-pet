/* ---------------------------------------------------------------
   demo.js — a página de demonstração.

   Aqui ficam as três coisas que o motor não faz sozinho: o mundo
   (a área de trabalho da página), o desenho no canvas e as
   conversas com o pet.
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
if (EMBED) document.body.classList.add("embed");

const world = new World();
const stage = document.getElementById("stage");
const canvas = document.getElementById("canvas");
const context = canvas.getContext("2d");
const fakeWindow = document.getElementById("fake-window");
const fakeWindowBar = document.getElementById("fake-window-bar");
const bubble = document.getElementById("bubble");
const bubbleText = document.getElementById("bubble-text");
const bubbleChoices = document.getElementById("bubble-choices");
const menu = document.getElementById("menu");
const loading = document.getElementById("loading");
const clock = document.getElementById("clock");
const petList = document.getElementById("pet-list");
const askButton = document.getElementById("ask");
const soundButton = document.getElementById("sound");

let pet = null;
let currentFolder = null;
let soundOn = true;
let drag = null;           // o que está sendo arrastado: o pet ou a janela

/* ----------------------------------------------------------- a janela do desktop */
world.windows.push({ id: "painel", title: "Meu Pet — Painel", left: 0, top: 0, right: 0, bottom: 0, z: 1 });

/** Mede o palco e a janela, e prepara o canvas. */
function measure() {
  const rect = stage.getBoundingClientRect();
  world.width = Math.max(320, Math.round(rect.width));
  world.height = Math.max(240, Math.round(rect.height));

  const winRect = fakeWindow.getBoundingClientRect();
  const win = world.windows[0];
  win.left = Math.round(winRect.left - rect.left);
  win.top = Math.round(winRect.top - rect.top);
  win.right = win.left + Math.round(winRect.width);
  win.bottom = win.top + Math.round(winRect.height);

  const ratio = Math.min(2, window.devicePixelRatio || 1);
  canvas.width = Math.round(world.width * ratio);
  canvas.height = Math.round(world.height * ratio);
  context.setTransform(ratio, 0, 0, ratio, 0, 0);
  context.imageSmoothingEnabled = false;
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
    pet.arriveFromSky();
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

function hitWindowBar(point) {
  const rect = fakeWindowBar.getBoundingClientRect();
  const stageRect = stage.getBoundingClientRect();
  return point.x >= rect.left - stageRect.left && point.x <= rect.right - stageRect.left &&
         point.y >= rect.top - stageRect.top && point.y <= rect.bottom - stageRect.top;
}

stage.addEventListener("pointerdown", (event) => {
  if (event.button === 2) return;
  const point = localPoint(event);

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

  if (hitWindowBar(point)) {
    drag = {
      what: "window",
      offsetX: point.x - world.windows[0].left,
      offsetY: point.y - world.windows[0].top
    };
    stage.classList.add("dragging-window");
    stage.setPointerCapture(event.pointerId);
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
  } else {
    const left = Math.max(0, Math.min(world.width - 80, point.x - drag.offsetX));
    const top = Math.max(0, Math.min(world.height - world.taskbarHeight - 20, point.y - drag.offsetY));
    fakeWindow.style.left = Math.round(left) + "px";
    fakeWindow.style.top = Math.round(top) + "px";
    measure();
  }
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
  const win = world.windows[0];
  pet.arriveFromSky((win.left + win.right) / 2 - pet.width / 2);
  pet.start();
  loading.hidden = true;

  for (const button of petList.querySelectorAll(".demo-pet")) {
    button.setAttribute("aria-pressed", String(button.dataset.folder === folder));
  }
}

/* ------------------------------------------------------------- o relógio */
function tickClock() {
  const now = new Date();
  clock.textContent = String(now.getHours()).padStart(2, "0") + ":" + String(now.getMinutes()).padStart(2, "0");
}

/* ----------------------------------------------------------------- início */
askButton.addEventListener("click", ask);
soundButton.addEventListener("click", toggleSound);
window.addEventListener("resize", measure);

// Um gancho para quem quiser inspecionar a demo pelo console do navegador.
let simulatedNow = 0;
window.meuPetDemo = {
  get pet() { return pet; },
  world,
  ask,
  render,
  /** Roda a simulação por alguns segundos, para testar sem depender da animação. */
  advance(milliseconds) {
    const step = 40;
    let now = Math.max(performance.now(), simulatedNow);
    const end = now + milliseconds;
    while (now < end) {
      now += step;
      render(now);
    }
    simulatedNow = now;
  }
};

buildPetList();
measure();
tickClock();
setInterval(tickClock, 15000);
requestAnimationFrame(frame);
loadPet(PETS[0].folder);
