"""Rule-based weighting for Music Mods Combined (v0.4.0+; clock periods and pause share from v0.6.0).

The game picks a track from a music type's list at random, so a track listed n times is n times as
likely. This module turns the merged (unweighted) track set of each music type into a weighted
list that hits the shares in rules.json, hour by hour.

Shares can differ by time of day (Nyghtfall leads at night and is rare by day), and the number of
tracks allowed to play changes through the day (tracks have their own hours). A fixed list can't
express either. So for each group that needs it, every track is replaced by one copy per clock
period (rules.json "periods", e.g. 20-22, 22-06, 06-08, 08-18, 18-20), made by build_combined.py.
Each copy is a new track record in Music_Mods_Combined.esp that plays the same music file with the
same conditions plus a time check for its period, and each period's copies get their own weight.

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
    r['period_names'] = [p[0] for p in r['periods']]
    r['period_window'] = {p[0]: (p[1], p[2]) for p in r['periods']}
    covered = [s for p in r['periods'] for s in range(SLOTS) if _in(s / 2, p[1], p[2])]
    if sorted(covered) != list(range(SLOTS)):
        raise SystemExit('rules.json: "periods" must cover the 24 h clock exactly once')
    return r


def _in(h, a, b):
    return a <= h < b if a < b else (h >= a or h < b)


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


def gate_ctda(period, rules):
    """Conditions placed FIRST on a copy (so a trailing OR in the original can't join them)."""
    a, b = rules['period_window'][period]

    def c(op, val, orf=False):
        return bytes([(OPS.index(op) << 5) | (1 if orf else 0), 0, 0, 0]) + struct.pack('<fHHIIIIi', val, 18, 0, 0, 0, 0, 0, -1)
    if a > b:                       # wraps past midnight
        return [c('>=', a, True), c('<', b)]
    return [c('>=', a), c('<', b)]


def gate_mask(period, rules):
    a, b = rules['period_window'][period]
    return tuple(_in(s / 2, a, b) for s in range(SLOTS))


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
    V = rules.get('vanilla_group', 'Vanilla')
    vanilla_peer = cls in rules.get('vanilla_peer_classes', [])
    if V in present and not vanilla_peer:
        t[V] = sh['vanilla']
    if 'Vanilla' in present and V != 'Vanilla':
        t['Vanilla'] = sh.get('vanilla_leftover', 0.0)
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
    elif V in t:
        t[V] += rest
    elif 'Vanilla' in t:
        t['Vanilla'] += rest
    elif t:
        k = next(iter(t)); t[k] += rest
    z = sum(t.values()) or 1
    return {g: v / z for g, v in t.items()}


def weigh(type_edid, entries, info, pause_share, rules):
    """entries: unique track keys in the merged list.
    info[key] = dict(group=..., silence=bool, mask=tuple)
    Returns (list of (entry, count), report) where entry is a key or (key, period) for a gated copy."""
    cls = rules['type_class'].get(type_edid, 'default')
    # Gate (split into one copy per clock period) every group whose share must change with the clock
    # (Nyghtfall, LORKHAN/vanilla and pauses where Nyghtfall leads at night) and every group whose
    # number of eligible tracks changes during the day (e.g. Chapter II's 08:00-18:00 tracks).
    gated_groups = {'Nyghtfall', rules.get('vanilla_group', 'Vanilla'), 'Vanilla', '_pause'} if cls == 'default' else set()
    for g in {('_pause' if info[k]['silence'] else info[k]['group']) for k in entries}:
        ks = [k for k in entries if ('_pause' if info[k]['silence'] else info[k]['group']) == g]
        cnt = [sum(1 for k in ks if info[k]['mask'][s]) for s in range(SLOTS)]
        if len(set(cnt)) > 1:
            gated_groups.add(g)
    # Units: entry -> (unit group, mask)
    units = []
    for k in entries:
        g = '_pause' if info[k]['silence'] else info[k]['group']
        if g in gated_groups:
            for p in rules['period_names']:
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
    smallest = None
    for i in range(1, 400):
        k = lo * (0.02 + i * 0.02) if i < 50 else lo * (0.5 + i * 0.05)
        cnt = {wg: (max(1, round(w[wg] * k)) if w[wg] > 0 else 0) for wg in wgroups}
        total = sum(cnt[wg] * per[wg] for wg in wgroups)
        if smallest is None or total < smallest[2]:
            smallest = (None, cnt, total, share_error(cnt, n, T, wgroups, base))
        if total > rules['max_entries']:
            continue                    # hard cap on list length
        err = share_error(cnt, n, T, wgroups, base)
        score = err + total * 3e-6          # prefer shorter lists when the error is about the same
        if best is None or score < best[0] - 1e-9:
            best = (score, cnt, total, err)
    _, cnt, total, err = best or smallest
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
