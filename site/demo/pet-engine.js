/* ---------------------------------------------------------------
   pet-engine.js — o motor do DesktopPet (C#) rodando no navegador.

   Tradução fiel de:
     src/dotNet/Xml.cs             — a leitura do animations.xml
     src/dotNet/Animations.cs      — a máquina de estados das animações
     src/dotNet/FormPet.cs         — o passo a passo (NextStep)
     src/dotNet/FormPet.Motion.cs  — o arraste e o arremesso
     src/dotNet/MotionTrack.cs     — a suavização do movimento

   As janelas do Windows viram retângulos da página: a área de trabalho
   simulada, a janelinha onde o pet anda e a barra de tarefas. Tudo o
   mais — quadros, intervalos, probabilidades, transições — continua
   vindo do animations.xml do próprio pet, o mesmo arquivo que o
   programa usa.

   Nada aqui é uma imitação do pet: é o mesmo XML, a mesma lógica.
   --------------------------------------------------------------- */

/* ---------------------------------------------------------------
   1. Valores dinâmicos (TValue em Animations.cs)

   O XML aceita expressões como "screenW+10-areaH/2". No C# elas
   passam por DataTable.Compute, que faz aritmética inteira. Aqui
   as expressões são avaliadas com a mesma regra (divisão trunca).
   --------------------------------------------------------------- */

const IDENTIFIERS = /\b(screenW|screenH|areaW|areaH|imageW|imageH|imageX|imageY|random|randS|scale)\b/g;

/** Avalia uma expressão inteira do XML (DataTable.Compute em C#). */
export function evaluateExpression(text, ctx) {
  if (text === null || text === undefined) return 0;
  const source = String(text).trim();
  if (source === "") return 0;

  const substituted = source.replace(IDENTIFIERS, (name) => {
    const value = ctx[name];
    return value === undefined || value === null ? 0 : String(Math.trunc(value));
  });

  // A expressão só pode conter números e as quatro operações.
  if (!/^[-+*/()\d\s]+$/.test(substituted)) return 0;

  let pos = 0;
  const peek = () => substituted[pos];
  const eat = (ch) => {
    if (substituted[pos] === ch) { pos++; return true; }
    return false;
  };

  // expr := term (('+' | '-') term)*
  function expr() {
    let value = term();
    for (;;) {
      if (eat("+")) value += term();
      else if (eat("-")) value -= term();
      else return value;
    }
  }
  // term := factor (('*' | '/') factor)*
  function term() {
    let value = factor();
    for (;;) {
      if (eat("*")) value *= factor();
      else if (eat("/")) {
        const divisor = factor();
        value = divisor === 0 ? 0 : Math.trunc(value / divisor);
      } else return value;
    }
  }
  // factor := number | '(' expr ')' | '-' factor
  function factor() {
    if (eat("-")) return -factor();
    if (eat("(")) {
      const value = expr();
      eat(")");
      return value;
    }
    const start = pos;
    while (pos < substituted.length && substituted[pos] >= "0" && substituted[pos] <= "9") pos++;
    if (start === pos) throw new Error("expressão inválida: " + source);
    return parseInt(substituted.slice(start, pos), 10);
  }

  try {
    const result = expr();
    if (pos !== substituted.length) return 0;
    return Math.trunc(result);
  } catch (e) {
    return 0;
  }
}

/** Um valor do XML: um número fixo ou uma expressão reavaliada a cada uso. */
export class TValue {
  constructor(text) {
    this.compute = (text === null || text === undefined) ? "" : String(text).trim();
    this.isDynamic = /random|randS|imageX|imageY/.test(this.compute);
    this.isScreen = /screen|area/.test(this.compute);
    this.value = 0;
    this.cached = null;
  }
  get(ctx) {
    if (!this.isDynamic && !this.isScreen && this.cached !== null) return this.cached;
    const result = evaluateExpression(this.compute, ctx);
    if (!this.isDynamic && !this.isScreen) this.cached = result;
    return result;
  }
}

/* ---------------------------------------------------------------
   2. As estruturas do animations.xml (Xml.LoadAnimations)
   --------------------------------------------------------------- */

const ONLY = {
  NONE: "none",
  TASKBAR: "taskbar",
  WINDOW: "window",
  WINDOWSIDE: "windowSide",
  HORIZONTAL: "horizontal",
  HORIZONTAL_PLUS: "horizontal+",
  VERTICAL: "vertical",
  VERTICAL_PLUS: "vertical+",
  PETSIDE: "petSide"
};

/* O valor que vem no XML, traduzido para o mesmo vocabulário. */
const ONLY_FROM_XML = {
  none: ONLY.NONE,
  taskbar: ONLY.TASKBAR,
  window: ONLY.WINDOW,
  windowSide: ONLY.WINDOWSIDE,
  horizontal: ONLY.HORIZONTAL,
  "horizontal+": ONLY.HORIZONTAL_PLUS,
  vertical: ONLY.VERTICAL,
  "vertical+": ONLY.VERTICAL_PLUS,
  petSide: ONLY.PETSIDE
};

function childElement(node, name) {
  for (let i = 0; i < node.children.length; i++) {
    if (node.children[i].localName === name) return node.children[i];
  }
  return null;
}

function childElements(node, name) {
  const out = [];
  if (!node) return out;
  for (let i = 0; i < node.children.length; i++) {
    if (node.children[i].localName === name) out.push(node.children[i]);
  }
  return out;
}

