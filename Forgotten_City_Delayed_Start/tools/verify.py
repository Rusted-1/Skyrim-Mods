"""Check ForgottenCity_DelayedStart.esp against the original ForgottenCity.esp.

  python verify.py ForgottenCity_DelayedStart.esp <path to ForgottenCity.esp> [--level 25]

Checks: ESL flag, masters, header version, the FCDS_CourierLevel global, and that the
000FCBeginQuest override matches the original node field-for-field except the level
condition, which must now read GetLevel(PlayerRef) >= FCDS_CourierLevel.
"""
import argparse
import struct
import sys

import esp

ap = argparse.ArgumentParser()
ap.add_argument("patch")
ap.add_argument("original")
ap.add_argument("--level", type=int, default=25)
a = ap.parse_args()
fails = []


def check(ok, msg):
    print(("PASS " if ok else "FAIL ") + msg)
    if not ok:
        fails.append(msg)


pb = open(a.patch, "rb").read()
flags, masters, h = esp.header(pb)
hedr = dict(esp.subs(pb[24:h]))["HEDR"]
check(flags & 0x200 != 0, "patch is ESL-flagged")
check(masters == ["Skyrim.esm", "ForgottenCity.esp"], f"masters {masters}")
check(abs(struct.unpack_from("<f", hedr)[0] - 1.70) < 1e-4, "header version 1.70")
recs = list(esp.records(pb, h))
check(struct.unpack_from("<i", hedr, 4)[0] == len(recs), f"record count {len(recs)}")
check(sorted(r["type"] for r in recs) == ["GLOB", "SMQN"], "exactly one GLOB and one SMQN")
check(all(r.get("version", 44) for r in recs), "records parsed")

glob = next(r for r in recs if r["type"] == "GLOB")
gs = dict(esp.subs(glob["data"]))
check(glob["fid"] == 0x02000800, f"global FormID {glob['fid']:08X} is in the light-plugin range")
check(gs["EDID"] == b"FCDS_CourierLevel\0", "global named FCDS_CourierLevel")
check(gs["FNAM"] == b"s", "global is a short (integer)")
check(struct.unpack("<f", gs["FLTV"])[0] == a.level, f"global value {struct.unpack('<f', gs['FLTV'])[0]}")

pat = next(r for r in recs if r["type"] == "SMQN")
ob = open(a.original, "rb").read()
_, _, oh = esp.header(ob)
orig = next((r for r in esp.records(ob, oh) if r["fid"] == pat["fid"]), None)
check(orig is not None and orig["type"] == "SMQN", f"override target {pat['fid']:08X} is an SMQN in ForgottenCity.esp")
ps, os_ = esp.subs(pat["data"]), esp.subs(orig["data"])
check([t for t, _ in ps] == [t for t, _ in os_], "same subrecord layout as the original node")
ctdas_p = [d for t, d in ps if t == "CTDA"]
ctdas_o = [d for t, d in os_ if t == "CTDA"]
check(ctdas_p[:2] == ctdas_o[:2], "quest-state conditions unchanged")
others_same = all(dp == do for (tp, dp), (to, do) in zip(ps, os_) if tp != "CTDA")
check(others_same, "parent, sibling, flags and quest to start unchanged")

lp, lo = ctdas_p[2], ctdas_o[2]
check(struct.unpack_from("<H", lo, 8)[0] == 80 and struct.unpack_from("<f", lo, 4)[0] == 5.0,
      "original third condition is GetLevel >= 5")
check(struct.unpack_from("<H", lp, 8)[0] == 80, "patched third condition is GetLevel")
check(lp[0] >> 5 == 3, "comparison is >=")
check(lp[0] & 0x04 != 0, "comparison uses a global")
check(struct.unpack_from("<I", lp, 4)[0] == glob["fid"], "global is FCDS_CourierLevel")
check(lp[12:] == lo[12:], "same run-on target (PlayerRef) as the original")

print()
print("ALL CHECKS PASSED" if not fails else f"{len(fails)} CHECK(S) FAILED")
sys.exit(1 if fails else 0)
