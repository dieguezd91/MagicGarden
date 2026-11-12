"""Summarize AudioSessionLogger CSVs into a balance report.

Usage:
    python analyze_audio_session.py [session.csv ...]
With no arguments it reads every CSV in the default Unity persistentDataPath/AudioSessions folder
(Windows: %USERPROFILE%/AppData/LocalLow/<Company>/<Product>/AudioSessions) found under LocalLow.

Flags per sound:
  SPAM       played more than SPAM_PER_MIN times per minute
  QUIET      average heard gain below QUIET_GAIN (likely inaudible in the mix)
  LOUD       average heard gain above LOUD_GAIN
  OFFSCREEN  more than half of its plays were farther than HALF_SCREEN units from the listener
"""
import csv
import glob
import os
import statistics
import sys
from collections import defaultdict

HALF_SCREEN = 11.5  # ortho size 6.43 * 16/9 ~= half the visible width
SPAM_PER_MIN = 60
QUIET_GAIN = 0.05
LOUD_GAIN = 0.8


def default_files():
    base = os.path.join(os.path.expanduser("~"), "AppData", "LocalLow")
    return sorted(glob.glob(os.path.join(base, "*", "*", "AudioSessions", "*.csv")))


def load(paths):
    rows = []
    for p in paths:
        with open(p, newline="", encoding="utf-8") as f:
            for r in csv.DictReader(f):
                r["_file"] = os.path.basename(p)
                for k in ("time", "volume", "spatialBlend", "dist2D", "dist3D", "heardGain", "activeVoices", "poolSize", "listenerZ", "srcZ"):
                    r[k] = float(r[k])
                rows.append(r)
    return rows


def report(rows):
    plays = [r for r in rows if r["type"] == "play"]
    snaps = [r for r in rows if r["type"] == "snap"]
    if not plays:
        print("No play events recorded.")
        return
    by_file = defaultdict(list)
    for r in plays:
        by_file[r["_file"]].append(r["time"])
    minutes = sum((max(t) - min(t)) for t in by_file.values()) / 60 or 1 / 60

    print(f"# Audio session report\n\nFiles: {len(by_file)} | sound events: {len(plays)} | recorded: {minutes:.1f} min\n")
    voices = [r["activeVoices"] for r in plays]
    print(f"Simultaneous pooled voices: avg {statistics.mean(voices):.1f}, max {max(voices):.0f} | pool size max {max(r['poolSize'] for r in plays):.0f}")
    zgap = statistics.mean(abs(r["listenerZ"] - r["srcZ"]) for r in plays if r["spatialBlend"] > 0) if any(r["spatialBlend"] > 0 for r in plays) else 0
    print(f"Average listener-to-source Z gap for spatial sounds: {zgap:.1f} units (adds to every 3D distance)\n")

    groups = defaultdict(list)
    for r in plays:
        groups[r["sound"]].append(r)
    print("| Sound | Plays | /min | Set vol (eff.) | Blend | Avg dist2D | Avg heard | Flags |")
    print("|---|---|---|---|---|---|---|---|")
    stats = []
    for name, rs in groups.items():
        per_min = len(rs) / minutes
        heard = statistics.mean(r["heardGain"] for r in rs)
        off = sum(r["dist2D"] > HALF_SCREEN for r in rs) / len(rs)
        flags = []
        if per_min > SPAM_PER_MIN: flags.append("SPAM")
        if heard < QUIET_GAIN: flags.append("QUIET")
        if heard > LOUD_GAIN: flags.append("LOUD")
        if off > 0.5: flags.append("OFFSCREEN")
        stats.append((heard, name, len(rs), per_min, statistics.mean(r["volume"] for r in rs),
                      statistics.mean(r["spatialBlend"] for r in rs), statistics.mean(r["dist2D"] for r in rs), flags))
    for heard, name, n, pm, vol, blend, d2, flags in sorted(stats, reverse=True):
        print(f"| {name} | {n} | {pm:.1f} | {vol:.2f} | {blend:.2f} | {d2:.1f} | {heard:.3f} | {' '.join(flags)} |")

    if snaps:
        print("\n## Other sources playing (sampled once per second)\n")
        sg = defaultdict(list)
        for r in snaps:
            if r["source"].startswith("PooledAudioSource") or r["source"].startswith("Audio Source Effect"):
                continue
            sg[(r["sound"], r["source"], r["mixerGroup"])].append(r)
        print("| Clip | Object | Mixer | Seconds heard | Avg volume |")
        print("|---|---|---|---|---|")
        for (clip, src, mix), rs in sorted(sg.items(), key=lambda kv: -len(kv[1])):
            print(f"| {clip} | {src} | {mix} | {len(rs)} | {statistics.mean(r['volume'] for r in rs):.2f} |")


if __name__ == "__main__":
    files = sys.argv[1:] or default_files()
    if not files:
        sys.exit("No session CSVs found. Record one in-game with F8 first.")
    report(load(files))
