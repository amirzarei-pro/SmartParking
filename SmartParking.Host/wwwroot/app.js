/* =========================================================
   SmartParking Admin UI — Pilot Mode (1 NodeMCU + 2 Sensors)
   - Device: NODE-001
   - Sensors: S1, S2
   - Slots: A1 <- S1, A2 <- S2
   - Ready to scale to N sensors / multiple devices
========================================================= */

const $ = (sel, root = document) => root.querySelector(sel);
const $$ = (sel, root = document) => Array.from(root.querySelectorAll(sel));

const state = {
    settings: { thresholdCm: 15, intervalSec: 5, offlineTimeoutSec: 20, enableSignalR: true, enableAudit: true },
    slots: [],
    devices: [],
    logs: [],
    simTimer: null,
    charts: { line: null, donut: null },
    route: "dashboard",
    selectedSlotId: null,
    filter: "all"
};

function nowISO() { return new Date().toISOString(); }
function fmtTime(d) {
    const dt = new Date(d);
    return dt.toLocaleString(undefined, { hour12: false, month: "short", day: "2-digit", hour: "2-digit", minute: "2-digit" });
}
function rand(min, max) { return Math.floor(Math.random() * (max - min + 1)) + min; }
function clamp(n, a, b) { return Math.max(a, Math.min(b, n)); }

function escapeHtml(str) {
    return String(str ?? "")
        .replaceAll("&", "&amp;").replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;").replaceAll('"', "&quot;")
        .replaceAll("'", "&#039;");
}

function cryptoRandomId() {
    return ([1e7] + -1e3 + -4e3 + -8e3 + -1e11).replace(/[018]/g, c =>
        (c ^ crypto.getRandomValues(new Uint8Array(1))[0] & 15 >> c / 4).toString(16)
    );
}

/* ---------------------------
   PILOT SEED: 1 device + 2 sensors + 2 slots
---------------------------- */
function seedPilot() {
    const deviceId = cryptoRandomId();

    // One device with 2 sensors
    state.devices = [{
        id: deviceId,
        code: "NODE-001",
        apiKey: "••••••••••",
        online: true,
        lastSeen: nowISO(),
        sensors: [
            { sensorId: "S1", label: "Sensor 1", slotLabel: "A1" },
            { sensorId: "S2", label: "Sensor 2", slotLabel: "A2" }
        ]
    }];

    // Two slots mapped to sensors
    const threshold = state.settings.thresholdCm;

    state.slots = [
        makeSlot("A1", "A", 1, "NODE-001", "S1", rand(threshold + 8, 60), "free"),
        makeSlot("A2", "A", 2, "NODE-001", "S2", rand(5, clamp(threshold - 2, 6, 30)), "occupied"),
    ];

    // Initial logs
    state.logs = [];
    pushLog({ level: "info", status: "free", message: "Pilot initialized: NODE-001 with 2 sensors", meta: "System", time: nowISO() }, { silent: true });
    pushLog({ level: "info", status: "free", message: "Slot A1 mapped to NODE-001/S1", meta: "Config", time: nowISO() }, { silent: true });
    pushLog({ level: "info", status: "occupied", message: "Slot A2 mapped to NODE-001/S2", meta: "Config", time: nowISO() }, { silent: true });
}

function makeSlot(label, zone, slotNum, deviceCode, sensorId, distanceCm, status) {
    return {
        id: cryptoRandomId(),
        label,
        zone,
        slotNum,
        deviceCode,     // NODE-001
        sensorId,       // S1/S2
        status,         // free | occupied | offline
        distanceCm,
        lastUpdate: nowISO()
    };
}

/* ---------------------------
   Routing
---------------------------- */
function setRoute(route) {
    state.route = route;

    $$("#nav .nav__item").forEach(btn => btn.classList.toggle("is-active", btn.dataset.route === route));
    $$(".route").forEach(r => r.classList.remove("is-active"));
    $(`#route-${route}`)?.classList.add("is-active");

    const crumb = { dashboard: "Dashboard", live: "Live Parking", devices: "Devices", logs: "Logs", settings: "Settings" }[route] || "Dashboard";
    $("#crumbCurrent").textContent = crumb;

    if (route === "dashboard") renderDashboard();
    if (route === "live") renderLive();
    if (route === "devices") renderDevices();
    if (route === "logs") renderLogs();
    if (route === "settings") renderSettings();
}

