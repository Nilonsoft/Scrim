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
    const trackTitle = document.getElementById('trackTitle');
    const trackArtist = document.getElementById('trackArtist');
    const bottomTrackName = document.getElementById('bottomTrackName');
    const listenerCount = document.getElementById('listenerCount');
    const clockTime = document.getElementById('clockTime');
    const requestForm = document.getElementById('requestForm');
    const requestInput = document.getElementById('requestInput');
    const requestSuccess = document.getElementById('requestSuccess');
    const queueList = document.getElementById('queueList');

    let isPlaying = false;
    let isConnecting = false;
    let isUserPlaying = false;
    let defaultSubtitle = "Live Broadcast";

    function updatePlayButtonUI() {
        if (!playBtn) return;
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
    const modeButtons = document.querySelectorAll('.vis-mode-btn');
    let currentVisMode = localStorage.getItem('scrim_vis_mode') || 'bars';

    function setVisMode(mode) {
        currentVisMode = mode;
        try {
            localStorage.setItem('scrim_vis_mode', mode);
        } catch (e) {}
        modeButtons.forEach(btn => {
            if (btn.dataset.mode === mode) {
                btn.classList.add('active');
            } else {
                btn.classList.remove('active');
            }
        });
    }

    modeButtons.forEach(btn => {
        btn.addEventListener('click', function () {
            setVisMode(this.dataset.mode);
        });
    });
    setVisMode(currentVisMode);

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

        if (currentVisMode === 'bars') {
            renderBarsMode(ctx, w, h, dims.dpr, accent, hasData);
        } else if (currentVisMode === 'wave') {
            renderWaveMode(ctx, w, h, dims.dpr, accent, hasData);
        } else if (currentVisMode === 'spectrum') {
            renderSpectrumMode(ctx, w, h, dims.dpr, accent, hasData);
        } else if (currentVisMode === 'pulse') {
            renderPulseMode(ctx, w, h, dims.dpr, accent, hasData);
        }
    }

    animateWaveform();

    // Live Stream Play / Stop Controls (Strictly for Web User)
    function stopStream() {
        isUserPlaying = false;
        isPlaying = false;
        isConnecting = false;
        updatePlayButtonUI();

        if (audio) {
            audio.pause();
            audio.removeAttribute('src'); // Stop streaming connection immediately
            audio.load(); // Tell browser to drop the pending HTTP connection
        }
    }

    function startStream() {
        isUserPlaying = true;
        isConnecting = true;
        updatePlayButtonUI();

        initAudioVisualizer();
        if (audioCtx && audioCtx.state === 'suspended') {
            audioCtx.resume();
        }

        const subtitleEl = document.getElementById('showSubtitle');
        if (subtitleEl) subtitleEl.textContent = "Connecting to live audio stream...";

        // Always load a fresh live connection with timestamp cache-buster so stream is real-time
        audio.src = '/stream?t=' + Date.now();
        audio.load();

        var playPromise = audio.play();
        if (playPromise !== undefined) {
            playPromise.then(function () {
                if (isUserPlaying) {
                    isPlaying = true;
                    isConnecting = false;
                    updatePlayButtonUI();
                    if (subtitleEl) subtitleEl.textContent = defaultSubtitle;
                }
            }).catch(function (err) {
                console.warn("Playback could not start:", err);
                if (isUserPlaying) {
                    isUserPlaying = false;
                    isPlaying = false;
                    isConnecting = false;
                    updatePlayButtonUI();
                    audio.removeAttribute('src');
                    if (subtitleEl) {
                        subtitleEl.textContent = "Station Offline — Broadcaster has not started transmission";
                    }
                }
            });
        }
    }

    if (playBtn && audio) {
        playBtn.addEventListener('click', function () {
            if (isUserPlaying || isPlaying || isConnecting) {
                stopStream();
                const subtitleEl = document.getElementById('showSubtitle');
                if (subtitleEl) subtitleEl.textContent = defaultSubtitle;
            } else {
                startStream();
            }
        });

        audio.addEventListener('playing', function () {
            if (isUserPlaying) {
                isPlaying = true;
                isConnecting = false;
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

        audio.addEventListener('error', function (e) {
            // Ignore error events triggered when user intentionally stopped or src is cleared
            if (!isUserPlaying || !audio.getAttribute('src')) {
                return;
            }
            console.warn("Audio element stream error or station offline:", e);
            isUserPlaying = false;
            isPlaying = false;
            isConnecting = false;
            updatePlayButtonUI();
            audio.removeAttribute('src');

            const subtitleEl = document.getElementById('showSubtitle');
            if (subtitleEl) {
                subtitleEl.textContent = "Station Offline — Broadcaster has not started transmission";
            }
        });
    }

    // Dynamic Branding Function
    function applyBranding(branding) {
        if (!branding) return;

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

        if (branding.customNavLinks && Array.isArray(branding.customNavLinks) && branding.customNavLinks.length > 0) {
            const navContainer = document.getElementById('navLinksContainer');
            if (navContainer) {
                navContainer.innerHTML = '';
                branding.customNavLinks.forEach(link => {
                    const a = document.createElement('a');
                    a.href = link.url || '#';
                    if (link.url && (link.url.startsWith('http://') || link.url.startsWith('https://'))) {
                        a.target = '_blank';
                        a.rel = 'noopener noreferrer';
                    }
                    a.className = 'nav-link';
                    a.textContent = link.label;
                    navContainer.appendChild(a);
                });
            }
        } else if (branding.navLinks) {
            const navContainer = document.getElementById('navLinksContainer');
            if (navContainer) {
                const links = branding.navLinks.split(',').map(s => s.trim()).filter(Boolean);
                if (links.length > 0) {
                    navContainer.innerHTML = '';
                    links.forEach(link => {
                        const a = document.createElement('a');
                        a.href = '#' + link.toLowerCase().replace(/\s+/g, '-');
                        a.className = 'nav-link';
                        a.textContent = link;
                        navContainer.appendChild(a);
                    });
                }
            }
        }
    }

    function updateLiveIndicator(isLive) {
        const onAirPill = document.getElementById('onAirPill');
        if (!onAirPill) return;
        const pillText = onAirPill.querySelector('.pill-text');
        if (isLive) {
            onAirPill.classList.remove('offline');
            if (pillText) pillText.textContent = 'ON AIR';
        } else {
            onAirPill.classList.add('offline');
            if (pillText) pillText.textContent = 'OFFLINE';
        }
    }

    // Initial branding and status load
    fetch('/api/branding')
        .then(res => res.json())
        .then(applyBranding)
        .catch(e => console.warn("Could not fetch branding", e));

    fetch('/api/status')
        .then(res => res.json())
        .then(function (data) {
            if (data && data.isLive !== undefined) {
                updateLiveIndicator(data.isLive);
            }
        })
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

    // Initial Metadata Fetch
    fetch('/api/metadata')
        .then(function (r) { return r.json(); })
        .then(function (data) {
            if (data && data.title) {
                updateTrackMetadata(data.title, data.artist, data.album, data.albumArtUrl, data.hasArt, data.duration, data.position, data.isPlaying);
            }
        })
        .catch(function (e) { console.warn("Could not fetch initial metadata", e); });

    function updateTrackMetadata(title, artist, album, albumArtUrl, hasArt, duration, position, isPlaying) {
        const albumArt = document.getElementById('albumArt');
        const metaContainer = document.getElementById('metaContainer');
        const albumBadge = document.getElementById('albumBadge');
        const albumSub = document.getElementById('albumSub');

        const cleanTitle = (title || "").trim();
        const hasRealTrack = cleanTitle !== "" && 
            cleanTitle !== "Awaiting Audio Source..." && 
            cleanTitle !== "Awaiting Track Info..." && 
            cleanTitle !== "Unknown Track" &&
            cleanTitle.toLowerCase() !== "stream" &&
            cleanTitle.toLowerCase() !== "stream.mp3" &&
            cleanTitle.toLowerCase() !== "live" &&
            cleanTitle.toLowerCase() !== "listen" &&
            !cleanTitle.startsWith("http://") &&
            !cleanTitle.startsWith("https://") &&
            !cleanTitle.includes("localhost:") &&
            !cleanTitle.includes("127.0.0.1:");

        if (hasRealTrack) {
            // Real track info received: reveal actual artwork and clean typography
            if (albumArt) {
                albumArt.classList.remove('loading');
                if (hasArt && albumArtUrl) {
                    const cacheBuster = albumArtUrl.startsWith('/') ? (albumArtUrl.includes('?') ? '&' : '?') + 't=' + Date.now() : '';
                    albumArt.style.backgroundImage = "url('" + albumArtUrl + cacheBuster + "')";
                    albumArt.style.backgroundSize = "cover";
                    albumArt.style.backgroundPosition = "center";
                } else if (!hasArt && !albumArt.style.backgroundImage) {
                    albumArt.style.backgroundImage = "linear-gradient(135deg, #1b3d68 0%, #059669 50%, #dc2626 100%)";
                }
            }
            if (metaContainer) metaContainer.classList.remove('meta-loading');

            if (trackTitle) trackTitle.textContent = cleanTitle;
            if (trackArtist) trackArtist.textContent = (artist || "LIVE STREAM").toUpperCase();
            if (bottomTrackName) bottomTrackName.textContent = cleanTitle;

            if (albumBadge) albumBadge.textContent = cleanTitle;
            if (albumSub) albumSub.textContent = (artist || "LIVE STREAM").toUpperCase();
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

            if (albumBadge) albumBadge.textContent = "ON AIR";
            if (albumSub) albumSub.textContent = "LIVE BROADCAST";
        }

        // Update Song Tracker Timing
        if (duration !== undefined && duration !== null) {
            const parsedDur = parseFloat(duration);
            currentDurationSec = !isNaN(parsedDur) && parsedDur > 0 ? parsedDur : 0;
        }
        if (position !== undefined && position !== null) {
            const parsedPos = parseFloat(position);
            currentPositionSec = !isNaN(parsedPos) && parsedPos >= 0 ? parsedPos : 0;
            lastPositionTimestamp = Date.now();
        }
        if (isPlaying !== undefined && isPlaying !== null) {
            isTrackPlaying = !!isPlaying;
        } else {
            isTrackPlaying = hasRealTrack;
        }

        updateSongTrackerUI();
    }

    // Connect to Server-Sent Events (SSE)
    try {
        const evtSource = new EventSource('/api/events');

        evtSource.onmessage = function (event) {
            try {
                const data = JSON.parse(event.data);

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
                    }
                }

                if (data.type === 'queue' && data.requests) {
                    updateQueue(data.requests);
                }
            } catch (err) {
                console.error("SSE parse error", err);
            }
        };
    } catch (e) {
        console.warn("SSE not available", e);
    }

    // Song Request Submission
    if (requestForm && requestInput) {
        requestForm.addEventListener('submit', function (e) {
            e.preventDefault();
            const text = requestInput.value.trim();
            if (!text) return;

            fetch('/api/requests', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ query: text })
            }).then(function (res) {
                if (res.ok) {
                    requestInput.value = '';
                    if (requestSuccess) {
                        requestSuccess.style.display = 'block';
                        setTimeout(function () {
                            requestSuccess.style.display = 'none';
                        }, 3500);
                    }
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
            li.innerHTML = `
                <span class="queue-track">${index + 1}. ${req.query}</span>
                <span class="queue-dur">(${req.status || 'Pending'})</span>
            `;
            queueList.appendChild(li);
        });
    }
});
