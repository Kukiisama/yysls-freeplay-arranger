(function () {
  'use strict';

  /* ---------------- 数据：燕云十六声自由演奏 · 36 键（低音 do – 高音 si） ---------------- */
  // 三排按键对应三个八度：下排 Z–M = 低音，中排 A–J = 中音，上排 Q–U = 高音。
  // 升半音（#1 / #4 / #5）按住 Shift，降半音（b3 / b7）按住 Ctrl。
  const OCTAVES = [
    { id: 'low',  label: '低音', base: 48, letters: 'ZXCVBNM' },   // C3 – B3
    { id: 'mid',  label: '中音', base: 60, letters: 'ASDFGHJ' },   // C4 – B4
    { id: 'high', label: '高音', base: 72, letters: 'QWERTYU' }    // C5 – B5
  ];

  // 一个八度里的 12 个音级：简谱写法、是否黑键、用哪个白键字母、要不要加升/降半音修饰键
  const DEGREES = [
    { semi: 0,  text: '1',  black: false, li: 0, mod: '' },
    { semi: 1,  text: '#1', black: true,  li: 0, mod: 'Shift+' },
    { semi: 2,  text: '2',  black: false, li: 1, mod: '' },
    { semi: 3,  text: 'b3', black: true,  li: 2, mod: 'Ctrl+' },
    { semi: 4,  text: '3',  black: false, li: 2, mod: '' },
    { semi: 5,  text: '4',  black: false, li: 3, mod: '' },
    { semi: 6,  text: '#4', black: true,  li: 3, mod: 'Shift+' },
    { semi: 7,  text: '5',  black: false, li: 4, mod: '' },
    { semi: 8,  text: '#5', black: true,  li: 4, mod: 'Shift+' },
    { semi: 9,  text: '6',  black: false, li: 5, mod: '' },
    { semi: 10, text: 'b7', black: true,  li: 6, mod: 'Ctrl+' },
    { semi: 11, text: '7',  black: false, li: 6, mod: '' }
  ];

  const OCT_MARK = { low: ',', mid: '', high: "'" };

  const NOTES = [];
  OCTAVES.forEach(o => {
    DEGREES.forEach(d => {
      NOTES.push({
        midi: o.base + d.semi,
        deg: d.text,
        oct: o.id,
        octLabel: o.label,
        name: d.text + OCT_MARK[o.id],
        mapping: d.mod + o.letters.charAt(d.li),
        mod: d.mod,
        base: o.letters.charAt(d.li),
        black: d.black,
        white: !d.black
      });
    });
  });

  const byMidi = new Map(NOTES.map(n => [n.midi, n]));
  const ROWS = NOTES.slice().reverse();          // 显示顺序：高音在上
  const ROW_H = 13;                              // 与 CSS 的 --innerh（468px）对应
  const COLS_UNIT = 16;                          // 初始 / 每次扩展的列数
  const MAX_HOLD_MS = 6000;                      // 游戏内单次按键最长 6 秒
  const MIN_HOLD_MS = 20;

  const $ = id => document.getElementById(id);
  const keysEl = $('keys');
  const rollEl = $('roll');
  const inner = $('rollInner');
  const rulerEl = $('ruler');
  const listEl = $('list');
  const countEl = $('count');
  const tipEl = $('emptyTip');
  const colTag = $('colTag');
  const toastEl = $('toast');
  const cmdEl = $('cmdview');
  const tbInfoEl = $('tbInfo');
  const pitchCol = $('pitchCol');

  /* ---------------- 状态 ---------------- */
  let notes = [];               // { id, midi, col, mapping }
  let cols = COLS_UNIT;         // 当前列数
  let colW = 52;                // 单格宽度（按可视宽度自适应）
  let bpm = 120;                // 每分钟四分音符数
  let gridFactor = 8;           // 每格 = 几分音符（4/8/16/32）
  let defaultLen = 1;           // 新音符默认长度（格）
  let panelTab = 'keys';        // keys | cmd
  let playStart = 0;            // 试听起点（列下标，0 起）
  let uid = 1;
  const undoStack = [];
  let soundOn = true;

  const shortMap = m => String(m).replace('Shift+', '↑').replace('Ctrl+', '↓');
  const currentMapping = n => n.mapping;
  const sortedNotes = () => notes.slice().sort((a, b) => (a.col - b.col) || (a.id - b.id));

  /* ---------------- 时间与命令 ---------------- */
  const stepMs = () => (60000 / bpm) * (4 / gridFactor);
  const maxLenCells = () => Math.max(1, Math.floor(MAX_HOLD_MS / stepMs()));
  const noteHoldMs = len => Math.min(MAX_HOLD_MS, Math.max(MIN_HOLD_MS, Math.round(len * stepMs())));

  function buildCommands() {
    const ordered = sortedNotes();
    const step = stepMs();
    const out = [];
    let cursor = 0;
    let lastOnset = 0;
    let lastHold = 0;
    ordered.forEach((n, i) => {
      const onset = n.col * step;
      const nextOnset = i + 1 < ordered.length ? ordered[i + 1].col * step : Infinity;
      // 按住时长 = 拉的格子长度（上限 6s，且不会盖住下一个音）
      let hold = noteHoldMs(n.len || 1);
      if (isFinite(nextOnset)) hold = Math.min(hold, Math.max(MIN_HOLD_MS, Math.round(nextOnset - onset)));

      const gap = Math.round(onset - cursor);
      if (gap > 0) out.push({ type: 'wait', text: gap + 'ms' });
      out.push({ type: 'note', text: 'KEY ' + n.mapping + ' ' + hold });
      cursor = onset + hold;
      lastOnset = onset;
      lastHold = hold;
    });
    return { lines: out, step: step, duration: ordered.length ? lastOnset + lastHold : 0 };
  }

  function fmtSec(ms) {
    return (ms / 1000).toFixed(ms < 10000 ? 2 : 1) + 's';
  }

  function renderCmd() {
    const built = buildCommands();
    const sorted = sortedNotes();
    if (!sorted.length) {
      cmdEl.innerHTML = '<span class="c"># 还没有音符\n# 在钢琴卷格子上点一下开始</span>';
      if (tbInfoEl) tbInfoEl.textContent = '每格 ' + Math.round(built.step) + 'ms';
      return;
    }
    const head = '<span class="c"># ' + bpm + ' BPM · 每格 1/' + gridFactor + ' 音符 = ' + Math.round(built.step) +
      'ms · 单音最长 ' + fmtSec(MAX_HOLD_MS) + ' · 全长 ' + fmtSec(built.duration) + '\n' +
      '# KEY &lt;按键&gt; &lt;按住ms&gt;　·　Shift = 升半音，Ctrl = 降半音</span>\n';
    const body = built.lines.map(l => l.type === 'wait'
      ? '<span class="w">' + l.text + '</span>'
      : l.text).join('\n');
    cmdEl.innerHTML = head + body;
  }

  /* ---------------- 声音 ---------------- */
  let ac = null;
  function ensureAC() {
    if (!ac) {
      const AC = window.AudioContext || window.webkitAudioContext;
      ac = new AC();
    }
    if (ac.state === 'suspended') ac.resume();
    return ac;
  }
  function tone(midi, durSec) {
    if (!soundOn) return;
    try {
      const ctx = ensureAC();
      const f = 440 * Math.pow(2, (midi - 69) / 12);
      const t0 = ctx.currentTime + 0.01;
      const dur = Math.max(0.08, Math.min(1.2, durSec || 0.9));

      const out = ctx.createGain();
      out.gain.setValueAtTime(0.0001, t0);
      out.gain.exponentialRampToValueAtTime(0.2, t0 + 0.035);
      out.gain.exponentialRampToValueAtTime(0.1, t0 + 0.25);
      out.gain.exponentialRampToValueAtTime(0.0001, t0 + dur);

      const lp = ctx.createBiquadFilter();
      lp.type = 'lowpass';
      lp.frequency.value = Math.min(6500, f * 6);
      lp.Q.value = 0.5;
      out.connect(lp);
      lp.connect(ctx.destination);

      const o1 = ctx.createOscillator();
      o1.type = 'sine';
      o1.frequency.value = f;

      const o2 = ctx.createOscillator();
      o2.type = 'triangle';
      o2.frequency.value = f * 2;
      const g2 = ctx.createGain();
      g2.gain.value = 0.14;

      const lfo = ctx.createOscillator();
      lfo.frequency.value = 5.2;
      const lfoG = ctx.createGain();
      lfoG.gain.value = f * 0.0042;
      lfo.connect(lfoG);
      lfoG.connect(o1.frequency);

      o1.connect(out);
      o2.connect(g2);
      g2.connect(out);
      [o1, o2, lfo].forEach(o => { o.start(t0); o.stop(t0 + dur + 0.05); });
    } catch (e) { /* 忽略音频异常 */ }
  }

  /* ---------------- 构建琴键（3 个八度 × 12 个音，按游戏三排排列） ---------------- */
  const keyEls = new Map();

  function jnHtml(n) {
    const acc = (n.deg.charAt(0) === '#' || n.deg.charAt(0) === 'b') ? n.deg.charAt(0) : '';
    const num = acc ? n.deg.slice(1) : n.deg;
    return '<span class="jn">' +
      (acc ? '<em>' + acc + '</em>' : '') +
      '<span class="digit">' + num +
        (n.oct === 'high' ? '<i class="dot up"></i>' : '') +
        (n.oct === 'low' ? '<i class="dot down"></i>' : '') +
      '</span>' +
    '</span>';
  }

  OCTAVES.slice().reverse().forEach(o => {       // 高音在上，和游戏里一致
    const row = document.createElement('div');
    row.className = 'krow';

    const lab = document.createElement('div');
    lab.className = 'klab ' + o.id;
    lab.textContent = o.label;
    row.appendChild(lab);

    DEGREES.forEach(d => {
      const n = byMidi.get(o.base + d.semi);
      const el = document.createElement('button');
      el.type = 'button';
      el.className = 'key ' + (d.black ? 'black' : 'white') + ' oct-' + o.id;
      el.dataset.midi = n.midi;
      el.title = o.label + ' ' + d.text + '（' + n.name + '）· ' + n.mapping;
      el.innerHTML = '<span class="kn">' + jnHtml(n) + '</span>' +
        '<span class="kp">' + n.mapping + '</span>';
      el.addEventListener('click', () => previewNote(n.midi));
      row.appendChild(el);
      keyEls.set(n.midi, el);
    });

    keysEl.appendChild(row);
  });

  /* ---------------- 卷左侧音高栏 ---------------- */
  ROWS.forEach((n, i) => {
    const p = document.createElement('div');
    p.className = 'pitchRow ' + (n.black ? 'black' : 'white') + ' oct-' + n.oct +
      (i % 12 === 0 ? ' octStart' : '');
    p.textContent = n.name;
    p.title = n.octLabel + ' ' + n.deg + ' · ' + n.mapping;
    pitchCol.appendChild(p);
  });

  /* ---------------- 卷背景 ---------------- */
  ROWS.forEach((n, i) => {
    if (n.black) {
      const b = document.createElement('div');
      b.className = 'band';
      b.style.top = (i * ROW_H) + 'px';
      b.style.height = ROW_H + 'px';
      inner.appendChild(b);
    }
  });

  function applyGrid() {
    // 每 1/4 拍画一条红线（每格 16 分时 = 4 格一条）
    const beatCells = Math.max(1, gridFactor / 4);
    const beatW = beatCells * colW;
    inner.style.backgroundImage =
      'repeating-linear-gradient(to right, transparent 0 ' + (beatW - 1) + 'px, rgba(232,196,106,.45) ' + (beatW - 1) + 'px ' + beatW + 'px),' +
      'repeating-linear-gradient(to right, transparent 0 ' + (colW - 1) + 'px, rgba(255,255,255,.065) ' + (colW - 1) + 'px ' + colW + 'px),' +
      'repeating-linear-gradient(to bottom, transparent 0 ' + (ROW_H - 1) + 'px, rgba(255,255,255,.05) ' + (ROW_H - 1) + 'px ' + ROW_H + 'px)';
  }

  function computeColW() {
    // 让初始 16 格正好铺满可视宽度；窗口太宽时限制最大格宽，避免块过大
    const w = (rollEl.clientWidth || 880) - 2;
    const raw = Math.min(84, Math.max(44, w / COLS_UNIT));
    colW = Math.round(raw * 100) / 100;
  }

  /* 试听起点指示条 */
  const cursorBand = document.createElement('div');
  cursorBand.className = 'cursorBand';
  inner.appendChild(cursorBand);

  /* 悬停预览块 */
  const ghost = document.createElement('div');
  ghost.className = 'ghost';
  ghost.style.height = (ROW_H - 3) + 'px';
  inner.appendChild(ghost);

  /* ---------------- 核心操作 ---------------- */
  function snapshot() {
    undoStack.push({ notes: notes.map(n => ({ id: n.id, midi: n.midi, col: n.col, len: n.len, mapping: n.mapping })), cols: cols });
    if (undoStack.length > 120) undoStack.shift();
  }

  function previewNote(midi) {
    tone(midi);
    flashKey(midi);
  }

  function addAt(midi, col) {
    if (col < 0 || col >= cols) return false;
    if (notes.some(n => n.midi === midi && n.col === col)) {   // 这一格已有音符
      tone(midi);
      return false;
    }
    snapshot();
    const id = uid++;
    const len = Math.min(Math.max(1, defaultLen), maxLenCells());
    notes.push({ id: id, midi: midi, col: col, len: len, mapping: currentMapping(byMidi.get(midi)) });
    if (col === cols - 1) {                                    // 写在第 16 列 → 再生成 16 格
      cols += COLS_UNIT;
      toast('已扩展到 ' + cols + ' 格');
    }
    tone(midi);
    flashKey(midi);
    render({ popId: id });
    revealCol(col);
    return true;
  }

  function selectCol(col) {
    playStart = Math.max(0, Math.min(col, cols - 1));
    render();
  }

  function flashKey(midi) {
    const el = keyEls.get(midi);
    if (!el) return;
    el.classList.add('hit');
    setTimeout(() => el.classList.remove('hit'), 170);
  }

  function removeById(id) {
    const i = notes.findIndex(n => n.id === id);
    if (i < 0) return;
    snapshot();
    notes.splice(i, 1);
    render();
  }

  function revealCol(col) {
    requestAnimationFrame(() => {
      const x = col * colW;
      const left = rollEl.scrollLeft;
      const right = left + rollEl.clientWidth;
      if (x + colW > right) rollEl.scrollLeft = x + colW - rollEl.clientWidth + 10;
      else if (x < left) rollEl.scrollLeft = Math.max(0, x - 10);
    });
  }

  /* ---------------- 渲染 ---------------- */
  function render(opts) {
    opts = opts || {};
    const prevScroll = listEl.scrollTop;

    applyGrid();

    if (playStart >= cols) playStart = Math.max(0, cols - 1);
    if (playStart < 0) playStart = 0;

    // 列号标尺
    rulerEl.innerHTML = '';
    for (let c = 0; c < cols; c++) {
      const t = document.createElement('div');
      t.className = 'tick' + ((c + 1) % 4 === 0 ? ' beat' : '') + (c === playStart ? ' cur' : '');
      t.style.left = (c * colW) + 'px';
      t.style.width = colW + 'px';
      t.textContent = c + 1;
      t.title = '选中第 ' + (c + 1) + ' 列，试听从这里开始';
      t.addEventListener('click', () => selectCol(c));
      rulerEl.appendChild(t);
    }
    rulerEl.style.width = (cols * colW) + 'px';

    // 试听起点竖条
    cursorBand.style.left = (playStart * colW) + 'px';
    cursorBand.style.width = colW + 'px';
    cursorBand.classList.add('on');

    // 钢琴卷：按格子坐标落块
    Array.prototype.forEach.call(inner.querySelectorAll('.blk'), el => el.remove());
    const ordered = sortedNotes();
    ordered.forEach((s, i) => {
      const n = byMidi.get(s.midi);
      const row = ROWS.indexOf(n);
      const b = document.createElement('div');
      b.className = 'blk' + (s.id === opts.popId ? ' pop' : '');
      b.dataset.i = i;
      b.dataset.id = s.id;
      b.dataset.len = s.len || 1;
      b.style.left = (s.col * colW + 2) + 'px';
      b.style.top = (row * ROW_H + 2) + 'px';
      b.style.width = Math.max(8, (s.len || 1) * colW - 6) + 'px';
      b.style.height = (ROW_H - 2) + 'px';
      const h = 198 - (s.midi - 48) * 2.6;
      b.style.background = 'linear-gradient(180deg,hsl(' + h + ',72%,66%),hsl(' + h + ',62%,48%))';
      b.title = '第 ' + (s.col + 1) + ' 格 · ' + n.octLabel + ' ' + n.deg + '（' + n.name + '） · ' + s.mapping +
        ' · 长 ' + (s.len || 1) + ' 格(' + noteHoldMs(s.len || 1) + 'ms)' + '（拖右边缘改长短，右键删除）';
      b.innerHTML = '<span>' + n.name + '</span><span class="t">' + shortMap(s.mapping) + '</span>';
      b.addEventListener('click', () => {
        if (suppressClick) return;
        tone(s.midi);
        flashKey(s.midi);
      });
      b.addEventListener('contextmenu', ev => { ev.preventDefault(); removeById(s.id); });
      inner.appendChild(b);
    });

    inner.style.width = (cols * colW) + 'px';
    tipEl.style.display = notes.length ? 'none' : '';
    updatePlayLabel();

    // 输出面板（按格子从左到右排）
    countEl.textContent = notes.length + ' 个音';
    colTag.textContent = cols + ' 格 · 左键写 · 右键删 · 滚轮左右滚';
    if (!ordered.length) {
      listEl.innerHTML = '<div class="listEmpty">还没有音符<br /><span>在钢琴卷格子上点一下</span></div>';
    } else {
      listEl.innerHTML = ordered.map((s, i) => {
        const n = byMidi.get(s.midi);
        return '<div class="item" data-id="' + s.id + '">' +
          '<span class="idx">' + String(i + 1).padStart(2, '0') + '</span>' +
          '<span class="nm" title="' + n.name + '">' + jnHtml(n) + '</span>' +
          '<span class="mapBtn" title="' + n.octLabel + ' ' + n.deg + ' · 游戏里按 ' + n.mapping + '">' +
            n.mapping +
          '</span>' +
          '<button class="del" data-act="del" title="删除这个音">×</button>' +
        '</div>';
      }).join('');
      listEl.scrollTop = prevScroll;
    }

    // 命令视图与节奏信息
    renderCmd();
    const step = stepMs();
    const total = notes.length ? (Math.max.apply(null, notes.map(n => n.col)) + 1) * step : 0;
    tbInfoEl.textContent = '每格 ' + Math.round(step) + 'ms · ' +
      (notes.length ? '全长 ' + fmtSec(total) : '未写音符');
  }

  /* ---------------- 钢琴卷交互 ---------------- */
  function cellAt(e) {
    const r = inner.getBoundingClientRect();
    const col = Math.floor((e.clientX - r.left) / colW);
    const row = Math.floor((e.clientY - r.top) / ROW_H);
    if (row < 0 || row >= ROWS.length || col < 0 || col >= cols) return null;
    return { row: row, col: col };
  }

  inner.addEventListener('mousemove', e => {
    if (e.target.closest('.blk')) { ghost.classList.remove('on'); return; }
    const c = cellAt(e);
    if (!c) { ghost.classList.remove('on'); return; }
    ghost.style.left = (c.col * colW + 2) + 'px';
    ghost.style.top = (c.row * ROW_H + 2) + 'px';
    ghost.style.width = Math.max(8, defaultLen * colW - 6) + 'px';
    ghost.classList.add('on');
  });

  inner.addEventListener('mouseleave', () => ghost.classList.remove('on'));

  /* ---------------- 拖动右边缘改音长 ---------------- */
  let drag = null;
  let suppressClick = false;

  function onDragMove(e) {
    if (!drag) return;
    const deltaCells = Math.round((e.clientX - drag.startX) / colW);
    const next = Math.max(1, Math.min(drag.startLen + deltaCells, maxLenCells()));
    if (next === drag.len) return;
    drag.len = next;
    const s = notes.find(n => n.id === drag.id);
    if (!s) return;
    s.len = next;
    const blk = inner.querySelector('.blk[data-id="' + s.id + '"]');
    if (blk) {
      blk.style.width = Math.max(8, next * colW - 6) + 'px';
      blk.dataset.len = next;
    }
    renderCmd();
  }

  function onDragEnd() {
    if (!drag) return;
    drag = null;
    suppressClick = true;
    window.setTimeout(() => { suppressClick = false; }, 60);
    document.body.style.cursor = '';
    window.removeEventListener('mousemove', onDragMove);
    window.removeEventListener('mouseup', onDragEnd);
    render();
  }

  inner.addEventListener('mousedown', e => {
    const blk = e.target.closest('.blk');
    if (!blk) return;
    const rect = blk.getBoundingClientRect();
    if (e.clientX < rect.right - 7) return;      // 只有右边缘 7px 才拖动
    const s = notes.find(n => n.id === Number(blk.dataset.id));
    if (!s) return;
    e.preventDefault();
    e.stopPropagation();
    snapshot();
    drag = { id: s.id, startX: e.clientX, startLen: s.len || 1, len: s.len || 1 };
    document.body.style.cursor = 'ew-resize';
    window.addEventListener('mousemove', onDragMove);
    window.addEventListener('mouseup', onDragEnd);
  });

  inner.addEventListener('click', e => {
    if (e.target.closest('.blk')) return;          // 音符块自己处理
    const c = cellAt(e);
    if (!c) return;
    addAt(ROWS[c.row].midi, c.col);                // 只写音符，不改选中列
    ghost.classList.remove('on');
  });

  // 卷内不弹浏览器右键菜单（右键删音符由块自身处理）
  inner.addEventListener('contextmenu', e => e.preventDefault());

  // 滚轮：上下滚 → 左右滚；滚到两端时把滚动交还给页面
  rollEl.addEventListener('wheel', e => {
    if (e.ctrlKey || e.metaKey) return;                        // 保留 Ctrl+滚轮 缩放
    const unit = e.deltaMode === 1 ? 16 : (e.deltaMode === 2 ? rollEl.clientWidth : 1);
    const raw = Math.abs(e.deltaY) >= Math.abs(e.deltaX) ? e.deltaY : e.deltaX;
    if (!raw) return;
    const before = rollEl.scrollLeft;
    rollEl.scrollLeft = before + raw * unit;
    if (rollEl.scrollLeft !== before) e.preventDefault();
  }, { passive: false });

  /* ---------------- 面板交互 ---------------- */
  listEl.addEventListener('click', e => {
    const item = e.target.closest('.item');
    if (!item) return;
    const id = Number(item.dataset.id);
    if (e.target.closest('.del')) { removeById(id); return; }
    // 点击条目本身也定位/闪烁一下对应琴键
    const s = notes.find(x => x.id === id);
    if (s) flashKey(s.midi);
  });

  /* ---------------- 工具栏 ---------------- */
  $('btnSound').addEventListener('click', function () {
    soundOn = !soundOn;
    this.classList.toggle('on', soundOn);
    this.textContent = soundOn ? '🔊 声音' : '🔇 静音';
    if (soundOn) tone(81);
  });

  $('btnUndo').addEventListener('click', () => {
    if (!undoStack.length) { toast('没有可撤销的操作'); return; }
    const st = undoStack.pop();
    notes = st.notes;
    cols = st.cols;
    render();
  });

  $('btnClear').addEventListener('click', () => {
    if (!notes.length) { toast('已经是空的了'); return; }
    stopPlay();
    snapshot();
    notes = [];
    cols = COLS_UNIT;
    playStart = 0;
    render();
  });

  $('btnCopy').addEventListener('click', () => {
    if (!notes.length) { toast('还没有音符'); return; }
    const ordered = sortedNotes();
    const text = ordered.map(s => s.mapping + ((s.len || 1) > 1 ? '×' + s.len : '')).join(' → ');
    if (navigator.clipboard && navigator.clipboard.writeText) {
      navigator.clipboard.writeText(text).then(
        () => toast('已复制 ' + ordered.length + ' 个键位'),
        () => fallbackCopy(text, ordered.length)
      );
    } else {
      fallbackCopy(text, ordered.length);
    }
  });

  function fallbackCopy(text, n) {
    const ta = document.createElement('textarea');
    ta.value = text;
    ta.style.position = 'fixed';
    ta.style.opacity = '0';
    document.body.appendChild(ta);
    ta.select();
    try { document.execCommand('copy'); toast('已复制 ' + n + ' 个键位'); }
    catch (e) { toast('复制失败，请手动选择'); }
    document.body.removeChild(ta);
  }

  /* ---------------- 节奏控制 ---------------- */
  const bpmEl = $('bpm');
  const gridEl = $('grid');
  const lenEl = $('lenSel');

  function syncTimingInputs() {
    bpmEl.value = bpm;
    gridEl.value = String(gridFactor);
    if (lenEl) lenEl.value = String(defaultLen);
  }

  function applyTiming() {
    const b = parseInt(bpmEl.value, 10);
    if (isFinite(b) && b >= 30 && b <= 300) bpm = b;
    gridFactor = parseInt(gridEl.value, 10) || 16;
    defaultLen = parseInt(lenEl.value, 10) || 1;
    render();
  }

  bpmEl.addEventListener('input', applyTiming);
  bpmEl.addEventListener('change', applyTiming);
  gridEl.addEventListener('change', applyTiming);
  if (lenEl) lenEl.addEventListener('change', applyTiming);

  $('panelTabs').addEventListener('click', e => {
    const btn = e.target.closest('.seg-btn');
    if (!btn) return;
    panelTab = btn.dataset.tab;
    Array.prototype.forEach.call(document.querySelectorAll('#panelTabs .seg-btn'),
      b => b.classList.toggle('on', b === btn));
    listEl.hidden = panelTab !== 'keys';
    cmdEl.hidden = panelTab !== 'cmd';
  });

  $('btnCopyCmd').addEventListener('click', () => {
    const built = buildCommands();
    if (!built.lines.length) { toast('还没有音符'); return; }
    const text = built.lines.map(l => l.text).join('\n');
    if (navigator.clipboard && navigator.clipboard.writeText) {
      navigator.clipboard.writeText(text).then(
        () => toast('已复制 ' + built.lines.length + ' 行命令'),
        () => fallbackCopy(text, built.lines.length)
      );
    } else {
      fallbackCopy(text, built.lines.length);
    }
  });

  /* ---------------- 试听 ---------------- */
  let playTimers = [];
  let playing = false;

  function updatePlayLabel() {
    const btn = $('btnPlay');
    if (playing) return;
    if (playStart > 0) {
      btn.textContent = '▶ 从第 ' + (playStart + 1) + ' 列试听';
      btn.title = '从第 ' + (playStart + 1) + ' 列开始播放';
    } else {
      btn.textContent = '▶ 试听';
      btn.title = '从头播放';
    }
  }

  function stopPlay() {
    playTimers.forEach(clearTimeout);
    playTimers = [];
    playing = false;
    document.querySelectorAll('.blk.playing').forEach(el => el.classList.remove('playing'));
    updatePlayLabel();
  }

  // 回到第 1 列（从头试听）
  function goHome() {
    if (playing) stopPlay();
    playStart = 0;
    render();
    rollEl.scrollLeft = 0;
  }

  $('btnHome').addEventListener('click', goHome);

  document.addEventListener('keydown', e => {
    if (e.key !== 'Home') return;
    const tag = (e.target && e.target.tagName) || '';
    if (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT') return;
    if (modal.classList.contains('on')) return;
    e.preventDefault();
    goHome();
  });

  $('btnPlay').addEventListener('click', () => {
    if (playing) { stopPlay(); return; }
    if (!notes.length) { toast('还没有音符'); return; }
    const list = sortedNotes().filter(n => n.col >= playStart);
    if (!list.length) { toast('第 ' + (playStart + 1) + ' 列起没有音符'); return; }

    const step = stepMs();
    const t0 = playStart * step;
    playing = true;
    $('btnPlay').textContent = '■ 停止';

    list.forEach(n => {
      const delay = Math.max(0, n.col * step - t0);
      playTimers.push(setTimeout(() => {
        tone(n.midi, Math.max(0.15, noteHoldMs(n.len || 1) / 1000));
        flashKey(n.midi);
        document.querySelectorAll('.blk.playing').forEach(el => el.classList.remove('playing'));
        const blk = inner.querySelector('.blk[data-id="' + n.id + '"]');
        if (blk) {
          blk.classList.add('playing');
          if (rollEl.scrollWidth > rollEl.clientWidth) {
            const x = blk.offsetLeft;
            rollEl.scrollTo({ left: Math.max(0, x - rollEl.clientWidth / 2), behavior: 'smooth' });
          }
        }
      }, delay));
    });

    const endAt = (list[list.length - 1].col * step + noteHoldMs(list[list.length - 1].len || 1)) - t0;
    playTimers.push(setTimeout(stopPlay, endAt + 250));
  });

  /* ---------------- 粘贴 / 导入 / 导出 ---------------- */
  const MAP_TO_MIDI = new Map();
  const NAME_TO_MIDI = new Map();
  NOTES.forEach(n => {
    MAP_TO_MIDI.set(n.mapping, n.midi);
    MAP_TO_MIDI.set(n.mapping.toLowerCase(), n.midi);
    MAP_TO_MIDI.set(shortMap(n.mapping), n.midi);
    MAP_TO_MIDI.set(n.mapping.replace('Shift+', '⇧').replace('Ctrl+', '⌃'), n.midi);
    NAME_TO_MIDI.set(n.name.toUpperCase(), n.midi);
  });

  function dedupe(list) {
    const seen = Object.create(null), out = [];
    list.forEach(p => {
      const k = p.midi + ':' + p.col;
      if (seen[k]) return;
      seen[k] = 1;
      out.push(p);
    });
    return out;
  }

  function parseSequence(text) {
    const raw = String(text == null ? '' : text).trim();
    if (!raw) return { ok: false, error: '还没有内容' };

    // ① JSON（导出的文件）
    if (raw.charAt(0) === '{' || raw.charAt(0) === '[') {
      let obj;
      try { obj = JSON.parse(raw); } catch (e) { return { ok: false, error: 'JSON 解析失败，检查一下括号和引号' }; }
      const list = Array.isArray(obj) ? obj : (obj && obj.notes);
      if (!Array.isArray(list)) return { ok: false, error: 'JSON 里没有 notes 数组' };
      const out = [];
      list.forEach(it => {
        const midi = Number(it && it.midi);
        const col = Number(it && it.col);
        if (!byMidi.has(midi) || !isFinite(col) || col < 0) return;
        const n = byMidi.get(midi);
        const keys = (it && typeof it.keys === 'string' && it.keys === n.mapping) ? it.keys : currentMapping(n);
        out.push({ midi: midi, col: Math.floor(col), len: Math.max(1, Math.floor(Number(it && it.len) || 1)), mapping: keys });
      });
      if (!out.length) return { ok: false, error: 'JSON 里没有可用音符（音高需在 C3–B5 之间）' };
      const maxCol = Math.max.apply(null, out.map(o => o.col));
      return {
        ok: true, kind: 'json', notes: dedupe(out),
        cols: Math.max(COLS_UNIT, Number(obj && obj.cols) || 0, maxCol + 1),
        timing: (obj && obj.timing && typeof obj.timing === 'object') ? obj.timing : null
      };
    }

    // ② 文本：键位链 / 音名，用箭头、换行、空格、逗号、分号分隔
    // 简谱低音写作 "1,"，先把紧跟在数字后的逗号保护起来，避免被当成音符分隔符
    let normalized = raw.replace(/[’‘]/g, "'").replace(/\s*\+\s*/g, '+');
    normalized = normalized.replace(/([0-9]),(\s|→|⇒|>|$)/g, '$1\u0001$2');
    const tokens = normalized
      .split(/→|⇒|->|=>|＞|>|\n|,|，|;|；|\||\s+/)
      .filter(Boolean)
      .map(t => t.replace(/\u0001/g, ','));
    if (!tokens.length) return { ok: false, error: '没有识别到内容' };
    const out = [], bad = [];
    tokens.forEach(t => {
      let word = t, len = 1;
      const m = /^(.*?)[×xX*](\d{1,3})$/.exec(t);
      if (m) { word = m[1]; len = Math.min(64, Math.max(1, parseInt(m[2], 10))); }
      let midi = MAP_TO_MIDI.get(word);
      if (midi === undefined) midi = MAP_TO_MIDI.get(word.toLowerCase());
      if (midi === undefined) midi = NAME_TO_MIDI.get(word.toUpperCase());
      if (midi === undefined) { bad.push(t); return; }
      out.push({ midi: midi, col: out.length, len: len, mapping: currentMapping(byMidi.get(midi)) });
    });
    if (bad.length) {
      return { ok: false, error: '无法识别：' + bad.slice(0, 3).join('、') + (bad.length > 3 ? ' 等 ' + bad.length + ' 个' : '') };
    }
    return { ok: true, kind: 'text', notes: dedupe(out), cols: Math.max(COLS_UNIT, out.length) };
  }

  function applyImport(parsed, append) {
    snapshot();
    const base = (append && notes.length) ? Math.max.apply(null, notes.map(n => n.col)) + 1 : 0;
    const list = parsed.notes.map(p => ({
      id: uid++, midi: p.midi, col: p.col + base,
      len: Math.max(1, Math.min(p.len || 1, 512)),
      mapping: p.mapping
    }));
    notes = append ? notes.concat(list) : list;

    const maxEnd = notes.length ? Math.max.apply(null, notes.map(n => n.col + (n.len || 1))) : 0;
    const need = Math.ceil(maxEnd / COLS_UNIT) * COLS_UNIT;
    let target = Math.max(COLS_UNIT, need);
    if (append) target = Math.max(target, cols);
    else if (parsed.cols) target = Math.max(target, Math.ceil(parsed.cols / COLS_UNIT) * COLS_UNIT);
    cols = target;
    playStart = 0;

    if (parsed.timing) {
      const t = parsed.timing;
      if (isFinite(t.bpm)) bpm = Math.min(300, Math.max(30, Number(t.bpm)));
      if ([4, 8, 16, 32].indexOf(Number(t.gridFactor)) >= 0) gridFactor = Number(t.gridFactor);
      if (isFinite(t.defaultLen)) defaultLen = Math.min(16, Math.max(1, Number(t.defaultLen)));
      syncTimingInputs();
    }

    render();
    if (maxEnd > 0) revealCol(Math.max(0, maxEnd - 1));
  }

  const modal = $('modal');
  const modalText = $('modalText');
  const modalInfo = $('modalInfo');
  const modalOk = $('modalOk');
  const modalAppend = $('modalAppend');
  const modalTitle = $('modalTitle');
  let pending = null;

  function updatePreview() {
    const parsed = parseSequence(modalText.value);
    pending = parsed.ok ? parsed : null;
    modalOk.disabled = !parsed.ok;
    modalOk.textContent = modalAppend.checked ? '导入并追加' : '导入并替换';

    if (!modalText.value.trim()) {
      modalInfo.className = 'modalInfo';
      modalInfo.textContent = '支持键位链（Q、Shift+Q、Ctrl+E、↑Q、↓E）、简谱音名（1、1\'、1,、#4、b3）或导出的 JSON 文件 · 打开后按 Ctrl+V 粘贴';
      return;
    }
    if (!parsed.ok) {
      modalInfo.className = 'modalInfo err';
      modalInfo.textContent = '✗ ' + parsed.error;
      return;
    }
    const span = Math.max.apply(null, parsed.notes.map(n => n.col)) + 1;
    modalInfo.className = 'modalInfo ok';
    modalInfo.textContent = '✓ 识别到 ' + parsed.notes.length + ' 个音，占用 ' + span + ' 格';
  }

  function openModal(text, title) {
    modalTitle.textContent = title || '粘贴序列';
    modalText.value = text || '';
    modalAppend.checked = false;
    modal.classList.add('on');
    updatePreview();
    setTimeout(() => modalText.focus(), 30);
  }

  function closeModal() {
    modal.classList.remove('on');
    pending = null;
  }

  modalText.addEventListener('input', updatePreview);
  modalAppend.addEventListener('change', updatePreview);
  $('modalClose').addEventListener('click', closeModal);
  $('modalCancel').addEventListener('click', closeModal);
  modal.addEventListener('click', e => { if (e.target === modal) closeModal(); });

  document.addEventListener('keydown', e => {
    if (!modal.classList.contains('on')) return;
    if (e.key === 'Escape') { closeModal(); return; }
    if (e.key === 'Enter' && (e.ctrlKey || e.metaKey) && !modalOk.disabled) { e.preventDefault(); modalOk.click(); }
  });

  modalOk.addEventListener('click', () => {
    if (!pending || !pending.ok) return;
    const n = pending.notes.length;
    applyImport(pending, modalAppend.checked);
    closeModal();
    toast('已导入 ' + n + ' 个音');
  });

  $('btnPaste').addEventListener('click', () => {
    openModal('', '粘贴序列');
  });

  /* ---------------- MIDI 导入 ---------------- */
  function parseMidiBuffer(buffer) {
    const dv = new DataView(buffer);
    let pos = 0;
    const u8 = () => dv.getUint8(pos++);
    const u16 = () => { const v = dv.getUint16(pos); pos += 2; return v; };
    const u32 = () => { const v = dv.getUint32(pos); pos += 4; return v; };
    const str4 = () => { let s = ''; for (let i = 0; i < 4; i++) s += String.fromCharCode(u8()); return s; };
    const varLen = () => { let v = 0, b; do { b = u8(); v = (v << 7) | (b & 0x7f); } while (b & 0x80); return v; };

    if (str4() !== 'MThd') throw new Error('不是 MIDI 文件');
    const hlen = u32();
    u16();                       // format
    const ntracks = u16();
    const division = u16();
    pos = 8 + hlen;

    const notes = [];
    let tempoUs = 500000;

    for (let t = 0; t < ntracks && pos + 8 <= dv.byteLength; t++) {
      if (str4() !== 'MTrk') break;
      const len = u32();
      const end = pos + len;
      let tick = 0;
      let running = 0;
      const open = new Map();

      while (pos < end) {
        tick += varLen();
        let status = dv.getUint8(pos);
        if (status & 0x80) { pos += 1; if (status < 0xF0) running = status; }
        else { status = running; }

        const type = status & 0xF0;
        if (status === 0xFF) {
          const meta = u8();
          const mlen = varLen();
          if (meta === 0x51 && mlen === 3) {
            tempoUs = (dv.getUint8(pos) << 16) | (dv.getUint8(pos + 1) << 8) | dv.getUint8(pos + 2);
          }
          pos += mlen;
        } else if (status === 0xF0 || status === 0xF7) {
          pos += varLen();
        } else if (type === 0x90 || type === 0x80) {
          const pitch = dv.getUint8(pos);
          const vel = dv.getUint8(pos + 1);
          pos += 2;
          if (type === 0x90 && vel > 0) {
            if (!open.has(pitch)) open.set(pitch, tick);
          } else {
            const st = open.get(pitch);
            if (st !== undefined) { notes.push({ start: st, end: tick, pitch: pitch }); open.delete(pitch); }
          }
        } else if (type === 0xC0 || type === 0xD0) {
          pos += 1;
        } else {
          pos += 2;
        }
      }
      open.forEach((st, pitch) => notes.push({ start: st, end: tick, pitch: pitch }));
      pos = end;
    }
    return { notes: notes, division: division, tempoUs: tempoUs };
  }

  function importMidi(buffer) {
    const parsed = parseMidiBuffer(buffer);
    if (!parsed.notes.length) { toast('MIDI 里没有音符'); return; }

    const division = parsed.division || 480;
    const ticksPerCell = Math.max(1, Math.round(division * 4 / gridFactor));
    const bpmFromMidi = Math.round(60000000 / parsed.tempoUs);

    // 整曲移八度到 C3–B5（低音 do – 高音 si）
    const pitches = parsed.notes.map(n => n.pitch);
    let minP = Math.min.apply(null, pitches);
    let maxP = Math.max.apply(null, pitches);
    let shift = 0;
    if (maxP > 83) shift = -12 * Math.ceil((maxP - 83) / 12);
    if (minP + shift < 48) shift += 12 * Math.ceil((48 - (minP + shift)) / 12);

    const cells = new Map();     // col -> { midi, len }，同列取最高音
    parsed.notes.forEach(n => {
      let p = n.pitch + shift;
      while (p < 48) p += 12;
      while (p > 83) p -= 12;
      if (p < 48 || p > 83) return;
      const col = Math.max(0, Math.round(n.start / ticksPerCell));
      const len = Math.max(1, Math.min(64, Math.round((n.end - n.start) / ticksPerCell)));
      const prev = cells.get(col);
      if (!prev || p > prev.midi) cells.set(col, { midi: p, len: len });
    });

    if (!cells.size) { toast('没有落在 C3–B5 之间的音'); return; }

    snapshot();
    const colsList = Array.from(cells.keys());
    const maxCol = Math.max.apply(null, colsList);
    notes = colsList.sort((a, b) => a - b).map(col => {
      const v = cells.get(col);
      return { id: uid++, midi: v.midi, col: col, len: v.len, mapping: currentMapping(byMidi.get(v.midi)) };
    });
    cols = Math.max(COLS_UNIT, Math.ceil((maxCol + 1) / COLS_UNIT) * COLS_UNIT);
    playStart = 0;
    if (isFinite(bpmFromMidi) && bpmFromMidi >= 30 && bpmFromMidi <= 300) {
      bpm = bpmFromMidi;
      syncTimingInputs();
    }
    render();
    toast('已导入 ' + notes.length + ' 个音 · ' + bpm + ' BPM');
  }

  $('btnImport').addEventListener('click', () => $('fileInput').click());

  $('fileInput').addEventListener('change', () => {
    const input = $('fileInput');
    const f = input.files && input.files[0];
    if (!f) return;

    if (/\.midi?$/i.test(f.name)) {
      const reader = new FileReader();
      reader.onload = () => {
        try { importMidi(reader.result); }
        catch (e) { toast('MIDI 解析失败：' + e.message); }
      };
      reader.onerror = () => toast('文件读取失败');
      reader.readAsArrayBuffer(f);
    } else {
      const reader = new FileReader();
      reader.onload = () => openModal(String(reader.result || ''), '导入「' + f.name + '」');
      reader.onerror = () => toast('文件读取失败');
      reader.readAsText(f, 'utf-8');
    }
    input.value = '';
  });

  function stamp() {
    const d = new Date();
    const p = v => String(v).padStart(2, '0');
    return d.getFullYear() + p(d.getMonth() + 1) + p(d.getDate()) + '_' + p(d.getHours()) + p(d.getMinutes());
  }

  $('btnExport').addEventListener('click', () => {
    if (!notes.length) { toast('还没有音符可以导出'); return; }
    const ordered = sortedNotes();
    const data = {
      app: 'yysls-freeplay-arranger',
      version: 1,
      cols: cols,
      count: ordered.length,
      timing: { bpm: bpm, gridFactor: gridFactor, defaultLen: defaultLen },
      notes: ordered.map(n => ({ col: n.col, len: n.len || 1, midi: n.midi, name: byMidi.get(n.midi).name, keys: n.mapping }))
    };
    try {
      const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' });
      const a = document.createElement('a');
      a.href = URL.createObjectURL(blob);
      a.download = '燕云乐谱_' + stamp() + '.json';
      document.body.appendChild(a);
      a.click();
      setTimeout(() => { URL.revokeObjectURL(a.href); a.remove(); }, 1200);
      toast('已导出 ' + ordered.length + ' 个音');
    } catch (err) {
      toast('导出失败：' + err.message);
    }
  });

  /* ---------------- 电脑键盘（映射游戏里的三排字母键） ---------------- */
  const NAT_BY_KEY = new Map();      // 字母 → 自然音 midi
  const SHARP_BY_KEY = new Map();    // 字母 → 同键加 Shift 的升半音 midi
  NOTES.forEach(n => {
    const letter = n.base.toLowerCase();
    if (!n.mod) NAT_BY_KEY.set(letter, n.midi);
    else if (n.mod === 'Shift+') SHARP_BY_KEY.set(letter, n.midi);
  });

  document.addEventListener('keydown', e => {
    if (e.repeat || e.ctrlKey || e.metaKey || e.altKey) return;   // 保留浏览器快捷键，降半音用琴键点
    const tag = (e.target && e.target.tagName) || '';
    if (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT') return;
    if (modal.classList.contains('on')) return;
    const k = (e.key || '').toLowerCase();
    if (k.length !== 1) return;
    const nat = NAT_BY_KEY.get(k);
    if (nat === undefined) return;
    let midi = nat;
    if (e.shiftKey) {
      const sharp = SHARP_BY_KEY.get(k);
      if (sharp !== undefined) midi = sharp;
    }
    e.preventDefault();
    previewNote(midi);
  });

  /* ---------------- 提示 ---------------- */
  let toastTimer = null;
  function toast(msg) {
    toastEl.textContent = msg;
    toastEl.classList.add('show');
    if (toastTimer) clearTimeout(toastTimer);
    toastTimer = setTimeout(() => toastEl.classList.remove('show'), 1600);
  }

  /* ---------------- 自适应 ---------------- */
  let resizeTimer = null;
  window.addEventListener('resize', () => {
    if (resizeTimer) clearTimeout(resizeTimer);
    resizeTimer = setTimeout(() => { computeColW(); render(); }, 120);
  });

  computeColW();
  syncTimingInputs();
  render();
  // 供测试 / 控制台调用
  window.__ocarina = {
    importMidi: function (buf) { importMidi(buf); },
    getNotes: function () { return notes.map(function (n) { return { midi: n.midi, col: n.col, len: n.len || 1, mapping: n.mapping }; }); },
    getState: function () { return { cols: cols, bpm: bpm, gridFactor: gridFactor, defaultLen: defaultLen, playStart: playStart }; },
    getCommands: function () { return buildCommands().lines.map(function (l) { return l.text; }); }
  };
})();