/* ---------------------------
   KPI / Counts
---------------------------- */
function countStatuses() {
    const free = state.slots.filter(s => s.status === "free").length;
    const occupied = state.slots.filter(s => s.status === "occupied").length;
    const offline = state.slots.filter(s => s.status === "offline").length;
    return { free, occupied, offline, total: state.slots.length };
}

function renderKPIs() {
    const c = countStatuses();
    $("#kpiTotal").textContent = c.total;
    $("#kpiOcc").textContent = c.occupied;
    $("#kpiFree").textContent = c.free;
    $("#kpiOnline").textContent = state.devices.filter(d => d.online).length;
    $("#apiLatency").textContent = rand(18, 55);
}

/* ---------------------------
   Charts
---------------------------- */
function buildCharts() {
    // Build simple 24h line based on pilot occupancy states
    const labels = Array.from({ length: 24 }).map((_, i) => `${String(i).padStart(2, "0")}:00`);
    const data = labels.map(() => {
        // pilot-friendly: approximate occupancy between 0..100 based on current ratio
        const ratio = state.slots.length ? (state.slots.filter(s => s.status === "occupied").length / state.slots.length) : 0;
        return clamp(Math.round(ratio * 100 + rand(-10, 10)), 0, 100);
    });

    const lineCtx = $("#chartLine");
    if (state.charts.line) state.charts.line.destroy();
    state.charts.line = new Chart(lineCtx, {
        type: "line",
        data: {
            labels, datasets: [{
                label: "Occupancy %",
                data,
                tension: 0.35,
                borderWidth: 2,
                pointRadius: 0,
                fill: true,
                backgroundColor: "rgba(79,195,255,0.10)",
                borderColor: "rgba(79,195,255,0.90)"
            }]
        },
        options: {
            responsive: true,
            plugins: { legend: { display: false }, tooltip: { intersect: false, mode: "index" } },
            scales: {
                x: { grid: { color: "rgba(255,255,255,0.06)" }, ticks: { color: "rgba(234,242,255,0.70)" } },
                y: { grid: { color: "rgba(255,255,255,0.06)" }, ticks: { color: "rgba(234,242,255,0.70)" }, min: 0, max: 100 }
            }
        }
    });

    const donutCtx = $("#chartDonut");
    const counts = countStatuses();
    if (state.charts.donut) state.charts.donut.destroy();
    state.charts.donut = new Chart(donutCtx, {
        type: "doughnut",
        data: {
            labels: ["Free", "Occupied", "Offline"],
            datasets: [{
                data: [counts.free, counts.occupied, counts.offline],
                backgroundColor: [
                    "rgba(51,214,159,0.85)",
                    "rgba(255,77,94,0.85)",
                    "rgba(107,114,128,0.80)"
                ],
                borderWidth: 0
            }]
        },
        options: { cutout: "68%", plugins: { legend: { display: false } } }
    });

    const peak = Math.max(...data);
    const peakIndex = data.indexOf(peak);
    $("#peakHour").textContent = `${labels[peakIndex]} (${peak}%)`;
    $("#avgOcc").textContent = `${Math.round(data.reduce((a, b) => a + b, 0) / data.length)}%`;
}

function updateDonut() {
    if (!state.charts.donut) return;
    const c = countStatuses();
    state.charts.donut.data.datasets[0].data = [c.free, c.occupied, c.offline];
    state.charts.donut.update();
}

/* ---------------------------
   Events / Logs
---------------------------- */
function pushLog(entry, { silent = false } = {}) {
    state.logs.unshift(entry);
    if (!silent && state.route === "logs") renderLogs();
    if (!silent) renderEvents();
}

function renderEvents() {
    const feed = $("#eventFeed");
    feed.innerHTML = "";

    const recent = state.logs.slice(0, 8);
    for (const l of recent) {
        const badgeClass = l.status === "free" ? "badge--free" : (l.status === "occupied" ? "badge--occ" : "badge--off");
        const el = document.createElement("div");
        el.className = "event";
        el.innerHTML = `
      <div class="event__left">
        <div class="event__title">${escapeHtml(l.message)}</div>
        <div class="event__meta">${escapeHtml(l.meta)} • ${fmtTime(l.time)}</div>
      </div>
      <div class="badge ${badgeClass}">${escapeHtml(l.status.toUpperCase())}</div>
    `;
        feed.appendChild(el);
    }
}

