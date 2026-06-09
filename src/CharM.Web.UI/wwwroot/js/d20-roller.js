import * as THREE from "./three.module.js";

window.charm = window.charm || {};
window.charm.d20Roller = (() => {
  const states = new Map();

  function init(rootId) {
    const root = document.getElementById(rootId);
    if (!root) return;
    if (states.has(rootId)) {
      resizeRenderer(states.get(rootId));
      return;
    }

    const viewport = root.querySelector(".d20-viewport");
    if (!viewport) return;

    const phi = (1 + Math.sqrt(5)) / 2;
    const baseVertices = [
      [-1, phi, 0], [1, phi, 0], [-1, -phi, 0], [1, -phi, 0],
      [0, -1, phi], [0, 1, phi], [0, -1, -phi], [0, 1, -phi],
      [phi, 0, -1], [phi, 0, 1], [-phi, 0, -1], [-phi, 0, 1]
    ].map(normalize);

    const faceData = [
      [0, 11, 5], [0, 5, 1], [0, 1, 7], [0, 7, 10], [0, 10, 11],
      [1, 5, 9], [5, 11, 4], [11, 10, 2], [10, 7, 6], [7, 1, 8],
      [3, 9, 4], [3, 4, 2], [3, 2, 6], [3, 6, 8], [3, 8, 9],
      [4, 9, 5], [2, 4, 11], [6, 2, 10], [8, 6, 7], [9, 8, 1]
    ].map((indices, index) => {
      const pts = indices.map((i) => baseVertices[i]);
      const centroid = scaleVector(addVectors(addVectors(pts[0], pts[1]), pts[2]), 1 / 3);
      let normal = normalize(cross(subtract(pts[1], pts[0]), subtract(pts[2], pts[0])));
      if (dot(normal, centroid) < 0) normal = scaleVector(normal, -1);
      return { indices, value: index + 1, centroid, normal };
    });

    const threeScene = new THREE.Scene();
    const camera = new THREE.PerspectiveCamera(30, 1, 0.1, 100);
    camera.position.set(0, 0.16, 7.35);

    const renderer = new THREE.WebGLRenderer({
      antialias: true,
      alpha: true,
      powerPreference: "high-performance"
    });
    renderer.outputColorSpace = THREE.SRGBColorSpace;
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    viewport.appendChild(renderer.domElement);

    const diceRoot = new THREE.Group();
    threeScene.add(diceRoot);

    const diceGroup = new THREE.Group();
    diceRoot.add(diceGroup);

    const dieGeometry = new THREE.IcosahedronGeometry(1.82, 0);
    const dieMaterial = new THREE.MeshStandardMaterial({
      color: 0x4fb5ff,
      emissive: 0x102646,
      emissiveIntensity: 0.42,
      metalness: 0.22,
      roughness: 0.34,
      flatShading: true
    });

    const dieMesh = new THREE.Mesh(dieGeometry, dieMaterial);
    dieMesh.castShadow = true;
    dieMesh.receiveShadow = true;
    diceGroup.add(dieMesh);

    const innerGlow = new THREE.Mesh(
      new THREE.IcosahedronGeometry(1.35, 1),
      new THREE.MeshBasicMaterial({
        color: 0x7dd3fc,
        transparent: true,
        opacity: 0.09
      })
    );
    diceGroup.add(innerGlow);

    const edgeMaterial = new THREE.LineBasicMaterial({
      color: 0xe7f6ff,
      transparent: true,
      opacity: 0.92
    });
    const edges = new THREE.LineSegments(new THREE.EdgesGeometry(dieGeometry), edgeMaterial);
    edges.scale.setScalar(1.001);
    diceGroup.add(edges);

    threeScene.add(new THREE.HemisphereLight(0xb5dcff, 0x08101d, 1.6));
    const keyLight = new THREE.DirectionalLight(0xc9ebff, 1.8);
    keyLight.position.set(3.2, 4.2, 5.4);
    keyLight.castShadow = true;
    keyLight.shadow.mapSize.set(1024, 1024);
    keyLight.shadow.bias = -0.0008;
    threeScene.add(keyLight);

    const rimLight = new THREE.PointLight(0x8b5cf6, 12, 20, 2);
    rimLight.position.set(-3.5, 1.8, -1.5);
    threeScene.add(rimLight);

    const fillLight = new THREE.PointLight(0x7dd3fc, 10, 20, 2);
    fillLight.position.set(2.8, -0.8, 4.8);
    threeScene.add(fillLight);

    const floor = new THREE.Mesh(
      new THREE.CircleGeometry(3.6, 48),
      new THREE.ShadowMaterial({ color: 0x000000, opacity: 0.22 })
    );
    floor.rotation.x = -Math.PI / 2;
    floor.position.set(0, -2.55, 0);
    floor.receiveShadow = true;
    threeScene.add(floor);

    const state = {
      THREE,
      root,
      viewport,
      resultBadge: root.querySelector(".d20-result-badge"),
      resultMeta: root.querySelector(".d20-result-badge small"),
      resultNumber: root.querySelector(".d20-result-badge strong"),
      particles: root.querySelector(".d20-particles"),
      faceData,
      camera,
      renderer,
      threeScene,
      diceRoot,
      diceGroup,
      dieMaterial,
      edgeMaterial,
      innerGlow,
      rolling: false,
      currentRotation: { x: -0.74, y: 0.92, z: 0.12 },
      currentScale: 1,
      currentLift: 0,
      currentTier: "normal",
      animation: null,
      activeResultFace: faceData[0],
    };

    states.set(rootId, state);
    resizeRenderer(state);
    requestAnimationFrame((now) => tick(state, now));
  }

  function roll(rootId, result, tier, modifier) {
    const state = states.get(rootId);
    if (!state || state.rolling) return;
    const descriptor = getDescriptor(result, tier);
    const target = getTargetRotationForResult(state, result);
    state.activeResultFace = state.faceData[result - 1];
    state.rolling = true;
    state.root.classList.add("rolling");
    state.currentTier = "normal";
    state.resultBadge?.classList.remove("show", "crit", "fumble");
    if (state.resultMeta) state.resultMeta.textContent = "result";
    if (state.resultNumber) state.resultNumber.textContent = "--";

    state.animation = {
      start: performance.now(),
      duration: 1850,
      from: { ...state.currentRotation },
      to: {
        x: target.x + (Math.PI * 2) * (2 + Math.floor(rand(0, 2))) + rand(-0.16, 0.16),
        y: target.y + (Math.PI * 2) * (2 + Math.floor(rand(0, 3))) + rand(-0.2, 0.2),
        z: target.z + (Math.PI * 2) * (1 + Math.floor(rand(0, 2))) + rand(-0.2, 0.2)
      },
      settled: target,
      modifier: Number.isFinite(modifier) ? modifier : 0,
      result,
      descriptor,
      lastShuffle: 0
    };
  }

  function showTotal(rootId, total, tier) {
    const state = states.get(rootId);
    if (!state) return;
    state.animation = null;
    state.rolling = false;
    state.root.classList.remove("rolling");
    state.currentTier = tier || "normal";
    state.resultBadge?.classList.remove("crit", "fumble");
    if (state.resultMeta) state.resultMeta.textContent = "dice total";
    if (state.resultNumber) state.resultNumber.textContent = String(total);
    state.resultBadge?.classList.add("show");
    burst(state, "#7dd3fc", 12);
  }

  function tick(state, now) {
    if (state.animation) {
      const elapsed = now - state.animation.start;
      const t = Math.min(elapsed / state.animation.duration, 1);
      const eased = easeOutCubic(t);
      const wobble = Math.sin(t * Math.PI * 9) * (1 - t) * 0.075;

      state.currentRotation.x = lerp(state.animation.from.x, state.animation.to.x, eased) + wobble;
      state.currentRotation.y = lerp(state.animation.from.y, state.animation.to.y, eased) - wobble * 0.9;
      state.currentRotation.z = lerp(state.animation.from.z, state.animation.to.z, eased) + wobble * 0.45;
      state.currentScale = 1 + Math.sin(t * Math.PI) * 0.035;
      state.currentLift = -Math.sin(t * Math.PI) * 8;

      if (t >= 1) {
        state.currentRotation = { ...state.animation.settled };
        state.currentScale = 1;
        state.currentLift = 0;
        state.currentTier = state.animation.descriptor.tier;
        if (state.resultMeta) {
          const mod = state.animation.modifier;
          state.resultMeta.textContent = mod ? `d20 ${mod >= 0 ? "+" : ""}${mod}` : "d20";
        }
        if (state.resultNumber) {
          state.resultNumber.textContent = String(state.animation.result + state.animation.modifier);
        }
        state.resultBadge?.classList.remove("crit", "fumble");
        if (state.animation.descriptor.tier === "crit") state.resultBadge?.classList.add("crit");
        if (state.animation.descriptor.tier === "fumble") state.resultBadge?.classList.add("fumble");
        state.resultBadge?.classList.add("show");
        burst(state, state.animation.descriptor.color, state.animation.result === 20 ? 24 : state.animation.result === 1 ? 18 : 12);
        state.animation = null;
        state.rolling = false;
        state.root.classList.remove("rolling");
      }
    }

    state.diceGroup.rotation.set(state.currentRotation.x, state.currentRotation.y, state.currentRotation.z);
    state.diceGroup.position.y = state.currentLift * 0.035;
    state.diceGroup.scale.setScalar(state.currentScale);
    state.innerGlow.rotation.y += 0.01;
    state.innerGlow.rotation.x += 0.004;
    applyTheme(state);
    positionResultBadge(state);
    state.renderer.render(state.threeScene, state.camera);
    requestAnimationFrame((time) => tick(state, time));
  }

  function resizeRenderer(state) {
    const rect = state.viewport.getBoundingClientRect();
    const width = Math.max(1, Math.round(rect.width));
    const height = Math.max(1, Math.round(rect.height));
    const dpr = Math.min(window.devicePixelRatio || 1, 2);
    state.renderer.setPixelRatio(dpr);
    state.renderer.setSize(width, height, false);
    state.camera.aspect = width / height;
    state.camera.updateProjectionMatrix();
  }

  function getTargetRotationForResult(state, result) {
    const THREE = state.THREE;
    const face = state.faceData[result - 1];
    const normal = new THREE.Vector3(...face.normal).normalize();
    const desired = new THREE.Vector3(0.04, 0.16, 0.986).normalize();
    const align = new THREE.Quaternion().setFromUnitVectors(normal, desired);
    const twist = new THREE.Quaternion().setFromAxisAngle(desired, ((result % 5) - 2) * 0.34);
    const euler = new THREE.Euler().setFromQuaternion(twist.multiply(align), "XYZ");
    return { x: euler.x, y: euler.y, z: euler.z };
  }

  function getDescriptor(result, forcedTier) {
    if (forcedTier === "crit" || result === 20) return { tier: "crit", color: "#facc15" };
    if (forcedTier === "fumble" || result === 1) return { tier: "fumble", color: "#fb7185" };
    if (result >= 17) return { tier: "high", color: "#7dd3fc" };
    if (result >= 12) return { tier: "high", color: "#7dd3fc" };
    if (result >= 7) return { tier: "mid", color: "#a78bfa" };
    return { tier: "low", color: "#fda4af" };
  }

  function getTheme(state) {
    if (state.currentTier === "crit") return { glow: "250,204,21", die: 0xf6c453, emissive: 0x5a3800, edge: 0xfff0bf };
    if (state.currentTier === "fumble") return { glow: "251,113,133", die: 0xf06a88, emissive: 0x4a0a1d, edge: 0xffd5df };
    return { glow: "125,211,252", die: 0x4fb5ff, emissive: 0x102646, edge: 0xe7f6ff };
  }

  function applyTheme(state) {
    const theme = getTheme(state);
    state.dieMaterial.color.setHex(theme.die);
    state.dieMaterial.emissive.setHex(theme.emissive);
    state.edgeMaterial.color.setHex(theme.edge);
    state.renderer.domElement.style.filter =
      `drop-shadow(0 18px 28px rgba(2, 6, 23, 0.34)) drop-shadow(0 0 28px rgba(${theme.glow}, 0.16))`;
  }

  function positionResultBadge(state) {
    if (!state.activeResultFace || !state.resultBadge?.classList.contains("show")) return;
    const THREE = state.THREE;
    const anchor = new THREE.Vector3(...state.activeResultFace.centroid).normalize().multiplyScalar(1.86);
    state.diceGroup.updateWorldMatrix(true, false);
    anchor.applyMatrix4(state.diceGroup.matrixWorld);
    anchor.project(state.camera);
    const viewportRect = state.renderer.domElement.getBoundingClientRect();
    const sceneRect = state.root.getBoundingClientRect();
    const x = (anchor.x * 0.5 + 0.5) * viewportRect.width + (viewportRect.left - sceneRect.left);
    const y = (-anchor.y * 0.5 + 0.5) * viewportRect.height + (viewportRect.top - sceneRect.top);
    state.resultBadge.style.left = `${x}px`;
    state.resultBadge.style.top = `${y}px`;
  }

  function burst(state, color, count) {
    if (!state.particles) return;
    state.particles.textContent = "";
    for (let i = 0; i < count; i += 1) {
      const dot = document.createElement("span");
      const angle = (Math.PI * 2 * i) / count + rand(-0.18, 0.18);
      const distance = rand(62, 138);
      dot.className = "d20-particle";
      dot.style.setProperty("--dx", `${Math.cos(angle) * distance}px`);
      dot.style.setProperty("--dy", `${Math.sin(angle) * distance}px`);
      dot.style.setProperty("--ps", `${rand(0.7, 1.7).toFixed(2)}`);
      dot.style.setProperty("--particle-color", color);
      state.particles.appendChild(dot);
    }
    setTimeout(() => { if (state.particles) state.particles.textContent = ""; }, 950);
  }

  function normalize(v) {
    const length = Math.hypot(v[0], v[1], v[2]) || 1;
    return [v[0] / length, v[1] / length, v[2] / length];
  }
  function dot(a, b) { return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]; }
  function cross(a, b) {
    return [
      a[1] * b[2] - a[2] * b[1],
      a[2] * b[0] - a[0] * b[2],
      a[0] * b[1] - a[1] * b[0]
    ];
  }
  function subtract(a, b) { return [a[0] - b[0], a[1] - b[1], a[2] - b[2]]; }
  function addVectors(a, b) { return [a[0] + b[0], a[1] + b[1], a[2] + b[2]]; }
  function scaleVector(v, amount) { return [v[0] * amount, v[1] * amount, v[2] * amount]; }
  function lerp(a, b, t) { return a + (b - a) * t; }
  function easeOutCubic(t) { return 1 - Math.pow(1 - t, 3); }
  function rand(min, max) { return Math.random() * (max - min) + min; }

  window.addEventListener("resize", () => {
    for (const state of states.values()) resizeRenderer(state);
  });

  return { init, roll, showTotal };
})();
