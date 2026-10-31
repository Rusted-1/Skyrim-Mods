"""Build ForgottenCity_DelayedStart.esp from scratch (no Creation Kit needed).

The patch is one light (ESL-flagged) plugin with two records:
  * GLOB FCDS_CourierLevel (new)  - the level the courier waits for (default 25).
  * SMQN 000FCBeginQuest (override of ForgottenCity.esp 010DCA3A) - the Story Manager
    node that starts the courier quest. Identical to the original except that its
    "GetLevel(player) >= 5" condition now compares against FCDS_CourierLevel.

Usage:
  python build_patch.py --out ForgottenCity_DelayedStart.esp [--level 25]
"""
import argparse
import struct

FORM_VERSION = 44          # Skyrim SE record form version
HEDR_VERSION = 1.70        # 1.70 works on every SE/AE build (1.71 needs 1.6.1130+)
ESL_FLAG = 0x200

MASTERS = ["Skyrim.esm", "ForgottenCity.esp"]
OWN = len(MASTERS) << 24   # our own new records live at index 02

GLOBAL_FID = OWN | 0x800
SMQN_FID = 0x010DCA3A      # 000FCBeginQuest in ForgottenCity.esp

# Values copied from the original 000FCBeginQuest node (ForgottenCity.esp 1.8)
PARENT_BRANCH = 0x0001320E     # Skyrim.esm Change Location event branch
PREV_SIBLING = 0x000CBA9F      # Skyrim.esm node it sits after
FC_QUEST01 = 0x01012BB9        # 000FCQuest01 (the main quest)
FC_QUESTSTART = 0x010DCFA3     # 000FCQuestStart (puts Cassia's letter on the courier)
PLAYER_REF = 0x00000014

# Condition function indices
GET_QUEST_RUNNING = 56
GET_QUEST_COMPLETED = 543
GET_LEVEL = 80
OP_EQ, OP_GE = 0, 3
FLAG_USE_GLOBAL = 0x04
RUN_SUBJECT, RUN_REFERENCE = 0, 2


def sub(tag, data):
    return tag.encode() + struct.pack("<H", len(data)) + data


def zstr(s):
    return s.encode("cp1252") + b"\0"


def u32(v):
    return struct.pack("<I", v)


def ctda(op, flags, value_bytes, func, p1=0, p2=0, run_on=RUN_SUBJECT, ref=0):
    return sub("CTDA", bytes([(op << 5) | flags]) + b"\0\0\0" + value_bytes
               + struct.pack("<HH", func, 0) + struct.pack("<IIIIi", p1, p2, run_on, ref, -1))


def record(rtype, fid, payload, flags=0):
    return (rtype.encode() + struct.pack("<IIIIHH", len(payload), flags, fid, 0, FORM_VERSION, 0)
            + payload)


def group(label, recs):
    body = b"".join(recs)
    return b"GRUP" + struct.pack("<I", 24 + len(body)) + label.encode() + struct.pack("<IHHHH", 0, 0, 0, 0, 0) + body


def build(level):
    glob = record("GLOB", GLOBAL_FID,
                  sub("EDID", zstr("FCDS_CourierLevel"))
                  + sub("FNAM", b"s")                       # short (integer) global
                  + sub("FLTV", struct.pack("<f", float(level))))

    smqn = record("SMQN", SMQN_FID,
                  sub("EDID", zstr("000FCBeginQuest"))
                  + sub("PNAM", u32(PARENT_BRANCH))
                  + sub("SNAM", u32(PREV_SIBLING))
                  + sub("CITC", u32(3))
                  + ctda(OP_EQ, 0, struct.pack("<f", 0.0), GET_QUEST_RUNNING, p1=FC_QUEST01)
                  + ctda(OP_EQ, 0, struct.pack("<f", 0.0), GET_QUEST_COMPLETED, p1=FC_QUEST01)
                  # GetLevel(PlayerRef) >= FCDS_CourierLevel   (was >= 5.0)
                  + ctda(OP_GE, FLAG_USE_GLOBAL, u32(GLOBAL_FID), GET_LEVEL,
                         run_on=RUN_REFERENCE, ref=PLAYER_REF)
                  + sub("DNAM", u32(0x00020000))            # same flags as original (Shares Event)
                  + sub("XNAM", u32(0))
                  + sub("QNAM", u32(1))
                  + sub("NNAM", u32(FC_QUESTSTART)))

    hdr = sub("HEDR", struct.pack("<fiI", HEDR_VERSION, 2, 0x801)) + sub("CNAM", zstr("Rusted-1"))
    hdr += sub("SNAM", zstr("Delays The Forgotten City's courier letter until the player reaches "
                            "the level in FCDS_CourierLevel (default 25)."))
    for m in MASTERS:
        hdr += sub("MAST", zstr(m)) + sub("DATA", struct.pack("<Q", 0))
    tes4 = record("TES4", 0, hdr, flags=ESL_FLAG)
    return tes4 + group("GLOB", [glob]) + group("SMQN", [smqn])


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default="ForgottenCity_DelayedStart.esp")
    ap.add_argument("--level", type=int, default=25)
    a = ap.parse_args()
    data = build(a.level)
    open(a.out, "wb").write(data)
    print(f"wrote {a.out} ({len(data)} bytes), courier level {a.level}")
