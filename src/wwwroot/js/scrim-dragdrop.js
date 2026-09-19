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
                filter: 'button, select, input, textarea, a, .scrim-no-drag',
                preventOnFilter: false,
                fallbackOnBody: true,
                swapThreshold: 0.65,
                onEnd: function (evt) {
                    if (dotNetHelper) {
                        dotNetHelper.invokeMethodAsync('OnCardDropped', evt.oldIndex, evt.newIndex, evt.from.id, evt.to.id);
                    }
                }
            });
        });

        // Initialize dynamic retro VU meter needle deflection
        this.initVuMeters();
    },

    initVuMeters: function () {
        if (this.vuInterval) return;

        var needleL = document.getElementById('vu-needle-left');
        var needleR = document.getElementById('vu-needle-right');
        
        var baseAngle = -38; // resting angle in degrees
        var maxAngle = 38;

        this.vuInterval = setInterval(function () {
            if (!needleL && !needleR) {
                needleL = document.getElementById('vu-needle-left');
                needleR = document.getElementById('vu-needle-right');
                return;
            }

            // Generate realistic random studio audio needle bounce
            var active = document.body.getAttribute('data-broadcasting') === 'true';
            var targetL = active ? (-15 + Math.random() * 40 - (Math.random() > 0.85 ? 15 : 0)) : (baseAngle + Math.random() * 4);
            var targetR = active ? (-12 + Math.random() * 38 - (Math.random() > 0.85 ? 15 : 0)) : (baseAngle + Math.random() * 4);

            targetL = Math.max(baseAngle, Math.min(maxAngle, targetL));
            targetR = Math.max(baseAngle, Math.min(maxAngle, targetR));

            if (needleL) {
                needleL.style.transform = 'rotate(' + targetL + 'deg)';
            }
            if (needleR) {
                needleR.style.transform = 'rotate(' + targetR + 'deg)';
            }
        }, 120);
    }
};