/* ---------------------------
   Dashboard Table
---------------------------- */
function statusBadge(status) {
    if (status === "free") return `<span class="badge badge--free">FREE</span>`;
    if (status === "occupied") return `<span class="badge badge--occ">OCCUPIED</span>`;
    return `<span class="badge badge--off">OFFLINE</span>`;
}

function renderSlotsTable() {
    const body = $("#slotsTable");
    body.innerHTML = "";

    const slots = state.slots
        .filter(s => state.filter === "all" ? true : s.status === state.filter)
        .sort((a, b) => a.slotNum - b.slotNum);

    for (const s of slots) {
        const row = document.createElement("div");
        row.className = "table__row";
        row.innerHTML = `
      <div class="cell" data-label="Slot">${escapeHtml(s.label)}</div>
      <div class="cell cell--muted" data-label="Zone">Zone ${escapeHtml(s.zone)}</div>
      <div class="cell" data-label="Status">${statusBadge(s.status)}</div>
      <div class="cell cell--muted" data-label="Distance">${s.distanceCm} cm</div>
      <div class="cell cell--muted" data-label="Last Update">${fmtTime(s.lastUpdate)}</div>
      <div class="cell cell--muted" data-label="Device">${escapeHtml(s.deviceCode)}/${escapeHtml(s.sensorId)}</div>
      <div class="cell cell--actions" data-label="Actions">
        <button class="btn btn--ghost" data-action="view" data-id="${s.id}">View</button>
        <button class="btn btn--ghost" data-action="tele" data-id="${s.id}">Telemetry</button>
      </div>
    `;
        body.appendChild(row);
    }

    body.querySelectorAll("[data-action='view']").forEach(b => b.addEventListener("click", () => {
        setRoute("live");
        selectSlot(b.dataset.id);
    }));

    body.querySelectorAll("[data-action='tele']").forEach(b => b.addEventListener("click", () => {
        simulateTelemetry(b.dataset.id);
    }));
}

/* ---------------------------
   Live Map (2 tiles in Pilot)
---------------------------- */
function renderLive() {
    const map = $("#slotMap");
    map.innerHTML = "";

    const slots = [...state.slots].sort((a, b) => a.slotNum - b.slotNum);
    for (const s of slots) {
        const tile = document.createElement("div");
        tile.className = `slot-tile ${s.status}`;
        tile.dataset.id = s.id;
        tile.innerHTML = `
      <div class="slot-tile__id">${escapeHtml(s.label)}</div>
      <div class="slot-tile__sub">${escapeHtml(s.deviceCode)}/${escapeHtml(s.sensorId)}</div>
      <div class="slot-tile__sub">${s.distanceCm} cm • ${fmtTime(s.lastUpdate)}</div>
    `;
        tile.addEventListener("click", () => selectSlot(s.id));
        map.appendChild(tile);
    }

    if (state.selectedSlotId) selectSlot(state.selectedSlotId, { silent: true });
}

