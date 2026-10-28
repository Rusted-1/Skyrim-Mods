"""Rule-based weighting for Music Mods Combined (v0.4.0+).

The game picks a track from a music type's list at random, so a track listed n times is n times as
likely. This module turns the merged (unweighted) track set of each music type into a weighted
list that hits the shares in rules.json, hour by hour.

Shares can differ between day and night (Nyghtfall leads at night and is rare by day), which a
fixed list can't express. For the groups whose share changes with the time of day, each track is
replaced by two copies made by build_combined.py: a day copy and a night copy. Each copy is a new
track record in Music_Mods_Combined.esp that plays the same music file with the same conditions
plus a time-of-day check. The day copies and night copies then get separate weights.

Shares are computed per half-hour slot over the 24 h clock from each track's own time conditions
(GetCurrentTime). Other conditions (place, dragon, eclipse) are treated as met.
"""
import json, struct

SLOTS = 48
OPS = ['==', '!=', '>', '>=', '<', '<=']


def load_rules(path):
    r = json.load(open(path, encoding='utf-8'))
    r['type_class'] = {t: c for c, v in r['classes'].items() for t in v.get('types', [])}
    a, b = r['night']
    r['night_slots'] = {s for s in range(SLOTS) if (s / 2 >= a or s / 2 < b)}
    return r


def _cmp(a, op, b):
    return {'==': a == b, '!=': a != b, '>': a > b, '>=': a >= b, '<': a < b, '<=': a <= b}[op]


def _eval(conds, h):
    """Skyrim condition logic: OR binds tighter than AND. Only GetCurrentTime (18) is evaluated."""
    groups, cur = [], []
    for c in conds:
        cur.append(c)
        if not c[0] & 1:
            groups.append(cur); cur = []
    if cur:
        groups.append(cur)

    def test(c):
        if struct.unpack_from('<H', c, 8)[0] != 18:
            return True
        return _cmp(h, OPS[c[0] >> 5], struct.unpack_from('<f', c, 4)[0])
    return all(any(test(c) for c in g) for g in groups)


def mask(conds):
    """Which half-hour slots a track's own time conditions allow."""
    return tuple(_eval(conds, s / 2) for s in range(SLOTS))


def gate_ctda(period, night):
    """Conditions placed FIRST on a copy (so a trailing OR in the original can't join them)."""
    a, b = night

    def c(op, val, orf=False):
        return bytes([(OPS.index(op) << 5) | (1 if orf else 0), 0, 0, 0]) + struct.pack('<fHHIIIIi', val, 18, 0, 0, 0, 0, 0, -1)
    if period == 'N':
        return [c('>=', a, True), c('<', b)]
    return [c('>=', b), c('<', a)]


def gate_mask(period, rules):
    ns = rules['night_slots']
    return tuple((s in ns) == (period == 'N') for s in range(SLOTS))


def targets(cls, present, night, rules):
    """Target share of each group among MUSIC picks, for one slot. present: groups with an eligible track."""
    sh = rules['shares']
    conf = rules['classes'].get(cls, {})
    lead = conf.get('lead')
    if lead not in present:
        lead = None
    if lead is None and night and rules['classes']['default'].get('night_lead') in present and cls == 'default':
        lead = rules['classes']['default']['night_lead']
    t = {}
    if 'Vanilla' in present:
        t['Vanilla'] = sh['vanilla']
    if 'Nyghtfall' in present and lead != 'Nyghtfall':
        t['Nyghtfall'] = sh['nyghtfall_minor']
    if lead:
        t[lead] = sh['lead']
    others = [g for g in present if g not in t]
    rest = max(0.0, 1 - sum(t.values()))
    if others:
        for g in others:
            t[g] = rest / len(others)
    elif lead:
        t[lead] += rest
    elif 'Vanilla' in t:
        t['Vanilla'] += rest
    elif t:
        k = next(iter(t)); t[k] += rest
    z = sum(t.values()) or 1
    return {g: v / z for g, v in t.items()}


