let ctx = null;

export function init() {
    if (!ctx || ctx.state === 'closed') {
        ctx = new (window.AudioContext || window.webkitAudioContext)();
    }
    if (ctx.state === 'suspended') {
        ctx.resume();
    }
}

export function playBeep() {
    try {
        if (!ctx || ctx.state === 'closed') return;
        var o = ctx.createOscillator();
        var g = ctx.createGain();

        o.type = 'sawtooth';
        o.frequency.value = 950;
        g.gain.value = 0.06;

        o.connect(g);
        g.connect(ctx.destination);

        o.start();
        setTimeout(function () {
            o.stop();
        }, 400);
    } catch (e) { }
}