function selectSlot(id, opts = {}) {
    state.selectedSlotId = id;
    const slot = state.slots.find(s => s.id === id);
    if (!slot) return;

    $$(".slot-tile").forEach(t => t.style.outline = (t.dataset.id === id) ? "2px solid rgba(79,195,255,.55)" : "none");
    $("#slotDetailPill").textContent = `${slot.label} • ${slot.status.toUpperCase()}`;

    const host = $("#slotDetails");
    host.innerHTML = `
    <div class="details__box">
      <div class="details__row"><div class="details__k">Slot</div><div class="details__v">${escapeHtml(slot.label)}</div></div>
      <div class="details__row"><div class="details__k">Device</div><div class="details__v">${escapeHtml(slot.deviceCode)}</div></div>
      <div class="details__row"><div class="details__k">Sensor Channel</div><div class="details__v">${escapeHtml(slot.sensorId)}</div></div>
      <div class="details__row"><div class="details__k">Status</div><div class="details__v">${statusBadge(slot.status)}</div></div>
      <div class="details__row"><div class="details__k">Distance</div><div class="details__v">${slot.distanceCm} cm</div></div>
      <div class="details__row"><div class="details__k">Last Update</div><div class="details__v">${fmtTime(slot.lastUpdate)}</div></div>
      <div class="divider"></div>
      <div class="details__row">
        <button class="btn btn--primary" id="btnSimOne">Simulate Telemetry</button>
        <button class="btn btn--ghost" id="btnToggleOffline">Toggle Offline</button>
      </div>
    </div>
  `;

    $("#btnSimOne").addEventListener("click", () => simulateTelemetry(slot.id));
    $("#btnToggleOffline").addEventListener("click", () => toggleOffline(slot.id));

    if (!opts.silent) toast("Slot selected", `${slot.label} loaded (pilot).`, "ok");
}

function toggleOffline(id) {
    const slot = state.slots.find(s => s.id === id);
    if (!slot) return;
    slot.status = (slot.status === "offline") ? "free" : "offline";
    slot.lastUpdate = nowISO();
    pushLog({ level: "warn", status: slot.status, message: `${slot.label} set to ${slot.status}`, meta: "Admin action", time: nowISO() });
    renderKPIs(); updateDonut();
    if (state.route === "live") renderLive();
    if (state.route === "dashboard") renderSlotsTable();
    selectSlot(id, { silent: true });
}

/* ---------------------------
   Telemetry Simulation (matches real API payload)
---------------------------- */
function simulateTelemetry(slotId) {
    const slot = state.slots.find(s => s.id === slotId);
    if (!slot) return;

    // Emulate device POST: { deviceCode:"NODE-001", sensorId:"S1", distanceCm: 12.4, ts:"..." }
    const dist = rand(4, 65);
    const threshold = state.settings.thresholdCm;

    const isOffline = Math.random() < 0.02;
    const status = isOffline ? "offline" : (dist < threshold ? "occupied" : "free");

    slot.distanceCm = dist;
    slot.status = status;
    slot.lastUpdate = nowISO();

    // update device heartbeat
    const dev = state.devices[0];
    dev.lastSeen = nowISO();
    dev.online = true;

    pushLog({
        level: "info",
        status,
        message: `Telemetry: ${slot.deviceCode}/${slot.sensorId} distance=${dist}cm`,
        meta: `POST /api/iot/telemetry • slot=${slot.label}`,
        time: nowISO()
    });

    renderKPIs(); updateDonut();
    if (state.route === "dashboard") renderSlotsTable();
    if (state.route === "live") renderLive();
    if (state.selectedSlotId === slot.id && state.route === "live") selectSlot(slot.id, { silent: true });
}