function childText(node, name, fallback = "") {
  const el = childElement(node, name);
  return el ? el.textContent.trim() : fallback;
}

function parseMovement(node) {
  return {
    x: new TValue(childText(node, "x", "0")),
    y: new TValue(childText(node, "y", "0")),
    interval: new TValue(childText(node, "interval", "200")),
    offsetY: parseInt(childText(node, "offsety", "0"), 10) || 0,
    opacity: childText(node, "opacity", "1") === "" ? 1 : parseFloat(childText(node, "opacity", "1"))
  };
}

function parseNextList(node) {
  return childElements(node, "next").map((el) => ({
    id: parseInt(el.textContent.trim(), 10) || 0,
    probability: parseInt(el.getAttribute("probability") || "0", 10) || 0,
    only: ONLY_FROM_XML[el.getAttribute("only")] || ONLY.NONE
  }));
}

/** Lê o animations.xml e devolve as estruturas do pet. */
export function parsePetXml(xmlText) {
  const doc = new DOMParser().parseFromString(xmlText, "application/xml");
  const failure = doc.getElementsByTagName("parsererror")[0];
  if (failure) throw new Error("animations.xml inválido: " + failure.textContent.trim());

  const root = doc.documentElement;

  const headerNode = childElement(root, "header");
  const header = {
    name: headerNode ? childText(headerNode, "petname", "Pet") : "Pet",
    title: headerNode ? childText(headerNode, "title", "") : "",
    author: headerNode ? childText(headerNode, "author", "") : "",
    info: headerNode ? childText(headerNode, "info", "") : ""
  };

  const imageNode = childElement(root, "image");
  const image = {
    tilesX: imageNode ? parseInt(childText(imageNode, "tilesx", "1"), 10) || 1 : 1,
    tilesY: imageNode ? parseInt(childText(imageNode, "tilesy", "1"), 10) || 1 : 1,
    skins: imageNode ? parseInt(childText(imageNode, "skins", "1"), 10) || 1 : 1,
    png: imageNode ? childText(imageNode, "png", "") : ""
  };

  const animations = new Map();
  const animationsNode = childElement(root, "animations");
  for (const node of childElements(animationsNode, "animation")) {
    const id = parseInt(node.getAttribute("id"), 10);
    const startNode = childElement(node, "start");
    const endNode = childElement(node, "end");
    const sequenceNode = childElement(node, "sequence");

    const frames = childElements(sequenceNode, "frame").map((el) => parseInt(el.textContent.trim(), 10) || 0);
    const repeatFrom = parseInt(sequenceNode ? sequenceNode.getAttribute("repeatfrom") || "0" : "0", 10) || 0;
    const repeat = new TValue(sequenceNode ? sequenceNode.getAttribute("repeat") || "0" : "0");
    // A ação é um elemento <action> dentro da sequência (XmlData: [XmlElement("action")]).
    const action = (sequenceNode ? childText(sequenceNode, "action", "") : "") ||
                   (sequenceNode ? sequenceNode.getAttribute("action") || "none" : "none");

    const animation = {
      id,
      name: childText(node, "name", String(id)),
      start: parseMovement(startNode),
      end: parseMovement(endNode),
      sequence: {
        frames,
        repeatFrom,
        repeat,
        action,
        totalSteps: frames.length + (frames.length - repeatFrom) * repeat.value
      },
      endAnimation: [],
      endBorder: [],
      endGravity: [],
      gravity: false,
      border: false
    };

    const sequence = childElement(node, "sequence");
    animation.endAnimation = parseNextList(sequence);

    const borderNode = childElement(node, "border");
    if (borderNode) {
      animation.border = true;
      animation.endBorder = parseNextList(borderNode);
    }

    const gravityNode = childElement(node, "gravity");
    if (gravityNode) {
      animation.gravity = true;
      animation.endGravity = parseNextList(gravityNode);
    }

    animations.set(id, animation);
  }

  const spawns = new Map();
  const spawnsNode = childElement(root, "spawns");
  for (const node of childElements(spawnsNode, "spawn")) {
    const id = parseInt(node.getAttribute("id"), 10);
    spawns.set(id, {
      id,
      probability: parseInt(node.getAttribute("probability") || "100", 10) || 0,
      start: {
        x: new TValue(childText(node, "x", "0")),
        y: new TValue(childText(node, "y", "0"))
      },
      next: parseInt(childText(node, "next", "1"), 10) || 1
    });
  }

  const children = new Map();
  const childsNode = childElement(root, "childs");
  for (const node of childElements(childsNode, "child")) {
    const animationId = parseInt(node.getAttribute("animationid"), 10);
    const list = children.get(animationId) || [];
    list.push({
      animationId,
      position: {
        x: new TValue(childText(node, "x", "0")),
        y: new TValue(childText(node, "y", "0"))
      },
      next: parseInt(childText(node, "next", "1"), 10) || 1
    });
    children.set(animationId, list);
  }

  const sounds = new Map();
  const soundsNode = childElement(root, "sounds");
  for (const node of childElements(soundsNode, "sound")) {
    const animationId = parseInt(node.getAttribute("animationid"), 10);
    const data = childText(node, "base64", "");
    if (!data) continue;
    sounds.set(animationId, {
      animationId,
      probability: parseInt(childText(node, "probability", "0"), 10) || 0,
      loop: parseInt(childText(node, "loop", "0"), 10) || 0,
      data
    });
  }

  // As animações-chave têm nome reservado (Xml.LoadAnimations).
  const keyAnimations = { drag: -1, fall: -1, toss: -1, fallSoft: -1, fallHard: -1, sync: -1, kill: -1 };
  for (const animation of animations.values()) {
    switch (animation.name) {
      case "drag": keyAnimations.drag = animation.id; break;
      case "fall": keyAnimations.fall = animation.id; break;
      case "toss": keyAnimations.toss = animation.id; break;
      case "fall soft": keyAnimations.fallSoft = animation.id; break;
      case "fall hard": keyAnimations.fallHard = animation.id; break;
      case "sync": keyAnimations.sync = animation.id; break;
      case "kill": keyAnimations.kill = animation.id; break;
    }
  }

  return { header, image, animations, spawns, children, sounds, keyAnimations };
}

