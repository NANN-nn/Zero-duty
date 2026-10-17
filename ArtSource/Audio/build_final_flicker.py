"""Reproducible voice-free electronic breakdown cue for the final monitor event.

Original FM, filtered noise and accelerating relay pulses, with the kick muted.
No recordings, external samples, speech or third-party music are used.
"""
from pathlib import Path
import json
import wave
import argparse
import numpy as np

RATE = 44100
DURATION = 4.0
ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / 'Assets/Resources/Monitor/Audio/Interactive/SFX_FinalFlicker.wav'


def generate(include_kick=False):
    rng = np.random.default_rng(20261017)
    t = np.arange(int(RATE * DURATION), dtype=np.float64) / RATE
    x = t / DURATION
    white = rng.normal(0, .4, len(t))
    filtered = np.convolve(white, np.ones(13) / 13, mode='same')
    pulse = np.zeros_like(t)
    impacts = np.zeros_like(t)
    onset = .025
    starts = []
    while onset < DURATION - .07:
        starts.append(onset)
        dt = t - onset
        width = .058 - .022 * (onset / DURATION)
        active = (dt >= 0) & (dt < width)
        u = np.clip(dt / width, 0, 1)
        # Raised-cosine edges avoid harsh waveform discontinuities.
        env = np.sin(np.pi * u) ** 1.5 * active
        carrier = np.sin(2 * np.pi * (230 * dt + 35 * dt * dt)
                         + 1.7 * np.sin(2 * np.pi * 67 * dt))
        pulse += env * (.43 * carrier + .20 * filtered)
        if len(starts) % 3 == 1:
            hit = (dt >= 0) * np.exp(-np.maximum(0, dt) * 23)
            hit *= np.clip(dt / .004, 0, 1)
            original_hit = .32 * hit * np.sin(2 * np.pi * (63 * dt - 15 * dt * dt))
            impacts += original_hit
        onset += (1 / (5.5 + 9.5 * (onset / DURATION))) * rng.uniform(.82, 1.15)
    rise = .035 * np.sin(2 * np.pi * (72 * t + 19 * t * t))
    charge = .026 * np.sin(2 * np.pi * (410 * t + 64 * t * t)) * x
    accepted_body = (pulse + impacts + rise + charge) * (.74 + .26 * x)
    body = (pulse + (impacts if include_kick else 0) + rise + charge) * (.74 + .26 * x)
    # Keep room for normal gameplay ambience; no hard clipping or abrupt tail.
    fade = np.minimum(np.clip(t / .015, 0, 1), np.clip((DURATION - t) / .045, 0, 1))
    left = body * fade
    right = (.94 * body + .06 * np.roll(pulse, 173)) * fade
    data = np.column_stack((left, right))
    data -= data.mean(axis=0)
    # Mute the kick without turning up or otherwise redesigning the other layers.
    # The original mix remains available only as a gain/reference regression check.
    reference = np.column_stack((accepted_body * fade,
        (.94 * accepted_body + .06 * np.roll(pulse, 173)) * fade))
    reference -= reference.mean(axis=0)
    normalization = .78 / np.max(np.abs(reference))
    data *= normalization
    # Enforce genuinely silent file endpoints after DC removal.
    data[:64] *= np.linspace(0, 1, 64)[:, None]
    data[-128:] *= np.linspace(1, 0, 128)[:, None]
    return data, starts


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--preview', type=Path)
    args = parser.parse_args()
    samples, starts = generate()
    assert samples.shape == (176400, 2)
    assert np.isfinite(samples).all()
    assert np.max(np.abs(samples)) <= .781
    assert .12 < np.sqrt(np.mean(samples ** 2)) < .25
    assert np.max(np.abs(samples[[0, -1]])) == 0
    gaps = np.diff(starts)
    assert np.mean(gaps[-6:]) < np.mean(gaps[:6]) * .65
    original, original_starts = generate(include_kick=True)
    # Protect the first-version reference and unchanged pulse timing.
    assert np.isclose(np.sqrt(np.mean(original ** 2)), .17092771748351007,
                      rtol=0, atol=1e-12), 'The original waveform must remain unchanged.'
    assert not np.array_equal(samples, original)
    assert np.sqrt(np.mean(samples ** 2)) < np.sqrt(np.mean(original ** 2))
    assert starts == original_starts
    for destination in [OUTPUT] + ([args.preview] if args.preview else []):
        destination.parent.mkdir(parents=True, exist_ok=True)
        with wave.open(str(destination), 'wb') as out:
            out.setnchannels(2)
            out.setsampwidth(2)
            out.setframerate(RATE)
            out.writeframes((samples * 32767).astype('<i2').tobytes())
    print(json.dumps({'seconds': DURATION, 'sample_rate': RATE,
                      'peak': float(np.max(np.abs(samples))),
                      'rms': float(np.sqrt(np.mean(samples ** 2))),
                      'first_pulse_gap': float(np.mean(gaps[:6])),
                      'last_pulse_gap': float(np.mean(gaps[-6:]))}, indent=2))