/* ---------------------------
   Devices page (Pilot: single device with 2 channels)
---------------------------- */
function renderDevices() {
    const host = $("#devicesGrid");
    if (!host) return;

    const q = ($("#deviceSearch")?.value || "").trim().toLowerCase();
    const devices = state.devices
        .filter(d => !q || d.code.toLowerCase().includes(q))
        .sort((a, b) => a.code.localeCompare(b.code));

    host.innerHTML = "";

    if (devices.length === 0) {
        host.innerHTML = `<div class="empty">
          <div class="empty__title">No devices found</div>
          <div class="empty__sub">Try a different search term.</div>
        </div>`;
        return;
    }

    for (const d of devices) {
        const statusPill = d.online
            ? `<span class="pill pill--ok"><span class="dot dot--online"></span> ONLINE</span>`
            : `<span class="pill pill--danger"><span class="dot"></span> OFFLINE</span>`;

        const channels = (d.sensors || []).map(s => {
            const slot = s.slotLabel && s.slotLabel !== "—" ? s.slotLabel : "Unmapped";
            return `<div class="kv2"><span>${escapeHtml(s.sensorId)} → Slot</span><span>${escapeHtml(slot)}</span></div>`;
        }).join("");

        const sub = (d.code === "NODE-001")
            ? "Pilot device with 2 ultrasonic channels"
            : `Device • ${(d.sensors || []).length} channel(s)`;

        const card = document.createElement("div");
        card.className = "card";
        card.innerHTML = `
          <div class="card__head">
            <div>
              <div class="card__title">${escapeHtml(d.code)}</div>
              <div class="card__sub">${escapeHtml(sub)}</div>
            </div>
            ${statusPill}
          </div>

          <div class="card__body">
            <div class="kv2"><span>Last seen</span><span>${d.lastSeen ? fmtTime(d.lastSeen) : "—"}</span></div>
            ${channels}
            <div class="actions">
              <button class="btn btn--ghost" data-dev-action="ping" data-dev-id="${d.id}">Ping</button>
              <button class="btn btn--primary" data-dev-action="map" data-dev-id="${d.id}">Map Sensors</button>
              ${d.code === "NODE-001" ? "" : `<button class="btn btn--ghost" data-dev-action="remove" data-dev-id="${d.id}">Remove</button>`}
            </div>
          </div>
        `;
        host.appendChild(card);
    }

    // One delegated handler for all device cards
    host.onclick = (e) => {
        const btn = e.target.closest("[data-dev-action]");
        if (!btn) return;

        const action = btn.dataset.devAction;
        const id = btn.dataset.devId;
        const dev = state.devices.find(x => x.id === id);
        if (!dev) return;

        if (action === "ping") {
            toast("Ping", `Ping sent to ${dev.code} (demo).`, "ok");
            pushLog({ level: "info", status: "free", message: `Ping sent to ${dev.code}`, meta: "Devices", time: nowISO() });
            return;
        }

        if (action === "map") {
            if (dev.code === "NODE-001") {
                openModal("Sensor Mapping (Pilot)", `
                  Current mapping is fixed for pilot:
                  <br/><b>S1 → A1</b>
                  <br/><b>S2 → A2</b>
                  <br/><br/>In scale-out, this becomes an admin feature calling:
                  <br/><b>POST /api/admin/device/map-sensor</b>
                `, closeModal);
            } else {
                openModal("Sensor Mapping", `
                  This is a demo UI. In pilot you have fixed slots <b>A1</b> and <b>A2</b> on <b>NODE-001</b>.
                  <br/><br/>In phase 2, you'll enable mapping for additional devices and create new slots.
                `, closeModal);
            }
            return;
        }

        if (action === "remove") {
            openModal("Remove Device", `
              Are you sure you want to remove <b>${escapeHtml(dev.code)}</b>?
              <br/><br/>This only affects the UI seed (demo).
            `, () => {
                state.devices = state.devices.filter(x => x.id !== dev.id);
                pushLog({ level: "warn", status: "offline", message: `Device removed: ${dev.code}`, meta: "Devices", time: nowISO() });
                closeModal();
                renderDevices();
            });
        }
    };
}


/* ---------------------------
   Logs
---------------------------- */
function renderLogs() {
    const level = $("#logLevel")?.value || "all";
    const list = (level === "all") ? state.logs : state.logs.filter(l => l.level === level);

    const host = $("#logList");
    host.innerHTML = "";

    for (const l of list.slice(0, 60)) {
        const el = document.createElement("div");
        el.className = "log";
        el.innerHTML = `
      <div>
        <div class="log__msg">${escapeHtml(l.message)}</div>
        <div class="log__meta">${escapeHtml(l.meta)} • ${fmtTime(l.time)}</div>
      </div>
      <div class="level ${escapeHtml(l.level)}">${l.level.toUpperCase()}</div>
    `;
        host.appendChild(el);
    }

    renderTerminal();
}

function renderTerminal() {
    const term = $("#terminal");
    term.innerHTML = "";
    for (const l of state.logs.slice(0, 40).reverse()) {
        const cls = l.level === "error" ? "e" : (l.level === "warn" ? "w" : "t");
        const line = document.createElement("div");
        line.className = "line";
        line.innerHTML = `<span class="${cls}">[${l.level.toUpperCase()}]</span> ${escapeHtml(fmtTime(l.time))} — ${escapeHtml(l.message)}`;
        term.appendChild(line);
    }
    term.scrollTop = term.scrollHeight;
}