/* ---------------------------------------------------------------
   3. A suavização do movimento (MotionTrack.cs)
   --------------------------------------------------------------- */

class MotionTrack {
  constructor() {
    this.fromX = 0; this.fromY = 0; this.toX = 0; this.toY = 0;
    this.started = 0; this.duration = 0; this.initialized = false;
  }
  reset(x, y, now) {
    this.fromX = this.toX = x;
    this.fromY = this.toY = y;
    this.started = now; this.duration = 0; this.initialized = true;
  }
  sample(now) {
    const t = this.duration <= 0 ? 1 : Math.max(0, Math.min(1, (now - this.started) / this.duration));
    return { x: this.fromX + (this.toX - this.fromX) * t, y: this.fromY + (this.toY - this.fromY) * t };
  }
  moveTo(x, y, now, milliseconds) {
    if (!this.initialized) { this.reset(x, y, now); return; }
    const position = this.sample(now);
    this.fromX = position.x; this.fromY = position.y;
    this.toX = x; this.toY = y;
    this.started = now; this.duration = Math.max(1, milliseconds);
  }
  isMoving(now) {
    return this.initialized && now < this.started + this.duration && (this.fromX !== this.toX || this.fromY !== this.toY);
  }
  follow(oldLeft, newLeft, ratio, dy) {
    this.fromX = newLeft + (this.fromX - oldLeft) * ratio;
    this.toX = newLeft + (this.toX - oldLeft) * ratio;
    this.fromY += dy; this.toY += dy;
  }
}

/** A velocidade do arraste, para saber com que força o pet foi jogado. */
class DragVelocity {
  constructor() { this.samples = []; }
  reset(x, y, now) { this.samples = [{ time: now, x, y }]; }
  add(x, y, now) {
    if (this.samples.length && now <= this.samples[this.samples.length - 1].time) return;
    this.samples.push({ time: now, x, y });
    while (this.samples.length > 2 && this.samples[1].time <= now - 80) this.samples.shift();
  }
  tossForce(now) {
    if (this.samples.length < 2) return { x: 0, y: 0 };
    const first = this.samples[0];
    const last = this.samples[this.samples.length - 1];
    const elapsed = last.time - first.time;
    if (elapsed < 8 || now - last.time > 80) return { x: 0, y: 0 };
    return { x: ((last.x - first.x) / elapsed) * 10, y: ((last.y - first.y) / elapsed) * 10 };
  }
}

/* ---------------------------------------------------------------
   4. O mundo: a área de trabalho simulada da página

   No Windows, o pet anda no topo das janelas e na barra de tarefas.
   Aqui, "janela" é um retângulo da página e a barra de tarefas é a
   faixa de baixo do palco.
   --------------------------------------------------------------- */

export class World {
  constructor() {
    this.width = 800;
    this.height = 460;
    this.taskbarHeight = 36;
    // Cada janela: { id, title, left, top, right, bottom, z, gone }
    // (z maior = na frente; gone = janela fechada ou minimizada, o pet
    //  não tem mais chão ali — como no Windows).
    this.windows = [];
  }

  /** A tela inteira (o palco). */
  get bounds() {
    return { left: 0, top: 0, right: this.width, bottom: this.height };
  }

  /** A área de trabalho: a tela sem a barra de tarefas. */
  get area() {
    return { left: 0, top: 0, right: this.width, bottom: this.height - this.taskbarHeight };
  }

  /** As janelas em ordem de frente para trás (as que ainda existem). */
  windowsTopFirst() {
    return this.windows
      .filter((win) => !win.gone)
      .sort((a, b) => b.z - a.z);
  }
}

/* ---------------------------------------------------------------
   5. O pet (FormPet.cs)
   --------------------------------------------------------------- */

export class Pet {
  constructor(world) {
    this.world = world;
    this.animations = new Map();
    this.spawns = new Map();
    this.childDefinitions = new Map();
    this.sounds = new Map();
    this.keyAnimations = { drag: -1, fall: -1, toss: -1, fallSoft: -1, fallHard: -1, sync: -1, kill: -1 };
    this.header = { name: "Pet", author: "", info: "" };
    this.sheet = null;          // a folha de sprites (uma imagem só)
    this.sheetUrl = "";
    this.tilesX = 1;
    this.tilesY = 1;
    this.tileWidth = 64;
    this.tileHeight = 64;

    this.x = 0; this.y = 0;      // posição do canto superior esquerdo do sprite
    this.offsetY = 0;            // deslocamento do quadro dentro da animação
    this.opacity = 1;
    this.width = 64; this.height = 64;
    this.isMovingLeft = true;
    this.isDragging = false;
    this.isTossing = false;
    this.tossForce = { x: 0, y: 0 };
    this.tossVertVel = 0;
    this.tossUpdatedAt = 0;
    this.step = 0;
    this.current = null;
    this.frameIndex = 0;
    this.flipped = false;
    this.interval = 200;
    this.nextStepAt = 0;
    this.randS = 10 + Math.floor(Math.random() * 80);
    this.window = null;          // a janela em que o pet está andando
    this.currentWindowSize = null;
    this.motion = new MotionTrack();
    this.dragVelocity = new DragVelocity();
    this.children = [];          // pets filhos (o balão, a bolinha…)
    this.isChild = false;
    this.depth = 0;
    this.alive = true;
    this.onEvent = () => {};
    this.volume = 0.6;
    this.audio = new Audio();
    this.audio.preload = "auto";
  }

