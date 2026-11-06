(() => {
  "use strict";

  const HEALTH_URL = "/api/Dashboard/Health";
  const AGENTS_URL = "/api/Dashboard/Agents";
  const REFRESH_MS = 5000;
  const FETCH_TIMEOUT_MS = 8000;

  const STATUS_LABEL = {
    ok: "Operacional",
    warn: "Atenção",
    error: "Falha",
    idle: "Ocioso",
  };

  const STATUS_ORDER = { error: 3, warn: 2, ok: 1, idle: 0 };

  const state = {
    health: null,
    paused: false,
    nextRefreshAt: 0,
    errorFilter: { level: "all", search: "" },
    referenceAgents: null,
    referenceLoaded: false,
  };

  const el = (id) => document.getElementById(id);

  /* ------------------------------------------------------------------ */
  /* Helpers                                                             */
  /* ------------------------------------------------------------------ */

  function safe(value) {
    return String(value ?? "")
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;")
      .replaceAll("'", "&#39;");
  }

  function worstStatus(...statuses) {
    const valid = statuses.filter((s) => s in STATUS_ORDER);
    if (valid.length === 0) return "idle";
    return valid.reduce((worst, current) =>
      STATUS_ORDER[current] > STATUS_ORDER[worst] ? current : worst, "idle");
  }

  function badge(status) {
    const key = status in STATUS_LABEL ? status : "idle";
    return `<span class="badge badge-${key}"><span class="dot"></span>${STATUS_LABEL[key]}</span>`;
  }

  function formatNumber(value, digits = 0) {
    if (value === null || value === undefined || value === "") return "—";
    const n = Number(value);
    if (!Number.isFinite(n)) return "—";
    return n.toLocaleString("pt-BR", {
      minimumFractionDigits: digits,
      maximumFractionDigits: digits,
    });
  }

  function formatPrice(value) {
    if (value === null || value === undefined || value === "") return "—";
    const n = Number(value);
    if (!Number.isFinite(n)) return "—";
    return n.toLocaleString("pt-BR", { maximumFractionDigits: 2 });
  }

  function formatTime(value) {
    if (!value) return "—";
    const d = new Date(value);
    if (Number.isNaN(d.getTime())) return "—";
    return d.toLocaleTimeString("pt-BR", { hour12: false });
  }

  function formatDateTime(value) {
    if (!value) return "—";
    const d = new Date(value);
    if (Number.isNaN(d.getTime())) return "—";
    const now = new Date();
    const sameDay = d.toDateString() === now.toDateString();
    const time = d.toLocaleTimeString("pt-BR", { hour12: false });
    if (sameDay) return time;
    return `${d.toLocaleDateString("pt-BR", { day: "2-digit", month: "2-digit" })} ${time}`;
  }

  function formatAge(seconds) {
    if (seconds === null || seconds === undefined || !Number.isFinite(Number(seconds))) {
      return "—";
    }
    const s = Math.max(0, Math.round(Number(seconds)));
    if (s < 60) return `${s}s`;
    if (s < 3600) return `${Math.floor(s / 60)}m ${s % 60}s`;
    if (s < 86400) return `${Math.floor(s / 3600)}h ${Math.floor((s % 3600) / 60)}m`;
    return `${Math.floor(s / 86400)}d ${Math.floor((s % 86400) / 3600)}h`;
  }

  function formatDuration(seconds) {
    const s = Math.max(0, Math.round(Number(seconds) || 0));
    const days = Math.floor(s / 86400);
    const hours = Math.floor((s % 86400) / 3600);
    const minutes = Math.floor((s % 3600) / 60);
    if (days > 0) return `${days}d ${hours}h ${minutes}m`;
    if (hours > 0) return `${hours}h ${minutes}m`;
    return `${minutes}m ${s % 60}s`;
  }

  function tfLabel(tf) {
    return Number(tf) === 1440 ? "1D" : `${tf}M`;
  }

  function lastOf(values) {
    const valid = values.filter(Boolean);
    if (valid.length === 0) return null;
    return valid.reduce((latest, current) =>
      new Date(current).getTime() > new Date(latest).getTime() ? current : latest);
  }

  function sumOf(values) {
    return values.reduce((total, value) => total + (Number(value) || 0), 0);
  }

  /* ------------------------------------------------------------------ */
  /* Data fetching                                                       */
  /* ------------------------------------------------------------------ */

  async function fetchHealth(force = false) {
    if (state.paused && !force) return;

    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), FETCH_TIMEOUT_MS);

    try {
      const response = await fetch(HEALTH_URL, {
        cache: "no-store",
        signal: controller.signal,
        headers: { Accept: "application/json" },
      });
      if (!response.ok) throw new Error(`HTTP ${response.status}`);
      state.health = await response.json();
      hideBanner();
      render();
    } catch (error) {
      const message = error.name === "AbortError" ? "timeout na consulta" : error.message;
      showBanner(`Falha ao consultar o servidor: ${message}`);
      if (!state.health) {
        el("overall-badge").className = "badge badge-error";
        el("overall-badge").innerHTML = `<span class="dot"></span>Sem conexão`;
      }
    } finally {
      clearTimeout(timer);
      state.nextRefreshAt = Date.now() + REFRESH_MS;
    }
  }

  async function loadReferenceAgents() {
    if (state.referenceLoaded) return;
    try {
      const response = await fetch(AGENTS_URL, { cache: "no-store" });
      if (!response.ok) throw new Error(`HTTP ${response.status}`);
      state.referenceAgents = await response.json();
      state.referenceLoaded = true;
      renderReference();
    } catch (error) {
      el("agents-reference-body").innerHTML =
        `<div class="empty"><strong>Não foi possível carregar a lista</strong>${safe(error.message)}</div>`;
    }
  }

  /* ------------------------------------------------------------------ */
  /* Render                                                              */
  /* ------------------------------------------------------------------ */

  function render() {
    if (!state.health) return;
    renderHeader();
    renderKpis();
    renderSymbols();
    renderAgents();
    renderErrors();
    renderFooter();
    if (state.referenceLoaded) renderReference();
  }

  function renderHeader() {
    const health = state.health;
    const overall = el("overall-badge");
    overall.className = `badge badge-${health.overall}`;
    overall.innerHTML = `<span class="dot"></span>${STATUS_LABEL[health.overall] ?? "—"}`;

    const dataDir = health.dataDirectory ?? {};
    const dirState = dataDir.exists ? (dataDir.writable ? "gravável" : "somente leitura") : "ausente";
    el("server-meta").textContent =
      `v${health.version} · ${health.environment} · uptime ${formatDuration(health.uptimeSeconds)} · Data: ${dirState}`;

    el("footer-meta").textContent =
      `B3WM Service Health · servidor ${formatDateTime(health.serverTime)} · gerado ${formatDateTime(health.generatedAt)}`;
  }

  function renderKpis() {
    const health = state.health;
    const symbols = health.symbols ?? [];
    const candles = symbols.flatMap((s) => s.candles ?? []);
    const structures = symbols.flatMap((s) => s.structure ?? []);

    const dataSourceStatus = worstStatus(...symbols.map((s) => s.dataSource?.status));
    const candleStatus = worstStatus(...candles.map((c) => c.status));
    const structureStatus = worstStatus(...structures.map((s) => s.status));
    const bubbleStatus = worstStatus(...symbols.map((s) => s.bubble?.status));
    const strategyStatus = worstStatus(...symbols.map((s) => s.strategies?.status));

    const lastTick = lastOf(symbols.map((s) => s.dataSource?.lastTickAt));
    const ticksPerMinute = sumOf(symbols.map((s) => s.dataSource?.ticksLastMinute));
    const okCandles = candles.filter((c) => c.status === "ok").length;

    const activeStrategies = sumOf(symbols.map((s) => s.strategies?.activeSessions));
    const decisions = sumOf(symbols.map((s) => s.strategies?.decisions));

    const jev = health.integrations?.jev ?? {};
    const python = health.integrations?.pythonBridge ?? {};
    const connections = health.connections ?? {};

    const structureSummary = symbols
      .map((s) => `${s.symbol}: ${(s.structure ?? [])
        .map((x) => `${tfLabel(x.timeFrame)}=${formatNumber(x.range)}`)
        .join(" ")}`)
      .join(" · ");

    const kpis = [
      {
        label: "Fonte de dados",
        status: dataSourceStatus,
        value: lastTick ? `Último tick ${formatTime(lastTick)}` : "Sem ticks",
        sub: `${formatNumber(ticksPerMinute)} ticks/min · ${formatNumber(sumOf(symbols.map((s) => s.dataSource?.totalTicks)))} total`,
      },
      {
        label: "Candles",
        status: candleStatus,
        value: `${okCandles}/${candles.length} timeframes OK`,
        sub: candles.length ? `Último candle ${formatDateTime(lastOf(candles.map((c) => c.lastBarAt)))}` : "Sem candles",
      },
      {
        label: "Estrutura",
        status: structureStatus,
        value: `${structures.length} timeframes`,
        sub: structureSummary || "Sem estrutura",
      },
      {
        label: "Bubbles",
        status: bubbleStatus,
        value: lastOf(symbols.map((s) => s.bubble?.lastAt))
          ? `Última ${formatTime(lastOf(symbols.map((s) => s.bubble?.lastAt)))}`
          : "Sem bubbles",
        sub: (() => {
          const withBubble = symbols.find((s) => s.bubble?.lastAt);
          if (!withBubble) return "Aguardando agressão";
          const b = withBubble.bubble;
          const agent = b.agentName || (b.agent != null ? `#${b.agent}` : "");
          return `${withBubble.symbol} · ${agent} · ${formatNumber(b.amount)}`;
        })(),
      },
      {
        label: "Strategies",
        status: strategyStatus,
        value: `${activeStrategies} ativa(s)`,
        sub: `${formatNumber(decisions)} decisões no dia`,
      },
      {
        label: "Jev / TypeSafe",
        status: jev.status,
        value: jev.model || "—",
        sub: jev.hasKey ? "chave configurada" : "sem chave configurada",
      },
      {
        label: "Bridge MT5",
        status: python.status,
        value: python.reachable ? `online · ${formatNumber(python.latencyMs)}ms` : "offline",
        sub: python.mt5Connected ? "MT5 conectado" : "MT5 desconectado",
      },
      {
        label: "Conexões SignalR",
        status: "ok",
        value: formatNumber(connections.connectedClients),
        sub: `${formatNumber(connections.totalConnections)} total desde o start`,
      },
    ];

    el("kpi-grid").innerHTML = kpis.map((kpi) => `
      <article class="kpi" data-status="${safe(kpi.status)}">
        <div class="kpi-label">${safe(kpi.label)}</div>
        <div class="kpi-value" title="${safe(kpi.value)}">${safe(kpi.value)}</div>
        <div class="kpi-sub" title="${safe(kpi.sub)}">${safe(kpi.sub)}</div>
      </article>
    `).join("");
  }

  function renderSymbols() {
    const symbols = state.health.symbols ?? [];
    el("symbols").innerHTML = symbols.map((symbol) => renderSymbol(symbol)).join("");
  }

  function renderSymbol(symbol) {
    const ds = symbol.dataSource ?? {};
    const candles = symbol.candles ?? [];
    const structures = symbol.structure ?? [];

    const candleRows = candles.length
      ? candles.map((c) => `
        <tr>
          <td><strong>${safe(tfLabel(c.timeFrame))}</strong></td>
          <td>${badge(c.status)}</td>
          <td>${safe(formatDateTime(c.lastBarAt))}</td>
          <td class="num">${safe(formatAge(c.ageSeconds))}</td>
          <td class="num">${safe(formatPrice(c.close))}</td>
          <td class="num">${safe(formatPrice(c.high))}</td>
          <td class="num">${safe(formatPrice(c.low))}</td>
          <td class="num">${safe(formatNumber(c.volume))}</td>
        </tr>
      `).join("")
      : `<tr><td colspan="8"><div class="empty">Sem candles em memória</div></td></tr>`;

    const structureRows = structures.length
      ? structures.map((s) => `
        <tr>
          <td><strong>${safe(tfLabel(s.timeFrame))}</strong></td>
          <td>${badge(s.status)}</td>
          <td class="num"><strong>${safe(formatNumber(s.range))}</strong></td>
          <td class="num">${safe(s.resolvedRange == null ? "—" : formatNumber(s.resolvedRange))}</td>
          <td class="num">${safe(formatNumber(s.defaultRange))}</td>
          <td class="num">${safe(formatPrice(s.upBorder))}</td>
          <td class="num">${safe(formatPrice(s.downBorder))}</td>
          <td>${safe(formatDateTime(s.lastAt))}</td>
          <td class="num">${safe(formatAge(s.ageSeconds))}</td>
        </tr>
      `).join("")
      : `<tr><td colspan="9"><div class="empty">Sem estrutura calculada</div></td></tr>`;

    const bubble = symbol.bubble ?? {};
    const volume = symbol.volume ?? {};
    const extreme = symbol.extreme ?? {};
    const vwap = symbol.vwap ?? {};
    const orchestration = symbol.orchestration ?? {};
    const strategies = symbol.strategies ?? {};

    const bubbleAgent = bubble.agentName
      || (bubble.agent != null ? `#${bubble.agent}` : "—");

    return `
      <article class="symbol-card">
        <header class="symbol-head">
          <div class="symbol-title">
            <h2>${safe(symbol.symbol)}</h2>
            <span class="symbol-tag">${safe(symbol.symbol)}</span>
          </div>
          ${badge(symbol.status)}
        </header>
        <div class="symbol-body">
          <div class="datasource-strip">
            <div>
              <div class="label">Fonte de dados</div>
              <strong>${ds.lastTickAt ? `Último tick ${formatDateTime(ds.lastTickAt)} (${formatAge(ds.ageSeconds)})` : "Sem ticks recebidos"}</strong>
            </div>
            <div class="label">
              ${formatNumber(ds.ticksLastMinute)} ticks/min · ${formatNumber(ds.totalTicks)} total
              · primeiro ${ds.firstTickAt ? formatDateTime(ds.firstTickAt) : "—"}
            </div>
            ${badge(ds.status)}
          </div>

          <div class="table-title"><h3>Candles por timeframe</h3><span class="muted">${candles.length} timeframes</span></div>
          <div class="table-wrap">
            <table>
              <thead><tr>
                <th>TF</th><th>Status</th><th>Último candle</th><th class="num">Idade</th>
                <th class="num">Close</th><th class="num">High</th><th class="num">Low</th><th class="num">Volume</th>
              </tr></thead>
              <tbody>${candleRows}</tbody>
            </table>
          </div>

          <div class="table-title"><h3>Estrutura · range atuando</h3><span class="muted">${structures.length} timeframes</span></div>
          <div class="table-wrap">
            <table>
              <thead><tr>
                <th>TF</th><th>Status</th><th class="num">Range atuando</th>
                <th class="num">Deduzido</th><th class="num">Default</th>
                <th class="num">Up border</th><th class="num">Down border</th>
                <th>Último update</th><th class="num">Idade</th>
              </tr></thead>
              <tbody>${structureRows}</tbody>
            </table>
          </div>

          <div class="service-grid">
            ${serviceCard("Bubble", bubble.status, [
              ["Último", formatDateTime(bubble.lastAt)],
              ["Idade", formatAge(bubble.ageSeconds)],
              ["Agente", bubbleAgent],
              ["Quantidade", formatNumber(bubble.amount)],
              ["Preço", formatPrice(bubble.price)],
              ["Lado", bubble.actionType || "—"],
            ])}
            ${serviceCard("Volume", volume.status, [
              ["Último", formatDateTime(volume.lastAt)],
              ["Idade", formatAge(volume.ageSeconds)],
              ["Níveis", formatNumber(volume.levels)],
              ["Volume total", formatNumber(volume.totalVolume)],
            ])}
            ${serviceCard("Topos / Vales", extreme.status, [
              ["Último", formatDateTime(extreme.lastAt)],
              ["Idade", formatAge(extreme.ageSeconds)],
              ["Extremos", formatNumber(extreme.count)],
            ])}
            ${serviceCard("VWAP", vwap.status, [
              ["Último", formatDateTime(vwap.lastAt)],
              ["Idade", formatAge(vwap.ageSeconds)],
              ["Valor", formatPrice(vwap.value)],
            ])}
            ${serviceCard("Orquestração", orchestration.status, [
              ["Fila do channel", formatNumber(orchestration.tickChannelQueued)],
              ["Tick processor", formatAge(orchestration.tickProcessorAgeSeconds)],
              ["Throttling", formatAge(orchestration.throttlingAgeSeconds)],
            ])}
            ${serviceCard("Strategies", strategies.status, [
              ["Ativas", formatNumber(strategies.activeSessions)],
              ["Pausadas", formatNumber(strategies.pausedSessions)],
              ["Total", formatNumber(strategies.totalSessions)],
              ["Decisões", formatNumber(strategies.decisions)],
              ["Última decisão", formatDateTime(strategies.lastDecisionAt)],
            ])}
          </div>
        </div>
      </article>
    `;
  }

  function serviceCard(title, status, rows) {
    return `
      <section class="service">
        <div class="service-head">
          <h3>${safe(title)}</h3>
          ${badge(status)}
        </div>
        <div class="service-rows">
          ${rows.map(([key, value]) => `
            <div class="service-row">
              <span class="k">${safe(key)}</span>
              <span class="v">${safe(value)}</span>
            </div>
          `).join("")}
        </div>
      </section>
    `;
  }

  function renderAgents() {
    const agents = state.health.unknownAgents ?? [];
    const count = el("agents-count");
    count.className = `badge ${agents.length ? "badge-warn" : "badge-ok"}`;
    count.textContent = agents.length ? `${agents.length} pendente(s)` : "Nenhum";

    if (agents.length === 0) {
      el("agents-body").innerHTML =
        `<div class="empty"><strong>Nenhum agente desconhecido</strong>Todos os agentes vistos estão mapeados no enum.</div>`;
      return;
    }

    const rows = agents.map((agent) => {
      const name = (agent.rawNames ?? []).length
        ? agent.rawNames.join(", ")
        : (agent.agentName || "—");
      const copyValue = (agent.rawNames ?? []).length
        ? agent.rawNames[0]
        : String(agent.agentCode);

      return `
        <tr>
          <td class="agent-code">${safe(agent.agentCode)}</td>
          <td class="wrap"><strong>${safe(name)}</strong></td>
          <td><span class="agent-source">${safe(agent.source || "—")}</span></td>
          <td>${safe((agent.symbols ?? []).join(", ") || "—")}</td>
          <td class="num">${safe(formatNumber(agent.count))}</td>
          <td>${safe(formatDateTime(agent.firstSeenAt))}</td>
          <td>${safe(formatDateTime(agent.lastSeenAt))}</td>
          <td><button type="button" class="btn btn-sm" data-copy="${safe(copyValue)}">Copiar</button></td>
        </tr>
      `;
    }).join("");

    el("agents-body").innerHTML = `
      <div class="table-wrap">
        <table>
          <thead><tr>
            <th>Código</th><th>Nome / bruto</th><th>Origem</th><th>Símbolos</th>
            <th class="num">Ocorrências</th><th>Primeiro</th><th>Último</th><th></th>
          </tr></thead>
          <tbody>${rows}</tbody>
        </table>
      </div>
      <div class="agent-hint">
        Para incluir, adicione o código/nome em
        <code>B3WM.Shared/Entity/Ticks2.cs</code>,
        <code>ExtractorRTD/Ticks2.cs</code> e
        <code>B3WM.Flutter/lib/models/ticks2.dart</code>.
      </div>
    `;
  }

  function renderErrors() {
    const errors = (state.health.recentErrors ?? []).filter((error) => {
      if (state.errorFilter.level !== "all" && error.level !== state.errorFilter.level) return false;
      if (!state.errorFilter.search) return true;
      const haystack = `${error.category} ${error.message} ${error.exceptionMessage ?? ""}`.toLowerCase();
      return haystack.includes(state.errorFilter.search.toLowerCase());
    });

    if (errors.length === 0) {
      el("errors-body").innerHTML =
        `<div class="empty"><strong>Nenhum erro para o filtro atual</strong>Os serviços não reportaram warnings/erros recentes.</div>`;
      return;
    }

    el("errors-body").innerHTML = `
      <div class="error-list">
        ${errors.map((error) => {
          const level = error.level === "Critical" ? "Critical" : error.level;
          const levelBadge = level === "Warning" ? "warn"
            : (level === "Error" || level === "Critical") ? "error" : "idle";
          const exception = error.exceptionMessage
            ? `<details class="error-exception">
                 <summary>${safe(error.exceptionType || "Exceção")}</summary>
                 <pre>${safe(error.exceptionMessage)}</pre>
               </details>`
            : "";
          return `
            <div class="error-item" data-level="${safe(level)}">
              <div class="error-top">
                <span class="error-time">${safe(formatDateTime(error.at))}</span>
                <span class="badge badge-${levelBadge}">${safe(level || "—")}</span>
                <span class="error-category">${safe(error.category || "—")}</span>
              </div>
              <div class="error-message">${safe(error.message || "—")}</div>
              ${exception}
            </div>
          `;
        }).join("")}
      </div>
    `;
  }

  function renderReference() {
    const body = el("agents-reference-body");
    if (!state.referenceAgents) return;

    const search = (el("agent-search").value || "").trim().toLowerCase();
    const known = (state.referenceAgents.known ?? []).filter((agent) => {
      if (!search) return true;
      return String(agent.code).includes(search) || agent.name.toLowerCase().includes(search);
    });

    body.innerHTML = `
      <div class="table-wrap">
        <table>
          <thead><tr><th>Código</th><th>Nome</th></tr></thead>
          <tbody>
            ${known.length ? known.map((agent) => `
              <tr><td class="agent-code">${safe(agent.code)}</td><td>${safe(agent.name)}</td></tr>
            `).join("") : `<tr><td colspan="2"><div class="empty">Nenhum agente encontrado</div></td></tr>`}
          </tbody>
        </table>
      </div>
    `;
  }

  function renderFooter() {
    // O rodapé é atualizado no header (dados do servidor); nada extra aqui.
  }

  /* ------------------------------------------------------------------ */
  /* Banner / toast / countdown                                          */
  /* ------------------------------------------------------------------ */

  function showBanner(message) {
    const banner = el("connection-banner");
    el("connection-banner-text").textContent = message;
    banner.hidden = false;
  }

  function hideBanner() {
    el("connection-banner").hidden = true;
  }

  let toastTimer = null;
  function showToast(message) {
    const toast = el("toast");
    toast.textContent = message;
    toast.hidden = false;
    clearTimeout(toastTimer);
    toastTimer = setTimeout(() => { toast.hidden = true; }, 2200);
  }

  function updateCountdown() {
    const node = el("countdown");
    if (state.paused) {
      node.textContent = "pausado";
      return;
    }
    if (document.hidden) {
      node.textContent = "aba oculta";
      return;
    }
    const remaining = Math.max(0, Math.ceil((state.nextRefreshAt - Date.now()) / 1000));
    node.textContent = `atualiza em ${remaining}s`;
  }

  /* ------------------------------------------------------------------ */
  /* Events                                                              */
  /* ------------------------------------------------------------------ */

  function bindEvents() {
    el("refresh-btn").addEventListener("click", () => fetchHealth(true));
    el("banner-retry").addEventListener("click", () => fetchHealth(true));

    el("pause-btn").addEventListener("click", () => {
      state.paused = !state.paused;
      el("pause-btn").textContent = state.paused ? "Retomar" : "Pausar";
      if (!state.paused) fetchHealth(true);
      updateCountdown();
    });

    el("error-level").addEventListener("change", (event) => {
      state.errorFilter.level = event.target.value;
      if (state.health) renderErrors();
    });

    el("error-search").addEventListener("input", (event) => {
      state.errorFilter.search = event.target.value;
      if (state.health) renderErrors();
    });

    el("agent-search").addEventListener("input", () => {
      if (state.referenceLoaded) renderReference();
    });

    el("agents-reference").addEventListener("toggle", (event) => {
      if (event.target.open) loadReferenceAgents();
    });

    document.addEventListener("click", async (event) => {
      const button = event.target.closest("[data-copy]");
      if (!button) return;
      const value = button.getAttribute("data-copy");
      try {
        await navigator.clipboard.writeText(value);
        showToast(`Copiado: ${value}`);
      } catch {
        showToast("Não foi possível copiar");
      }
    });

    document.addEventListener("visibilitychange", () => {
      if (!document.hidden && !state.paused) fetchHealth(true);
    });
  }

  /* ------------------------------------------------------------------ */
  /* Boot                                                                */
  /* ------------------------------------------------------------------ */

  bindEvents();
  updateCountdown();
  fetchHealth(true);

  setInterval(() => {
    if (!state.paused && !document.hidden && Date.now() >= state.nextRefreshAt) {
      fetchHealth();
    }
    updateCountdown();
  }, 1000);
})();
