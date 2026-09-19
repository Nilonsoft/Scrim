// Global Indie Radio - Web Player Logic

document.addEventListener('DOMContentLoaded', function () {
    const audio = document.getElementById('audioElement');
    const playBtn = document.getElementById('playBtn');
    const playIcon = playBtn ? playBtn.querySelector('.icon-play') : null;
    const pauseIcon = playBtn ? playBtn.querySelector('.icon-pause') : null;
    const volumeSlider = document.getElementById('volumeSlider');
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
    let defaultSubtitle = "Live Broadcast";

    function updatePlayButtonUI() {
        if (!playBtn) return;
        if (isPlaying) {
            if (playIcon) playIcon.style.display = 'none';
            if (pauseIcon) pauseIcon.style.display = 'block';
            playBtn.classList.add('playing');
            playBtn.classList.remove('connecting');
        } else if (isConnecting) {
            if (playIcon) playIcon.style.display = 'block';
            if (pauseIcon) pauseIcon.style.display = 'none';
            playBtn.classList.remove('playing');
            playBtn.classList.add('connecting');
        } else {
            if (playIcon) playIcon.style.display = 'block';
            if (pauseIcon) pauseIcon.style.display = 'none';
            playBtn.classList.remove('playing');
            playBtn.classList.remove('connecting');
        }
    }

    // Real-Time Audio Reactive Waveform Visualizer
    let audioCtx = null;
    let analyser = null;
    let sourceNode = null;
    let freqData = null;
    const waveBars = document.querySelectorAll('.waveform-container .wave-bar');

    function initAudioVisualizer() {
        if (analyser || !audio) return;
        try {
            const AudioContextClass = window.AudioContext || window.webkitAudioContext;
            if (!AudioContextClass) return;
            audioCtx = new AudioContextClass();
            analyser = audioCtx.createAnalyser();
            analyser.fftSize = 64;
            analyser.smoothingTimeConstant = 0.75;
            freqData = new Uint8Array(analyser.frequencyBinCount);

            sourceNode = audioCtx.createMediaElementSource(audio);
            sourceNode.connect(analyser);
            analyser.connect(audioCtx.destination);
        } catch (e) {
            console.warn("AudioContext visualizer initialization:", e);
        }
    }

    function animateWaveform() {
        requestAnimationFrame(animateWaveform);
        if (!waveBars || waveBars.length === 0) return;

        if (isPlaying && analyser && freqData) {
            analyser.getByteFrequencyData(freqData);
            for (let i = 0; i < waveBars.length; i++) {
                // Read frequency bins across the spectrum (skip bin 0 for DC offset)
                const binIndex = Math.min(i + 1, freqData.length - 1);
                const val = freqData[binIndex] || 0;
                const norm = val / 255.0;
                const h = Math.max(4, Math.round(norm * 44 + 4));
                waveBars[i].style.height = h + 'px';
                if (norm > 0.08) {
                    waveBars[i].style.background = 'rgba(0, 210, 255, ' + (0.35 + norm * 0.65) + ')';
                } else {
                    waveBars[i].style.background = 'rgba(255, 255, 255, 0.25)';
                }
            }
        } else {
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

    // Audio Playback with Event Synchronization
    if (playBtn && audio) {
        audio.addEventListener('playing', function () {
            isPlaying = true;
            isConnecting = false;
            updatePlayButtonUI();
            const subtitleEl = document.getElementById('showSubtitle');
            if (subtitleEl) subtitleEl.textContent = defaultSubtitle;
        });

        audio.addEventListener('pause', function () {
            isPlaying = false;
            isConnecting = false;
            updatePlayButtonUI();
        });

        audio.addEventListener('waiting', function () {
            if (isPlaying) {
                isConnecting = true;
                updatePlayButtonUI();
            }
        });

        audio.addEventListener('canplay', function () {
            isConnecting = false;
            updatePlayButtonUI();
        });

        audio.addEventListener('error', function (e) {
            console.warn("Audio element stream error or station offline:", e);
            isPlaying = false;
            isConnecting = false;
            updatePlayButtonUI();
            audio.removeAttribute('src');

            const subtitleEl = document.getElementById('showSubtitle');
            if (subtitleEl) {
                subtitleEl.textContent = "Station Offline — Broadcaster has not started transmission";
            }
        });

        playBtn.addEventListener('click', function () {
            // Ensure audio visualizer graph is initialized and unpaused on user interaction
            initAudioVisualizer();
            if (audioCtx && audioCtx.state === 'suspended') {
                audioCtx.resume();
            }

            if (isPlaying || isConnecting) {
                // Stop playback cleanly
                audio.pause();
                audio.removeAttribute('src');
                isPlaying = false;
                isConnecting = false;
                updatePlayButtonUI();
            } else {
                // Initiate stream playback
                isConnecting = true;
                updatePlayButtonUI();

                const subtitleEl = document.getElementById('showSubtitle');
                if (subtitleEl) subtitleEl.textContent = "Connecting to live audio stream...";

                // Directly assign live stream endpoint with cache buster
                audio.src = '/stream?t=' + Date.now();
                var playPromise = audio.play();
                if (playPromise !== undefined) {
                    playPromise.then(function () {
                        isPlaying = true;
                        isConnecting = false;
                        updatePlayButtonUI();
                        if (subtitleEl) subtitleEl.textContent = defaultSubtitle;
                    }).catch(function (err) {
                        console.warn("Playback could not start immediately:", err);
                        isPlaying = false;
                        isConnecting = false;
                        updatePlayButtonUI();
                        if (subtitleEl) {
                            subtitleEl.textContent = "Station Offline — Broadcaster is not on air yet";
                        }
                    });
                }
            }
        });
    }

    if (volumeSlider && audio) {
        audio.volume = parseFloat(volumeSlider.value);
        volumeSlider.addEventListener('input', function (e) {
            audio.volume = parseFloat(e.target.value);
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