  /* -------------------------------------------------------------
     Carregamento
     ------------------------------------------------------------- */

  async load(url) {
    const response = await fetch(url);
    if (!response.ok) throw new Error("não foi possível carregar " + url);
    const parsed = parsePetXml(await response.text());

    this.animations = parsed.animations;
    this.spawns = parsed.spawns;
    this.childDefinitions = parsed.children;
    this.sounds = parsed.sounds;
    this.keyAnimations = parsed.keyAnimations;
    this.header = parsed.header;

    // A folha de sprites: uma imagem com tilesx × tilesy quadros.
    this.sheetUrl = "data:image/png;base64," + parsed.image.png.replace(/\s+/g, "");
    await new Promise((resolve, reject) => {
      const image = new Image();
      image.onload = () => { this.sheet = image; resolve(); };
      image.onerror = () => reject(new Error("não foi possível decodificar a folha de sprites"));
      image.src = this.sheetUrl;
    });

    this.tileWidth = Math.floor(this.sheet.width / parsed.image.tilesX);
    this.tileHeight = Math.floor(this.sheet.height / parsed.image.tilesY);
    this.tilesX = parsed.image.tilesX;
    this.tilesY = parsed.image.tilesY;
    this.width = this.tileWidth;
    this.height = this.tileHeight;

    this.play();
  }

  /* -------------------------------------------------------------
     O contexto das expressões do XML (screenW, areaH, imageW…)
     ------------------------------------------------------------- */

  context() {
    const area = this.world.area;
    return {
      screenW: this.world.bounds.right,
      screenH: this.world.bounds.bottom,
      areaW: area.right - area.left,
      areaH: area.bottom - area.top + area.top,
      imageW: this.width,
      imageH: this.height,
      imageX: -1,
      imageY: -1,
      random: Math.floor(Math.random() * 100),
      randS: this.randS,
      scale: 1
    };
  }

  /* -------------------------------------------------------------
     Escolha do próximo spawn e das transições (Animations.cs)
     ------------------------------------------------------------- */

  getRandomSpawn() {
    let total = 0;
    for (const spawn of this.spawns.values()) total += spawn.probability;
    if (total <= 0) {
      return { start: { x: new TValue("0"), y: new TValue("0") }, next: 1, probability: 100 };
    }
    let pick = Math.floor(Math.random() * total);
    let sum = 0;
    for (const spawn of this.spawns.values()) {
      sum += spawn.probability;
      if (sum >= pick) return spawn;
    }
    return this.spawns.values().next().value;
  }

  /**
   * Escolhe a próxima animação respeitando as regras de "only" e as
   * probabilidades — a mesma lógica de Animations.SetNextGeneralAnimation.
   */
  setNextGeneralAnimation(list, where) {
    if (!list || list.length === 0) return -1;

    const isHorizontalPlus = where === ONLY.HORIZONTAL || where === ONLY.WINDOW || where === ONLY.TASKBAR;
    const isVerticalPlus = where === ONLY.VERTICAL || where === ONLY.WINDOWSIDE || where === ONLY.PETSIDE;

    const allowed = (item) => {
      if (item.only === ONLY.NONE) return true;
      if (isHorizontalPlus && item.only === ONLY.HORIZONTAL_PLUS) return true;
      if (isVerticalPlus && item.only === ONLY.VERTICAL_PLUS) return true;
      return item.only === where;
    };

    const candidates = list.filter(allowed);
    if (!candidates.length) return -1;

    let max = 0;
    for (const item of candidates) max += item.probability;
    if (max <= 0) return -1;

    const value = 1 + Math.floor(Math.random() * max);
    let sum = 0;
    for (const item of candidates) {
      sum += item.probability;
      if (sum >= value) return item.id;
    }
    return candidates[candidates.length - 1].id;
  }

  setNextBorderAnimation(where) {
    if (!this.current) return -1;
    return this.setNextGeneralAnimation(this.current.endBorder, where);
  }
  setNextSequenceAnimation(where) {
    if (!this.current) return -1;
    return this.setNextGeneralAnimation(this.current.endAnimation, where);
  }
  setNextGravityAnimation(where) {
    if (!this.current) return -1;
    return this.setNextGeneralAnimation(this.current.endGravity, where);
  }

  /* -------------------------------------------------------------
     Sons
     ------------------------------------------------------------- */

  startSound(id) {
    const sound = this.sounds.get(id);
    if (!sound || this.volume <= 0) return;
    if (Math.floor(Math.random() * 100) >= sound.probability) return;
    try {
      this.audio.pause();
      this.audio.src = "data:audio/mpeg;base64," + sound.data.replace(/\s+/g, "");
      this.audio.loop = sound.loop > 0;
      this.audio.volume = this.volume;
      const playPromise = this.audio.play();
      if (playPromise && playPromise.catch) playPromise.catch(() => {});
    } catch (e) {
      /* sem som, sem drama */
    }
  }

