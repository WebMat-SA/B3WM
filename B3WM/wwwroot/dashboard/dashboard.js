(() => {
  "use strict";

  const HEALTH_URL = "/api/Dashboard/Health";
  const AGENTS_URL = "/api/Dashboard/Agents";
  const REFRESH_MS = 5000;
  const FETCH_TIMEOUT_MS = 8000;
  const DASHBOARD_VERSION = "2026-11-06.9";

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
    ticksHistory: {},
    structureTimelines: {},
    tradeDates: {},
    collapsedSymbols: new Set(),
    timelineLoadedAt: 0,
    timelineLoading: false,
    lastDailyStructureAt: "",
  };

  const el = (id) => document.getElementById(id);

  /* ------------------------------------------------------------------ */
  /* Helpers                                                             */
  /* ------------------------------------------------------------------ */

  function safe(value) {
    return String(value ?? "")
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;")
      .replace(/'/g, "&#39;");
  }

  // Se a pagina veio pelo perfil HTTP (porta 5000), redireciona para o
  // HTTPS (5002). Evita o fetch cross-origin apos o 307 do UseHttpsRedirection,
  // que alguns navegadores bloqueiam por causa do certificado self-signed.
  function ensureHttps() {
    if (window.location.protocol !== "http:") return true;
    if (window.location.port !== "5000") return true;

    const target =
      `https://${window.location.hostname}:5002${window.location.pathname}${window.location.search}`;
    window.location.replace(target);
    return false;
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

    let data;
    try {
      const response = await fetch(HEALTH_URL, {
        cache: "no-store",
        signal: controller.signal,
        headers: { Accept: "application/json" },
      });

      if (!response.ok) {
        if (response.status === 404) {
          const legacy = await fetch("/api/Dashboard/Get", { cache: "no-store" });
          if (legacy.ok) {
            throw new Error(
              "o servidor está com um build antigo (sem /api/Dashboard/Health). Reinicie com o build atual.");
          }
        }
        throw new Error(`HTTP ${response.status} em ${HEALTH_URL}`);
      }

      data = await response.json();
    } catch (error) {
      const message = error.name === "AbortError"
        ? `timeout após ${FETCH_TIMEOUT_MS / 1000}s`
        : error.message;
      showBanner(
        `Falha ao consultar ${HEALTH_URL}: ${message}. ` +
        "Confirme que o servidor está em https://localhost:5002 e que o certificado é confiável.");
      console.error("[dashboard] health fetch failed", error);
      if (!state.health) {
        el("overall-badge").className = "badge badge-error";
        el("overall-badge").innerHTML = `<span class="dot"></span>Sem conexão`;
      }
      clearTimeout(timer);
      state.nextRefreshAt = Date.now() + REFRESH_MS;
      return;
    }

    clearTimeout(timer);
    state.nextRefreshAt = Date.now() + REFRESH_MS;
    state.health = data;
    hideBanner();
    updateTicksHistory(data);

    try {
      render();
    } catch (error) {
      showBanner(`Dados recebidos, mas falhou ao renderizar o dashboard: ${error.message}`);
      console.error("[dashboard] render failed", error);
    }

    maybeLoadStructureTimelines();
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
    renderCharts();
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
      `B3WM Service Health · dashboard ${DASHBOARD_VERSION} · servidor ${formatDateTime(health.serverTime)} · gerado ${formatDateTime(health.generatedAt)}`;
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
    const isOpen = !state.collapsedSymbols.has(symbol.symbol);

    return `
      <details class="symbol-card" data-symbol="${safe(symbol.symbol)}" ${isOpen ? "open" : ""}>
        <summary class="symbol-head" title="Clique para expandir/recolher">
          <div class="symbol-title">
            <h2>${safe(symbol.symbol)}</h2>
            <span class="symbol-tag">${safe(symbol.symbol)}</span>
          </div>
          <div class="symbol-actions">
            ${badge(symbol.status)}
            <span class="chevron" aria-hidden="true">▾</span>
          </div>
        </summary>
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
      </details>
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
      const codeLabel = agent.agentCode === 0 ? "—" : agent.agentCode;

      return `
        <tr>
          <td class="agent-code">${safe(codeLabel)}</td>
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
        <strong>Código 0 = não mapeado pelo extractor.</strong> O nome bruto é a informação
        confiável; use-o para localizar o código correto. Para incluir, adicione o código/nome em
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

  /* ------------------------------------------------------------------ */
  /* Charts                                                              */
  /* ------------------------------------------------------------------ */

  function updateTicksHistory(health) {
    const now = Date.now();
    for (const symbol of health.symbols ?? []) {
      const list = state.ticksHistory[symbol.symbol] ?? (state.ticksHistory[symbol.symbol] = []);
      list.push({ at: now, value: Number(symbol.dataSource?.ticksLastMinute) || 0 });
      while (list.length > 60) list.shift();
    }
  }

  function maybeLoadStructureTimelines(force = false) {
    const health = state.health;
    if (!health || state.timelineLoading) return;

    const symbols = health.symbols ?? [];
    const dailyLast = symbols
      .map((s) => (s.structure ?? []).find((x) => Number(x.timeFrame) === 1440)?.lastAt)
      .filter(Boolean)
      .sort()
      .pop() ?? "";

    const now = Date.now();
    if (!force &&
        dailyLast === state.lastDailyStructureAt &&
        now - state.timelineLoadedAt < 60000) {
      return;
    }

    const endDate = toIsoDate(new Date());
    const startDate = toIsoDate(new Date(Date.now() - 45 * 24 * 60 * 60 * 1000));
    const requests = [];

    for (const symbol of symbols) {
      const daily = (symbol.structure ?? []).find((x) => Number(x.timeFrame) === 1440);
      if (!daily) continue;

      const symbolName = encodeURIComponent(symbol.symbol);
      const structureUrl =
        `/api/Data/GetStructureHistory/${symbolName}/1440/` +
        `${encodeURIComponent(daily.range)}?days=45`;
      const barsUrl =
        `/api/Data/GetBarRange/${symbolName}/${startDate}/${endDate}/1440`;

      const structurePromise = fetch(structureUrl, { cache: "no-store" })
        .then((response) => (response.ok ? response.json() : []))
        .then((items) => {
          const byDate = new Map();
          for (const item of items ?? []) {
            const key = String(item.date ?? "").slice(0, 10);
            if (key) byDate.set(key, item);
          }
          return Array.from(byDate.values())
            .sort((a, b) => String(a.date).localeCompare(String(b.date)));
        })
        .catch(() => state.structureTimelines[symbol.symbol] ?? []);

      const tradeDatesPromise = fetch(barsUrl, { cache: "no-store" })
        .then((response) => (response.ok ? response.json() : null))
        .then((bars) => {
          if (!Array.isArray(bars)) return null;
          return new Set(
            bars
              .map((bar) => String(bar.date ?? "").slice(0, 10))
              .filter(Boolean)
          );
        })
        .catch(() => null);

      requests.push(
        Promise.all([structurePromise, tradeDatesPromise]).then(([structures, tradeDates]) => {
          state.structureTimelines[symbol.symbol] = structures;
          state.tradeDates[symbol.symbol] = tradeDates;
        })
      );
    }

    if (requests.length === 0) return;

    state.timelineLoading = true;
    state.lastDailyStructureAt = dailyLast;
    Promise.all(requests).finally(() => {
      state.timelineLoading = false;
      state.timelineLoadedAt = Date.now();
      if (state.health) renderCharts();
    });
  }

  function renderCharts() {
    if (!state.health) return;
    el("charts").innerHTML = (state.health.symbols ?? []).map(renderChartCard).join("");
  }

  function renderChartCard(symbol) {
    const charts = symbol.charts ?? {};
    const c1 = charts.candles1Min ?? [];
    const c5 = charts.candles5Min ?? [];
    const bubbles = charts.bubbles ?? [];
    const ticks = (state.ticksHistory[symbol.symbol] ?? []).map((t) => t.value);
    const price = c1.length
      ? c1[c1.length - 1].close
      : (c5.length ? c5[c5.length - 1].close : null);

    const tickValues = ticks;
    const tickBlock = chartBlock("Ticks/min", `
      ${sparklineSvg(tickValues, {
        color: "#38bdf8",
        fill: "rgba(56,189,248,0.14)",
        label: "ticks por minuto",
      })}
      ${chartLegend([
        ["mín", formatNumber(tickValues.length ? Math.min(...tickValues) : null)],
        ["máx", formatNumber(tickValues.length ? Math.max(...tickValues) : null)],
        ["atual", formatNumber(tickValues.length ? tickValues[tickValues.length - 1] : null)],
      ])}
    `);

    const candleBlock = (title, candles) => {
      const closes = (candles ?? [])
        .map((c) => Number(c.close))
        .filter((v) => Number.isFinite(v));
      return chartBlock(title, `
        ${candleSparklineSvg(candles)}
        ${chartLegend([
          ["último", formatPrice(closes.length ? closes[closes.length - 1] : null)],
          ["mín", formatPrice(closes.length ? Math.min(...closes) : null)],
          ["máx", formatPrice(closes.length ? Math.max(...closes) : null)],
        ])}
      `);
    };

    return `
      <article class="chart-card">
        <div class="chart-head">
          <h3>${safe(symbol.symbol)}</h3>
          <div class="chart-price">${price == null ? "—" : formatPrice(price)}</div>
          ${badge(symbol.status)}
        </div>
        <div class="chart-grid">
          ${tickBlock}
          ${candleBlock("Candles 1M", c1)}
          ${candleBlock("Candles 5M", c5)}
          ${chartBlock("Estrutura · preço vs bordas", structureLadderHtml(symbol))}
        </div>
        ${chartBlock(`Bubbles recentes${bubbles.length ? ` (${bubbles.length})` : ""}`, bubbleScatterHtml(bubbles))}
        ${chartBlock("Cobertura da estrutura 1D", dailyTimelineHtml(symbol))}
      </article>
    `;
  }

  function chartBlock(title, content) {
    return `<div class="chart-block"><h4>${safe(title)}</h4>${content}</div>`;
  }

  function chartLegend(items) {
    return `<div class="chart-legend">${items
      .map(([key, value]) => `<span>${safe(key)} <strong>${safe(value)}</strong></span>`)
      .join("")}</div>`;
  }

  function sparklineSvg(values, options = {}) {
    const {
      width = 320,
      height = 72,
      color = "#38bdf8",
      fill = "rgba(56,189,248,0.14)",
      label = "",
    } = options;

    if (!values || values.length < 2) {
      return `<div class="chart-empty">Coletando dados…</div>`;
    }

    const pad = { top: 8, right: 6, bottom: 8, left: 6 };
    const w = width - pad.left - pad.right;
    const h = height - pad.top - pad.bottom;
    const min = Math.min(...values);
    const max = Math.max(...values);
    const range = max - min || 1;

    const points = values.map((value, index) => {
      const x = pad.left + (index / (values.length - 1)) * w;
      const y = pad.top + h - ((value - min) / range) * h;
      return [x, y];
    });

    const path = points
      .map(([x, y], index) => `${index === 0 ? "M" : "L"}${x.toFixed(1)},${y.toFixed(1)}`)
      .join(" ");

    const area = `${path} L${(pad.left + w).toFixed(1)},${(pad.top + h).toFixed(1)} ` +
      `L${pad.left.toFixed(1)},${(pad.top + h).toFixed(1)} Z`;

    const grid = [0.25, 0.5, 0.75].map((fraction) => {
      const y = pad.top + h * fraction;
      return `<line x1="${pad.left}" y1="${y.toFixed(1)}" x2="${pad.left + w}" ` +
        `y2="${y.toFixed(1)}" stroke="rgba(143,161,189,0.12)" stroke-width="1"/>`;
    }).join("");

    const last = points[points.length - 1];

    return `<svg class="chart-svg" viewBox="0 0 ${width} ${height}" ` +
      `preserveAspectRatio="none" role="img" aria-label="${safe(label)}">` +
      `${grid}` +
      `<path d="${area}" fill="${fill}" stroke="none"/>` +
      `<path d="${path}" fill="none" stroke="${color}" stroke-width="2" ` +
      `stroke-linejoin="round" stroke-linecap="round"/>` +
      `<circle cx="${last[0].toFixed(1)}" cy="${last[1].toFixed(1)}" r="3" fill="${color}"/>` +
      `</svg>`;
  }

  function candleSparklineSvg(candles) {
    if (!candles || candles.length < 2) {
      return `<div class="chart-empty">Sem candles recentes</div>`;
    }

    const width = 320;
    const height = 96;
    const pad = { top: 8, right: 6, bottom: 24, left: 6 };
    const w = width - pad.left - pad.right;
    const priceH = height - pad.top - pad.bottom;

    const closes = candles.map((c) => Number(c.close));
    const volumes = candles.map((c) => Number(c.volume) || 0);
    const min = Math.min(...closes);
    const max = Math.max(...closes);
    const range = max - min || 1;
    const maxVolume = Math.max(...volumes, 1);

    const points = closes.map((value, index) => {
      const x = pad.left + (index / (closes.length - 1)) * w;
      const y = pad.top + priceH - ((value - min) / range) * priceH;
      return [x, y];
    });

    const path = points
      .map(([x, y], index) => `${index === 0 ? "M" : "L"}${x.toFixed(1)},${y.toFixed(1)}`)
      .join(" ");

    const barWidth = Math.max(1.5, (w / candles.length) * 0.55);
    const bars = candles.map((candle, index) => {
      const x = pad.left + (index / (candles.length - 1)) * w - barWidth / 2;
      const barHeight = ((Number(candle.volume) || 0) / maxVolume) * (pad.bottom - 5);
      const y = height - barHeight;
      return `<rect x="${x.toFixed(1)}" y="${y.toFixed(1)}" width="${barWidth.toFixed(1)}" ` +
        `height="${barHeight.toFixed(1)}" rx="1" fill="rgba(56,189,248,0.32)"/>`;
    }).join("");

    const last = points[points.length - 1];

    return `<svg class="chart-svg" viewBox="0 0 ${width} ${height}" ` +
      `preserveAspectRatio="none" role="img" aria-label="candles recentes">` +
      `<line x1="${pad.left}" y1="${(pad.top + priceH).toFixed(1)}" ` +
      `x2="${pad.left + w}" y2="${(pad.top + priceH).toFixed(1)}" ` +
      `stroke="rgba(143,161,189,0.14)" stroke-width="1"/>` +
      `${bars}` +
      `<path d="${path}" fill="none" stroke="#38bdf8" stroke-width="2" ` +
      `stroke-linejoin="round" stroke-linecap="round"/>` +
      `<circle cx="${last[0].toFixed(1)}" cy="${last[1].toFixed(1)}" r="3" fill="#38bdf8"/>` +
      `</svg>`;
  }

  function bubbleScatterHtml(bubbles) {
    if (!bubbles || bubbles.length === 0) {
      return `<div class="chart-empty">Sem bubbles recentes</div>`;
    }

    const width = 680;
    const height = 170;
    const pad = { top: 14, right: 12, bottom: 26, left: 58 };
    const plotW = width - pad.left - pad.right;
    const plotH = height - pad.top - pad.bottom;

    const times = bubbles
      .map((b) => new Date(b.date).getTime())
      .filter((v) => Number.isFinite(v));
    const prices = bubbles
      .map((b) => Number(b.price))
      .filter((v) => Number.isFinite(v));
    const amounts = bubbles.map((b) => Number(b.amount) || 0);

    if (times.length === 0 || prices.length === 0) {
      return `<div class="chart-empty">Sem dados de bubbles</div>`;
    }

    let minT = Math.min(...times);
    let maxT = Math.max(...times);
    if (minT === maxT) {
      minT -= 60000;
      maxT += 60000;
    }

    let minP = Math.min(...prices);
    let maxP = Math.max(...prices);
    const pricePad = (maxP - minP) * 0.12 || 1;
    minP -= pricePad;
    maxP += pricePad;

    const maxAmount = Math.max(...amounts, 1);
    const x = (t) => pad.left + ((t - minT) / (maxT - minT || 1)) * plotW;
    const y = (p) => pad.top + plotH - ((p - minP) / (maxP - minP || 1)) * plotH;
    const radius = (amount) => 3 + Math.sqrt(amount / maxAmount) * 10;

    const grid = [0.25, 0.5, 0.75].map((fraction) => {
      const gy = pad.top + plotH * fraction;
      return `<line x1="${pad.left}" y1="${gy.toFixed(1)}" ` +
        `x2="${(pad.left + plotW).toFixed(1)}" y2="${gy.toFixed(1)}" ` +
        `stroke="rgba(143,161,189,0.12)" stroke-width="1"/>`;
    }).join("");

    const points = bubbles.map((bubble) => {
      const t = new Date(bubble.date).getTime();
      const p = Number(bubble.price);
      if (!Number.isFinite(t) || !Number.isFinite(p)) return "";

      const color = bubble.actionType === "Buy"
        ? "#22c55e"
        : bubble.actionType === "Sale"
          ? "#ef4444"
          : "#94a3b8";
      const agent = bubble.agentName || (bubble.agent != null ? `#${bubble.agent}` : "—");
      const title = `${formatDateTime(bubble.date)} · ${agent} · ` +
        `${formatNumber(bubble.amount)} @ ${formatPrice(bubble.price)} · ` +
        `${bubble.actionType || "—"}`;

      return `<circle cx="${x(t).toFixed(1)}" cy="${y(p).toFixed(1)}" ` +
        `r="${radius(Number(bubble.amount) || 0).toFixed(1)}" fill="${color}" ` +
        `fill-opacity="0.55" stroke="${color}" stroke-width="1">` +
        `<title>${safe(title)}</title></circle>`;
    }).join("");

    const priceLabels = [minP, maxP].map((value, index) => {
      const py = index === 0 ? pad.top + plotH : pad.top;
      return `<text x="${pad.left - 6}" y="${(py + 3).toFixed(1)}" ` +
        `text-anchor="end" font-size="9" fill="rgba(143,161,189,0.8)">` +
        `${formatPrice(value)}</text>`;
    }).join("");

    const timeLabels = [minT, maxT].map((value, index) => {
      const tx = index === 0 ? pad.left : pad.left + plotW;
      const anchor = index === 0 ? "start" : "end";
      return `<text x="${tx.toFixed(1)}" y="${height - 6}" text-anchor="${anchor}" ` +
        `font-size="9" fill="rgba(143,161,189,0.8)">` +
        `${formatTime(new Date(value))}</text>`;
    }).join("");

    return `
      <svg class="chart-svg" viewBox="0 0 ${width} ${height}" preserveAspectRatio="xMidYMid meet" role="img" aria-label="bubbles recentes">
        ${grid}
        ${priceLabels}
        ${timeLabels}
        ${points}
      </svg>
      <div class="chart-legend" style="margin-top:4px;">
        <span><span class="legend-dot" style="background:#22c55e"></span>compra</span>
        <span><span class="legend-dot" style="background:#ef4444"></span>venda</span>
        <span><span class="legend-dot" style="background:#94a3b8"></span>outros</span>
        <span>tamanho = quantidade</span>
      </div>
    `;
  }

  function structureLadderHtml(symbol) {
    const wanted = [1, 5, 15, 1440];
    const rows = (symbol.structure ?? [])
      .filter((s) => wanted.includes(Number(s.timeFrame)))
      .sort((a, b) => Number(a.timeFrame) - Number(b.timeFrame))
      .map((structure) => {
        const tf = Number(structure.timeFrame);
        const candle = (symbol.candles ?? []).find((c) => Number(c.timeFrame) === tf);
        const fallback = (symbol.candles ?? []).find((c) => Number(c.timeFrame) === 1);
        const price = candle?.close ?? fallback?.close ?? null;
        return ladderRow(tfLabel(tf), structure, price);
      })
      .join("");

    return rows || `<div class="chart-empty">Sem estrutura calculada</div>`;
  }

  function ladderRow(label, structure, price) {
    const down = Number(structure.downBorder);
    const up = Number(structure.upBorder);
    const current = Number(price);
    const hasPrice = Number.isFinite(current);

    const values = [down, up, hasPrice ? current : null].filter((v) => Number.isFinite(v));
    if (values.length === 0) return "";

    let min = Math.min(...values);
    let max = Math.max(...values);
    const padding = (max - min) * 0.06 || 1;
    min -= padding;
    max += padding;

    const width = 320;
    const x = (value) => ((value - min) / (max - min)) * width;
    const downX = x(down);
    const upX = x(up);
    const priceX = hasPrice ? x(current) : null;
    const rangeLeft = Math.min(downX, upX);
    const rangeWidth = Math.abs(upX - downX);
    const inside = hasPrice && current >= Math.min(down, up) && current <= Math.max(down, up);
    const markerColor = inside ? "#22c55e" : "#f59e0b";

    return `
      <div class="ladder-row">
        <span class="ladder-label">${safe(label)}</span>
        <div>
          <svg class="ladder-svg" viewBox="0 0 ${width} 26" preserveAspectRatio="none" role="img" aria-label="${safe(label)} estrutura">
            <rect x="0" y="10" width="${width}" height="6" rx="3" fill="rgba(143,161,189,0.16)"/>
            <rect x="${rangeLeft.toFixed(1)}" y="10" width="${Math.max(2, rangeWidth).toFixed(1)}" height="6" rx="3" fill="rgba(56,189,248,0.35)" stroke="rgba(56,189,248,0.65)"/>
            ${priceX == null ? "" : `<line x1="${priceX.toFixed(1)}" y1="3" x2="${priceX.toFixed(1)}" y2="23" stroke="${markerColor}" stroke-width="2"/><circle cx="${priceX.toFixed(1)}" cy="13" r="3.2" fill="${markerColor}"/>`}
          </svg>
          <div class="ladder-values"><span>${formatPrice(down)}</span><span>${formatPrice(up)}</span></div>
        </div>
      </div>
    `;
  }

  function dailyTimelineHtml(symbol) {
    const timeline = state.structureTimelines[symbol.symbol];
    if (timeline == null) {
      return `<div class="chart-empty">${state.timelineLoading ? "Carregando histórico 1D…" : "Histórico 1D indisponível"}</div>`;
    }

    const byDate = new Map();
    for (const item of timeline) {
      const key = String(item.date ?? "").slice(0, 10);
      if (key) byDate.set(key, item);
    }

    const days = lastBusinessDays(10);
    const todayIso = toIsoDate(new Date());
    const tradeDates = state.tradeDates[symbol.symbol];

    const dots = days.map((day) => {
      const iso = toIsoDate(day);
      const item = byDate.get(iso);
      const isToday = iso === todayIso;
      const hasTrade = tradeDates ? tradeDates.has(iso) : true;
      const cls = item ? "ok" : isToday ? "pending" : hasTrade ? "missing" : "no-trade";
      const label = `${iso.slice(8, 10)}/${iso.slice(5, 7)}`;
      const title = item
        ? `${label} · estrutura ok (up ${formatPrice(item.upBorder)} / down ${formatPrice(item.downBorder)})`
        : isToday
          ? `${label} · hoje (em formação; fecha no fim do dia)`
          : hasTrade
            ? `${label} · sem estrutura 1D`
            : `${label} · sem pregão (feriado/fim de semana)`;
      return `<div class="timeline-day ${cls}" title="${safe(title)}"></div>`;
    }).join("");

    const labels = days.map((day) => {
      const iso = toIsoDate(day);
      return `<span>${safe(iso.slice(8, 10))}/${safe(iso.slice(5, 7))}</span>`;
    }).join("");

    const missing = days.filter((day) => {
      const iso = toIsoDate(day);
      const hasTrade = tradeDates ? tradeDates.has(iso) : true;
      return iso !== todayIso && hasTrade && !byDate.has(iso);
    }).length;

    return `
      <div class="timeline-track">${dots}</div>
      <div class="timeline-labels">${labels}</div>
      <div class="chart-legend" style="margin-top:8px;">
        <span><span class="legend-dot" style="background:var(--ok)"></span>com estrutura</span>
        <span><span class="legend-dot" style="background:var(--error)"></span>sem estrutura</span>
        <span><span class="legend-dot" style="background:var(--idle)"></span>hoje (em formação)</span>
        <span><span class="legend-dot" style="background:rgba(100,116,139,0.35)"></span>sem pregão</span>
      </div>
      <div class="chart-note" style="margin-top:6px;">
        ${missing > 0
          ? `<strong>${missing} dia(s) de pregão sem estrutura 1D</strong> na janela. A estrutura é calculada no fechamento diário; se o servidor ficou off na virada, o dia fica sem registro.`
          : "Cobertura 1D completa nos dias de pregão da janela exibida."}
      </div>
    `;
  }

  function lastBusinessDays(count) {
    const days = [];
    const cursor = new Date();
    cursor.setHours(0, 0, 0, 0);

    while (days.length < count) {
      const dow = cursor.getDay();
      if (dow !== 0 && dow !== 6) days.push(new Date(cursor));
      cursor.setDate(cursor.getDate() - 1);
    }

    return days.reverse();
  }

  function toIsoDate(date) {
    const d = new Date(date);
    const year = d.getFullYear();
    const month = String(d.getMonth() + 1).padStart(2, "0");
    const day = String(d.getDate()).padStart(2, "0");
    return `${year}-${month}-${day}`;
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

    // Persiste o estado de expandido/recolhido entre os polls de 5s.
    // Usamos "click" (e não "toggle") para não sermos afetados por eventos
    // disparados quando o DOM é recriado a cada atualização.
    el("symbols").addEventListener("click", (event) => {
      const summary = event.target.closest("summary.symbol-head");
      if (!summary) return;

      const details = summary.closest("details.symbol-card");
      if (!details) return;

      const symbol = details.dataset.symbol;
      if (!symbol) return;

      // O <details> nativo inverte o estado logo após o clique.
      if (details.open) state.collapsedSymbols.add(symbol);
      else state.collapsedSymbols.delete(symbol);
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

  if (!ensureHttps()) return;

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
