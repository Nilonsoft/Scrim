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
    const trackArtist = document.getElementById('trackArtist');
    const bottomTrackName = document.getElementById('bottomTrackName');
    const listenerCount = document.getElementById('listenerCount');
    const clockTime = document.getElementById('clockTime');
    const stationBannerWrap = document.getElementById('stationBannerWrap');
    const stationBannerImg = document.getElementById('stationBannerImg');
    const requestForm = document.getElementById('requestForm');
    const requestInput = document.getElementById('requestInput');
    const dedicationInput = document.getElementById('dedicationInput');
    const requestSuccess = document.getElementById('requestSuccess');
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
    let userExplicitlyStopped = false;
    let autoplayUnlocked = false;

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

    function scheduleStreamReconnect() {
        if (reconnectTimeout || userExplicitlyStopped || !isUserPlaying) return;
        reconnectTimeout = setTimeout(function () {
            reconnectTimeout = null;
            if (!userExplicitlyStopped && isUserPlaying) {
                console.log("[Audio] Attempting automatic stream reconnect...");
                startStream(true);
            }
        }, 1500);
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
        updatePlayButtonUI();

        if (audio) {
            audio.pause();
            audio.removeAttribute('src'); // Stop streaming connection immediately
            audio.load(); // Tell browser to drop the pending HTTP connection
        }
    }

    let currentStreamEndpoint = '/stream';

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
                    updatePlayButtonUI();
                    if (subtitleEl) {
                        subtitleEl.textContent = defaultSubtitle;
                    }
                }
            }).catch(function (err) {
                console.warn("Playback could not start:", err);
                if (isUserPlaying) {
                    if (err.name === 'NotAllowedError') {
                        isUserPlaying = false;
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
                userExplicitlyStopped = true;
                stopStream();
                const subtitleEl = document.getElementById('showSubtitle');
                if (subtitleEl) {
                    subtitleEl.textContent = defaultSubtitle;
                }
            } else {
                userExplicitlyStopped = false;
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
                scheduleStreamReconnect();
            }
        });

        audio.addEventListener('error', function (e) {
            // Ignore error events triggered when user intentionally stopped or src is cleared
            if (!isUserPlaying || !audio.getAttribute('src')) {
                return;
            }
            console.warn("Audio element stream error or station offline:", e);
            if (!userExplicitlyStopped) {
                isPlaying = false;
                isConnecting = true;
                updatePlayButtonUI();
                const subtitleEl = document.getElementById('showSubtitle');
                if (subtitleEl) {
                    subtitleEl.textContent = "Broadcast interrupted — waiting for DJ to resume...";
                }
                scheduleStreamReconnect();
            } else {
                isUserPlaying = false;
                isPlaying = false;
                isConnecting = false;
                updatePlayButtonUI();
                audio.removeAttribute('src');
            }
        });
    }

    // Dynamic Branding Function
    function applyBranding(branding) {
        if (!branding) return;

        if (branding.streamUrl) {
            currentStreamEndpoint = branding.streamUrl;
        }

        if (branding.isPrivate !== undefined) {
            setPrivateStreamMode(branding.isPrivate);
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
                        if (link.url && (link.url.startsWith('http://') || link.url.startsWith('https://'))) {
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
                    a.href = '#' + link.toLowerCase().replace(/\s+/g, '-');
                    a.className = 'nav-link';
                    a.textContent = link;
                    navContainer.appendChild(a);
                });
            }
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

    // Initial branding and status load
    fetch('/api/branding')
        .then(res => res.json())
        .then(applyBranding)
        .catch(e => console.warn("Could not fetch branding", e));

    fetch('/api/status')
        .then(res => res.json())
        .then(function (data) {
            if (data) {
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
        })
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

    let lastTrackKey = "";
    let lastTrackTitle = "";

    function updateTrackMetadata(title, artist, album, albumArtUrl, hasArt, duration, position, isPlaying) {
        const albumArt = document.getElementById('albumArt');
        const metaContainer = document.getElementById('metaContainer');
        const albumBadge = document.getElementById('albumBadge');
        const albumSub = document.getElementById('albumSub');

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
        }
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
                    if (Math.abs(diff) > 2.5 || diff > 0) {
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
    try {
        const evtSource = new EventSource('/api/events');

        evtSource.onopen = function () {
            console.log('[SSE] Real-time connection established with Scrim app');
        };

        evtSource.onerror = function (e) {
            console.warn('[SSE] Event stream connection paused, auto-reconnecting...', e);
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
                        if (data.isLive && (!isPlaying || !audio.src)) {
                            checkAutoplay(true);
                        }
                    }
                    if (data.format !== undefined) {
                        updateStreamQuality(data.format, data.bitrate);
                    }
                }

                if (data.type === 'queue' && data.requests) {
                    updateQueue(data.requests);
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

    // Song Request Submission
    if (requestForm && requestInput) {
        requestForm.addEventListener('submit', function (e) {
            e.preventDefault();
            const text = requestInput.value.trim();
            if (!text) return;

            const dedication = dedicationInput ? dedicationInput.value.trim() : '';

            fetch('/api/requests', {
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
    function initNickname() {
        if (!chatNicknameInput) return;
        let nick = '';
        try {
            nick = localStorage.getItem('scrim_chat_nickname') || '';
        } catch (e) {}

        if (!nick) {
            const randNum = Math.floor(100 + Math.random() * 900);
            nick = 'Listener #' + randNum;
            try {
                localStorage.setItem('scrim_chat_nickname', nick);
            } catch (e) {}
        }
        chatNicknameInput.value = nick;

        chatNicknameInput.addEventListener('change', function () {
            let val = chatNicknameInput.value.trim();
            if (!val) {
                val = 'Listener #' + Math.floor(100 + Math.random() * 900);
                chatNicknameInput.value = val;
            }
            try {
                localStorage.setItem('scrim_chat_nickname', val);
            } catch (e) {}
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

        msgDiv.innerHTML = `
            <div class="chat-msg-header">
                <div class="chat-msg-sender-wrap">
                    <strong class="chat-msg-sender" style="color: ${escapeHtml(senderColor)};">${escapeHtml(msg.sender || 'Anonymous')}</strong>
                    ${msg.isHost ? '<span class="chat-host-badge">DJ</span>' : ''}
                </div>
                <span class="chat-msg-time">${timeStr}</span>
            </div>
            <div class="chat-msg-text">${escapeHtml(msg.text || '')}</div>
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

    // Fetch initial chat state & recent history
    fetch('/api/chat?clientId=' + encodeURIComponent(anonClientId))
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
                if (Array.isArray(data.messages)) {
                    data.messages.forEach(appendChatMessage);
                }
            }
        })
        .catch(function (err) {
            console.warn("Could not load initial chat", err);
        });

    function submitChatMessage(sender, text) {
        if (!text) return;
        if (chatSendBtn) chatSendBtn.disabled = true;

        fetch('/api/chat', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ sender: sender, text: text, clientId: anonClientId })
        }).then(function (res) {
            if (chatSendBtn) chatSendBtn.disabled = !isChatEnabled || isUserBanned;
            if (res.ok) {
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

    // Chat form submission
    if (chatForm && chatMessageInput) {
        chatForm.addEventListener('submit', function (e) {
            e.preventDefault();
            if (!isChatEnabled || isUserBanned) return;
            const text = chatMessageInput.value.trim();
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
        fetch('/api/reactions?clientId=' + encodeURIComponent(anonClientId)).then(function (res) {
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

        fetch('/api/reactions', {
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
        fetch('/api/history').then(function (res) {
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

            const thumb = document.createElement('div');
            thumb.className = 'history-thumb';
            if (track.hasArt && track.albumArtUrl) {
                const img = document.createElement('img');
                img.src = track.albumArtUrl;
                img.alt = track.title || 'Track Art';
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

            const info = document.createElement('div');
            info.className = 'history-track-info';

            const titleEl = document.createElement('div');
            titleEl.className = 'history-track-title';
            titleEl.textContent = track.title || 'Unknown Track';

            const artistEl = document.createElement('div');
            artistEl.className = 'history-track-artist';
            artistEl.textContent = track.artist || 'Live Broadcast';

            info.appendChild(titleEl);
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

            row.appendChild(thumb);
            row.appendChild(info);
            row.appendChild(rightWrap);

            historyItemsList.appendChild(row);
        });
    }

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
});