  /* -------------------------------------------------------------
     Posicionamento
     ------------------------------------------------------------- */

  /** Nasce (ou renasce) em um ponto escolhido pelo XML. */
  play(fromSky = false) {
    const spawn = this.getRandomSpawn();
    const ctx = this.context();
    const bounds = this.world.bounds;

    this.x = bounds.left + spawn.start.x.get(ctx);
    this.y = bounds.top + spawn.start.y.get(ctx);
    if (!this.isMovingLeft) {
      // Andando para a direita: nasce fora da tela, à esquerda.
      this.x = bounds.left - (spawn.start.x.get(ctx) - bounds.right) - this.width;
    }
    this.offsetY = 0;
    this.window = null;
    this.currentWindowSize = null;

    let first = spawn.next;
    if (fromSky && spawn.start.y.get(ctx) >= 0 && this.keyAnimations.fall >= 0) {
      // Este pet não tem spawn acima da tela: começa caindo do alto.
      this.y = bounds.top - this.height - 10;
      this.x = bounds.left + bounds.right / 2 - this.width / 2 + (Math.random() * 300 - 150);
      first = this.keyAnimations.fall;
    }

    this.motion.reset(this.x, this.y, this.now());
    this.setNewAnimation(first);
    this.onEvent({ type: "spawn" });
  }

  /**
   * Chega do céu, como no primeiro arranque do programa
   * (FormPet.Play com Onboarding.TakeSkyArrival): nasce acima da tela
   * caindo e pode pousar no topo de uma janela.
   */
  arriveFromSky(x = null) {
    const bounds = this.world.bounds;
    this.x = x === null
      ? bounds.left + (bounds.right - bounds.left) / 2 - this.width / 2
      : x;
    this.y = bounds.top - this.height - 10;
    this.offsetY = 0;
    this.window = null;
    this.currentWindowSize = null;
    this.motion.reset(this.x, this.y, this.now());
    this.setNewAnimation(this.keyAnimations.fall >= 0 ? this.keyAnimations.fall : 1);
    this.onEvent({ type: "spawn" });
  }

  now() {
    return performance.now();
  }

  /** Troca de animação (FormPet.SetNewAnimation). */
  setNewAnimation(id) {
    if (id < 0) { this.play(); return; }
    const animation = this.animations.get(id);
    if (!animation) { this.play(); return; }

    // O primeiro passo já conta com o passo 0 (FormPet: AnimationStep = -1,
    // e o timer o transforma em 0 na primeira batida).
    this.step = -1;
    this.current = animation;
    this.updateAnimationValues(animation);
    this.offsetY = animation.start.offsetY;
    this.startSound(id);
    this.createChildren(id);
    this.interval = animation.start.interval.get(this.context());
  }

  /** Reavalia as expressões dinâmicas de uma animação (Animations.UpdateAnimationValues). */
  updateAnimationValues(animation) {
    const ctx = this.context();
    animation.sequence.totalSteps =
      animation.sequence.frames.length +
      (animation.sequence.frames.length - animation.sequence.repeatFrom) * animation.sequence.repeat.get(ctx);
  }

  /* -------------------------------------------------------------
     Os filhos (o balão, a bolinha que aparece do lado do pet)
     ------------------------------------------------------------- */

  createChildren(animationId) {
    const definitions = this.childDefinitions.get(animationId);
    if (!definitions || !definitions.length) return;
    if (this.children.length >= 5) return;
    // No C#, um filho pode ter até cinco sub-filhos (FormPet.SetNewAnimation).
    if (this.depth >= 5) return;

    for (const definition of definitions) {
      const ctx = this.context();
      ctx.imageX = this.x;
      ctx.imageY = this.y;
      const child = new Pet(this.world);
      // O filho usa a mesma folha de sprites do pai.
      child.animations = this.animations;
      child.spawns = this.spawns;
      child.childDefinitions = this.childDefinitions;
      child.sounds = this.sounds;
      child.keyAnimations = this.keyAnimations;
      child.header = this.header;
      child.sheet = this.sheet;
      child.sheetUrl = this.sheetUrl;
      child.tileWidth = this.tileWidth;
      child.tileHeight = this.tileHeight;
      child.width = this.width;
      child.height = this.height;
      child.volume = this.volume;
      child.onEvent = this.onEvent;
      child.isChild = true;
      child.depth = (this.depth || 0) + 1;
      child.parent = this;
      child.x = this.x + definition.position.x.get(ctx);
      child.y = this.y + definition.position.y.get(ctx);
      child.motion.reset(child.x, child.y, child.now());
      child.setNewAnimation(definition.next);
      child.nextStepAt = child.now();
      this.children.push(child);
    }
  }

  /* -------------------------------------------------------------
     O passo a passo (FormPet.NextStep)
     ------------------------------------------------------------- */

