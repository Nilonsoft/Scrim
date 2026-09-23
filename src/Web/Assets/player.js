// Global Indie Radio - Web Player Logic

document.addEventListener('DOMContentLoaded', function () {
    const audio = document.getElementById('audioElement');
    const playBtn = document.getElementById('playBtn');
    const playIcon = playBtn ? playBtn.querySelector('.icon-play') : null;
    const pauseIcon = playBtn ? playBtn.querySelector('.icon-pause') : null;
    const volumeSlider = document.getElementById('volumeSlider');
    const volumeBtn = document.getElementById('volumeBtn');
    const volOnIcon = volumeBtn ? volumeBtn.querySelector('.icon-vol-on') : null;
    const volMuteIcon = volumeBtn ? volumeBtn.querySelector('.icon-vol-mute') : null;
    const topSpeakerBtn = document.getElementById('topSpeakerBtn');
    const topVolOnIcon = topSpeakerBtn ? topSpeakerBtn.querySelector('.icon-top-vol-on') : null;
    const topVolMuteIcon = topSpeakerBtn ? topSpeakerBtn.querySelector('.icon-top-vol-mute') : null;
    const pwaInstallBtn = document.getElementById('pwaInstallBtn');
    const trackTitle = document.getElementById('trackTitle');
    const trackTitleLink = document.getElementById('trackTitleLink');
    const trackArtist = document.getElementById('trackArtist');
    const bottomTrackName = document.getElementById('bottomTrackName');
    const bottomTrackLink = document.getElementById('bottomTrackLink');
    const listenerCount = document.getElementById('listenerCount');
    const clockTime = document.getElementById('clockTime');
    const stationBannerWrap = document.getElementById('stationBannerWrap');
    const stationBannerImg = document.getElementById('stationBannerImg');
    const requestForm = document.getElementById('requestForm');
    const requestInput = document.getElementById('requestInput');
    const dedicationInput = document.getElementById('dedicationInput');
    const requestSuccess = document.getElementById('requestSuccess');
    const requestStatusPill = document.getElementById('requestStatusPill');
    const requestDisabledBanner = document.getElementById('requestDisabledBanner');
    const submitRequestBtn = document.getElementById('submitRequestBtn');
    const queueList = document.getElementById('queueList');

    // Song Reaction Elements
    const songReactionsBar = document.getElementById('songReactionsBar');
    const btnReactionThumbsUp = document.getElementById('btnReactionThumbsUp');
    const btnReactionLove = document.getElementById('btnReactionLove');
    const btnReactionThumbsDown = document.getElementById('btnReactionThumbsDown');
    const countThumbsUp = document.getElementById('countThumbsUp');
    const countHeart = document.getElementById('countHeart');
    const countThumbsDown = document.getElementById('countThumbsDown');

    // Recently Played History Elements
    const historyToggleBtn = document.getElementById('historyToggleBtn');
    const historyCard = document.getElementById('historyCard');
    const historyCountPill = document.getElementById('historyCountPill');
    const historyEmptyHint = document.getElementById('historyEmptyHint');
    const historyItemsList = document.getElementById('historyItemsList');
    const historyMessagesContainer = document.getElementById('historyMessagesContainer');

    let isPlaying = false;
    let isConnecting = false;
    let isUserPlaying = false;
    let defaultSubtitle = "Live Broadcast";

    let activeStationMount = '';
    let stationsList = [];

    const urlParams = new URLSearchParams(window.location.search);
    const initialStationParam = urlParams.get('station') || urlParams.get('mount') || '';
    if (initialStationParam) {
        activeStationMount = initialStationParam.replace(/^\/+/, '');
    }

    function getStationUrl(endpoint, extraParams) {
        let base = endpoint;
        const params = [];
        if (activeStationMount) {
            params.push('station=' + encodeURIComponent(activeStationMount));
        }
        if (extraParams) {
            params.push(extraParams);
        }
        if (params.length > 0) {
            base += (base.includes('?') ? '&' : '?') + params.join('&');
        }
        return base;
    }

    function updatePwaManifest(mount, stationName) {
        const link = document.getElementById('pwaManifestLink') || document.querySelector('link[rel="manifest"]');
        if (link) {
            const m = (mount || activeStationMount || '').replace(/^\/+/, '');
            link.href = m ? `/manifest.webmanifest?station=${encodeURIComponent(m)}` : '/manifest.webmanifest';
        }
        const appleTitle = document.getElementById('appleAppTitle');
        if (appleTitle && stationName) {
            appleTitle.content = stationName;
        }
    }
    if (activeStationMount) {
        updatePwaManifest(activeStationMount);
    }

    // Live Clock (User's Local Time)
    function updateLiveClock() {
        if (!clockTime) return;
        const now = new Date();
        clockTime.textContent = now.toLocaleTimeString([], {
            hour: 'numeric',
            minute: '2-digit',
            second: '2-digit'
        });
        if (clockTime.parentElement) {
            clockTime.parentElement.title = now.toLocaleDateString([], {
                weekday: 'long',
                year: 'numeric',
                month: 'long',
                day: 'numeric'
            });
        }
    }
    updateLiveClock();
    setInterval(updateLiveClock, 1000);

    function updatePlayButtonUI() {
        if (!playBtn) return;
        if (topSpeakerBtn) {
            if (isPlaying) {
                topSpeakerBtn.classList.add('playing');
            } else {
                topSpeakerBtn.classList.remove('playing');
            }
        }
        if (isPlaying) {
            if (playIcon) playIcon.style.display = 'none';
            if (pauseIcon) pauseIcon.style.display = 'block';
            playBtn.classList.add('playing');
            playBtn.classList.remove('connecting');
            playBtn.title = "Stop Stream (Local)";
        } else if (isConnecting) {
            if (playIcon) playIcon.style.display = 'block';
            if (pauseIcon) pauseIcon.style.display = 'none';
            playBtn.classList.remove('playing');
            playBtn.classList.add('connecting');
            playBtn.title = "Connecting...";
        } else {
            if (playIcon) playIcon.style.display = 'block';
            if (pauseIcon) pauseIcon.style.display = 'none';
            playBtn.classList.remove('playing');
            playBtn.classList.remove('connecting');
            playBtn.title = "Play Live Stream";
        }
    }

    // Volume & Web Audio Graph
    let currentVolume = 0.85;
    try {
        const saved = localStorage.getItem('scrim_volume');
        if (saved !== null) {
            const parsed = parseFloat(saved);
            if (!isNaN(parsed) && parsed >= 0 && parsed <= 1) currentVolume = parsed;
        }
    } catch (e) {}

    let lastUnmutedVolume = currentVolume > 0.05 ? currentVolume : 0.85;

    let audioCtx = null;
    let analyser = null;
    let gainNode = null;
    let sourceNode = null;
    let freqData = null;
    let timeData = null;

    const canvas = document.getElementById('visualizerCanvas');
    const ctx = canvas ? canvas.getContext('2d') : null;
    let modeButtons = document.querySelectorAll('.vis-mode-btn');
    let userSelectedVisMode = localStorage.getItem('scrim_vis_mode');
    let currentVisMode = userSelectedVisMode || 'bars';
    let baseModeMap = { 'bars': 'bars', 'wave': 'wave', 'spectrum': 'spectrum', 'pulse': 'pulse' };

    function setVisMode(mode) {
        currentVisMode = mode;
        try {
            localStorage.setItem('scrim_vis_mode', mode);
        } catch (e) {}
        const btns = document.querySelectorAll('.vis-mode-btn');
        btns.forEach(btn => {
            if (btn.dataset.mode === mode) {
                btn.classList.add('active');
            } else {
                btn.classList.remove('active');
            }
        });
    }

    modeButtons.forEach(btn => {
        btn.addEventListener('click', function () {
            userSelectedVisMode = this.dataset.mode;
            setVisMode(this.dataset.mode);
        });
    });
    setVisMode(currentVisMode);

    function updateVisualizerReactors(reactors, defaultMode) {
        const group = document.getElementById('visModeGroup');
        if (!group || !Array.isArray(reactors) || reactors.length === 0) return;

        const enabled = reactors.filter(r => r.isEnabled !== false);
        if (enabled.length === 0) return;

        baseModeMap = {};
        enabled.forEach(r => {
            baseModeMap[r.id] = r.baseMode || 'bars';
        });

        // If user hasn't explicitly picked a mode stored in localStorage, use defaultMode if available
        if (!userSelectedVisMode && defaultMode && enabled.some(r => r.id === defaultMode)) {
            currentVisMode = defaultMode;
        } else if (!enabled.some(r => r.id === currentVisMode)) {
            const def = enabled.find(r => r.id === defaultMode) || enabled[0];
            currentVisMode = def.id;
        }

        group.innerHTML = '';
        enabled.forEach(r => {
            const btn = document.createElement('button');
            btn.className = 'vis-mode-btn' + (r.id === currentVisMode ? ' active' : '');
            btn.dataset.mode = r.id;
            const emojiStr = r.emoji ? r.emoji + ' ' : '';
            btn.textContent = `${emojiStr}${r.label || r.id}`;
            btn.title = `${r.label || r.id} (${r.baseMode || 'visualizer'})`;
            btn.addEventListener('click', function () {
                userSelectedVisMode = r.id;
                setVisMode(r.id);
            });
            group.appendChild(btn);
        });

        setVisMode(currentVisMode);
    }

    function initAudioVisualizer() {
        if (sourceNode || !audio) return;
        try {
            const AudioContextClass = window.AudioContext || window.webkitAudioContext;
            if (!AudioContextClass) return;
            if (!audioCtx) {
                audioCtx = new AudioContextClass();
            }
            if (audioCtx.state === 'suspended') {
                audioCtx.resume();
            }
            if (!analyser) {
                analyser = audioCtx.createAnalyser();
                analyser.fftSize = 256;
                analyser.smoothingTimeConstant = 0.82;
                analyser.minDecibels = -90;
                analyser.maxDecibels = -10;
                freqData = new Uint8Array(analyser.frequencyBinCount);
                timeData = new Uint8Array(analyser.fftSize);
            }
            if (!gainNode) {
                gainNode = audioCtx.createGain();
                gainNode.gain.setValueAtTime(currentVolume, audioCtx.currentTime);
                gainNode.connect(audioCtx.destination);
            }
            sourceNode = audioCtx.createMediaElementSource(audio);
            sourceNode.connect(analyser);
            analyser.connect(gainNode);
        } catch (e) {
            console.warn("AudioContext visualizer/gain initialization:", e);
        }
    }

    function setVolume(val) {
        currentVolume = Math.max(0, Math.min(1, parseFloat(val)));
        if (volumeSlider) {
            volumeSlider.value = currentVolume;
        }
        if (audio) {
            audio.volume = currentVolume;
        }
        if (gainNode && audioCtx) {
            gainNode.gain.setValueAtTime(currentVolume, audioCtx.currentTime);
        }
        updateVolumeUI();
        try {
            localStorage.setItem('scrim_volume', currentVolume.toString());
        } catch (e) {}
    }

    function updateVolumeUI() {
        const isMuted = currentVolume <= 0.001;
        if (volOnIcon) volOnIcon.style.display = isMuted ? 'none' : 'block';
        if (volMuteIcon) volMuteIcon.style.display = isMuted ? 'block' : 'none';

        if (topVolOnIcon) topVolOnIcon.style.display = isMuted ? 'none' : 'block';
        if (topVolMuteIcon) topVolMuteIcon.style.display = isMuted ? 'block' : 'none';

        if (topSpeakerBtn) {
            if (isMuted) {
                topSpeakerBtn.classList.add('muted');
                topSpeakerBtn.title = "Unmute Audio (M)";
            } else {
                topSpeakerBtn.classList.remove('muted');
                topSpeakerBtn.title = "Mute Audio (M)";
            }
        }
    }

    if (volumeSlider) {
        volumeSlider.value = currentVolume;
        volumeSlider.addEventListener('input', function (e) {
            const val = parseFloat(e.target.value);
            if (val > 0) lastUnmutedVolume = val;
            setVolume(val);
        });
    }

    if (volumeBtn) {
        volumeBtn.addEventListener('click', function () {
            if (currentVolume > 0.001) {
                lastUnmutedVolume = currentVolume;
                setVolume(0);
            } else {
                setVolume(lastUnmutedVolume || 0.85);
            }
        });
    }

    if (topSpeakerBtn) {
        topSpeakerBtn.addEventListener('click', function () {
            if (currentVolume > 0.001) {
                lastUnmutedVolume = currentVolume;
                setVolume(0);
            } else {
                setVolume(lastUnmutedVolume || 0.85);
            }
        });
    }

    // Keyboard shortcut 'M' to quickly mute/unmute audio from anywhere on the page
    document.addEventListener('keydown', function (e) {
        if (e.key === 'm' || e.key === 'M') {
            if (document.activeElement && (document.activeElement.tagName === 'INPUT' || document.activeElement.tagName === 'TEXTAREA')) {
                return;
            }
            if (currentVolume > 0.001) {
                lastUnmutedVolume = currentVolume;
                setVolume(0);
            } else {
                setVolume(lastUnmutedVolume || 0.85);
            }
        }
    });

    updateVolumeUI();

    // Multi-Mode Audio Visualizer Engine
    const BAR_COUNT = 28;
    const peakCaps = new Float32Array(BAR_COUNT);
    const barValues = new Float32Array(BAR_COUNT);
    let idleAngle = 0;

    function syncCanvasDimensions() {
        if (!canvas) return { w: 0, h: 0, dpr: 1 };
        const dpr = window.devicePixelRatio || 1;
        const rect = canvas.getBoundingClientRect();
        const displayW = Math.round(rect.width * dpr);
        const displayH = Math.round(rect.height * dpr);
        if (canvas.width !== displayW || canvas.height !== displayH) {
            canvas.width = displayW;
            canvas.height = displayH;
        }
        return { w: canvas.width, h: canvas.height, dpr: dpr };
    }

    function renderBarsMode(ctx, w, h, dpr, accent, hasData) {
        const spacing = 3 * dpr;
        const totalSpacing = spacing * (BAR_COUNT - 1);
        const barWidth = Math.max(2, (w - totalSpacing) / BAR_COUNT);

        for (let i = 0; i < BAR_COUNT; i++) {
            let target = 0.04;
            if (hasData && freqData) {
                // Logarithmic frequency bin spread across 128 bins
                const binIndex = Math.min(
                    Math.floor(Math.pow(i / (BAR_COUNT - 1), 1.6) * (freqData.length - 2)) + 1,
                    freqData.length - 1
                );
                const raw = freqData[binIndex] || 0;
                // Frequency weighting: scale down booming sub-bass so it doesn't max out, boost highs
                const weight = i < 4 ? 0.70 : (i < 14 ? 0.95 : 1.35);
                target = Math.min(0.90, Math.max(0.04, (raw / 255.0) * weight * 0.82));
            } else {
                // Calm idle sine bounce
                target = 0.05 + Math.sin(idleAngle + i * 0.28) * 0.035;
            }

            barValues[i] += (target - barValues[i]) * 0.35;
            if (barValues[i] > peakCaps[i]) {
                peakCaps[i] = barValues[i];
            } else {
                peakCaps[i] = Math.max(0.04, peakCaps[i] - 0.012);
            }

            const barH = Math.max(3 * dpr, barValues[i] * (h - 8 * dpr));
            const x = i * (barWidth + spacing);
            const y = h - barH;

            // Gradient bar fill
            const grad = ctx.createLinearGradient(0, y, 0, h);
            grad.addColorStop(0, '#ffffff');
            grad.addColorStop(0.25, accent);
            grad.addColorStop(1, 'rgba(0, 0, 0, 0.4)');

            ctx.fillStyle = grad;
            ctx.beginPath();
            ctx.roundRect(x, y, barWidth, barH, [2 * dpr, 2 * dpr, 0, 0]);
            ctx.fill();

            // Floating peak cap dot
            const peakY = Math.max(2 * dpr, h - peakCaps[i] * (h - 8 * dpr) - 2 * dpr);
            ctx.fillStyle = hasData ? '#ffffff' : 'rgba(255, 255, 255, 0.4)';
            ctx.fillRect(x, peakY, barWidth, 2 * dpr);
        }
    }

    function renderWaveMode(ctx, w, h, dpr, accent, hasData) {
        const centerY = h / 2;

        // Draw center baseline
        ctx.strokeStyle = 'rgba(255, 255, 255, 0.08)';
        ctx.lineWidth = 1 * dpr;
        ctx.beginPath();
        ctx.moveTo(0, centerY);
        ctx.lineTo(w, centerY);
        ctx.stroke();

        ctx.beginPath();
        if (hasData && timeData) {
            const step = Math.max(1, Math.floor(timeData.length / w));
            const pointsCount = Math.floor(timeData.length / step);
            for (let i = 0; i < pointsCount; i++) {
                const x = (i / (pointsCount - 1)) * w;
                const v = (timeData[i * step] - 128) / 128.0;
                // Leave headroom (0.42) so wave never clips the canvas borders
                const y = centerY + v * (h * 0.42);
                if (i === 0) {
                    ctx.moveTo(x, y);
                } else {
                    ctx.lineTo(x, y);
                }
            }
        } else {
            // Idle ambient oscilloscope wave
            const points = 80;
            for (let i = 0; i <= points; i++) {
                const x = (i / points) * w;
                const y = centerY + Math.sin(idleAngle * 0.8 + (i / points) * Math.PI * 4) * (h * 0.14);
                if (i === 0) {
                    ctx.moveTo(x, y);
                } else {
                    ctx.lineTo(x, y);
                }
            }
        }

        // Radiant glow pass
        ctx.save();
        ctx.shadowBlur = 12 * dpr;
        ctx.shadowColor = accent;
        ctx.strokeStyle = accent;
        ctx.lineWidth = 2.5 * dpr;
        ctx.stroke();
        ctx.restore();

        // Crisp inner core stroke
        ctx.strokeStyle = '#ffffff';
        ctx.lineWidth = 1.2 * dpr;
        ctx.stroke();
    }

    function renderSpectrumMode(ctx, w, h, dpr, accent, hasData) {
        const points = 48;
        const pts = [];

        for (let i = 0; i < points; i++) {
            const x = (i / (points - 1)) * w;
            let val = 0.05;
            if (hasData && freqData) {
                const binIndex = Math.min(
                    Math.floor(Math.pow(i / (points - 1), 1.5) * (freqData.length - 2)) + 1,
                    freqData.length - 1
                );
                const raw = freqData[binIndex] || 0;
                const weight = i < 6 ? 0.72 : (i < 24 ? 0.95 : 1.30);
                val = Math.min(0.88, Math.max(0.04, (raw / 255.0) * weight * 0.80));
            } else {
                val = 0.06 + Math.sin(idleAngle + i * 0.22) * 0.035;
            }
            const y = h - val * (h - 6 * dpr);
            pts.push({ x: x, y: y });
        }

        // Fill area under the curve
        const grad = ctx.createLinearGradient(0, 0, 0, h);
        grad.addColorStop(0, accent + 'b3'); // ~70% opacity
        grad.addColorStop(0.6, accent + '33'); // ~20% opacity
        grad.addColorStop(1, 'rgba(0, 0, 0, 0)');

        ctx.beginPath();
        ctx.moveTo(0, h);
        for (let i = 0; i < pts.length; i++) {
            ctx.lineTo(pts[i].x, pts[i].y);
        }
        ctx.lineTo(w, h);
        ctx.closePath();
        ctx.fillStyle = grad;
        ctx.fill();

        // Glowing contour line
        ctx.save();
        ctx.shadowBlur = 10 * dpr;
        ctx.shadowColor = accent;
        ctx.strokeStyle = '#ffffff';
        ctx.lineWidth = 2 * dpr;
        ctx.beginPath();
        for (let i = 0; i < pts.length; i++) {
            if (i === 0) {
                ctx.moveTo(pts[i].x, pts[i].y);
            } else {
                ctx.lineTo(pts[i].x, pts[i].y);
            }
        }
        ctx.stroke();
        ctx.restore();
    }

    function renderPulseMode(ctx, w, h, dpr, accent, hasData) {
        const cx = w / 2;
        const cy = h / 2;
        let bassNorm = 0.05;

        if (hasData && freqData) {
            let sum = 0;
            for (let i = 1; i <= 6; i++) {
                sum += freqData[i];
            }
            bassNorm = Math.min(0.88, Math.max(0.05, (sum / (6 * 255.0)) * 0.85));
        } else {
            bassNorm = 0.06 + Math.sin(idleAngle * 1.2) * 0.03;
        }

        const maxR = Math.min(cx, cy) * 0.95;
        const baseR = maxR * 0.35 + bassNorm * (maxR * 0.60);

        // Outer ripple ring
        ctx.save();
        ctx.shadowBlur = 14 * dpr;
        ctx.shadowColor = accent;
        ctx.strokeStyle = accent + '66';
        ctx.lineWidth = 1.5 * dpr;
        ctx.beginPath();
        ctx.arc(cx, cy, Math.min(maxR, baseR * 1.35), 0, Math.PI * 2);
        ctx.stroke();

        // Mid pulse ring
        ctx.strokeStyle = accent;
        ctx.lineWidth = 2 * dpr;
        ctx.beginPath();
        ctx.arc(cx, cy, baseR, 0, Math.PI * 2);
        ctx.stroke();

        // Center glowing core
        const coreGrad = ctx.createRadialGradient(cx, cy, 0, cx, cy, baseR * 0.7);
        coreGrad.addColorStop(0, '#ffffff');
        coreGrad.addColorStop(0.5, accent);
        coreGrad.addColorStop(1, 'rgba(0, 0, 0, 0)');
        ctx.fillStyle = coreGrad;
        ctx.beginPath();
        ctx.arc(cx, cy, baseR * 0.7, 0, Math.PI * 2);
        ctx.fill();
        ctx.restore();

        // Horizontal sound rays
        const rayCount = 18;
        const rayWidth = (cx - maxR * 0.6) / rayCount;
        for (let i = 0; i < rayCount; i++) {
            const raw = (hasData && freqData) ? (freqData[i * 2 + 6] || 0) / 255.0 : 0.1;
            const rayH = Math.max(2 * dpr, raw * (h * 0.35));
            const leftX = cx - maxR * 0.55 - (i + 1) * rayWidth;
            const rightX = cx + maxR * 0.55 + i * rayWidth;

            ctx.fillStyle = accent + '80';
            ctx.fillRect(leftX, cy - rayH / 2, rayWidth * 0.65, rayH);
            ctx.fillRect(rightX, cy - rayH / 2, rayWidth * 0.65, rayH);
        }
    }

    function animateWaveform() {
        requestAnimationFrame(animateWaveform);
        if (!canvas || !ctx) return;

        const dims = syncCanvasDimensions();
        const w = dims.w;
        const h = dims.h;
        if (w === 0 || h === 0) return;

        ctx.clearRect(0, 0, w, h);

        const accent = getComputedStyle(document.documentElement).getPropertyValue('--accent-color').trim() || '#00d2ff';

        let hasData = false;
        if (isPlaying && analyser && freqData && timeData) {
            analyser.getByteFrequencyData(freqData);
            analyser.getByteTimeDomainData(timeData);

            let sum = 0;
            for (let i = 2; i < 35; i++) {
                sum += freqData[i];
            }
            if (sum > 8) {
                hasData = true;
            }
        }

        idleAngle += 0.035;

        const baseMode = baseModeMap[currentVisMode] || currentVisMode;
        if (baseMode === 'bars') {
            renderBarsMode(ctx, w, h, dims.dpr, accent, hasData);
        } else if (baseMode === 'wave') {
            renderWaveMode(ctx, w, h, dims.dpr, accent, hasData);
        } else if (baseMode === 'spectrum') {
            renderSpectrumMode(ctx, w, h, dims.dpr, accent, hasData);
        } else if (baseMode === 'pulse') {
            renderPulseMode(ctx, w, h, dims.dpr, accent, hasData);
        }
    }

    animateWaveform();

    // Live Stream Play / Stop Controls (Strictly for Web User)
    let userExplicitlyStopped = false;
    let autoplayUnlocked = false;
    let pendingLiveAutoStart = false;

    function unlockAutoplay() {
        if (autoplayUnlocked) {
            return;
        }
        autoplayUnlocked = true;
        document.removeEventListener('pointerdown', unlockAutoplay, true);
        document.removeEventListener('click', unlockAutoplay, true);
        document.removeEventListener('keydown', unlockAutoplay, true);
        document.removeEventListener('touchstart', unlockAutoplay, true);
        if (!userExplicitlyStopped && !isPlaying && !isConnecting) {
            startStream(false);
        }
    }

    let reconnectTimeout = null;
    let reconnectAttempts = 0;

    function scheduleStreamReconnect(delayMs) {
        if (userExplicitlyStopped || !isUserPlaying) return;
        if (reconnectTimeout) {
            clearTimeout(reconnectTimeout);
            reconnectTimeout = null;
        }

        const delay = (delayMs !== undefined)
            ? delayMs
            : Math.min(1500 * Math.pow(1.5, Math.min(reconnectAttempts, 5)), 6000);

        reconnectTimeout = setTimeout(function () {
            reconnectTimeout = null;
            if (userExplicitlyStopped || !isUserPlaying) return;

            // Probe server status first to avoid unhandled media decode aborts on 503 or refused socket
            fetch(getStationUrl('/api/status'), { cache: 'no-store' })
                .then(function (res) {
                    if (!res.ok) throw new Error("HTTP " + res.status);
                    return res.json();
                })
                .then(function (data) {
                    if (userExplicitlyStopped || !isUserPlaying) return;
                    handleStatusData(data);
                    if (data.isLive !== false) {
                        console.log("[Audio] Station is live, reconnecting stream...");
                        reconnectAttempts = 0;
                        startStream(true);
                    } else {
                        console.log("[Audio] Server online, waiting for broadcaster transmission...");
                        isConnecting = true;
                        isPlaying = false;
                        updatePlayButtonUI();
                        const subtitleEl = document.getElementById('showSubtitle');
                        if (subtitleEl) subtitleEl.textContent = "Station Standby — Waiting for broadcaster...";
                        scheduleStreamReconnect(2500);
                    }
                })
                .catch(function (err) {
                    console.log("[Audio] Server rebooting or unreachable, retrying...", err);
                    reconnectAttempts++;
                    isConnecting = true;
                    isPlaying = false;
                    updatePlayButtonUI();
                    const subtitleEl = document.getElementById('showSubtitle');
                    if (subtitleEl) subtitleEl.textContent = "Server restarting — auto-reconnecting...";
                    scheduleStreamReconnect();
                });
        }, delay);
    }

    function checkAutoplay(isLive) {
        if (!isLive || userExplicitlyStopped) {
            return;
        }
        if (!isPlaying && !isConnecting) {
            startStream(true);
        }
    }

    function attemptAutoplay() {
        if (userExplicitlyStopped || isPlaying || isConnecting) {
            return;
        }
        startStream(true);
    }

    function stopStream() {
        isUserPlaying = false;
        isPlaying = false;
        isConnecting = false;
        reconnectAttempts = 0;
        if (reconnectTimeout) {
            clearTimeout(reconnectTimeout);
            reconnectTimeout = null;
        }
        updatePlayButtonUI();

        if (audio) {
            audio.pause();
            audio.removeAttribute('src'); // Stop streaming connection immediately
            audio.load(); // Tell browser to drop the pending HTTP connection
        }
    }

    let currentStreamEndpoint = activeStationMount ? ('/' + activeStationMount) : '/stream';

    function setPrivateStreamMode(isPrivate) {
        const overlay = document.getElementById('privateStreamOverlay');
        if (!overlay) return;
        if (isPrivate) {
            overlay.style.display = 'flex';
            userExplicitlyStopped = true;
            stopStream();
        } else {
            overlay.style.display = 'none';
        }
    }
    window.setPrivateStreamMode = setPrivateStreamMode;
    window.getCurrentStreamEndpoint = () => currentStreamEndpoint;

    function startStream(isAutoplay) {
        isUserPlaying = true;
        isConnecting = true;
        updatePlayButtonUI();

        initAudioVisualizer();
        if (audioCtx && audioCtx.state === 'suspended') {
            audioCtx.resume();
        }

        const subtitleEl = document.getElementById('showSubtitle');
        if (subtitleEl) {
            subtitleEl.textContent = "Connecting to live audio stream...";
        }

        // Always load a fresh live connection with timestamp cache-buster so stream is real-time
        audio.src = currentStreamEndpoint + (currentStreamEndpoint.includes('?') ? '&' : '?') + 't=' + Date.now();
        audio.load();

        var playPromise = audio.play();
        if (playPromise !== undefined) {
            playPromise.then(function () {
                if (isUserPlaying) {
                    isPlaying = true;
                    isConnecting = false;
                    reconnectAttempts = 0;
                    if (reconnectTimeout) {
                        clearTimeout(reconnectTimeout);
                        reconnectTimeout = null;
                    }
                    updatePlayButtonUI();
                    if (subtitleEl) {
                        subtitleEl.textContent = defaultSubtitle;
                    }
                }
            }).catch(function (err) {
                console.warn("Playback could not start:", err);
                if (userExplicitlyStopped) {
                    return;
                }
                if (err.name === 'NotAllowedError') {
                    isPlaying = false;
                    isConnecting = false;
                    updatePlayButtonUI();
                    audio.removeAttribute('src');
                    if (subtitleEl) {
                        subtitleEl.textContent = "Click anywhere to listen live";
                    }
                    document.addEventListener('pointerdown', unlockAutoplay, true);
                    document.addEventListener('click', unlockAutoplay, true);
                    document.addEventListener('keydown', unlockAutoplay, true);
                    document.addEventListener('touchstart', unlockAutoplay, true);
                    return;
                }

                // Temporary server reboot, 503, or decoding glitch: keep user playing intent and auto-retry
                isPlaying = false;
                isConnecting = true;
                updatePlayButtonUI();
                if (subtitleEl) {
                    subtitleEl.textContent = "Broadcaster offline / rebooting — auto-reconnecting...";
                }
                scheduleStreamReconnect();
            });
        }
    }

    if (playBtn && audio) {
        playBtn.addEventListener('click', function () {
            if (isUserPlaying || isPlaying || isConnecting) {
                userExplicitlyStopped = true;
                pendingLiveAutoStart = false;
                stopStream();
                const subtitleEl = document.getElementById('showSubtitle');
                if (subtitleEl) {
                    subtitleEl.textContent = defaultSubtitle;
                }
            } else {
                userExplicitlyStopped = false;
                reconnectAttempts = 0;
                startStream(false);
            }
        });

        audio.addEventListener('playing', function () {
            if (reconnectTimeout) {
                clearTimeout(reconnectTimeout);
                reconnectTimeout = null;
            }
            if (isUserPlaying) {
                isPlaying = true;
                isConnecting = false;
                reconnectAttempts = 0;
                updatePlayButtonUI();
                const subtitleEl = document.getElementById('showSubtitle');
                if (subtitleEl) subtitleEl.textContent = defaultSubtitle;
            }
        });

        audio.addEventListener('pause', function () {
            if (!isUserPlaying) {
                isPlaying = false;
                isConnecting = false;
                updatePlayButtonUI();
            }
        });

        audio.addEventListener('waiting', function () {
            if (isUserPlaying) {
                isConnecting = true;
                updatePlayButtonUI();
            }
        });

        audio.addEventListener('canplay', function () {
            if (isUserPlaying) {
                isConnecting = false;
                updatePlayButtonUI();
            }
        });

        audio.addEventListener('ended', function () {
            if (isUserPlaying && !userExplicitlyStopped) {
                isPlaying = false;
                isConnecting = true;
                updatePlayButtonUI();
                const subtitleEl = document.getElementById('showSubtitle');
                if (subtitleEl) subtitleEl.textContent = "Broadcast lapsed — auto-reconnecting...";
                scheduleStreamReconnect();
            }
        });

        audio.addEventListener('stalled', function () {
            if (isUserPlaying && !userExplicitlyStopped && !isPlaying) {
                scheduleStreamReconnect(2000);
            }
        });

        audio.addEventListener('error', function (e) {
            // Ignore error events triggered when user intentionally stopped or src is cleared
            if (userExplicitlyStopped || !isUserPlaying || !audio.getAttribute('src')) {
                return;
            }
            console.warn("Audio element stream error or station offline:", e);
            isPlaying = false;
            isConnecting = true;
            updatePlayButtonUI();
            const subtitleEl = document.getElementById('showSubtitle');
            if (subtitleEl) {
                subtitleEl.textContent = "Broadcast interrupted — waiting for DJ to resume...";
            }
            scheduleStreamReconnect();
        });
    }

    let currentBranding = null;
    let lastAlbumArtUrl = null;

    // Dynamic Branding Function
    function applyBranding(branding) {
        if (!branding) return;
        currentBranding = branding;

        if (branding.streamUrl) {
            currentStreamEndpoint = branding.streamUrl;
        }

        if (branding.isPrivate !== undefined) {
            setPrivateStreamMode(branding.isPrivate);
        }

        // Apply or clear dynamic ambient backdrop based on owner setting
        if (branding.enableDynamicBackdrop) {
            let artUrl = lastAlbumArtUrl;
            if (!artUrl) {
                const artEl = document.getElementById('albumArt');
                if (artEl && artEl.style.backgroundImage) {
                    const match = artEl.style.backgroundImage.match(/url\(['"]?([^'"]+)['"]?\)/);
                    if (match && match[1] && !match[1].includes('linear-gradient')) {
                        artUrl = match[1];
                    }
                }
            }
            if (!artUrl) {
                artUrl = '/api/albumart?t=' + Date.now();
            }
            lastAlbumArtUrl = artUrl;
            applyDynamicAmbientBackdrop(artUrl);
        } else {
            document.body.classList.remove('has-ambient-backdrop');
            document.body.style.removeProperty('--ambient-c1');
            document.body.style.removeProperty('--ambient-c2');
            document.documentElement.style.removeProperty('--ambient-c1');
            document.documentElement.style.removeProperty('--ambient-c2');
        }

        updateStationModalContent(branding);
        updatePartyHubBranding(branding);

        if (branding.enableSongRequests !== undefined) {
            updateSongRequestsEnabled(branding.enableSongRequests);
        }

        if (branding.theme) {
            document.documentElement.setAttribute('data-theme', branding.theme);
        }

        if (branding.customThemeVariables && typeof branding.customThemeVariables === 'object') {
            for (const [prop, val] of Object.entries(branding.customThemeVariables)) {
                if (prop && val) {
                    document.documentElement.style.setProperty(prop, val);
                }
            }
        }

        if (branding.pageTitle) {
            document.title = branding.pageTitle;
            const titleEl = document.getElementById('webTitle');
            if (titleEl) titleEl.textContent = branding.pageTitle;
        }

        if (branding.stationName) {
            const brandEl = document.getElementById('stationBrand');
            if (brandEl) brandEl.textContent = branding.stationName;
            const albumSub = document.querySelector('.album-sub');
            if (albumSub) albumSub.textContent = branding.stationName;
        }

        if (branding.showTitle || branding.hostName) {
            const subtitleEl = document.getElementById('showSubtitle');
            if (subtitleEl) {
                const hostPart = branding.hostName ? ` (Host: ${branding.hostName})` : '';
                subtitleEl.textContent = `${branding.showTitle || 'Live Broadcast'}${hostPart}`;
            }
        }

        if (branding.genreTag) {
            const genreEl = document.getElementById('trackGenre');
            if (genreEl) genreEl.textContent = branding.genreTag;
        }

        if (branding.accentColor) {
            const hex = branding.accentColor.trim();
            if (/^#([0-9a-fA-F]{3}){1,2}$/.test(hex)) {
                let r, g, b;
                if (hex.length === 4) {
                    r = parseInt(hex[1] + hex[1], 16);
                    g = parseInt(hex[2] + hex[2], 16);
                    b = parseInt(hex[3] + hex[3], 16);
                } else {
                    r = parseInt(hex.substring(1, 3), 16);
                    g = parseInt(hex.substring(3, 5), 16);
                    b = parseInt(hex.substring(5, 7), 16);
                }
                const luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255;
                const contrast = luminance > 0.6 ? '#0b0d14' : '#ffffff';

                document.documentElement.style.setProperty('--accent-color', hex);
                document.documentElement.style.setProperty('--accent-contrast', contrast);
                document.documentElement.style.setProperty('--accent-glow', `0 0 20px rgba(${r}, ${g}, ${b}, 0.55)`);
                document.documentElement.style.setProperty('--accent-glow-subtle', `0 2px 8px rgba(${r}, ${g}, ${b}, 0.35)`);
                document.documentElement.style.setProperty('--on-air-red', hex);
                document.documentElement.style.setProperty('--on-air-glow', `0 0 20px rgba(${r}, ${g}, ${b}, 0.55)`);
            }
        }

        // Top Station Banner
        if (stationBannerWrap && stationBannerImg) {
            if (branding.bannerUrl && branding.bannerUrl.trim()) {
                stationBannerImg.src = branding.bannerUrl.trim();
                stationBannerWrap.style.display = 'block';
                stationBannerImg.onerror = function () {
                    stationBannerWrap.style.display = 'none';
                };
            } else {
                stationBannerWrap.style.display = 'none';
                stationBannerImg.src = '';
            }
        }

        const navContainer = document.getElementById('navLinksContainer');
        if (navContainer) {
            navContainer.innerHTML = '';
            if (branding.customNavLinks && Array.isArray(branding.customNavLinks)) {
                if (branding.customNavLinks.length > 0) {
                    branding.customNavLinks.forEach(link => {
                        const a = document.createElement('a');
                        a.href = link.url || '#';
                        const urlLower = (link.url || '').toLowerCase();
                        const labelLower = (link.label || '').toLowerCase();
                        if (urlLower === '#schedule' || urlLower === '#about' || labelLower === 'schedule' || labelLower === 'about') {
                            a.addEventListener('click', function (e) {
                                e.preventDefault();
                                toggleStationModal(true);
                            });
                        } else if (link.url && (link.url.startsWith('http://') || link.url.startsWith('https://'))) {
                            a.target = '_blank';
                            a.rel = 'noopener noreferrer';
                        }
                        a.className = 'nav-link';
                        a.textContent = link.label;
                        navContainer.appendChild(a);
                    });
                }
                // If customNavLinks is empty array, it means broadcaster removed all links - leave navContainer empty
            } else if (branding.navLinks) {
                const links = branding.navLinks.split(',').map(s => s.trim()).filter(Boolean);
                links.forEach(link => {
                    const a = document.createElement('a');
                    const url = '#' + link.toLowerCase().replace(/\s+/g, '-');
                    a.href = url;
                    const urlLower = url.toLowerCase();
                    const labelLower = link.toLowerCase();
                    if (urlLower === '#schedule' || urlLower === '#about' || labelLower === 'schedule' || labelLower === 'about') {
                        a.addEventListener('click', function (e) {
                            e.preventDefault();
                            toggleStationModal(true);
                        });
                    }
                    a.className = 'nav-link';
                    a.textContent = link;
                    navContainer.appendChild(a);
                });
            }
        }

        if (branding.visualizerReactors && Array.isArray(branding.visualizerReactors)) {
            updateVisualizerReactors(branding.visualizerReactors, branding.defaultVisualizerMode);
        }
    }

    function updateLiveIndicator(isLive) {
        const onAirPill = document.getElementById('onAirPill');
        const subtitleEl = document.getElementById('showSubtitle');
        if (!onAirPill) return;
        const pillText = onAirPill.querySelector('.pill-text');
        if (isLive) {
            onAirPill.classList.remove('offline');
            if (pillText) pillText.textContent = 'ON AIR';
            if (subtitleEl && (subtitleEl.textContent.includes("Station") || subtitleEl.textContent.includes("Broadcaster"))) {
                subtitleEl.textContent = defaultSubtitle;
            }
        } else {
            onAirPill.classList.add('offline');
            if (pillText) pillText.textContent = 'STANDBY';
            if (subtitleEl && !isPlaying && !isConnecting) {
                subtitleEl.textContent = "Station Standby — Broadcaster not live";
            }
        }
    }

    let lastQualityKey = '';
    function updateStreamQuality(format, bitrate) {
        const streamQualityPill = document.getElementById('streamQualityPill');
        const streamFormatEl = document.getElementById('streamFormat');
        const streamBitrateEl = document.getElementById('streamBitrate');
        if (!streamFormatEl || !streamBitrateEl) return;

        const fmt = (format || 'MP3').toString().toUpperCase();
        let brText = '';
        if (fmt === 'FLAC' || !bitrate || bitrate === 0) {
            brText = 'LOSSLESS';
        } else {
            brText = `${bitrate} kbps`;
        }

        const currentKey = `${fmt}-${brText}`;
        const hasChanged = lastQualityKey && lastQualityKey !== currentKey;
        lastQualityKey = currentKey;

        streamFormatEl.textContent = fmt;
        streamBitrateEl.textContent = brText;

        if (streamQualityPill && hasChanged) {
            streamQualityPill.classList.remove('updated');
            void streamQualityPill.offsetWidth;
            streamQualityPill.classList.add('updated');
            setTimeout(function () {
                streamQualityPill.classList.remove('updated');
            }, 1200);
        }
    }

    function handleStatusData(data) {
        if (!data) return;
        if (data.streamUrl) {
            currentStreamEndpoint = data.streamUrl;
        }
        if (data.isPrivate !== undefined) {
            setPrivateStreamMode(data.isPrivate);
        }
        if (data.isLive !== undefined) {
            updateLiveIndicator(data.isLive);
            if (data.isLive) {
                checkAutoplay(true);
            }
        }
        if (data.format !== undefined) {
            updateStreamQuality(data.format, data.bitrate);
        }
    }

    // Initial branding and status load
    fetch(getStationUrl('/api/branding'))
        .then(res => res.json())
        .then(applyBranding)
        .catch(e => console.warn("Could not fetch branding", e));

    fetch(getStationUrl('/api/status'))
        .then(res => res.json())
        .then(handleStatusData)
        .catch(e => console.warn("Could not fetch status", e));
    // Live Song Tracker State
    let currentDurationSec = 0;
    let currentPositionSec = 0;
    let lastPositionTimestamp = 0;
    let isTrackPlaying = false;
    let trackerTimer = null;
    const scrubFill = document.getElementById('scrubFill');
    const currentTimeEl = document.getElementById('currentTime');
    const totalTimeEl = document.getElementById('totalTime');

    function formatTime(seconds) {
        if (!seconds || isNaN(seconds) || seconds < 0) return "0:00";
        const totalSec = Math.floor(seconds);
        const mins = Math.floor(totalSec / 60);
        const secs = totalSec % 60;
        if (mins >= 60) {
            const hrs = Math.floor(mins / 60);
            const remMins = mins % 60;
            return hrs + ":" + (remMins < 10 ? "0" : "") + remMins + ":" + (secs < 10 ? "0" : "") + secs;
        }
        return mins + ":" + (secs < 10 ? "0" : "") + secs;
    }

    function updateSongTrackerUI() {
        if (!scrubFill) return;

        if (currentDurationSec > 0) {
            let pos = currentPositionSec;
            if (isTrackPlaying && lastPositionTimestamp > 0) {
                const elapsed = (Date.now() - lastPositionTimestamp) / 1000;
                pos = Math.min(currentDurationSec, currentPositionSec + elapsed);
            }

            const pct = Math.min(100, Math.max(0, (pos / currentDurationSec) * 100));
            scrubFill.style.width = pct.toFixed(2) + '%';

            if (currentTimeEl) {
                currentTimeEl.textContent = formatTime(pos);
            }
            if (totalTimeEl) {
                totalTimeEl.textContent = formatTime(currentDurationSec);
            }
            syncLyricsPosition(pos);
        } else {
            scrubFill.style.width = '100%';
            if (currentTimeEl) {
                currentTimeEl.textContent = 'LIVE';
            }
            if (totalTimeEl) {
                totalTimeEl.textContent = 'STREAM';
            }
        }
    }

    if (!trackerTimer) {
        trackerTimer = setInterval(function () {
            if (currentDurationSec > 0 && isTrackPlaying) {
                updateSongTrackerUI();
            }
        }, 250);
    }

    // Restore cached track & artwork from sessionStorage for zero-flash page reloads
    try {
        const cachedRaw = sessionStorage.getItem('scrim_cached_track');
        if (cachedRaw) {
            const cached = JSON.parse(cachedRaw);
            if (cached && cached.title) {
                updateTrackMetadata(cached.title, cached.artist, cached.album, cached.albumArtUrl, cached.hasArt, cached.duration, cached.position, cached.isPlaying);
            }
        }
    } catch { }

    // Initial Metadata Fetch
    fetch(getStationUrl('/api/metadata'))
        .then(function (r) { return r.json(); })
        .then(function (data) {
            if (data && data.title) {
                updateTrackMetadata(data.title, data.artist, data.album, data.albumArtUrl, data.hasArt, data.duration, data.position, data.isPlaying);
            }
        })
        .catch(function (e) { console.warn("Could not fetch initial metadata", e); });

    let lastTrackKey = "";
    let lastTrackTitle = "";

    function updateTrackMetadata(title, artist, album, albumArtUrl, hasArt, duration, position, isPlaying) {
        const albumArt = document.getElementById('albumArt');
        const metaContainer = document.getElementById('metaContainer');

        const cleanTitle = (title || "").trim();
        const cleanArtist = (artist || "").trim();
        const currentSongKey = cleanTitle + "::" + cleanArtist;
        const trackChanged = (cleanTitle !== "" && currentSongKey !== lastTrackKey);
        if (cleanTitle !== "") {
            lastTrackKey = currentSongKey;
            lastTrackTitle = cleanTitle;
        }
        if (trackChanged) {
            refreshReactionsState();
            fetchLyrics(cleanTitle, cleanArtist, duration);
        }
        const lowerTitle = cleanTitle.toLowerCase();
        const hasRealTrack = cleanTitle !== "" && 
            cleanTitle !== "Awaiting Audio Source..." && 
            cleanTitle !== "Awaiting Track Info..." && 
            cleanTitle !== "Unknown Track" &&
            lowerTitle !== "stream" &&
            lowerTitle !== "stream.mp3" &&
            lowerTitle !== "live" &&
            lowerTitle !== "listen" &&
            !lowerTitle.startsWith("http://") &&
            !lowerTitle.startsWith("https://") &&
            !lowerTitle.includes("localhost:") &&
            !lowerTitle.includes("127.0.0.1:") &&
            !lowerTitle.startsWith("scrim") &&
            !lowerTitle.includes("scrim broadcast") &&
            !lowerTitle.includes("live broadcast player");

        if (hasRealTrack) {
            // Real track info received: reveal actual artwork and clean typography
            if (albumArt) {
                albumArt.classList.remove('loading');
                if (hasArt && albumArtUrl) {
                    const cacheBuster = '?t=' + Date.now();
                    const fullArtUrl = albumArtUrl.startsWith('/') ? (albumArtUrl + cacheBuster) : albumArtUrl;
                    const isNewTrack = trackChanged || !albumArt.style.backgroundImage || albumArt.style.backgroundImage.includes('linear-gradient');
                    if (isNewTrack) {
                        albumArt.style.backgroundImage = "url('" + fullArtUrl + "')";
                        albumArt.style.backgroundSize = "cover";
                        albumArt.style.backgroundPosition = "center";
                        lastAlbumArtUrl = fullArtUrl;
                    }
                    if (currentBranding && currentBranding.enableDynamicBackdrop) {
                        applyDynamicAmbientBackdrop(lastAlbumArtUrl || fullArtUrl);
                    }
                } else if (!hasArt && !albumArt.style.backgroundImage) {
                    albumArt.style.backgroundImage = "linear-gradient(135deg, #1b3d68 0%, #059669 50%, #dc2626 100%)";
                    if (currentBranding && currentBranding.enableDynamicBackdrop) {
                        document.body.classList.remove('has-ambient-backdrop');
                    }
                }
            }
            if (metaContainer) metaContainer.classList.remove('meta-loading');

            if (trackTitle) trackTitle.textContent = cleanTitle;
            if (trackArtist) trackArtist.textContent = (artist || "LIVE STREAM").toUpperCase();
            if (bottomTrackName) bottomTrackName.textContent = cleanTitle;

            // Google search link for current song + artist
            const searchTerms = cleanArtist ? (cleanTitle + ' ' + cleanArtist) : cleanTitle;
            const searchUrl = 'https://www.google.com/search?q=' + encodeURIComponent(searchTerms);
            if (trackTitleLink) {
                trackTitleLink.href = searchUrl;
                trackTitleLink.title = 'Search "' + cleanTitle + '" on Google';
            }
            if (bottomTrackLink) {
                bottomTrackLink.href = searchUrl;
                bottomTrackLink.title = 'Search "' + cleanTitle + '" on Google';
            }

            // Cache in sessionStorage so refreshing immediately preserves artwork and track info
            try {
                sessionStorage.setItem('scrim_cached_track', JSON.stringify({
                    title: cleanTitle,
                    artist: cleanArtist,
                    album: album,
                    albumArtUrl: albumArtUrl,
                    hasArt: hasArt,
                    duration: duration,
                    position: position,
                    isPlaying: isPlaying
                }));
            } catch { }
        } else {
            // If already displaying a valid track, don't revert to placeholder on temporary stream/connection events
            const currentTitle = trackTitle ? trackTitle.textContent : "";
            if (currentTitle && currentTitle !== "Awaiting Track Info..." && currentTitle !== "Awaiting Audio Source..." && currentTitle !== "Awaiting Stream...") {
                return;
            }

            // No track info from app: show sleek radar loading animation
            if (albumArt) {
                albumArt.classList.add('loading');
                albumArt.style.backgroundImage = "";
            }
            if (metaContainer) metaContainer.classList.add('meta-loading');

            if (trackTitle) trackTitle.textContent = "Awaiting Track Info...";
            if (trackArtist) trackArtist.textContent = "LIVE BROADCAST";
            if (bottomTrackName) bottomTrackName.textContent = "Awaiting Stream...";

            if (trackTitleLink) {
                trackTitleLink.removeAttribute('href');
                trackTitleLink.title = 'Awaiting Track Info...';
            }
            if (bottomTrackLink) {
                bottomTrackLink.removeAttribute('href');
                bottomTrackLink.title = 'Awaiting Stream...';
            }
        }

        // Update Song Tracker Timing
        if (duration !== undefined && duration !== null) {
            const parsedDur = parseFloat(duration);
            currentDurationSec = !isNaN(parsedDur) && parsedDur > 0 ? parsedDur : 0;
        }
        if (position !== undefined && position !== null) {
            const parsedPos = parseFloat(position);
            if (!isNaN(parsedPos) && parsedPos >= 0) {
                if (trackChanged || lastPositionTimestamp === 0) {
                    currentPositionSec = parsedPos;
                    lastPositionTimestamp = Date.now();
                } else {
                    let estimatedCurrent = currentPositionSec;
                    if (isTrackPlaying && lastPositionTimestamp > 0) {
                        const elapsed = (Date.now() - lastPositionTimestamp) / 1000;
                        estimatedCurrent = currentPositionSec + elapsed;
                    }

                    const diff = parsedPos - estimatedCurrent;
                    if (Math.abs(diff) > 2.5 || diff > 0.3) {
                        currentPositionSec = parsedPos;
                        lastPositionTimestamp = Date.now();
                    } else {
                        // Smoothly advance without jumping backwards due to OS polling latency
                        currentPositionSec = estimatedCurrent;
                        lastPositionTimestamp = Date.now();
                    }
                }
            }
        }
        if (isPlaying !== undefined && isPlaying !== null) {
            isTrackPlaying = !!isPlaying;
        } else {
            isTrackPlaying = hasRealTrack;
        }

        updateSongTrackerUI();
    }

    // Connect to Server-Sent Events (SSE)
    let evtSource = null;
    let sseReconnectTimer = null;
    function connectEventSource() {
        if (evtSource) {
            try { evtSource.close(); } catch (e) {}
            evtSource = null;
        }
        if (sseReconnectTimer) {
            clearTimeout(sseReconnectTimer);
            sseReconnectTimer = null;
        }
        try {
            evtSource = new EventSource(getStationUrl('/api/events'));

            evtSource.onopen = function () {
                console.log('[SSE] Real-time connection established with Scrim app');
                if (sseReconnectTimer) {
                    clearTimeout(sseReconnectTimer);
                    sseReconnectTimer = null;
                }
                // Server is confirmed online: if user wanted audio, trigger rapid stream reconnect
                if (isUserPlaying && (!isPlaying || audio.paused)) {
                    scheduleStreamReconnect(150);
                }
            };

            evtSource.onerror = function (e) {
                console.warn('[SSE] Event stream connection paused, scheduling auto-reconnect...', e);
                try {
                    if (evtSource) evtSource.close();
                } catch (err) {}
                evtSource = null;
                if (!sseReconnectTimer) {
                    sseReconnectTimer = setTimeout(connectEventSource, 3000);
                }
            };

            evtSource.onmessage = function (event) {
                try {
                    const data = JSON.parse(event.data);

                    if (data.type === 'private_stream' || data.isPrivate) {
                        setPrivateStreamMode(true);
                    }

                    if (data.streamUrl) {
                        currentStreamEndpoint = data.streamUrl;
                    }

                    if (data.type === 'metadata') {
                        updateTrackMetadata(data.title, data.artist, data.album, data.albumArtUrl, data.hasArt, data.duration, data.position, data.isPlaying);
                    }

                    if (data.type === 'branding') {
                        applyBranding(data);
                    }

                    if (data.type === 'stats') {
                        if (listenerCount) listenerCount.textContent = data.listeners || '1';
                        if (data.isLive !== undefined) {
                            updateLiveIndicator(data.isLive);
                            if (data.isLive) {
                                if (isUserPlaying && !userExplicitlyStopped && (!isPlaying || audio.paused)) {
                                    if (reconnectTimeout) {
                                        clearTimeout(reconnectTimeout);
                                        reconnectTimeout = null;
                                    }
                                    startStream(true);
                                } else if (pendingLiveAutoStart && !userExplicitlyStopped) {
                                    pendingLiveAutoStart = false;
                                    startStream(false);
                                } else if (!isPlaying || !audio.src) {
                                    checkAutoplay(true);
                                }
                            }
                        }
                        if (data.format !== undefined) {
                            updateStreamQuality(data.format, data.bitrate);
                        }
                    }

                    if (data.type === 'queue' && data.requests) {
                        updateQueue(data.requests);
                    }

                if (data.type === 'poll_update' && data.poll) {
                    renderLivePoll(data.poll);
                }

                if (data.type === 'poll_ended') {
                    removeLivePoll();
                }

                if (data.type === 'party_celebrate') {
                    triggerPartyCelebration(data.sender);
                }

                if (data.type === 'chat_init') {
                    if (data.enabled !== undefined) {
                        setChatStatus(data.enabled);
                    }
                    if (Array.isArray(data.blacklist)) {
                        currentBlacklist = data.blacklist;
                    }
                    if (Array.isArray(data.messages)) {
                        data.messages.forEach(appendChatMessage);
                    }
                }

                if (data.type === 'assign_nickname' && data.target && data.assigned) {
                    handleNicknameAssignment(data.target, data.assigned);
                }

                if (data.type === 'chat') {
                    const msg = (data.message && typeof data.message === 'object') ? data.message : data;
                    if (msg && (msg.text !== undefined || msg.id)) {
                        appendChatMessage(msg);
                    }
                }

                if (data.type === 'chat_message_deleted' && data.id) {
                    removeChatMessage(data.id);
                }

                if (data.type === 'chat_clear') {
                    clearChatMessages();
                }

                if (data.type === 'chat_status' && data.enabled !== undefined) {
                    setChatStatus(data.enabled);
                }

                if (data.type === 'reaction') {
                    updateReactionCounts(data.counts);
                    let targetBtn = null;
                    if (data.reaction === 'thumbs_up' || data.reaction === 'thumbsup' || data.reaction === 'like') {
                        targetBtn = btnReactionThumbsUp;
                    } else if (data.reaction === 'heart' || data.reaction === 'love') {
                        targetBtn = btnReactionLove;
                    } else if (data.reaction === 'thumbs_down' || data.reaction === 'thumbsdown' || data.reaction === 'dislike') {
                        targetBtn = btnReactionThumbsDown;
                    }
                    spawnFloatingReaction(data.reaction, targetBtn);
                }

                if (data.type === 'reaction_init' && data.counts) {
                    updateReactionCounts(data.counts);
                }

                if (data.type === 'reaction_reset') {
                    updateReactionCounts(data.counts || { thumbsUp: 0, thumbsDown: 0, heart: 0 });
                    refreshReactionsState();
                }

                if (data.type === 'history_init' || data.type === 'history_update') {
                    renderSongHistory(data.history || []);
                }
            } catch (err) {
                console.error("SSE parse error", err);
            }
        };
    } catch (e) {
        console.warn("SSE not available", e);
    }
    }
    connectEventSource();

    function updateSongRequestsEnabled(enabled) {
        const isEnabled = (enabled !== false);
        if (requestStatusPill) {
            requestStatusPill.textContent = isEnabled ? 'OPEN' : 'PAUSED';
            requestStatusPill.classList.toggle('chat-paused', !isEnabled);
        }
        if (requestDisabledBanner) {
            requestDisabledBanner.style.display = isEnabled ? 'none' : 'flex';
        }
        if (requestInput) {
            requestInput.disabled = !isEnabled;
            requestInput.placeholder = isEnabled ? 'Enter Song Title & Artist...' : 'Song requests are currently paused';
        }
        if (dedicationInput) {
            dedicationInput.disabled = !isEnabled;
        }
        if (submitRequestBtn) {
            submitRequestBtn.disabled = !isEnabled;
            submitRequestBtn.style.opacity = isEnabled ? '' : '0.5';
            submitRequestBtn.style.cursor = isEnabled ? '' : 'not-allowed';
        }
    }

    // Song Request Submission
    if (requestForm && requestInput) {
        requestForm.addEventListener('submit', function (e) {
            e.preventDefault();
            const text = requestInput.value.trim();
            if (!text) return;

            const dedication = dedicationInput ? dedicationInput.value.trim() : '';

            fetch(getStationUrl('/api/requests'), {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ query: text, dedication: dedication })
            }).then(function (res) {
                if (res.ok) {
                    requestInput.value = '';
                    if (dedicationInput) dedicationInput.value = '';
                    if (requestSuccess) {
                        requestSuccess.style.display = 'block';
                        requestSuccess.textContent = dedication
                            ? `Request submitted & dedicated to ${dedication}!`
                            : 'Request submitted to DJ!';
                        setTimeout(function () {
                            requestSuccess.style.display = 'none';
                        }, 4000);
                    }
                } else if (res.status === 403) {
                    updateSongRequestsEnabled(false);
                }
            }).catch(function (err) {
                console.error("Request failed", err);
            });
        });
    }

    function updateQueue(requests) {
        if (!queueList) return;
        if (!Array.isArray(requests) || requests.length === 0) {
            queueList.innerHTML = '<li class="queue-item" style="color: #64748b; font-style: italic; justify-content: center;"><span>No listener requests pending</span></li>';
            return;
        }

        queueList.innerHTML = '';
        requests.forEach(function (req, index) {
            const li = document.createElement('li');
            li.className = 'queue-item';
            const dedicationHtml = req.dedication ? `<span class="queue-dedication">❤️ Dedicated to: ${escapeHtml(req.dedication)}</span>` : '';
            li.innerHTML = `
                <div class="queue-info">
                    <span class="queue-track">${index + 1}. ${escapeHtml(req.query)}</span>
                    ${dedicationHtml}
                </div>
                <span class="queue-dur">(${escapeHtml(req.status || 'Pending')})</span>
            `;
            queueList.appendChild(li);
        });
    }

    // ==========================================================================
    // Live Station Chat & Identification Logic
    // ==========================================================================
    let anonClientId = '';
    try {
        anonClientId = localStorage.getItem('scrim_anon_uid') || '';
    } catch (e) {}
    if (!anonClientId) {
        anonClientId = (typeof crypto !== 'undefined' && crypto.randomUUID)
            ? crypto.randomUUID()
            : 'c_' + Math.random().toString(36).substring(2, 15) + Date.now().toString(36);
        try {
            localStorage.setItem('scrim_anon_uid', anonClientId);
        } catch (e) {}
    }

    const chatStatusPill = document.getElementById('chatStatusPill');
    const chatDisabledBanner = document.getElementById('chatDisabledBanner');
    const chatBannedBanner = document.getElementById('chatBannedBanner');
    const chatMessagesContainer = document.getElementById('chatMessagesContainer');
    const chatMessagesList = document.getElementById('chatMessagesList');
    const chatEmptyHint = document.getElementById('chatEmptyHint');
    const chatForm = document.getElementById('chatForm');
    const chatNicknameInput = document.getElementById('chatNicknameInput');
    const chatMessageInput = document.getElementById('chatMessageInput');
    const chatSendBtn = document.getElementById('chatSendBtn');

    const chatRulesModal = document.getElementById('chatRulesModal');
    const chatRulesAgreeBtn = document.getElementById('chatRulesAgreeBtn');
    const chatRulesCancelBtn = document.getElementById('chatRulesCancelBtn');
    let isUserBanned = false;
    let pendingChatSubmit = null;

    let isChatEnabled = true;
    const seenMessageIds = new Set();

    // Anonymous Nickname management
    let currentTakenNicknames = new Set();

    function isNicknameAvailableLocally(name) {
        if (!name) return false;
        return !currentTakenNicknames.has(name.trim().toLowerCase());
    }

    function generateUniqueNickname() {
        for (let i = 0; i < 100; i++) {
            const randNum = Math.floor(100 + Math.random() * 900);
            const candidate = 'Listener #' + randNum;
            if (isNicknameAvailableLocally(candidate)) {
                return candidate;
            }
        }
        return 'Listener #' + Date.now().toString().slice(-4);
    }

    function initNickname() {
        if (!chatNicknameInput) return;
        let nick = '';
        try {
            nick = localStorage.getItem('scrim_chat_nickname') || '';
        } catch (e) {}

        if (!nick) {
            nick = generateUniqueNickname();
            try {
                localStorage.setItem('scrim_chat_nickname', nick);
            } catch (e) {}
        }
        chatNicknameInput.value = nick;

        chatNicknameInput.addEventListener('change', function () {
            let val = chatNicknameInput.value.trim();
            if (!val) {
                val = generateUniqueNickname();
                chatNicknameInput.value = val;
            }
            if (currentTakenNicknames.has(val.toLowerCase())) {
                chatNicknameInput.style.borderColor = '#ef4444';
                chatNicknameInput.title = 'This nickname is already in use by another listener this session.';
                if (chatMessageInput) {
                    const origPh = chatMessageInput.placeholder;
                    chatMessageInput.placeholder = '⚠️ Nickname already taken! Please choose another.';
                    setTimeout(function () {
                        chatMessageInput.placeholder = origPh;
                    }, 4000);
                }
            } else {
                chatNicknameInput.style.borderColor = '';
                chatNicknameInput.title = 'Change your anonymous chat handle';
                try {
                    localStorage.setItem('scrim_chat_nickname', val);
                } catch (e) {}
            }
        });
    }

    initNickname();

    function setChatStatus(enabled) {
        isChatEnabled = !!enabled;
        if (chatStatusPill) {
            if (isChatEnabled) {
                chatStatusPill.textContent = 'LIVE';
                chatStatusPill.classList.remove('paused');
            } else {
                chatStatusPill.textContent = 'PAUSED';
                chatStatusPill.classList.add('paused');
            }
        }
        if (chatDisabledBanner && !isUserBanned) {
            chatDisabledBanner.style.display = isChatEnabled ? 'none' : 'flex';
        }
        if (chatMessageInput && !isUserBanned) {
            chatMessageInput.disabled = !isChatEnabled;
            chatMessageInput.placeholder = isChatEnabled ? 'Type a message...' : 'Chat is currently paused by the host';
        }
        if (chatSendBtn && !isUserBanned) {
            chatSendBtn.disabled = !isChatEnabled;
        }
    }

    function setUserBanned(banned) {
        isUserBanned = !!banned;
        if (chatBannedBanner) {
            chatBannedBanner.style.display = isUserBanned ? 'flex' : 'none';
        }
        if (isUserBanned) {
            if (chatDisabledBanner) chatDisabledBanner.style.display = 'none';
            if (chatMessageInput) {
                chatMessageInput.disabled = true;
                chatMessageInput.placeholder = 'You are banned from station chat';
            }
            if (chatSendBtn) {
                chatSendBtn.disabled = true;
            }
        } else {
            setChatStatus(isChatEnabled);
        }
    }

    function hasAgreedToChatRules() {
        try {
            return localStorage.getItem('scrim_chat_rules_agreed') === 'true';
        } catch (e) {
            return false;
        }
    }

    function showChatRulesModal(pendingFn) {
        pendingChatSubmit = pendingFn;
        if (chatRulesModal) {
            chatRulesModal.style.display = 'flex';
        }
    }

    function hideChatRulesModal() {
        pendingChatSubmit = null;
        if (chatRulesModal) {
            chatRulesModal.style.display = 'none';
        }
    }

    if (chatRulesAgreeBtn) {
        chatRulesAgreeBtn.addEventListener('click', function (e) {
            if (e && e.preventDefault) e.preventDefault();
            try {
                localStorage.setItem('scrim_chat_rules_agreed', 'true');
            } catch (err) {}
            const fn = pendingChatSubmit;
            hideChatRulesModal();
            if (typeof fn === 'function') {
                fn();
            }
        });
    }

    if (chatRulesCancelBtn) {
        chatRulesCancelBtn.addEventListener('click', function () {
            hideChatRulesModal();
        });
    }

    function escapeHtml(str) {
        if (!str) return '';
        return String(str)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#039;');
    }

    function formatMessageTime(isoString) {
        if (!isoString) return '';
        try {
            const d = new Date(isoString);
            return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' });
        } catch (e) {
            return '';
        }
    }

    function appendChatMessage(msg) {
        if (!msg || !chatMessagesList) return;
        if (msg.id && seenMessageIds.has(msg.id)) return;
        if (msg.id) seenMessageIds.add(msg.id);

        if (chatEmptyHint) {
            chatEmptyHint.style.display = 'none';
        }

        const msgDiv = document.createElement('div');
        msgDiv.className = 'chat-msg' + (msg.isHost ? ' host-msg' : '');
        if (msg.id) {
            msgDiv.id = `chatMsg_${msg.id}`;
            msgDiv.setAttribute('data-msg-id', msg.id);
        }
        const timeStr = formatMessageTime(msg.timestamp);
        const senderColor = msg.color || (msg.isHost ? '#ef4444' : '#00d2ff');
        const textWithEmojis = convertEmoticons(msg.text || '');

        msgDiv.innerHTML = `
            <div class="chat-msg-header">
                <div class="chat-msg-sender-wrap">
                    <strong class="chat-msg-sender" style="color: ${escapeHtml(senderColor)};">${escapeHtml(msg.sender || 'Anonymous')}</strong>
                    ${msg.isHost ? '<span class="chat-host-badge">DJ</span>' : ''}
                </div>
                <span class="chat-msg-time">${timeStr}</span>
            </div>
            <div class="chat-msg-text">${escapeHtml(textWithEmojis)}</div>
        `;

        chatMessagesList.appendChild(msgDiv);

        // Auto-scroll to bottom smoothly
        if (chatMessagesContainer) {
            chatMessagesContainer.scrollTop = chatMessagesContainer.scrollHeight;
        }
    }

    function removeChatMessage(id) {
        if (!id || !chatMessagesList) return;
        const el = document.getElementById(`chatMsg_${id}`) || chatMessagesList.querySelector(`[data-msg-id="${id}"]`);
        if (el && el.parentNode) {
            el.parentNode.removeChild(el);
        }
        if (seenMessageIds.has(id)) {
            seenMessageIds.delete(id);
        }
        const remaining = chatMessagesList.querySelectorAll('.chat-msg');
        if (remaining.length === 0 && chatEmptyHint) {
            chatEmptyHint.style.display = 'block';
            chatMessagesList.appendChild(chatEmptyHint);
        }
    }

    function clearChatMessages() {
        seenMessageIds.clear();
        if (!chatMessagesList) return;
        chatMessagesList.innerHTML = '';
        if (chatEmptyHint) {
            chatEmptyHint.style.display = 'block';
            chatMessagesList.appendChild(chatEmptyHint);
        }
    }

    let currentBlacklist = [];

    function isBlacklistedNickname(nick) {
        if (!nick || !currentBlacklist.length) return false;
        const lower = nick.toLowerCase();
        return currentBlacklist.some(function (b) {
            return b && lower.includes(b.toLowerCase());
        });
    }

    function handleNicknameAssignment(target, assigned) {
        if (!chatNicknameInput || !target || !assigned) return;
        const current = chatNicknameInput.value.trim();
        if (current.toLowerCase() === target.toLowerCase()) {
            chatNicknameInput.value = assigned;
            try {
                localStorage.setItem('scrim_chat_nickname', assigned);
            } catch (e) {}
            if (chatMessageInput) {
                const prevPh = chatMessageInput.placeholder;
                chatMessageInput.placeholder = `DJ assigned you nickname "${assigned}"! (You can edit it anytime)`;
                setTimeout(function () {
                    chatMessageInput.placeholder = prevPh;
                }, 6000);
            }
        }
    }

    function loadChatState() {
        fetch(getStationUrl('/api/chat', 'clientId=' + encodeURIComponent(anonClientId)))
            .then(function (r) { return r.json(); })
            .then(function (data) {
                if (data) {
                    if (data.isBanned) {
                        setUserBanned(true);
                    }
                    if (data.enabled !== undefined) {
                        setChatStatus(data.enabled);
                    }
                    if (Array.isArray(data.blacklist)) {
                        currentBlacklist = data.blacklist;
                    }
                    if (Array.isArray(data.takenNicknames)) {
                        currentTakenNicknames = new Set(data.takenNicknames.map(function (n) { return (n || '').trim().toLowerCase(); }));
                        if (chatNicknameInput) {
                            const cur = chatNicknameInput.value.trim();
                            if (cur && currentTakenNicknames.has(cur.toLowerCase())) {
                                const newNick = generateUniqueNickname();
                                chatNicknameInput.value = newNick;
                                try {
                                    localStorage.setItem('scrim_chat_nickname', newNick);
                                } catch (e) {}
                            }
                        }
                    }
                    if (Array.isArray(data.messages)) {
                        if (chatMessagesContainer) chatMessagesContainer.innerHTML = '';
                        data.messages.forEach(appendChatMessage);
                    }
                }
            })
            .catch(function (err) {
                console.warn("Could not load initial chat", err);
            });
    }
    loadChatState();

    function submitChatMessage(sender, text) {
        if (!text) return;
        if (chatSendBtn) chatSendBtn.disabled = true;

        fetch(getStationUrl('/api/chat'), {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ sender: sender, text: text, clientId: anonClientId })
        }).then(function (res) {
            if (chatSendBtn) chatSendBtn.disabled = !isChatEnabled || isUserBanned;
            if (res.ok) {
                if (chatNicknameInput) chatNicknameInput.style.borderColor = '';
                chatMessageInput.value = '';
                chatMessageInput.focus();
                return res.json().then(function (data) {
                    if (data && data.message) {
                        appendChatMessage(data.message);
                    }
                }).catch(function () {});
            } else if (res.status === 403) {
                return res.json().then(function (errData) {
                    if (errData && errData.isBanned) {
                        setUserBanned(true);
                    } else {
                        setChatStatus(false);
                    }
                }).catch(function () {
                    setChatStatus(false);
                });
            } else if (res.status === 400) {
                return res.json().then(function (errData) {
                    const errMsg = (errData && errData.error) ? errData.error : "Message could not be sent.";
                    if (errData && errData.nameTaken) {
                        if (chatNicknameInput) {
                            chatNicknameInput.style.borderColor = '#ef4444';
                            chatNicknameInput.focus();
                            chatNicknameInput.select();
                        }
                    }
                    if (chatMessageInput) {
                        const originalPh = chatMessageInput.placeholder;
                        chatMessageInput.placeholder = `⚠️ ${errMsg}`;
                        setTimeout(function () {
                            chatMessageInput.placeholder = originalPh;
                        }, 4000);
                    }
                }).catch(function () {});
            }
        }).catch(function (err) {
            if (chatSendBtn) chatSendBtn.disabled = !isChatEnabled || isUserBanned;
            console.error("Failed to post chat message", err);
        });
    }

    // Emoticon Auto-Conversion
    function convertEmoticons(text) {
        if (!text) return '';
        let res = text;
        // Shortcodes
        res = res.replace(/:fire:/gi, '🔥');
        res = res.replace(/:(?:thumbsup|\+1):/gi, '👍');
        res = res.replace(/:(?:thumbsdown|-1):/gi, '👎');
        res = res.replace(/:(?:party|tada):/gi, '🎉');
        res = res.replace(/:(?:music|note):/gi, '🎵');
        res = res.replace(/:radio:/gi, '📻');
        res = res.replace(/:rocket:/gi, '🚀');
        res = res.replace(/:100:/gi, '💯');
        res = res.replace(/:skull:/gi, '💀');
        res = res.replace(/:star:/gi, '⭐');
        res = res.replace(/:eyes:/gi, '👀');
        res = res.replace(/:sparkles:/gi, '✨');
        res = res.replace(/:clap:/gi, '👏');
        res = res.replace(/:wave:/gi, '👋');
        res = res.replace(/:heart:/gi, '❤️');

        // Hearts: <3 and </3 (avoid digits like <300)
        res = res.replace(/<\/3/g, '💔');
        res = res.replace(/<3(?!\d)/g, '❤️');

        // Surprised: :O, :o, :-O, :-o, =O, =o, :0
        res = res.replace(/(?<!\d)(?:[:=]-?[oO0])(?![a-zA-Z0-9])/g, '😮');

        // Laugh / Big grin: :D, :-D, =D, =-D, xD, XD
        res = res.replace(/(?:(?<!\d)(?:[:=]-?D|=D)|(?<![a-zA-Z0-9])[xX]-?D)(?![a-zA-Z0-9])/g, '😀');

        // Smile: :), :-), =), =-), :], =]
        res = res.replace(/(?:[:=]-?[\)\]])/g, '😊');

        // Wink: ;), ;-)
        res = res.replace(/(?:;-?[\)\]])/g, '😉');

        // Sad / Cry: :(, :-(, =(, =-(, :'(
        res = res.replace(/(?:[:=]-?[\(\[]|:'-?\()/g, '😢');

        // Tongue: :P, :-P, :p, :-p, =P, =p
        res = res.replace(/(?<!\d)(?:[:=]-?[pP])(?![a-zA-Z0-9])/g, '😛');

        // Neutral: :|, :-|, =|, =/
        res = res.replace(/(?<![a-zA-Z0-9])(?:[:=]-?[/\\|])(?![a-zA-Z0-9])/g, '😐');

        // Sunglasses: B), 8)
        res = res.replace(/(?<![a-zA-Z0-9])(?:[B8]-?\))(?![a-zA-Z0-9])/g, '😎');

        return res;
    }

    // Chat form submission & Emoji picker wiring
    const chatEmojiToggleBtn = document.getElementById('chatEmojiToggleBtn');
    const chatEmojiPicker = document.getElementById('chatEmojiPicker');

    if (chatMessageInput) {
        chatMessageInput.addEventListener('input', function () {
            const curVal = chatMessageInput.value;
            const converted = convertEmoticons(curVal);
            if (converted !== curVal) {
                const prevPos = chatMessageInput.selectionStart;
                const diff = converted.length - curVal.length;
                chatMessageInput.value = converted;
                if (prevPos !== null) {
                    const nextPos = Math.max(0, prevPos + diff);
                    chatMessageInput.setSelectionRange(nextPos, nextPos);
                }
            }
        });
    }

    if (chatEmojiToggleBtn && chatEmojiPicker) {
        chatEmojiToggleBtn.addEventListener('click', function (e) {
            e.stopPropagation();
            const isHidden = chatEmojiPicker.style.display === 'none' || !chatEmojiPicker.style.display;
            chatEmojiPicker.style.display = isHidden ? 'block' : 'none';
            chatEmojiToggleBtn.classList.toggle('active', isHidden);
            chatEmojiPicker.setAttribute('aria-hidden', isHidden ? 'false' : 'true');
        });

        chatEmojiPicker.addEventListener('click', function (e) {
            const btn = e.target.closest('.chat-emoji-item');
            if (!btn || !chatMessageInput) return;
            const emoji = btn.getAttribute('data-emoji') || btn.textContent.trim();
            if (!emoji) return;

            const start = chatMessageInput.selectionStart !== null ? chatMessageInput.selectionStart : chatMessageInput.value.length;
            const end = chatMessageInput.selectionEnd !== null ? chatMessageInput.selectionEnd : chatMessageInput.value.length;
            const val = chatMessageInput.value;
            chatMessageInput.value = val.substring(0, start) + emoji + val.substring(end);
            const newPos = start + emoji.length;
            chatMessageInput.focus();
            chatMessageInput.setSelectionRange(newPos, newPos);
        });

        document.addEventListener('click', function (e) {
            if (chatEmojiPicker.style.display !== 'none' && !chatEmojiPicker.contains(e.target) && e.target !== chatEmojiToggleBtn && !chatEmojiToggleBtn.contains(e.target)) {
                chatEmojiPicker.style.display = 'none';
                chatEmojiToggleBtn.classList.remove('active');
                chatEmojiPicker.setAttribute('aria-hidden', 'true');
            }
        });
    }

    if (chatForm && chatMessageInput) {
        chatForm.addEventListener('submit', function (e) {
            e.preventDefault();
            if (!isChatEnabled || isUserBanned) return;
            const text = convertEmoticons(chatMessageInput.value.trim());
            if (!text) return;

            const sender = (chatNicknameInput ? chatNicknameInput.value.trim() : '') || 'Anonymous';

            if (!hasAgreedToChatRules()) {
                showChatRulesModal(function () {
                    submitChatMessage(sender, text);
                });
                return;
            }

            submitChatMessage(sender, text);
        });
    }

    // Song Reactions System (Single-Vote Switcher & Per-Song Memory)
    let currentUserReaction = null;

    function highlightUserReaction(reaction) {
        currentUserReaction = reaction;
        const allBtns = [btnReactionThumbsUp, btnReactionLove, btnReactionThumbsDown];
        allBtns.forEach(function (b) {
            if (b) b.classList.remove('active');
        });

        if (!reaction) return;
        if (reaction === 'thumbs_up' || reaction === 'thumbsup' || reaction === 'like') {
            if (btnReactionThumbsUp) btnReactionThumbsUp.classList.add('active');
        } else if (reaction === 'heart' || reaction === 'love') {
            if (btnReactionLove) btnReactionLove.classList.add('active');
        } else if (reaction === 'thumbs_down' || reaction === 'thumbsdown' || reaction === 'dislike') {
            if (btnReactionThumbsDown) btnReactionThumbsDown.classList.add('active');
        }
    }

    function updateReactionCounts(counts) {
        if (!counts) return;
        if (countThumbsUp && counts.thumbsUp !== undefined) {
            countThumbsUp.textContent = counts.thumbsUp;
        }
        if (countHeart && counts.heart !== undefined) {
            countHeart.textContent = counts.heart;
        }
        if (countThumbsDown && counts.thumbsDown !== undefined) {
            countThumbsDown.textContent = counts.thumbsDown;
        }
    }

    function refreshReactionsState() {
        fetch(getStationUrl('/api/reactions', 'clientId=' + encodeURIComponent(anonClientId))).then(function (res) {
            if (res.ok) return res.json();
            return null;
        }).then(function (data) {
            if (data) {
                if (data.counts) updateReactionCounts(data.counts);
                highlightUserReaction(data.userReaction || null);
            }
        }).catch(function () {});
    }

    function spawnFloatingReaction(type, sourceEl) {
        const emojiMap = {
            'thumbs_up': '👍',
            'thumbsup': '👍',
            'like': '👍',
            'heart': '❤️',
            'love': '❤️',
            'thumbs_down': '👎',
            'thumbsdown': '👎',
            'dislike': '👎'
        };
        const emoji = emojiMap[type] || '❤️';
        const particle = document.createElement('span');
        particle.className = 'floating-reaction-particle';
        particle.textContent = emoji;

        let startX = window.innerWidth / 2;
        let startY = window.innerHeight / 2;

        if (sourceEl) {
            const rect = sourceEl.getBoundingClientRect();
            startX = rect.left + rect.width / 2;
            startY = rect.top + window.scrollY;
        }

        // Random horizontal drift (-15px to +15px)
        const drift = (Math.random() - 0.5) * 30;
        particle.style.left = (startX + drift) + 'px';
        particle.style.top = startY + 'px';
        particle.style.position = 'absolute';
        particle.style.pointerEvents = 'none';
        particle.style.zIndex = '9999';

        document.body.appendChild(particle);

        setTimeout(function () {
            if (particle.parentNode) {
                particle.parentNode.removeChild(particle);
            }
        }, 1250);
    }

    function sendReaction(type, btnEl) {
        if (!type) return;
        if (btnEl) {
            btnEl.classList.add('pop');
            setTimeout(function () {
                btnEl.classList.remove('pop');
            }, 300);
        }

        // Spawn particle only if we're selecting or switching to a new reaction
        if (currentUserReaction !== type) {
            spawnFloatingReaction(type, btnEl);
        }

        fetch(getStationUrl('/api/reactions'), {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ type: type, clientId: anonClientId })
        }).then(function (res) {
            return res.json();
        }).then(function (data) {
            if (data) {
                if (data.counts) {
                    updateReactionCounts(data.counts);
                }
                highlightUserReaction(data.userReaction || null);
            }
        }).catch(function (err) {
            console.error("Failed to post reaction", err);
        });
    }

    if (btnReactionThumbsUp) {
        btnReactionThumbsUp.addEventListener('click', function () {
            sendReaction('thumbs_up', btnReactionThumbsUp);
        });
    }
    if (btnReactionLove) {
        btnReactionLove.addEventListener('click', function () {
            sendReaction('heart', btnReactionLove);
        });
    }
    if (btnReactionThumbsDown) {
        btnReactionThumbsDown.addEventListener('click', function () {
            sendReaction('thumbs_down', btnReactionThumbsDown);
        });
    }

    // Initial reactions & user vote fetch
    refreshReactionsState();

    // Recently Played History Logic & Initial Fetch
    function fetchSongHistory() {
        fetch(getStationUrl('/api/history')).then(function (res) {
            if (res.ok) return res.json();
            return null;
        }).then(function (data) {
            if (Array.isArray(data)) {
                renderSongHistory(data);
            } else if (data && Array.isArray(data.history)) {
                renderSongHistory(data.history);
            }
        }).catch(function () {});
    }

    if (historyToggleBtn && historyCard) {
        historyToggleBtn.addEventListener('click', function () {
            historyCard.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
            historyCard.style.outline = '2px solid var(--accent-color, #00d2ff)';
            historyCard.style.outlineOffset = '2px';
            setTimeout(function () {
                historyCard.style.outline = 'none';
            }, 1200);
        });
    }

    fetchSongHistory();

    function renderSongHistory(items) {
        if (!historyItemsList) return;
        if (!Array.isArray(items) || items.length === 0) {
            if (historyEmptyHint) historyEmptyHint.style.display = 'block';
            historyItemsList.innerHTML = '';
            if (historyCountPill) historyCountPill.textContent = '0 TRACKS';
            return;
        }

        if (historyEmptyHint) historyEmptyHint.style.display = 'none';
        if (historyCountPill) historyCountPill.textContent = `${items.length} TRACK${items.length === 1 ? '' : 'S'}`;

        historyItemsList.innerHTML = '';
        items.forEach(function (track) {
            const row = document.createElement('div');
            row.className = 'history-row';

            const cleanTitle = (track.title || '').trim();
            const cleanArtist = (track.artist || '').trim();
            const searchTerms = cleanArtist ? (cleanTitle + ' ' + cleanArtist) : cleanTitle;
            const searchUrl = 'https://www.google.com/search?q=' + encodeURIComponent(searchTerms || 'music');
            const searchTitle = 'Search "' + (cleanTitle || 'Track') + (cleanArtist ? ' - ' + cleanArtist : '') + '" on Google';

            const thumbWrap = document.createElement('a');
            thumbWrap.className = 'history-thumb-link';
            thumbWrap.href = searchUrl;
            thumbWrap.target = '_blank';
            thumbWrap.rel = 'noopener noreferrer';
            thumbWrap.title = searchTitle;

            const thumb = document.createElement('div');
            thumb.className = 'history-thumb';
            if (track.hasArt && track.albumArtUrl) {
                const img = document.createElement('img');
                img.src = track.albumArtUrl;
                img.alt = cleanTitle || 'Track Art';
                thumb.appendChild(img);
            } else {
                thumb.innerHTML = `
                    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <path d="M9 18V5l12-2v13"></path>
                        <circle cx="6" cy="18" r="3"></circle>
                        <circle cx="18" cy="16" r="3"></circle>
                    </svg>
                `;
            }
            thumbWrap.appendChild(thumb);

            const info = document.createElement('div');
            info.className = 'history-track-info';

            const titleLink = document.createElement('a');
            titleLink.className = 'history-track-title history-track-title-link';
            titleLink.href = searchUrl;
            titleLink.target = '_blank';
            titleLink.rel = 'noopener noreferrer';
            titleLink.title = searchTitle;

            const titleText = document.createElement('span');
            titleText.textContent = cleanTitle || 'Unknown Track';
            titleLink.appendChild(titleText);

            const searchIcon = document.createElement('span');
            searchIcon.className = 'history-search-icon';
            searchIcon.innerHTML = `
                <svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
                    <circle cx="11" cy="11" r="8"></circle>
                    <line x1="21" y1="21" x2="16.65" y2="16.65"></line>
                </svg>
            `;
            titleLink.appendChild(searchIcon);

            const artistEl = document.createElement('div');
            artistEl.className = 'history-track-artist';
            artistEl.textContent = cleanArtist || 'Live Broadcast';

            info.appendChild(titleLink);
            info.appendChild(artistEl);

            const rightWrap = document.createElement('div');
            rightWrap.className = 'history-track-right';

            const reactionsEl = document.createElement('div');
            reactionsEl.className = 'history-track-reactions';

            const hasReactions = (track.thumbsUp || 0) > 0 || (track.heart || 0) > 0 || (track.thumbsDown || 0) > 0;
            if (hasReactions) {
                if ((track.thumbsUp || 0) > 0) {
                    const upBadge = document.createElement('span');
                    upBadge.className = 'history-rx-badge rx-thumbs-up';
                    upBadge.title = `${track.thumbsUp} Thumbs Up`;
                    upBadge.innerHTML = `👍 <span>${track.thumbsUp}</span>`;
                    reactionsEl.appendChild(upBadge);
                }
                if ((track.heart || 0) > 0) {
                    const heartBadge = document.createElement('span');
                    heartBadge.className = 'history-rx-badge rx-heart';
                    heartBadge.title = `${track.heart} Loves`;
                    heartBadge.innerHTML = `❤️ <span>${track.heart}</span>`;
                    reactionsEl.appendChild(heartBadge);
                }
                if ((track.thumbsDown || 0) > 0) {
                    const downBadge = document.createElement('span');
                    downBadge.className = 'history-rx-badge rx-thumbs-down';
                    downBadge.title = `${track.thumbsDown} Thumbs Down`;
                    downBadge.innerHTML = `👎 <span>${track.thumbsDown}</span>`;
                    reactionsEl.appendChild(downBadge);
                }
            } else {
                const noneBadge = document.createElement('span');
                noneBadge.className = 'history-rx-none';
                noneBadge.textContent = 'No reactions';
                reactionsEl.appendChild(noneBadge);
            }

            const timeEl = document.createElement('div');
            timeEl.className = 'history-track-time';
            timeEl.textContent = track.playedAt || '';

            rightWrap.appendChild(reactionsEl);
            rightWrap.appendChild(timeEl);

            row.appendChild(thumbWrap);
            row.appendChild(info);
            row.appendChild(rightWrap);

            historyItemsList.appendChild(row);
        });
    }

    // =========================================================================
    // Direct Audio Stream Modal & Copy Logic (For VLC, Games, External Players)
    // =========================================================================
    const directStreamModal = document.getElementById('directStreamModal');
    const directStreamBtn = document.getElementById('directStreamBtn');
    const directStreamPill = document.getElementById('directStreamPill');
    const directStreamCloseBtn = document.getElementById('directStreamCloseBtn');
    const directStreamDoneBtn = document.getElementById('directStreamDoneBtn');
    const directStreamCopyBtn = document.getElementById('directStreamCopyBtn');
    const directStreamCopyBtnText = document.getElementById('directStreamCopyBtnText');
    const directStreamUrlInput = document.getElementById('directStreamUrlInput');
    const directStreamOpenTabLink = document.getElementById('directStreamOpenTabLink');
    const directStreamM3uLink = document.getElementById('directStreamM3uLink');
    const directStreamMetaNote = document.getElementById('directStreamMetaNote');

    function getAbsoluteStreamUrl() {
        const mount = currentStreamEndpoint ? (currentStreamEndpoint.startsWith('/') ? currentStreamEndpoint : ('/' + currentStreamEndpoint)) : '/stream';
        const loc = window.location;
        const origin = loc.origin || (loc.protocol + '//' + loc.host);
        let pathname = loc.pathname || '';
        if (pathname.endsWith('/index.html')) {
            pathname = pathname.slice(0, -11);
        } else if (pathname.endsWith('/')) {
            pathname = pathname.slice(0, -1);
        }
        if (!pathname || pathname === '/') {
            return origin + mount;
        }
        return origin + pathname + mount;
    }

    function getAbsoluteM3uUrl() {
        const loc = window.location;
        const origin = loc.origin || (loc.protocol + '//' + loc.host);
        let pathname = loc.pathname || '';
        if (pathname.endsWith('/index.html')) {
            pathname = pathname.slice(0, -11);
        } else if (pathname.endsWith('/')) {
            pathname = pathname.slice(0, -1);
        }
        if (!pathname || pathname === '/') {
            return origin + '/listen.m3u';
        }
        return origin + pathname + '/listen.m3u';
    }

    function updateAllDirectStreamUrls() {
        const absUrl = getAbsoluteStreamUrl();
        const m3uUrl = getAbsoluteM3uUrl();

        if (directStreamUrlInput) {
            directStreamUrlInput.value = absUrl;
        }
        if (directStreamOpenTabLink) {
            directStreamOpenTabLink.href = absUrl;
        }
        if (directStreamM3uLink) {
            directStreamM3uLink.href = m3uUrl;
        }
        if (directStreamMetaNote) {
            const fmt = (streamFormat && streamFormat.textContent) ? streamFormat.textContent : 'MP3';
            const br = (streamBitrate && streamBitrate.textContent) ? streamBitrate.textContent : '128 kbps';
            directStreamMetaNote.textContent = `Broadcast: ${fmt} • ${br} • Real-time low-latency stream`;
        }
    }

    function copyTextToClipboard(text, btnEl, textEl, originalLabel) {
        function onCopied() {
            if (btnEl) btnEl.classList.add('copied');
            if (textEl) textEl.textContent = 'Copied! ✓';
            setTimeout(function () {
                if (btnEl) btnEl.classList.remove('copied');
                if (textEl) textEl.textContent = originalLabel;
            }, 2200);
        }

        if (navigator.clipboard && navigator.clipboard.writeText) {
            navigator.clipboard.writeText(text).then(onCopied).catch(fallback);
        } else {
            fallback();
        }

        function fallback() {
            try {
                const ta = document.createElement('textarea');
                ta.value = text;
                ta.style.position = 'fixed';
                ta.style.opacity = '0';
                document.body.appendChild(ta);
                ta.select();
                const success = document.execCommand('copy');
                document.body.removeChild(ta);
                if (success) onCopied();
            } catch (e) {
                console.warn('[Copy] Failed to copy:', e);
            }
        }
    }

    function openDirectStreamModal() {
        if (!directStreamModal) return;
        updateAllDirectStreamUrls();
        directStreamModal.style.display = 'flex';
        setTimeout(function () {
            if (directStreamUrlInput) {
                directStreamUrlInput.focus();
                directStreamUrlInput.select();
            }
        }, 50);
    }

    function closeDirectStreamModal() {
        if (directStreamModal) {
            directStreamModal.style.display = 'none';
        }
    }

    window.openDirectStreamModal = openDirectStreamModal;
    window.closeDirectStreamModal = closeDirectStreamModal;
    window.getAbsoluteStreamUrl = getAbsoluteStreamUrl;
    window.updateAllDirectStreamUrls = updateAllDirectStreamUrls;

    if (directStreamBtn) {
        directStreamBtn.addEventListener('click', openDirectStreamModal);
    }
    if (directStreamPill) {
        directStreamPill.addEventListener('click', openDirectStreamModal);
    }
    if (directStreamCloseBtn) {
        directStreamCloseBtn.addEventListener('click', closeDirectStreamModal);
    }
    if (directStreamDoneBtn) {
        directStreamDoneBtn.addEventListener('click', closeDirectStreamModal);
    }

    if (directStreamModal) {
        directStreamModal.addEventListener('click', function (e) {
            if (e.target === directStreamModal) {
                closeDirectStreamModal();
            }
        });
    }

    if (directStreamUrlInput) {
        directStreamUrlInput.addEventListener('click', function () {
            this.select();
        });
    }

    if (directStreamCopyBtn && directStreamUrlInput) {
        directStreamCopyBtn.addEventListener('click', function () {
            const urlToCopy = directStreamUrlInput.value || getAbsoluteStreamUrl();
            copyTextToClipboard(urlToCopy, directStreamCopyBtn, directStreamCopyBtnText, 'Copy URL');
        });
    }

    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' && directStreamModal && directStreamModal.style.display === 'flex') {
            closeDirectStreamModal();
        }
    });

    // Populate all direct stream URL fields immediately on page load based on the current browser URL
    updateAllDirectStreamUrls();

    // Progressive Web App (PWA) Support
    if ('serviceWorker' in navigator) {
        window.addEventListener('load', function () {
            navigator.serviceWorker.register('/sw.js').then(function (reg) {
                console.log('[PWA] ServiceWorker registered with scope:', reg.scope);
                reg.update();
            }).catch(function (err) {
                console.warn('[PWA] ServiceWorker registration:', err);
            });
        });
    }

    let deferredPrompt = null;
    window.addEventListener('beforeinstallprompt', function (e) {
        e.preventDefault();
        deferredPrompt = e;
        if (pwaInstallBtn) {
            pwaInstallBtn.style.display = 'inline-flex';
        }
    });

    if (pwaInstallBtn) {
        pwaInstallBtn.addEventListener('click', function () {
            if (!deferredPrompt) {
                return;
            }
            deferredPrompt.prompt();
            deferredPrompt.userChoice.then(function (choiceResult) {
                if (choiceResult.outcome === 'accepted') {
                    console.log('[PWA] User accepted installation');
                }
                deferredPrompt = null;
                pwaInstallBtn.style.display = 'none';
            });
        });
    }

    window.addEventListener('appinstalled', function () {
        console.log('[PWA] App installed successfully');
        if (pwaInstallBtn) {
            pwaInstallBtn.style.display = 'none';
        }
    });

    // =========================================================================
    // Dynamic Ambient Backdrop Module (Spotify / Apple Music Style)
    // =========================================================================
    function extractDominantColors(imgSrc, callback) {
        if (!imgSrc) return;
        const img = new Image();
        img.crossOrigin = 'Anonymous';
        img.onload = function () {
            try {
                const canvas = document.createElement('canvas');
                canvas.width = 32;
                canvas.height = 32;
                const ctx = canvas.getContext('2d');
                ctx.drawImage(img, 0, 0, 32, 32);
                const data = ctx.getImageData(0, 0, 32, 32).data;

                let rTotal = 0, gTotal = 0, bTotal = 0, count = 0;
                let maxSat = 0;
                let accent = { r: 0, g: 210, b: 255 };

                for (let i = 0; i < data.length; i += 16) {
                    const r = data[i];
                    const g = data[i + 1];
                    const b = data[i + 2];
                    const a = data[i + 3];
                    if (a < 128) continue;

                    rTotal += r;
                    gTotal += g;
                    bTotal += b;
                    count++;

                    const max = Math.max(r, g, b);
                    const min = Math.min(r, g, b);
                    const delta = max - min;
                    if (delta > maxSat && max > 45 && min < 215) {
                        maxSat = delta;
                        accent = { r: r, g: g, b: b };
                    }
                }

                if (count > 0) {
                    const avgR = Math.round(rTotal / count);
                    const avgG = Math.round(gTotal / count);
                    const avgB = Math.round(bTotal / count);
                    callback({
                        c1: `rgba(${avgR}, ${avgG}, ${avgB}, 0.52)`,
                        c2: `rgba(${accent.r}, ${accent.g}, ${accent.b}, 0.44)`
                    });
                }
            } catch (e) {
                console.warn('[Ambient] Backdrop color extraction skipped:', e);
            }
        };
        img.onerror = function (err) {
            console.warn('[Ambient] Could not load image for backdrop extraction:', err);
        };
        const sep = imgSrc.includes('?') ? '&' : '?';
        img.src = imgSrc + (imgSrc.includes('cb=') ? '' : `${sep}cb=${Date.now()}`);
    }

    function applyDynamicAmbientBackdrop(albumArtUrl) {
        if (!currentBranding || !currentBranding.enableDynamicBackdrop) {
            document.body.classList.remove('has-ambient-backdrop');
            document.body.style.removeProperty('--ambient-c1');
            document.body.style.removeProperty('--ambient-c2');
            document.documentElement.style.removeProperty('--ambient-c1');
            document.documentElement.style.removeProperty('--ambient-c2');
            return;
        }

        if (!albumArtUrl) {
            const artEl = document.getElementById('albumArt');
            if (artEl && artEl.style.backgroundImage) {
                const match = artEl.style.backgroundImage.match(/url\(['"]?([^'"]+)['"]?\)/);
                if (match && match[1] && !match[1].includes('linear-gradient')) {
                    albumArtUrl = match[1];
                }
            }
        }

        if (!albumArtUrl) return;

        extractDominantColors(albumArtUrl, function (colors) {
            if (!currentBranding || !currentBranding.enableDynamicBackdrop) return;
            document.body.style.setProperty('--ambient-c1', colors.c1);
            document.body.style.setProperty('--ambient-c2', colors.c2);
            document.documentElement.style.setProperty('--ambient-c1', colors.c1);
            document.documentElement.style.setProperty('--ambient-c2', colors.c2);
            document.body.classList.add('has-ambient-backdrop');
        });
    }

    // =========================================================================
    // Party / TV Full-Screen Mode Module
    // =========================================================================
    const tvModeBtn = document.getElementById('tvModeBtn');
    let tvIdleTimer = null;

    function resetTvIdleTimer() {
        if (!document.body.classList.contains('tv-mode')) return;
        document.body.classList.remove('tv-idle');
        clearTimeout(tvIdleTimer);
        tvIdleTimer = setTimeout(function () {
            if (document.body.classList.contains('tv-mode')) {
                document.body.classList.add('tv-idle');
            }
        }, 3500);
    }

    if (tvModeBtn) {
        tvModeBtn.addEventListener('click', function () {
            const isTv = document.body.classList.toggle('tv-mode');
            if (isTv) {
                if (!document.fullscreenElement && document.documentElement.requestFullscreen) {
                    document.documentElement.requestFullscreen().catch(function () {});
                }
                resetTvIdleTimer();
                window.addEventListener('mousemove', resetTvIdleTimer);
                window.addEventListener('keydown', resetTvIdleTimer);
            } else {
                if (document.fullscreenElement && document.exitFullscreen) {
                    document.exitFullscreen().catch(function () {});
                }
                document.body.classList.remove('tv-idle');
                clearTimeout(tvIdleTimer);
                window.removeEventListener('mousemove', resetTvIdleTimer);
                window.removeEventListener('keydown', resetTvIdleTimer);
            }
        });
    }

    document.addEventListener('fullscreenchange', function () {
        if (!document.fullscreenElement && document.body.classList.contains('tv-mode')) {
            document.body.classList.remove('tv-mode', 'tv-idle');
            clearTimeout(tvIdleTimer);
        }
    });

    // =========================================================================
    // Live Synced & Plain Lyrics Module (LRCLIB Open API)
    // =========================================================================
    const lyricsToggleBtn = document.getElementById('lyricsToggleBtn');
    const lyricsDrawer = document.getElementById('lyricsDrawer');
    const lyricsOverlay = document.getElementById('lyricsOverlay');
    const lyricsCloseBtn = document.getElementById('lyricsCloseBtn');
    const lyricsBody = document.getElementById('lyricsBody');
    const lyricsTrackInfo = document.getElementById('lyricsTrackInfo');
    const lyricsTypeBadge = document.getElementById('lyricsTypeBadge');

    let currentLyrics = [];
    let isSyncedLyrics = false;
    let lastActiveLyricIdx = -1;
    let currentLyricsTrackKey = "";

    // Sync Offset Delay (audio stream buffering offset)
    let lyricsSyncOffset = 2.0;
    try {
        const savedOffset = localStorage.getItem('scrim_lyrics_offset');
        if (savedOffset !== null && !isNaN(parseFloat(savedOffset))) {
            lyricsSyncOffset = parseFloat(savedOffset);
        }
    } catch { }

    const lyricsSyncMinus = document.getElementById('lyricsSyncMinus');
    const lyricsSyncPlus = document.getElementById('lyricsSyncPlus');
    const lyricsSyncLabel = document.getElementById('lyricsSyncLabel');

    function updateLyricsSyncLabel() {
        if (!lyricsSyncLabel) return;
        const sign = lyricsSyncOffset > 0 ? '-' : (lyricsSyncOffset < 0 ? '+' : '');
        lyricsSyncLabel.textContent = `Sync: ${sign}${Math.abs(lyricsSyncOffset).toFixed(1)}s`;
    }

    if (lyricsSyncMinus) {
        lyricsSyncMinus.addEventListener('click', function (e) {
            e.stopPropagation();
            lyricsSyncOffset = Math.min(10.0, Math.round((lyricsSyncOffset + 0.5) * 10) / 10);
            try { localStorage.setItem('scrim_lyrics_offset', lyricsSyncOffset.toString()); } catch { }
            updateLyricsSyncLabel();
            lastActiveLyricIdx = -1;
        });
    }

    if (lyricsSyncPlus) {
        lyricsSyncPlus.addEventListener('click', function (e) {
            e.stopPropagation();
            lyricsSyncOffset = Math.max(-5.0, Math.round((lyricsSyncOffset - 0.5) * 10) / 10);
            try { localStorage.setItem('scrim_lyrics_offset', lyricsSyncOffset.toString()); } catch { }
            updateLyricsSyncLabel();
            lastActiveLyricIdx = -1;
        });
    }

    updateLyricsSyncLabel();

    function toggleLyricsDrawer(show) {
        const active = show !== undefined ? show : !(lyricsDrawer && lyricsDrawer.classList.contains('active'));
        if (lyricsDrawer) lyricsDrawer.classList.toggle('active', active);
        if (lyricsOverlay) lyricsOverlay.classList.toggle('active', active);
    }

    if (lyricsToggleBtn) lyricsToggleBtn.addEventListener('click', function () { toggleLyricsDrawer(); });
    if (lyricsCloseBtn) lyricsCloseBtn.addEventListener('click', function () { toggleLyricsDrawer(false); });
    if (lyricsOverlay) lyricsOverlay.addEventListener('click', function () { toggleLyricsDrawer(false); });

    function parseLrc(lrcText) {
        const lines = lrcText.split('\n');
        const result = [];
        const timeRegex = /\[(\d{2}):(\d{2})(?:\.(\d{2,3}))?\]/g;
        lines.forEach(line => {
            const matches = [...line.matchAll(timeRegex)];
            const text = line.replace(timeRegex, '').trim();
            if (!text) return;
            matches.forEach(m => {
                const mins = parseInt(m[1], 10);
                const secs = parseInt(m[2], 10);
                const ms = m[3] ? (m[3].length === 2 ? parseInt(m[3], 10) * 10 : parseInt(m[3], 10)) : 0;
                result.push({
                    time: mins * 60 + secs + (ms / 1000),
                    text: text
                });
            });
        });
        result.sort((a, b) => a.time - b.time);
        return result;
    }

    function renderNoLyrics(msg) {
        if (lyricsBody) lyricsBody.innerHTML = `<div class="lyrics-empty">${msg || 'No lyrics found for this track.'}</div>`;
        if (lyricsTypeBadge) lyricsTypeBadge.textContent = 'Not Found';
    }

    function renderLyricsLines(lines) {
        if (!lyricsBody) return;
        lyricsBody.innerHTML = '';
        if (lines.length === 0) {
            renderNoLyrics();
            return;
        }
        lines.forEach((item, idx) => {
            const p = document.createElement('p');
            p.className = 'lyric-line';
            p.id = `lyric-line-${idx}`;
            p.textContent = item.text;
            lyricsBody.appendChild(p);
        });
    }

    function fetchLyrics(title, artist, duration) {
        if (!title || title === "Awaiting Track Info...") return;
        const trackKey = (title + "::" + (artist || "")).toLowerCase();
        if (trackKey === currentLyricsTrackKey) return;
        currentLyricsTrackKey = trackKey;

        if (lyricsTrackInfo) lyricsTrackInfo.textContent = `${title} • ${artist || 'Unknown'}`;
        if (lyricsBody) lyricsBody.innerHTML = '<div class="lyrics-empty">Searching lyrics on LRCLIB...</div>';
        currentLyrics = [];
        isSyncedLyrics = false;
        lastActiveLyricIdx = -1;

        const durParam = (duration && duration > 0) ? `&duration=${Math.round(duration)}` : '';
        const url = `https://lrclib.net/api/get?artist_name=${encodeURIComponent(artist || '')}&track_name=${encodeURIComponent(title)}${durParam}`;

        fetch(url)
            .then(res => {
                if (!res.ok) throw new Error('Lyrics not found');
                return res.json();
            })
            .then(data => {
                if (data.syncedLyrics) {
                    currentLyrics = parseLrc(data.syncedLyrics);
                    isSyncedLyrics = true;
                    if (lyricsTypeBadge) lyricsTypeBadge.textContent = 'Synced';
                    renderLyricsLines(currentLyrics);
                } else if (data.plainLyrics) {
                    const plainLines = data.plainLyrics.split('\n').filter(l => l.trim().length > 0);
                    isSyncedLyrics = false;
                    if (lyricsTypeBadge) lyricsTypeBadge.textContent = 'Plain';
                    renderLyricsLines(plainLines.map(t => ({ text: t })));
                } else {
                    throw new Error('Empty lyrics');
                }
            })
            .catch(() => {
                // Fallback to broad search if exact lookup missed
                fetch(`https://lrclib.net/api/search?q=${encodeURIComponent(title + ' ' + (artist || ''))}`)
                    .then(res => res.json())
                    .then(list => {
                        if (Array.isArray(list) && list.length > 0) {
                            const item = list[0];
                            if (item.syncedLyrics) {
                                currentLyrics = parseLrc(item.syncedLyrics);
                                isSyncedLyrics = true;
                                if (lyricsTypeBadge) lyricsTypeBadge.textContent = 'Synced';
                                renderLyricsLines(currentLyrics);
                                return;
                            } else if (item.plainLyrics) {
                                const plainLines = item.plainLyrics.split('\n').filter(l => l.trim().length > 0);
                                isSyncedLyrics = false;
                                if (lyricsTypeBadge) lyricsTypeBadge.textContent = 'Plain';
                                renderLyricsLines(plainLines.map(t => ({ text: t })));
                                return;
                            }
                        }
                        renderNoLyrics();
                    })
                    .catch(() => renderNoLyrics());
            });
    }

    function syncLyricsPosition(currentTimeSec) {
        if (!isSyncedLyrics || currentLyrics.length === 0 || !lyricsDrawer || !lyricsDrawer.classList.contains('active')) return;
        const effectiveTime = Math.max(0, currentTimeSec - lyricsSyncOffset);
        let activeIdx = -1;
        for (let i = 0; i < currentLyrics.length; i++) {
            if (effectiveTime >= currentLyrics[i].time) {
                activeIdx = i;
            } else {
                break;
            }
        }
        if (activeIdx !== lastActiveLyricIdx && activeIdx >= 0) {
            if (lastActiveLyricIdx >= 0) {
                const oldEl = document.getElementById(`lyric-line-${lastActiveLyricIdx}`);
                if (oldEl) oldEl.classList.remove('active');
            }
            const newEl = document.getElementById(`lyric-line-${activeIdx}`);
            if (newEl) {
                newEl.classList.add('active');
                newEl.scrollIntoView({ behavior: 'smooth', block: 'center' });
            }
            lastActiveLyricIdx = activeIdx;
        }
    }

    // =========================================================================
    // Station Schedule & Broadcaster Bio Module
    // =========================================================================
    const stationModalOverlay = document.getElementById('stationModalOverlay');
    const stationModalCloseBtn = document.getElementById('stationModalCloseBtn');
    const stationBioSection = document.getElementById('stationBioSection');
    const stationBioText = document.getElementById('stationBioText');
    const stationScheduleSection = document.getElementById('stationScheduleSection');
    const stationScheduleText = document.getElementById('stationScheduleText');
    const stationSocialsSection = document.getElementById('stationSocialsSection');
    const stationSocialRow = document.getElementById('stationSocialRow');

    function toggleStationModal(show) {
        const active = show !== undefined ? show : !(stationModalOverlay && stationModalOverlay.classList.contains('active'));
        if (stationModalOverlay) {
            stationModalOverlay.style.display = active ? 'flex' : 'none';
            stationModalOverlay.classList.toggle('active', active);
        }
    }

    if (stationModalCloseBtn) stationModalCloseBtn.addEventListener('click', function () { toggleStationModal(false); });
    if (stationModalOverlay) {
        stationModalOverlay.addEventListener('click', function (e) {
            if (e.target === stationModalOverlay) toggleStationModal(false);
        });
    }

    function updateStationModalContent(branding) {
        if (!branding) return;

        // Bio section
        if (stationBioSection && stationBioText) {
            if (branding.broadcasterBio && branding.broadcasterBio.trim()) {
                stationBioText.textContent = branding.broadcasterBio.trim();
                stationBioSection.style.display = 'block';
            } else {
                stationBioSection.style.display = 'none';
            }
        }

        // Schedule section
        if (stationScheduleSection && stationScheduleText) {
            if (branding.scheduleDescription && branding.scheduleDescription.trim()) {
                stationScheduleText.textContent = branding.scheduleDescription.trim();
                stationScheduleSection.style.display = 'block';
            } else {
                stationScheduleSection.style.display = 'none';
            }
        }

        // Socials section
        if (stationSocialsSection && stationSocialRow) {
            stationSocialRow.innerHTML = '';
            let hasSocials = false;

            if (branding.socialDiscord && branding.socialDiscord.trim()) {
                hasSocials = true;
                const dUrl = branding.socialDiscord.startsWith('http') ? branding.socialDiscord : `https://${branding.socialDiscord}`;
                stationSocialRow.innerHTML += `<a href="${dUrl}" target="_blank" rel="noopener noreferrer" class="station-social-btn">💬 Discord</a>`;
            }
            if (branding.socialTwitch && branding.socialTwitch.trim()) {
                hasSocials = true;
                const tUrl = branding.socialTwitch.startsWith('http') ? branding.socialTwitch : `https://twitch.tv/${branding.socialTwitch.replace('@', '')}`;
                stationSocialRow.innerHTML += `<a href="${tUrl}" target="_blank" rel="noopener noreferrer" class="station-social-btn">🟣 Twitch</a>`;
            }
            if (branding.socialTwitter && branding.socialTwitter.trim()) {
                hasSocials = true;
                const xUrl = branding.socialTwitter.startsWith('http') ? branding.socialTwitter : `https://x.com/${branding.socialTwitter.replace('@', '')}`;
                stationSocialRow.innerHTML += `<a href="${xUrl}" target="_blank" rel="noopener noreferrer" class="station-social-btn">✖ Twitter / X</a>`;
            }

            stationSocialsSection.style.display = hasSocials ? 'block' : 'none';
        }
    }

    // =========================================================================
    // DJ Live Polls & Track Battles Module
    // =========================================================================
    const livePollContainer = document.getElementById('livePollContainer');
    let votedPollIds = new Set();

    function escapePollHtml(str) {
        if (!str) return '';
        return str.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
    }

    function renderLivePoll(poll) {
        if (!livePollContainer) return;
        if (!poll || !poll.id || !poll.options || poll.options.length === 0) {
            livePollContainer.style.display = 'none';
            livePollContainer.innerHTML = '';
            return;
        }

        const hasVoted = votedPollIds.has(poll.id);
        const totalVotes = poll.totalVotes || 0;

        let optionsHtml = '';
        poll.options.forEach((opt, idx) => {
            const pct = totalVotes > 0 ? Math.round(((opt.votes || 0) / totalVotes) * 100) : 0;
            optionsHtml += `
                <button type="button" class="poll-option-btn" data-poll-id="${poll.id}" data-opt-idx="${idx}" ${hasVoted ? 'disabled' : ''}>
                    <div class="poll-option-bar" style="width: ${hasVoted ? pct : 0}%"></div>
                    <span class="poll-option-text">${escapePollHtml(opt.text)}</span>
                    ${hasVoted ? `<span class="poll-option-pct">${pct}% (${opt.votes || 0})</span>` : ''}
                </button>
            `;
        });

        livePollContainer.innerHTML = `
            <div class="live-poll-card">
                <div class="poll-header">
                    <span class="poll-badge">⚡ LIVE POLL</span>
                    <span style="font-size: 11px; color: var(--text-dim, #64748b);">${hasVoted ? 'Vote Recorded' : 'Tap to Vote'}</span>
                </div>
                <div class="poll-question">${escapePollHtml(poll.question)}</div>
                <div class="poll-options-list">${optionsHtml}</div>
                <div class="poll-total-votes">${totalVotes} total ${totalVotes === 1 ? 'vote' : 'votes'}</div>
            </div>
        `;
        livePollContainer.style.display = 'block';

        if (!hasVoted) {
            livePollContainer.querySelectorAll('.poll-option-btn').forEach(btn => {
                btn.addEventListener('click', function () {
                    const pId = this.getAttribute('data-poll-id');
                    const optIdx = parseInt(this.getAttribute('data-opt-idx'), 10);
                    submitPollVote(pId, optIdx);
                });
            });
        }
    }

    function removeLivePoll() {
        if (livePollContainer) {
            livePollContainer.style.display = 'none';
            livePollContainer.innerHTML = '';
        }
    }

    function submitPollVote(pollId, optionIndex) {
        votedPollIds.add(pollId);
        fetch('/api/poll/vote', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ pollId: pollId, optionIndex: optionIndex })
        }).catch(err => console.warn('Vote submission note:', err));
    }

    // =========================================================================
    // PARTY HUB, B2B STAGE PRESENCE & CELEBRATION MODULE
    // =========================================================================
    let lastActiveDj = null;
    let toastTimeout = null;

    function updatePartyHubBranding(branding) {
        if (!branding) return;

        const badge = document.getElementById('decksPresenceBadge');
        const hostAvatar = document.getElementById('hostDjAvatar');
        const guestAvatar = document.getElementById('guestDjAvatar');
        const activeName = document.getElementById('activeDjName');
        const guestBioBtn = document.getElementById('guestBioBtn');
        const stageLabel = document.getElementById('stageLabel');

        if (!badge) return;

        const hostName = (branding.hostDj && branding.hostDj.name) || branding.hostName || 'Host DJ';
        const hostAvatarUrl = (branding.hostDj && branding.hostDj.avatarUrl) ? branding.hostDj.avatarUrl : (branding.logoUrl || '/icon.svg');

        if (branding.isPartyHub && branding.guestDj) {
            badge.style.display = 'inline-flex';
            if (hostAvatar) {
                hostAvatar.src = hostAvatarUrl;
                hostAvatar.title = hostName;
                hostAvatar.classList.remove('b2b-active');
                hostAvatar.onerror = function () { this.onerror = null; this.src = '/icon.svg'; };
            }
            if (guestAvatar) {
                guestAvatar.src = branding.guestDj.avatarUrl || '/icon.svg';
                guestAvatar.title = branding.guestDj.name || 'Guest DJ';
                guestAvatar.style.display = 'block';
                guestAvatar.classList.add('b2b-active');
                guestAvatar.onerror = function () { this.onerror = null; this.src = '/icon.svg'; };
            }
            if (activeName) {
                activeName.textContent = branding.guestDj.name || 'Guest DJ';
            }
            if (stageLabel) {
                stageLabel.textContent = 'GUEST ON THE DECKS';
            }
            if (guestBioBtn) {
                guestBioBtn.style.display = 'inline-block';
            }

            // Detect DJ Hand-off
            if (lastActiveDj !== null && lastActiveDj !== branding.guestDj.name) {
                showDjHandoffToast(`Guest DJ: ${branding.guestDj.name}`);
            }
            lastActiveDj = branding.guestDj.name;
        } else {
            // Normal solo broadcast
            if (lastActiveDj !== null && lastActiveDj !== hostName && branding.isPartyHub === false) {
                showDjHandoffToast(`Decks returned to: ${hostName}`);
            }
            lastActiveDj = hostName;

            if (guestAvatar) {
                guestAvatar.style.display = 'none';
                guestAvatar.classList.remove('b2b-active');
            }
            if (hostAvatar) {
                hostAvatar.src = hostAvatarUrl;
                hostAvatar.title = hostName;
                hostAvatar.classList.add('b2b-active');
                hostAvatar.onerror = function () { this.onerror = null; this.src = '/icon.svg'; };
            }
            if (guestBioBtn) {
                guestBioBtn.style.display = 'none';
            }
            if (activeName) {
                activeName.textContent = hostName;
            }
            if (stageLabel) {
                stageLabel.textContent = 'NOW ON THE DECKS';
            }
            badge.style.display = 'inline-flex';
        }
    }

    function showDjHandoffToast(msg) {
        const toast = document.getElementById('djHandoffToast');
        const msgEl = document.getElementById('djHandoffMsg');
        if (!toast || !msgEl) return;

        msgEl.textContent = msg;
        toast.style.display = 'flex';

        if (toastTimeout) clearTimeout(toastTimeout);
        toastTimeout = setTimeout(() => {
            toast.style.display = 'none';
        }, 5000);
    }

    function openGuestDjModal() {
        if (!currentBranding || !currentBranding.guestDj) return;
        const modal = document.getElementById('guestDjModalOverlay');
        const avatar = document.getElementById('guestDjModalAvatar');
        const name = document.getElementById('guestDjModalName');
        const bio = document.getElementById('guestDjBioText');
        const socialsSec = document.getElementById('guestDjSocialsSection');
        const socialRow = document.getElementById('guestDjSocialRow');

        if (!modal) return;

        const g = currentBranding.guestDj;
        if (avatar) avatar.src = g.avatarUrl || '/icon.svg';
        if (name) name.textContent = g.name || 'Guest DJ';
        if (bio) bio.textContent = g.bio || 'Special guest session on the decks.';

        if (socialRow && socialsSec) {
            socialRow.innerHTML = '';
            let hasSocials = false;

            if (g.discord && g.discord.trim()) {
                const url = g.discord.startsWith('http') ? g.discord : `https://discord.gg/${g.discord.replace(/^#/, '')}`;
                const a = document.createElement('a');
                a.href = url;
                a.target = '_blank';
                a.rel = 'noopener noreferrer';
                a.className = 'station-social-btn discord';
                a.textContent = 'Discord';
                socialRow.appendChild(a);
                hasSocials = true;
            }
            if (g.twitch && g.twitch.trim()) {
                const url = g.twitch.startsWith('http') ? g.twitch : `https://twitch.tv/${g.twitch.replace(/^@/, '')}`;
                const a = document.createElement('a');
                a.href = url;
                a.target = '_blank';
                a.rel = 'noopener noreferrer';
                a.className = 'station-social-btn twitch';
                a.textContent = 'Twitch';
                socialRow.appendChild(a);
                hasSocials = true;
            }
            if (g.twitter && g.twitter.trim()) {
                const url = g.twitter.startsWith('http') ? g.twitter : `https://x.com/${g.twitter.replace(/^@/, '')}`;
                const a = document.createElement('a');
                a.href = url;
                a.target = '_blank';
                a.rel = 'noopener noreferrer';
                a.className = 'station-social-btn twitter';
                a.textContent = 'X (Twitter)';
                socialRow.appendChild(a);
                hasSocials = true;
            }

            socialsSec.style.display = hasSocials ? 'block' : 'none';
        }

        modal.style.display = 'flex';
    }

    function closeGuestDjModal() {
        const modal = document.getElementById('guestDjModalOverlay');
        if (modal) modal.style.display = 'none';
    }

    const guestBioBtn = document.getElementById('guestBioBtn');
    if (guestBioBtn) guestBioBtn.addEventListener('click', openGuestDjModal);

    const guestAvatar = document.getElementById('guestDjAvatar');
    if (guestAvatar) guestAvatar.addEventListener('click', openGuestDjModal);

    const guestCloseBtn = document.getElementById('guestDjModalCloseBtn');
    if (guestCloseBtn) guestCloseBtn.addEventListener('click', closeGuestDjModal);

    const guestModal = document.getElementById('guestDjModalOverlay');
    if (guestModal) {
        guestModal.addEventListener('click', (e) => {
            if (e.target === guestModal) closeGuestDjModal();
        });
    }

    // Party Celebration Pyro & Confetti Burst
    function triggerPartyCelebration(sender) {
        const layer = document.getElementById('partyParticlesLayer');
        if (!layer) return;

        const emojis = ['🎉', '✨', '🔥', '⚡', '💖', '💥', '🎵', '🎧', '🙌', '🌟'];
        const count = 30;

        for (let i = 0; i < count; i++) {
            const p = document.createElement('span');
            p.className = 'party-particle';
            p.textContent = emojis[Math.floor(Math.random() * emojis.length)];

            const left = Math.random() * 96 + 2; // 2% to 98%
            const duration = 1.8 + Math.random() * 1.4; // 1.8s - 3.2s
            const delay = Math.random() * 0.4;
            const size = 18 + Math.random() * 22; // 18px - 40px

            p.style.left = `${left}%`;
            p.style.fontSize = `${size}px`;
            p.style.animationDuration = `${duration}s`;
            p.style.animationDelay = `${delay}s`;

            layer.appendChild(p);

            setTimeout(() => {
                if (p.parentNode) p.parentNode.removeChild(p);
            }, (duration + delay + 0.2) * 1000);
        }
    }

    const btnReactionParty = document.getElementById('btnReactionParty');
    if (btnReactionParty) {
        btnReactionParty.addEventListener('click', () => {
            btnReactionParty.classList.add('pop');
            setTimeout(() => btnReactionParty.classList.remove('pop'), 200);

            triggerPartyCelebration('You');

            fetch('/api/party/celebrate', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ sender: 'Listener' })
            }).catch(err => console.warn('Celebration note:', err));
        });
    }

    // Multi-Station Console / Channel Dial Management
    function loadStations() {
        fetch('/api/stations')
            .then(res => res.ok ? res.json() : [])
            .then(stations => {
                if (!Array.isArray(stations)) return;
                stationsList = stations;
                renderStationDial(stations);
                if (stations.length > 0 && !activeStationMount) {
                    const first = stations[0];
                    activeStationMount = (first.mount || 'stream').replace(/^\/+/, '');
                    currentStreamEndpoint = '/' + activeStationMount;
                    renderStationDial(stations);
                }
                const activeSt = stations.find(s => (s.mount || '').replace(/^\/+/, '').toLowerCase() === (activeStationMount || '').toLowerCase()) || stations[0];
                if (activeSt) {
                    currentStreamEndpoint = '/' + (activeSt.mount || activeStationMount || 'stream').replace(/^\/+/, '');
                    updatePwaManifest(activeStationMount, activeSt.stationName || activeSt.name);
                }
            })
            .catch(() => {});
    }

    function renderStationDial(stations) {
        const dial = document.getElementById('stationChannelDial');
        const pillsContainer = document.getElementById('channelDialPills');
        if (!dial || !pillsContainer) return;

        if (!stations || stations.length <= 1) {
            dial.style.display = 'none';
            return;
        }

        dial.style.display = 'flex';
        pillsContainer.innerHTML = '';

        stations.forEach(st => {
            const btn = document.createElement('button');
            btn.type = 'button';
            btn.className = 'channel-pill';
            const m = (st.mount || 'stream').replace(/^\/+/, '');
            const isActive = activeStationMount
                ? (activeStationMount.toLowerCase() === m.toLowerCase())
                : (m === (currentStreamEndpoint || '').replace(/^\/+/, '').toLowerCase());

            if (isActive) {
                btn.classList.add('active');
            }

            const liveDot = document.createElement('span');
            liveDot.className = 'channel-pill-dot ' + (st.isLive ? 'live' : 'offline');

            const nameSpan = document.createElement('span');
            nameSpan.className = 'channel-pill-name';
            nameSpan.textContent = st.name || st.stationName || m;

            btn.appendChild(liveDot);
            btn.appendChild(nameSpan);

            btn.addEventListener('click', () => {
                switchToStation(st);
            });

            pillsContainer.appendChild(btn);
        });
    }

    function switchToStation(station) {
        const mount = (station.mount || 'stream').replace(/^\/+/, '');
        if (activeStationMount && activeStationMount.toLowerCase() === mount.toLowerCase()) return;

        activeStationMount = mount;
        currentStreamEndpoint = '/' + mount;

        updatePwaManifest(mount, station.stationName || station.name);

        try {
            const newUrl = new URL(window.location.href);
            newUrl.searchParams.set('station', mount);
            window.history.replaceState({}, '', newUrl);
        } catch (e) {}

        renderStationDial(stationsList);

        if (isPlaying || isUserPlaying) {
            stopStream();
            startStream(false);
        } else {
            audio.removeAttribute('src');
        }

        refreshStationData();
    }

    function refreshStationData() {
        connectEventSource();

        fetch(getStationUrl('/api/branding'))
            .then(res => res.json())
            .then(applyBranding)
            .catch(() => {});

        fetch(getStationUrl('/api/status'))
            .then(res => res.json())
            .then(handleStatusData)
            .catch(() => {});

        fetch(getStationUrl('/api/metadata'))
            .then(r => r.json())
            .then(data => {
                if (data && data.title) {
                    updateTrackMetadata(data.title, data.artist, data.album, data.albumArtUrl, data.hasArt, data.duration, data.position, data.isPlaying);
                }
            })
            .catch(() => {});

        loadChatState();
        refreshReactionsState();
        fetchSongHistory();
    }

    // Initialize Stations Dial
    loadStations();
});
