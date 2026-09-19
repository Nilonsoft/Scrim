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
    const waveBars = document.querySelectorAll('.waveform-container .wave-bar');

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
                analyser.fftSize = 64;
                analyser.smoothingTimeConstant = 0.75;
                freqData = new Uint8Array(analyser.frequencyBinCount);
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

    function animateWaveform() {
        requestAnimationFrame(animateWaveform);
        if (!waveBars || waveBars.length === 0) return;

        let hasData = false;
        if (isPlaying && analyser && freqData) {
            analyser.getByteFrequencyData(freqData);
            
            // Check if analyser has non-zero audio signal
            let total = 0;
            for (let k = 1; k < Math.min(20, freqData.length); k++) {
                total += freqData[k];
            }

            if (total > 0) {
                hasData = true;
                for (let i = 0; i < waveBars.length; i++) {
                    const binIndex = Math.min(i + 1, freqData.length - 1);
                    const val = freqData[binIndex] || 0;
                    const norm = val / 255.0;
                    // Dynamic logarithmic scaling for lively, music-reactive bounce
                    const scaled = Math.min(1.0, Math.pow(norm, 0.65) * 1.55);
                    const h = Math.max(4, Math.round(scaled * 42 + 4));
                    waveBars[i].style.height = h + 'px';
                    if (scaled > 0.05) {
                        waveBars[i].style.background = 'rgba(0, 210, 255, ' + (0.45 + scaled * 0.55) + ')';
                    } else {
                        waveBars[i].style.background = 'rgba(255, 255, 255, 0.25)';
                    }
                }
            }
        }

        if (!hasData) {
            // Decay bars smoothly back to resting 4px baseline
            for (let i = 0; i < waveBars.length; i++) {
                const currentH = parseFloat(waveBars[i].style.height) || 4;
                if (currentH > 4.1) {
                    waveBars[i].style.height = Math.max(4, (currentH * 0.85)).toFixed(1) + 'px';
                } else {
                    waveBars[i].style.height = '4px';
                    waveBars[i].style.background = 'rgba(255, 255, 255, 0.25)';
                }
            }
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
            document.documentElement.style.setProperty('--on-air-red', branding.accentColor);
            document.documentElement.style.setProperty('--on-air-glow', `0 0 20px ${branding.accentColor}88`);
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
    // Initial Metadata Fetch
    fetch('/api/metadata')
        .then(function (r) { return r.json(); })
        .then(function (data) {
            if (data && data.title) {
                updateTrackMetadata(data.title, data.artist, data.album, data.albumArtUrl, data.hasArt);
            }
        })
        .catch(function (e) { console.warn("Could not fetch initial metadata", e); });

    function updateTrackMetadata(title, artist, album, albumArtUrl, hasArt) {
        const albumArt = document.getElementById('albumArt');
        const metaContainer = document.getElementById('metaContainer');
        const albumBadge = document.getElementById('albumBadge');
        const albumSub = document.getElementById('albumSub');

        const hasRealTrack = title && title.trim() !== "" && 
            title !== "Awaiting Audio Source..." && 
            title !== "Awaiting Track Info..." && 
            title !== "Unknown Track";

        if (hasRealTrack) {
            // Real track info received: reveal actual artwork and clean typography
            if (albumArt) {
                albumArt.classList.remove('loading');
                if (hasArt && albumArtUrl) {
                    albumArt.style.backgroundImage = "url('" + albumArtUrl + "')";
                    albumArt.style.backgroundSize = "cover";
                    albumArt.style.backgroundPosition = "center";
                } else if (!hasArt) {
                    albumArt.style.backgroundImage = "linear-gradient(135deg, #1b3d68 0%, #059669 50%, #dc2626 100%)";
                }
            }
            if (metaContainer) metaContainer.classList.remove('meta-loading');

            if (trackTitle) trackTitle.textContent = title;
            if (trackArtist) trackArtist.textContent = (artist || "LIVE STREAM").toUpperCase();
            if (bottomTrackName) bottomTrackName.textContent = title;

            if (albumBadge) albumBadge.textContent = title;
            if (albumSub) albumSub.textContent = (artist || "LIVE STREAM").toUpperCase();
        } else {
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
    }

    // Connect to Server-Sent Events (SSE)
    try {
        const evtSource = new EventSource('/api/events');

        evtSource.onmessage = function (event) {
            try {
                const data = JSON.parse(event.data);

                if (data.type === 'metadata') {
                    updateTrackMetadata(data.title, data.artist, data.album, data.albumArtUrl, data.hasArt);
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
