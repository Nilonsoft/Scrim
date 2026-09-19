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

    // Live GMT Clock updater
    function updateClock() {
        if (!clockTime) return;
        const now = new Date();
        let hours = now.getUTCHours();
        const minutes = String(now.getUTCMinutes()).padStart(2, '0');
        const ampm = hours >= 12 ? 'PM' : 'AM';
        hours = hours % 12;
        hours = hours ? hours : 12;
        clockTime.textContent = `${hours}:${minutes} ${ampm} GMT`;
    }
    updateClock();
    setInterval(updateClock, 1000);

    // Audio Playback
    if (playBtn && audio) {
        playBtn.addEventListener('click', function () {
            if (isPlaying) {
                audio.pause();
                audio.src = '';
                isPlaying = false;
                if (playIcon) playIcon.style.display = 'block';
                if (pauseIcon) pauseIcon.style.display = 'none';
            } else {
                audio.src = '/stream?t=' + Date.now();
                audio.play().then(function () {
                    isPlaying = true;
                    if (playIcon) playIcon.style.display = 'none';
                    if (pauseIcon) pauseIcon.style.display = 'block';
                }).catch(function (e) {
                    console.error("Stream playback error:", e);
                });
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

    // Initial branding load
    fetch('/api/branding')
        .then(res => res.json())
        .then(applyBranding)
        .catch(e => console.warn("Could not fetch branding", e));

    // Connect to Server-Sent Events (SSE)
    try {
        const evtSource = new EventSource('/api/events');

        evtSource.onmessage = function (event) {
            try {
                const data = JSON.parse(event.data);

                if (data.type === 'metadata') {
                    const title = data.title || "Unknown Track";
                    const artist = data.artist || "Unknown Artist";
                    if (trackTitle) trackTitle.textContent = title;
                    if (trackArtist) trackArtist.textContent = artist.toUpperCase();
                    if (bottomTrackName) bottomTrackName.textContent = title;
                }

                if (data.type === 'branding') {
                    applyBranding(data);
                }

                if (data.type === 'stats' && listenerCount) {
                    listenerCount.textContent = data.listeners || '1';
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
        if (!queueList || !Array.isArray(requests)) return;
        if (requests.length === 0) return;

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
