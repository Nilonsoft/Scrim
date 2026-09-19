// Scrim Broadcast Console - Reactive Drag & Drop Engine and Interactive FX

window.scrimDragDrop = {
    sortables: {},
    vuInterval: null,

    init: function (columnIds, dotNetHelper) {
        if (!Array.isArray(columnIds)) {
            columnIds = [columnIds];
        }

        if (typeof Sortable === 'undefined') {
            console.error("SortableJS is not loaded!");
            return;
        }

        var self = this;
        columnIds.forEach(function (elementId) {
            var el = document.getElementById(elementId);
            if (!el) return;

            // Destroy existing instance if any
            if (self.sortables[elementId]) {
                self.sortables[elementId].destroy();
            }

            self.sortables[elementId] = new Sortable(el, {
                group: 'scrim-dashboard-grid',
                animation: 250, // Fluid react animation when dragging over other cards
                easing: 'cubic-bezier(0.2, 1, 0.3, 1)',
                draggable: '.scrim-draggable-item',
                handle: '.scrim-drag-handle', // Dedicated drag handle or card header
                ghostClass: 'scrim-sortable-ghost',
                chosenClass: 'scrim-sortable-chosen',
                dragClass: 'scrim-sortable-drag',
                forceFallback: true, // Crucial for WebView2 / embedded browsers to bypass HTML5 drag blocks
                fallbackClass: 'scrim-sortable-drag',
                fallbackOnBody: true,
                fallbackTolerance: 3, // Distinguishes clicks from drags
                filter: 'button, select, input, textarea, a, .scrim-no-drag, .scrim-dots-btn, .pill-option',
                preventOnFilter: false,
                swapThreshold: 0.65,
                onEnd: function (evt) {
                    var from = evt.from;
                    var to = evt.to;
                    var item = evt.item;
                    var oldIndex = evt.oldIndex;
                    var newIndex = evt.newIndex;

                    // Revert DOM change so Blazor can reconcile its Virtual DOM state safely without collision
                    if (from !== to || oldIndex !== newIndex) {
                        if (oldIndex < from.children.length) {
                            from.insertBefore(item, from.children[oldIndex]);
                        } else {
                            from.appendChild(item);
                        }
                    }

                    if (dotNetHelper) {
                        dotNetHelper.invokeMethodAsync('OnCardDropped', oldIndex, newIndex, from.id, to.id);
                    }
                }
            });
        });

        // Initialize dynamic retro VU meter needle deflection
        this.initVuMeters();
    },

    initVuMeters: function () {
        var needleL = document.getElementById('vu-needle-left');
        var needleR = document.getElementById('vu-needle-right');
        if (needleL) needleL.style.transform = 'rotate(-38deg)';
        if (needleR) needleR.style.transform = 'rotate(-38deg)';
    },

    setVuLevels: function (levelL, levelR) {
        var needleL = document.getElementById('vu-needle-left');
        var needleR = document.getElementById('vu-needle-right');

        var baseAngle = -45; // Resting angle at -20dB/silence mark
        var maxAngle = 40;   // Peak deflection into the red zone (+3dB)

        // Calculate needle angle with logarithmic-style analog ballistics
        var calcAngle = function (level) {
            if (!level || level <= 0.002) return baseAngle;
            var deflection = Math.pow(Math.min(1.0, level), 0.52) * (maxAngle - baseAngle);
            return Math.max(baseAngle, Math.min(maxAngle, baseAngle + deflection));
        };

        var angleL = calcAngle(levelL);
        var angleR = calcAngle(levelR);

        if (needleL) {
            needleL.style.transform = 'rotate(' + angleL.toFixed(1) + 'deg)';
        }
        if (needleR) {
            needleR.style.transform = 'rotate(' + angleR.toFixed(1) + 'deg)';
        }
    },

    setStreamOutLevels: function (levelL, levelR, micLevel, isLive) {
        window.scrimDragDrop.setVuLevels(levelL, levelR);

        var barL = document.getElementById('meter-stream-bar-l');
        var barR = document.getElementById('meter-stream-bar-r');
        var textL = document.getElementById('meter-stream-text-l');
        var textR = document.getElementById('meter-stream-text-r');
        var micBar = document.getElementById('meter-mic-bar');
        var micText = document.getElementById('meter-mic-text');
        var txBadge = document.getElementById('stream-tx-badge');
        var topbarTx = document.getElementById('topbar-tx-indicator');

        var toDbStr = function (lvl) {
            if (!lvl || lvl <= 0.0005) {
                return "-inf dB";
            }
            var db = 20 * Math.log10(lvl);
            if (db < -60) {
                return "-60 dB";
            }
            return db.toFixed(1) + " dB";
        };

        var pctL = Math.min(100, Math.pow(Math.max(0, levelL), 0.65) * 100);
        var pctR = Math.min(100, Math.pow(Math.max(0, levelR), 0.65) * 100);
        var pctMic = Math.min(100, Math.pow(Math.max(0, micLevel), 0.65) * 100);

        if (barL) {
            barL.style.width = pctL.toFixed(1) + '%';
        }
        if (barR) {
            barR.style.width = pctR.toFixed(1) + '%';
        }
        if (textL) {
            textL.innerText = toDbStr(levelL);
        }
        if (textR) {
            textR.innerText = toDbStr(levelR);
        }

        if (micBar) {
            micBar.style.width = pctMic.toFixed(1) + '%';
        }
        if (micText) {
            micText.innerText = toDbStr(micLevel);
        }

        var hasAudio = (levelL > 0.005 || levelR > 0.005);
        if (txBadge) {
            if (isLive) {
                if (hasAudio) {
                    txBadge.innerHTML = '<span class="tx-pulse-dot active"></span> TRANSMITTING TO STREAM';
                    txBadge.className = 'tx-badge tx-active';
                } else {
                    txBadge.innerHTML = '<span class="tx-pulse-dot idle"></span> STREAM READY (SILENCE)';
                    txBadge.className = 'tx-badge tx-idle';
                }
            } else {
                txBadge.innerHTML = '<span class="tx-pulse-dot off"></span> OFF-AIR (PREVIEW MONITOR)';
                txBadge.className = 'tx-badge tx-off';
            }
        }

        if (topbarTx) {
            var avgPct = (pctL + pctR) / 2;
            if (isLive) {
                // When live/transmitting, maintain an active RF carrier pilot floor (minimum 8%) plus audio level modulation
                var txPct = Math.max(8, avgPct);
                topbarTx.style.width = txPct.toFixed(0) + '%';
            } else {
                topbarTx.style.width = '0%';
            }
        }
    }
};