  nextStep(now) {
    const animation = this.current;
    if (!animation) return;

    const totalSteps = Math.max(1, animation.sequence.totalSteps);
    const frames = animation.sequence.frames;

    // O quadro que está na tela.
    let spriteStep = this.step;
    if (!this.isDragging && !this.isTossing) spriteStep = Math.min(spriteStep, totalSteps - 1);
    if (frames.length) {
      if (spriteStep < frames.length) {
        this.frameIndex = frames[spriteStep];
      } else {
        const tail = frames.length - animation.sequence.repeatFrom;
        const index = tail > 0
          ? ((spriteStep - frames.length + animation.sequence.repeatFrom) % tail) + animation.sequence.repeatFrom
          : 0;
        this.frameIndex = frames[Math.max(0, Math.min(frames.length - 1, index))];
      }
    }

    // Intervalo, opacidade e deslocamento interpolados.
    this.interval = Math.round(
      animation.start.interval.get(this.context()) +
      ((animation.end.interval.get(this.context()) - animation.start.interval.get(this.context())) * this.step / totalSteps)
    );
    this.opacity = animation.start.opacity + (animation.end.opacity - animation.start.opacity) * this.step / totalSteps;
    this.offsetY = animation.start.offsetY + (animation.end.offsetY - animation.start.offsetY) * this.step / totalSteps;

    if (this.isDragging) return;
    if (this.isTossing) return;

    // O movimento do passo.
    let x = animation.start.x.get(this.context());
    let y = animation.start.y.get(this.context());
    if (totalSteps > 1) {
      x += (animation.end.x.get(this.context()) - animation.start.x.get(this.context())) * this.step / (totalSteps - 1);
      y += (animation.end.y.get(this.context()) - animation.start.y.get(this.context())) * this.step / (totalSteps - 1);
    }
    if (!this.isMovingLeft) x = -x;

    let newAnimation = false;
    let nextAnimation = -1;
    let landedOnWindow = false;

    const area = this.world.area;

    if (x < 0) {
      // Andando para a esquerda: borda esquerda da tela ou borda direita de uma janela.
      if (this.x + x < area.left) {
        const border = this.setNextBorderAnimation(ONLY.VERTICAL);
        if (border >= 0) {
          this.x = area.left;
          x = 0;
          nextAnimation = border;
          newAnimation = true;
        }
      } else {
        for (const win of this.world.windowsTopFirst()) {
          if (this.x + x <= win.right && this.x + x >= win.right + x &&
              this.y + y < win.bottom && this.y + y + this.height > win.top &&
              !this.checkTopWindow(false, win) &&
              win.right < area.right - this.width) {
            const border = this.setNextBorderAnimation(ONLY.WINDOWSIDE);
            if (border >= 0) {
              this.x = win.right;
              x = 0;
              nextAnimation = border;
              newAnimation = true;
            }
            break;
          }
        }
      }

      if (this.window) {
        const win = this.window;
        if (this.x + x < win.left) {
          const border = this.setNextBorderAnimation(ONLY.WINDOW);
          if (border >= 0) {
            this.x = win.left;
            x = 0;
            nextAnimation = border;
            newAnimation = true;
          } else {
            this.window = null;
          }
        }
      }
    } else if (x > 0) {
      // Andando para a direita.
      if (this.x + x + this.width > area.right) {
        const border = this.setNextBorderAnimation(ONLY.VERTICAL);
        if (border >= 0) {
          this.x = area.right - this.width;
          x = 0;
          nextAnimation = border;
          newAnimation = true;
        }
      } else {
        for (const win of this.world.windowsTopFirst()) {
          if (this.x + x + this.width >= win.left && this.x + x + this.width <= win.left + x &&
              this.y + y < win.bottom && this.y + y + this.height > win.top &&
              !this.checkTopWindow(false, win) &&
              win.left > area.left + this.width) {
            const border = this.setNextBorderAnimation(ONLY.WINDOWSIDE);
            if (border >= 0) {
              this.x = win.left - this.width;
              x = 0;
              nextAnimation = border;
              newAnimation = true;
            }
            break;
          }
        }
      }

      if (this.window) {
        const win = this.window;
        if (this.x + x + this.width > win.right) {
          const border = this.setNextBorderAnimation(ONLY.WINDOW);
          if (border >= 0) {
            this.x = win.right - this.width;
            x = 0;
            nextAnimation = border;
            newAnimation = true;
          } else {
            this.window = null;
          }
        }
      }
    }

    if (!newAnimation && y > 0) {
      // Caindo: a barra de tarefas ou o topo de uma janela.
      if (this.y + y > area.bottom - this.height) {
        const border = this.setNextBorderAnimation(ONLY.TASKBAR);
        if (border >= 0) {
          this.y = area.bottom - this.height;
          this.offsetY = 0;
          y = 0;
          nextAnimation = border;
          newAnimation = true;
        }
      } else {
        const windowTop = this.fallDetect(y);
        if (windowTop > 0) {
          const border = this.setNextBorderAnimation(ONLY.WINDOW);
          if (border >= 0) {
            this.y = windowTop - this.height;
            this.offsetY = 0;
            y = 0;
            nextAnimation = border;
            newAnimation = true;
            landedOnWindow = true;
          }
        }
      }
    } else if (!newAnimation && y < 0) {
      // Subindo: o topo da tela.
      if (this.y + y < area.top) {
        const border = this.setNextBorderAnimation(ONLY.HORIZONTAL);
        if (border >= 0) {
          this.y = area.top;
          x = 0;
          nextAnimation = border;
          newAnimation = true;
        }
      }
    }

    if (!newAnimation && this.step >= totalSteps) {
      // A sequência acabou.
      if (animation.sequence.action === "flip") {
        this.isMovingLeft = !this.isMovingLeft;
        this.flipped = !this.flipped;
      }
      let next;
      if (this.window) {
        next = this.setNextSequenceAnimation(ONLY.WINDOW);
      } else if (this.x < area.left - this.width || this.x > area.right ||
                 this.y < area.top - this.height || this.y > area.bottom) {
        next = -1;
      } else {
        next = this.setNextSequenceAnimation(
          this.y + this.height + y >= area.bottom - 2 ? ONLY.TASKBAR : ONLY.NONE
        );
      }
      if (next >= 0) {
        nextAnimation = next;
        newAnimation = true;
      } else if (this.isChild) {
        // O filho não tem spawn: quando a animação acaba, ele some.
        this.alive = false;
        return;
      } else {
        this.play();
        return;
      }
    } else if (!newAnimation && animation.gravity) {
      // Gravidade: sem janela embaixo, o pet cai.
      if (!this.window) {
        if (this.y + y < area.bottom - this.height) {
          if (this.y + y + 3 >= area.bottom - this.height) {
            y = area.bottom - this.y - this.height;
          } else {
            nextAnimation = this.setNextGravityAnimation(ONLY.NONE);
            newAnimation = true;
          }
        }
      } else if (this.step > 0 && this.checkTopWindow(true)) {
        if (!this.followWindow()) {
          this.window = null;
          nextAnimation = this.setNextGravityAnimation(ONLY.WINDOW);
          newAnimation = true;
        }
      }
    }

    this.x += x;
    this.y += y;

    // A janela em que o pet está andando.
    if (landedOnWindow && this.window) {
      this.currentWindowSize = { left: this.window.left, top: this.window.top, right: this.window.right };
    }

    this.motion.moveTo(this.x, this.y + this.offsetY, now, this.interval);

    if (newAnimation) {
      this.setNewAnimation(nextAnimation);
    }
  }

