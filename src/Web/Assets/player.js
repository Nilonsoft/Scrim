document.addEventListener('DOMContentLoaded', () => {
    const audio = document.getElementById('audioElement');
    const playBtn = document.getElementById('playBtn');
    const volumeSlider = document.getElementById('volumeSlider');
    const trackTitle = document.getElementById('trackTitle');
    const trackArtist = document.getElementById('trackArtist');
    const listenerCount = document.getElementById('listenerCount');
    const requestForm = document.getElementById('requestForm');
    const requestInput = document.getElementById('requestInput');
    const requestQueue = document.getElementById('requestQueue');

    let isPlaying = false;

    // Audio Playback
    playBtn.addEventListener('click', () => {
        if (isPlaying) {
            audio.pause();
            audio.src = ''; // disconnect to prevent buffering lag
            playBtn.classList.remove('playing');
            playBtn.innerHTML = '<svg viewBox="0 0 24 24" fill="currentColor"><path d="M8 5v14l11-7z"/></svg>';
            isPlaying = false;
        } else {
            // Add timestamp to prevent caching the stream
            audio.src = '/stream?t=' + new Date().getTime();
            audio.play().then(() => {
                playBtn.classList.add('playing');
                playBtn.innerHTML = '<svg viewBox="0 0 24 24" fill="currentColor"><path d="M6 19h4V5H6v14zm8-14v14h4V5h-4z"/></svg>';
                isPlaying = true;
            }).catch(e => console.error("Playback failed:", e));
        }
    });

    volumeSlider.addEventListener('input', (e) => {
        audio.volume = e.target.value;
    });

    // Server-Sent Events for Metadata
    const evtSource = new EventSource('/api/events');
    
    evtSource.onmessage = function(event) {
        try {
            const data = JSON.parse(event.data);
            
            if (data.type === "metadata") {
                trackTitle.textContent = data.title || "Unknown Track";
                trackArtist.textContent = data.artist || "Unknown Artist";
            }
            
            if (data.type === "stats") {
                listenerCount.textContent = data.listeners || "0";
            }
            
            if (data.type === "queue") {
                updateQueue(data.requests || []);
            }
        } catch (e) {
            console.error("Failed to parse SSE data", e);
        }
    };

    // Song Requests
    requestForm.addEventListener('submit', (e) => {
        e.preventDefault();
        const requestText = requestInput.value.trim();
        if (!requestText) return;

        fetch('/api/requests', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ query: requestText })
        }).then(res => {
            if (res.ok) {
                requestInput.value = '';
                // Optimistically add to UI, though SSE will sync it eventually
                const li = document.createElement('li');
                li.innerHTML = `<span>${requestText}</span><span class="req-status">Pending</span>`;
                requestQueue.appendChild(li);
            }
        }).catch(err => console.error(err));
    });

    function updateQueue(requests) {
        requestQueue.innerHTML = '';
        requests.forEach(req => {
            const li = document.createElement('li');
            li.innerHTML = `<span>${req.query}</span><span class="req-status">${req.status}</span>`;
            requestQueue.appendChild(li);
        });
    }
});