/* ---------------------------
   Settings
---------------------------- */
function renderSettings() {
    $("#threshold").value = state.settings.thresholdCm;
    $("#interval").value = state.settings.intervalSec;
    $("#offlineTimeout").value = state.settings.offlineTimeoutSec;
    $("#enableSignalR").checked = state.settings.enableSignalR;
    $("#enableAudit").checked = state.settings.enableAudit;
}

/* ---------------------------
   Toast + Modal (same as template)
---------------------------- */
function toast(title, msg, type = "ok") {
    const host = $("#toasts");
    const el = document.createElement("div");
    el.className = `toast toast--${type}`;
    el.innerHTML = `<div class="toast__title">${escapeHtml(title)}</div><div class="toast__msg">${escapeHtml(msg)}</div>`;
    host.appendChild(el);
    setTimeout(() => el.remove(), 3200);
}

function openModal(title, html, onConfirm) {
    $("#modalTitle").textContent = title;
    $("#modalBody").innerHTML = html;
    $("#modalBackdrop").hidden = false;
    $("#modalPrimary").onclick = () => onConfirm?.();
}
function closeModal() {
    $("#modalBackdrop").hidden = true;
    $("#modalPrimary").onclick = null;
}
$$("[data-modal-close]").forEach(b => b.addEventListener("click", closeModal));
$("#modalBackdrop").addEventListener("click", (e) => { if (e.target.id === "modalBackdrop") closeModal(); });

/* ---------------------------
   Simulation (pilot: alternates between A1/A2)
---------------------------- */
function startSimulation() {
    if (state.simTimer) return;
    state.simTimer = setInterval(() => {
        const slot = state.slots[rand(0, state.slots.length - 1)];
        simulateTelemetry(slot.id);
    }, 1400);
    toast("Live simulation", "Simulating pilot telemetry for 2 sensors.", "ok");
}
function stopSimulation() {
    if (!state.simTimer) return;
    clearInterval(state.simTimer);
    state.simTimer = null;
    toast("Stopped", "Live simulation stopped.", "warn");
}

/* ---------------------------
   Wire UI events
---------------------------- */

/* ---------------------------
   Devices: Add Device (demo-ready, scalable)
---------------------------- */
function openAddDeviceModal() {
    openModal("Add Device", `
      <div class="form">
        <label class="form__label">Device code</label>
        <input class="input" id="newDeviceCode" placeholder="e.g., NODE-002" />
        <div class="spacer"></div>
        <label class="form__label">Sensor channels</label>
        <input class="input" id="newDeviceSensors" type="number" min="1" max="16" value="2" />
        <div class="hint">Pilot uses NODE-001 with A1/A2. Additional devices are added as demo records until phase 2 mapping is enabled.</div>
      </div>
    `, () => {
        const code = ($("#newDeviceCode")?.value || "").trim().toUpperCase();
        const n = parseInt($("#newDeviceSensors")?.value || "2", 10);

        if (!code) return toast("Invalid", "Device code is required.", "warn");
        if (!/^[A-Z0-9_-]{3,20}$/.test(code)) return toast("Invalid", "Use 3–20 chars: A–Z, 0–9, _ or -", "warn");
        if (!Number.isFinite(n) || n < 1 || n > 16) return toast("Invalid", "Sensor channels must be between 1 and 16.", "warn");
        if (state.devices.some(d => d.code.toUpperCase() === code)) return toast("Duplicate", "This device code already exists.", "warn");

        const dev = {
            id: cryptoRandomId(),
            code,
            apiKey: "••••••••••",
            online: false,
            lastSeen: null,
            sensors: Array.from({ length: n }).map((_, i) => ({
                sensorId: `S${i + 1}`,
                label: `Sensor ${i + 1}`,
                slotLabel: "—"
            }))
        };

        state.devices.push(dev);
        pushLog({ level: "info", status: "free", message: `Device added: ${code} (${n} channel(s))`, meta: "Devices", time: nowISO() });

        closeModal();
        if (state.route === "devices") renderDevices();
        toast("Added", `${code} added (demo).`, "ok");
    });

    // Auto-focus for better UX
    setTimeout(() => $("#newDeviceCode")?.focus(), 0);
}

