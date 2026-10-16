"""Original 30-second instrumental sketches and a seamless procedural gas leak.

No external recordings, sampled voices, or third-party melodies are used.
Run with the bundled Python and NumPy; outputs are reproducible PCM WAV assets.
"""
from pathlib import Path
import json
import wave
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
DEST = ROOT / 'Assets/Resources/Monitor/Audio'
RATE = 44100
DURATION = 30
RNG = np.random.default_rng(20261014)


def save(path, data, peak=.82):
    data = np.asarray(data, dtype=np.float64)
    data -= data.mean(axis=0)
    rms = np.sqrt(np.mean(data ** 2))
    data *= min(.13 / max(rms, 1e-9), peak / max(np.abs(data).max(), 1e-9))
    path.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(path), 'wb') as out:
        out.setnchannels(1 if data.ndim == 1 else data.shape[1])
        out.setsampwidth(2)
        out.setframerate(RATE)
        out.writeframes((np.clip(data, -1, 1) * 32767).astype('<i2').tobytes())
    return {'file': str(path.relative_to(ROOT)), 'seconds': len(data) / RATE,
            'peak': float(np.abs(data).max()), 'rms': float(np.sqrt(np.mean(data ** 2)))}


def note(midi, duration, instrument):
    t = np.arange(int((duration + .8) * RATE)) / RATE
    f = 440 * 2 ** ((midi - 69) / 12)
    if instrument == 'piano':
        y = sum(a * np.sin(2 * np.pi * f * h * t) * np.exp(-t * (1.0 + h * .6))
                for h, a in [(1, 1), (2, .27), (3.001, .14), (4.003, .045)])
        attack = .009
    elif instrument == 'pluck':
        y = sum((.65 ** (h - 1) / h) * np.sin(2 * np.pi * f * h * t)
                * np.exp(-t * (1.9 + h * .8)) for h in range(1, 8))
        attack = .006
    elif instrument == 'bass':
        y = np.sin(2 * np.pi * f * t) + .16 * np.sin(2 * np.pi * f * 2 * t)
        y *= np.exp(-t * 2.6)
        attack = .016
    else:
        fm = 1.4 * np.exp(-t * 5) * np.sin(2 * np.pi * f * 2 * t)
        y = np.sin(2 * np.pi * f * t + fm) * np.exp(-t * 1.7)
        y += .14 * np.sin(2 * np.pi * f * 1.001 * t) * np.exp(-t * 2.1)
        attack = .012
    envelope = np.minimum(t / attack, 1) * np.minimum(np.maximum(duration + .8 - t, 0) / .12, 1)
    envelope *= np.exp(-np.maximum(t - duration, 0) * 6)
    return y * envelope


def add(mix, sound, at, gain, pan=0):
    start = max(0, int(at * RATE))
    size = min(len(sound), len(mix) - start)
    if size <= 0:
        return
    mix[start:start+size, 0] += sound[:size] * gain * np.sqrt((1-pan)/2)
    mix[start:start+size, 1] += sound[:size] * gain * np.sqrt((1+pan)/2)


def finish(mix):
    dry = mix.copy()
    for seconds, amount in [(.071, .08), (.137, .075), (.229, .07), (.367, .05), (.511, .035)]:
        shift = int(seconds * RATE)
        mix[shift:] += dry[:-shift, ::-1] * amount
    # Gentle high-frequency roll-off for a small, comfortable tabletop player.
    freq = np.fft.rfftfreq(len(mix), 1/RATE)
    response = 1 / np.sqrt(1 + (freq / 4800) ** 4)
    mix = np.fft.irfft(np.fft.rfft(mix, axis=0) * response[:, None], n=len(mix), axis=0)
    mix[:int(.5*RATE)] *= np.linspace(0, 1, int(.5*RATE))[:, None]
    mix[-int(2*RATE):] *= np.linspace(1, 0, int(2*RATE))[:, None]
    return mix