  /* -------------------------------------------------------------
     A queda sobre as janelas (FormPet.FallDetect)
     ------------------------------------------------------------- */

  fallDetect(y) {
    if (y <= 0) return -1;
    const area = this.world.area;

    for (const win of this.world.windowsTopFirst()) {
      if (this.y + this.height < win.top && this.y + this.height + y >= win.top &&
          this.x >= win.left - this.width / 2 && this.x + this.width <= win.right + this.width / 2 &&
          this.y > 20 + area.top) {
        this.window = win;
        this.currentWindowSize = { left: win.left, top: win.top, right: win.right };
        if (!this.checkTopWindow(false, win)) {
          return win.top;
        }
        this.window = null;
      }
    }
    return -1;
  }

  /**
   * A janela ainda vale como chão? Falso quando outra janela está
   * por cima dela (FormPet.CheckTopWindow).
   */
  checkTopWindow(bCheck, win = this.window) {
    if (!win) return false;

    // A janela foi fechada ou minimizada: o pet perde o chão e cai,
    // como no Windows quando a janela em que ele estava some.
    if (win.gone) return true;

    if (bCheck && this.currentWindowSize) {
      if (this.currentWindowSize.top !== win.top ||
          this.currentWindowSize.left !== win.left ||
          this.currentWindowSize.right !== win.right) {
        return true;
      }
    }

    for (const other of this.world.windowsTopFirst()) {
      if (other === win) break;                 // a partir daqui só há janelas atrás
      if (other.top < win.top && other.bottom > win.top &&
          other.left < this.x && other.right > this.x + 40) {
        return true;
      }
    }
    return false;
  }

  /** O pet acompanha a janela em que está andando (FormPet.FollowWindow). */
  followWindow() {
    if (!this.window || !this.currentWindowSize) return false;
    const win = this.window;

    if (this.currentWindowSize.top !== win.top ||
        this.currentWindowSize.left !== win.left ||
        this.currentWindowSize.right !== win.right) {
      const oldWidth = this.currentWindowSize.right - this.currentWindowSize.left;
      const ratio = oldWidth > 0 ? (win.right - win.left) / oldWidth : 1;
      const dy = win.top - this.currentWindowSize.top;
      this.x = win.left + (this.x - this.currentWindowSize.left) * ratio;
      this.y += dy;
      this.motion.follow(this.currentWindowSize.left, win.left, ratio, dy);
      this.currentWindowSize = { left: win.left, top: win.top, right: win.right };
      return true;
    }
    return false;
  }

  /* -------------------------------------------------------------
     Arraste e arremesso (FormPet.Motion.cs)
     ------------------------------------------------------------- */

  beginDrag() {
    this.isDragging = true;
    this.isTossing = false;
    this.window = null;
    this.offsetY = 0;
    this.motion.reset(this.x, this.y, this.now());
    this.dragVelocity.reset(this.x, this.y, this.now());
    if (this.keyAnimations.drag >= 0) this.setNewAnimation(this.keyAnimations.drag);
  }

  dragTo(x, y, now) {
    this.x = x;
    this.y = y;
    this.offsetY = 0;
    this.dragVelocity.add(this.x, this.y, now);
    this.motion.reset(this.x, this.y, now);
  }

  /** Solta o pet: com força ele é arremessado, sem força ele simplesmente cai. */
  endDrag(now) {
    if (!this.isDragging) return;
    this.dragTo(this.x, this.y, now);
    this.tossForce = this.dragVelocity.tossForce(now);
    const length = Math.hypot(this.tossForce.x, this.tossForce.y);

    if (length > 5) {
      if (this.keyAnimations.toss >= 0) this.setNewAnimation(this.keyAnimations.toss);
      this.isTossing = true;
      this.tossVertVel = this.tossForce.y;
      this.tossUpdatedAt = now;
      this.interval = 30;
    } else {
      this.isTossing = false;
      if (this.keyAnimations.fall >= 0) this.setNewAnimation(this.keyAnimations.fall);
    }
    this.isDragging = false;
  }