function wireEvents() {
    // nav
    $$("#nav .nav__item").forEach(btn => btn.addEventListener("click", () => { setRoute(btn.dataset.route); if (isNarrow()) closeSidebarMobile(); }));
    $$("[data-route-jump]").forEach(btn => btn.addEventListener("click", () => setRoute(btn.dataset.routeJump)));

    // devices
    $("#btnAddDevice")?.addEventListener("click", () => {
        openAddDeviceModal();
    });

    $("#deviceSearch")?.addEventListener("input", () => {
        if (state.route === "devices") renderDevices();
    });


    // filters
    $$(".seg__btn").forEach(b => b.addEventListener("click", () => {
        $$(".seg__btn").forEach(x => x.classList.remove("is-active"));
        b.classList.add("is-active");
        state.filter = b.dataset.filter;
        renderSlotsTable();
    }));

    // refresh
    $("#btnRefresh").addEventListener("click", () => {
        renderKPIs();
        buildCharts();
        renderEvents();
        renderSlotsTable();
        toast("Refreshed", "Pilot UI refreshed.", "ok");
    });

    // seed button: re-seed pilot
    $("#btnSeedData").addEventListener("click", () => {
        seedPilot();
        renderDashboard();
        toast("Seeded", "Pilot seed applied: NODE-001 + 2 sensors.", "ok");
    });

    // add slot in pilot: disabled (to keep consistency)
    $("#btnAddSlot").addEventListener("click", () => {
        openModal("Pilot Mode", `
      In pilot you have: <b>1 device</b> and <b>2 sensors</b>.
      <br/>Slots are fixed: <b>A1</b> and <b>A2</b>.
      <br/><br/>Scale-out phase will enable adding slots/devices.
    `, closeModal);
    });

    // live buttons
    $("#btnLegend").addEventListener("click", () => {
        openModal("Legend", `
      <div style="display:flex;flex-direction:column;gap:10px;">
        <div><span class="badge badge--free">FREE</span> Available</div>
        <div><span class="badge badge--occ">OCCUPIED</span> Car detected</div>
        <div><span class="badge badge--off">OFFLINE</span> Device/sensor not responding</div>
      </div>
    `, closeModal);
    });

    $("#btnForceRefresh").addEventListener("click", () => {
        if (!state.selectedSlotId) return toast("No slot selected", "Select A1 or A2 first.", "warn");
        simulateTelemetry(state.selectedSlotId);
    });

    $("#btnMarkOffline").addEventListener("click", () => {
        if (!state.selectedSlotId) return toast("No slot selected", "Select A1 or A2 first.", "warn");
        toggleOffline(state.selectedSlotId);
    });

    $("#btnReassign").addEventListener("click", () => {
        openModal("Pilot Mode", `
      Sensor mapping is fixed in pilot:
      <br/><b>S1 → A1</b>
      <br/><b>S2 → A2</b>
      <br/><br/>In phase 2, this becomes an editable mapping screen.
    `, closeModal);
    });

    // logs controls
    $("#logLevel").addEventListener("change", () => renderLogs());
    $("#btnExport").addEventListener("click", () => exportLogs());
    $("#btnInject").addEventListener("click", () => {
        pushLog({ level: "info", status: "free", message: "Injected test event (pilot)", meta: "Manual", time: nowISO() });
        toast("Injected", "Event added.", "ok");
        renderLogs();
    });
    $("#btnClearLogs").addEventListener("click", () => {
        state.logs = [];
        renderLogs();
        toast("Cleared", "Logs cleared.", "warn");
    });

    // settings
    $("#btnSaveSettings").addEventListener("click", () => {
        state.settings.thresholdCm = parseInt($("#threshold").value, 10);
        state.settings.intervalSec = parseInt($("#interval").value, 10);
        state.settings.offlineTimeoutSec = parseInt($("#offlineTimeout").value, 10);
        state.settings.enableSignalR = $("#enableSignalR").checked;
        state.settings.enableAudit = $("#enableAudit").checked;

        toast("Saved", "Settings saved (pilot).", "ok");
        pushLog({ level: "info", status: "free", message: "Settings updated", meta: "Admin", time: nowISO() });

        // Optional: rerun charts so they reflect latest threshold logic
        buildCharts();
    });

    $("#btnResetSettings").addEventListener("click", () => {
        state.settings = { thresholdCm: 15, intervalSec: 5, offlineTimeoutSec: 20, enableSignalR: true, enableAudit: true };
        renderSettings();
        toast("Reset", "Settings reset.", "warn");
    });

    // simulation
    $("#btnSimLive").addEventListener("click", startSimulation);
    $("#btnClearSim").addEventListener("click", stopSimulation);

    // theme toggle hook
    $("#btnTheme").addEventListener("click", () => {
        document.body.classList.toggle("theme-alt");
        toast("Theme", "Theme toggled (demo).", "ok");
    });

    // sidebar (desktop collapse + mobile off-canvas)
    $("#btnMobileMenu").addEventListener("click", () => {
        if (!isNarrow()) return;
        const sb = $("#sidebar");
        if (!sb) return;

        if (!sb.classList.contains("is-open")) openSidebarMobile();
        else closeSidebarMobile();
    });

    $("#btnCollapse").addEventListener("click", () => {
        if (isNarrow()) return;
        toggleSidebarCollapsed();
    });

    $("#sidebarOverlay").addEventListener("click", closeSidebarMobile);

    window.addEventListener("resize", () => {
        if (isNarrow()) closeSidebarMobile();
        applySidebarCollapsedState();
    });


    // ctrl+k focus search
    document.addEventListener("keydown", (e) => {
        if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "k") {
            e.preventDefault();
            $("#globalSearch").focus();
        }
        if (e.key === "Escape") {
            closeModal();
            closeSidebarMobile();
        }
    });

    // global search: A1/A2 focus
    $("#globalSearch").addEventListener("input", () => {
        const q = $("#globalSearch").value.trim().toLowerCase();
        if (!q) return;
        const slot = state.slots.find(s => s.label.toLowerCase().includes(q));
        if (slot) { setRoute("live"); selectSlot(slot.id, { silent: true }); }
    });
}