def compose(index):
    mix = np.zeros((RATE * DURATION, 2))
    if index == 0:
        bpm, beats, instrument = 72, 4, 'piano'
        chords = [[53,57,60,64], [55,59,62,64], [52,55,59,62], [45,52,55,60],
                  [50,57,60,64], [43,55,59,62], [48,55,60,64], [48,55,59,64]]
        melodies = [[72,76,79,76], [74,71,69,67], [71,74,76,74], [72,69,67,64],
                    [65,69,72,76], [74,71,69,67], [67,72,76,74], [72,67,64,72]]
        filename = 'Player_01_QuietBreak_Piano.wav'
    elif index == 1:
        bpm, beats, instrument = 96, 3, 'pluck'
        chords = [[48,55,60,64], [53,57,60,65], [55,59,62,67], [48,55,60,64]] * 3
        chords += [[55,59,62,67], [48,55,60,64]]
        melodies = [[76,79,76], [77,76,72], [74,71,67], [72,76,72]] * 3
        melodies += [[74,71,67], [72,67,72]]
        filename = 'Player_02_LittleWaltz_Pluck.wav'
    else:
        bpm, beats, instrument = 86, 4, 'rhodes'
        chords = [[48,55,59,64], [45,52,55,60], [50,57,60,64], [43,53,59,62],
                  [52,55,59,62], [45,52,55,60], [50,57,60,64], [43,53,59,62],
                  [48,55,59,64], [48,55,59,64]]
        melodies = [[76,0,74,72], [69,72,0,76], [77,76,74,0], [71,74,72,67],
                    [71,0,74,76], [72,69,67,0], [69,72,76,74], [71,67,69,71],
                    [72,76,74,72], [67,64,0,72]]
        filename = 'Player_03_LateCoffee_Rhodes.wav'
    beat = 60/bpm
    for bar, chord in enumerate(chords):
        start = bar * beats * beat
        if start >= 27.5:
            break
        add(mix, note(chord[0]-12, beat*1.6, 'bass'), start, .17, -.08)
        for k, pitch in enumerate(chord[1:]):
            at = start + (.045*k if index == 2 else .5*k*beat)
            add(mix, note(pitch, beat*1.8, instrument), at, .15, -.25 + .20*k)
        for k, pitch in enumerate(melodies[bar]):
            if pitch:
                at = start + beat*k + .08*beat + RNG.uniform(-.018, .018)
                add(mix, note(pitch, beat*.8, instrument), at, RNG.uniform(.19, .24), .14)
        if index == 2:
            # Muted kick and a quiet brush-like offbeat; deliberately no sharp snare.
            t = np.arange(int(.2*RATE))/RATE
            kick = np.sin(2*np.pi*(58*t + .6*(1-np.exp(-25*t)))) * np.exp(-t*28)
            for b in [0, 2]:
                add(mix, kick, start+b*beat, .075)
            for b in range(beats):
                t = np.arange(int(.08*RATE))/RATE
                brush = RNG.normal(0, 1, len(t)) * np.exp(-t*65) * np.minimum(t/.006, 1)
                add(mix, brush, start+(b+.66)*beat, .008, -.3)
    return save(DEST/'Music'/filename, finish(mix))


def leak():
    n = RATE*8
    f = np.fft.rfftfreq(n, 1/RATE)
    # Periodic spectral synthesis gives a continuous air jet with no seam/click.
    phase = RNG.uniform(0, 2*np.pi, len(f))
    spectrum = np.exp(1j*phase) * np.sqrt(f / (f + 250)) / np.sqrt(1+(f/5400)**4)
    spectrum *= 1 + .6*np.exp(-((f-1850)/1000)**2)
    spectrum[0] = spectrum[-1] = 0
    air = np.fft.irfft(spectrum, n)
    air /= np.std(air)
    t = np.arange(n)/RATE
    air *= .75 + .09*np.sin(2*np.pi*t/8) + .035*np.sin(2*np.pi*7*t/8)
    # A broad low turbulence layer, rather than a pitched valve whistle.
    low = np.fft.irfft(np.exp(1j*RNG.uniform(0,2*np.pi,len(f))) / (1+(f/220)**2), n)
    low /= np.std(low)
    return save(DEST/'Enviroment/SFX_GasLeak_Loop.wav', .85*air + .16*low)


if __name__ == '__main__':
    report = [compose(i) for i in range(3)] + [leak()]
    (Path(__file__).parent/'audio_build_report.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(json.dumps(report, indent=2))