  /** O arremesso: a física de quando o pet voa e quica (FormPet.AdvanceToss). */
  advanceToss(now) {
    const dt = Math.max(0, Math.min(50, now - this.tossUpdatedAt)) / 30;
    this.tossUpdatedAt = now;
    if (dt === 0) return;

    const area = this.world.area;
    const windowSide = this.tossWindowSideCollision(dt);

    let nextX = this.x + this.tossForce.x * dt;
    const areaRight = area.right - this.width;

    if (nextX < area.left || nextX > areaRight || windowSide !== 0) {
      let edge = 0;
      if (nextX < area.left) edge = area.left;
      else if (nextX > areaRight) edge = areaRight;
      else edge = windowSide;

      nextX = edge - (nextX - edge) * 0.5;
      this.tossForce.x *= -0.5;
    }
    this.x = Math.max(area.left, Math.min(areaRight, nextX));

    const dy = this.tossVertVel * dt + 0.75 * dt * dt;
    this.tossVertVel += 1.5 * dt;

    const windowTop = dy > 0 ? this.fallDetect(Math.ceil(dy)) : -1;
    let ground = area.bottom - this.height;
    let land = this.y + dy >= ground;
    if (windowTop !== -1 && windowTop - this.height <= ground) {
      ground = windowTop - this.height;
      land = true;
    }
    this.y = land ? ground : this.y + dy;
    this.offsetY = 0;
    this.motion.reset(this.x, this.y, now);

    if (land) {
      if ((this.tossForce.x < 0 && !this.isMovingLeft) || (this.tossForce.x > 0 && this.isMovingLeft)) {
        this.isMovingLeft = !this.isMovingLeft;
        this.flipped = !this.flipped;
      }
      this.isTossing = false;
      const landing = this.tossVertVel < 40 ? this.keyAnimations.fallSoft : this.keyAnimations.fallHard;
      if (landing >= 0) this.setNewAnimation(landing);
      else if (this.keyAnimations.fall >= 0) this.setNewAnimation(this.keyAnimations.fall);
    }
  }

  /** Quica nas laterais das janelas? (FormPet.TossWindowSideCollision) */
  tossWindowSideCollision(dt) {
    if (this.tossForce.x === 0) return 0;

    for (const win of this.world.windowsTopFirst()) {
      if (this.tossForce.x < 0) {
        if (this.x + this.tossForce.x * dt <= win.right &&
            this.x + this.tossForce.x * dt >= win.right + this.tossForce.x * dt &&
            this.y + this.tossVertVel * dt < win.bottom &&
            this.y + this.tossVertVel * dt + this.height > win.top &&
            !this.checkTopWindow(false, win) &&
            win.right < this.world.area.right - this.width) {
          return win.right;
        }
      } else if (this.tossForce.x > 0) {
        if (this.x + this.tossForce.x * dt + this.width >= win.left &&
            this.x + this.tossForce.x * dt + this.width <= win.left + this.tossForce.x * dt &&
            this.y + this.tossVertVel * dt < win.bottom &&
            this.y + this.tossVertVel * dt + this.height > win.top &&
            !this.checkTopWindow(false, win) &&
            win.left > this.world.area.left + this.width) {
          return win.left - this.width;
        }
      }
    }
    return 0;
  }

  /* -------------------------------------------------------------
     O laço do tempo
     ------------------------------------------------------------- */

  /** Avança o pet. Chamar uma vez por quadro. */
  update(now) {
    if (this.isDragging) return;

    if (this.isTossing) {
      this.advanceToss(now);
      return;
    }

    if (this.window) this.followWindow();

    let guard = 0;
    while (now >= this.nextStepAt && guard++ < 5) {
      // Como no programa: uma animação nova começa no passo 0
      // (FormPet.SetNewAnimation deixa AnimationStep = -1; o timer o zera).
      if (this.step < 0) this.step = 0;
      this.nextStep(now);
      this.step++;
      this.nextStepAt = now + Math.max(16, this.interval);
    }

    for (const child of this.children) child.update(now);
    this.children = this.children.filter((child) => child.alive !== false);
  }

  /** Começa a rodar (o primeiro passo acontece logo). */
  start(now = this.now()) {
    this.nextStepAt = now;
    this.motion.reset(this.x, this.y, now);
  }

  /** A primeira animação deste pet com um dos nomes dados (FormPet.FindAnimation). */
  findAnimation(names) {
    for (const name of names) {
      for (const animation of this.animations.values()) {
        if (animation.name && animation.name.toLowerCase() === String(name).toLowerCase()) {
          return animation.id;
        }
      }
    }
    return -1;
  }

  /** Faz o pet reagir com uma animação (FormPet.React). */
  react(names) {
    if (this.isDragging || this.isTossing) return false;
    const id = this.findAnimation(names);
    if (id < 0) return false;
    this.setNewAnimation(id);
    return true;
  }

  /** Onde o pet está desenhado, com o deslocamento do quadro. */
  renderState() {
    const position = this.motion.initialized ? this.motion.sample(this.now()) : { x: this.x, y: this.y };
    return {
      x: position.x,
      y: position.y,
      width: this.width,
      height: this.height,
      frame: this.frameIndex,
      flipped: this.flipped,
      opacity: this.opacity,
      animation: this.current ? this.current.name : ""
    };
  }
}