function exportLogs() {
    const json = JSON.stringify(state.logs, null, 2);
    const blob = new Blob([json], { type: "application/json" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = "smartparking-logs-pilot.json";
    a.click();
    URL.revokeObjectURL(url);
    toast("Exported", "Logs exported as JSON.", "ok");
}

const SIDEBAR_COLLAPSE_KEY = "smartparking.sidebar.collapsed";

function isNarrow() {
    // Keep in sync with CSS @media (max-width: 980px)
    return window.matchMedia("(max-width: 980px)").matches;
}

function setBodyScrollLock(lock) {
    document.documentElement.style.overflow = lock ? "hidden" : "";
}

function openSidebarMobile() {
    if (!isNarrow()) return;
    $("#sidebar")?.classList.add("is-open");
    $("#sidebarOverlay")?.removeAttribute("hidden");
    setBodyScrollLock(true);
}

function closeSidebarMobile() {
    $("#sidebar")?.classList.remove("is-open");
    $("#sidebarOverlay")?.setAttribute("hidden", "");
    setBodyScrollLock(false);
}

function applySidebarCollapsedState() {
    const app = $("#app");
    if (!app) return;

    // Collapse only makes sense on desktop
    if (isNarrow()) {
        app.classList.remove("has-collapsed");
        return;
    }

    const collapsed = localStorage.getItem(SIDEBAR_COLLAPSE_KEY) === "1";
    app.classList.toggle("has-collapsed", collapsed);
}

function toggleSidebarCollapsed() {
    const app = $("#app");
    if (!app) return;

    const next = !app.classList.contains("has-collapsed");
    app.classList.toggle("has-collapsed", next);
    localStorage.setItem(SIDEBAR_COLLAPSE_KEY, next ? "1" : "0");
}
/* ---------------------------
   Dashboard wrapper
---------------------------- */
function renderDashboard() {
    renderKPIs();
    buildCharts();
    renderEvents();
    renderSlotsTable();
}

/* ---------------------------
   Boot
---------------------------- */
function boot() {
    seedPilot();
    wireEvents();
    setRoute("dashboard");
    toast("Ready", "Pilot Admin loaded: NODE-001 + 2 ultrasonic sensors.", "ok");
}


document.addEventListener("DOMContentLoaded", () => {
    closeModal();
    closeSidebarMobile();
    applySidebarCollapsedState();
    boot();
});