def weigh(type_edid, entries, info, pause_share, rules):
    """entries: unique track keys in the merged list.
    info[key] = dict(group=..., silence=bool, mask=tuple)
    Returns (list of (entry, count), report) where entry is a key or (key, 'D'|'N') for a gated copy."""
    cls = rules['type_class'].get(type_edid, 'default')
    # Gate (split into day and night copies) every group whose share must change with the clock:
    # Nyghtfall, vanilla and pauses where Nyghtfall leads at night, plus any group whose number of
    # eligible tracks differs between day and night (e.g. Chapter II's day/night palettes).
    gated_groups = {'Nyghtfall', 'Vanilla', '_pause'} if cls == 'default' else set()
    ns = rules['night_slots']
    for g in {('_pause' if info[k]['silence'] else info[k]['group']) for k in entries}:
        ks = [k for k in entries if ('_pause' if info[k]['silence'] else info[k]['group']) == g]
        cnt = [sum(1 for k in ks if info[k]['mask'][s]) for s in range(SLOTS)]
        if len({cnt[s] for s in ns}) > 1 or len({cnt[s] for s in range(SLOTS) if s not in ns}) > 1 \
                or {cnt[s] for s in ns} != {cnt[s] for s in range(SLOTS) if s not in ns}:
            gated_groups.add(g)
    # Units: entry -> (unit group, mask)
    units = []
    for k in entries:
        g = '_pause' if info[k]['silence'] else info[k]['group']
        if g in gated_groups:
            for p in ('D', 'N'):
                m = tuple(a and b for a, b in zip(info[k]['mask'], gate_mask(p, rules)))
                if any(m):
                    units.append(((k, p), g + ':' + p, m))
        else:
            units.append((k, g, info[k]['mask']))
    wgroups = sorted({u[1] for u in units})
    n = {wg: [sum(1 for u in units if u[1] == wg and u[2][s]) for s in range(SLOTS)] for wg in wgroups}
    base = lambda wg: wg.split(':')[0]

    # Per-slot targets as fractions of all picks (music + pauses)
    T = []
    for s in range(SLOTS):
        present = {base(wg) for wg in wgroups if n[wg][s] and base(wg) != '_pause'}
        has_pause = any(n[wg][s] for wg in wgroups if base(wg) == '_pause')
        tm = targets(cls, present, s in rules['night_slots'], rules)
        p = pause_share if has_pause else 0.0
        row = {g: v * (1 - p) for g, v in tm.items()}
        if has_pause:
            row['_pause'] = p
        T.append(row)

    # Alternating least squares: weight per weight-group, slot totals free.
    w = {wg: 1.0 for wg in wgroups}
    for _ in range(60):
        Z = [sum(w[wg] * n[wg][s] for wg in wgroups) or 1 for s in range(SLOTS)]
        for wg in wgroups:
            num = sum(n[wg][s] * T[s].get(base(wg), 0) * Z[s] for s in range(SLOTS))
            den = sum(n[wg][s] ** 2 for s in range(SLOTS))
            w[wg] = num / den if den else 0.0
    # Integer counts: pick the scale with the lowest share error that fits max_entries.
    best = None
    pos = [w[wg] for wg in wgroups if w[wg] > 0]
    lo = 1 / min(pos) if pos else 1
    per = {wg: sum(1 for u in units if u[1] == wg) for wg in wgroups}
    for i in range(1, 400):
        k = lo * (0.5 + i * 0.05)
        cnt = {wg: (max(1, round(w[wg] * k)) if w[wg] > 0 else 0) for wg in wgroups}
        total = sum(cnt[wg] * per[wg] for wg in wgroups)
        if total > rules['max_entries'] and best:
            break
        err = share_error(cnt, n, T, wgroups, base)
        score = err + total * 3e-6          # prefer shorter lists when the error is about the same
        if best is None or score < best[0] - 1e-9:
            best = (score, cnt, total, err)
    _, cnt, total, err = best
    out = [(u[0], cnt[u[1]]) for u in units if cnt[u[1]] > 0]
    dropped = [u[0] for u in units if cnt[u[1]] == 0]
    rep = dict(cls=cls, err=err, night_slots=rules['night_slots'], total=total, weights={wg: cnt[wg] for wg in wgroups},
               units={wg: per[wg] for wg in wgroups}, dropped=dropped,
               shares=achieved(cnt, n, wgroups, base), targets=T)
    return out, rep


def achieved(cnt, n, wgroups, base):
    res = []
    for s in range(SLOTS):
        mass = {}
        for wg in wgroups:
            mass[base(wg)] = mass.get(base(wg), 0) + cnt[wg] * n[wg][s]
        z = sum(mass.values()) or 1
        res.append({g: v / z for g, v in mass.items() if v})
    return res


def share_error(cnt, n, T, wgroups, base):
    a = achieved(cnt, n, wgroups, base)
    return sum(abs(a[s].get(g, 0) - T[s].get(g, 0)) for s in range(SLOTS) for g in set(a[s]) | set(T[s])) / SLOTS / 2
