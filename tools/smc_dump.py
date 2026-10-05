#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Full SMC dump: enumerate all keys, read key_info (len/type/flags) + value.
Output: JSON (machine-readable) + categorized markdown summary (human-readable).
READ-ONLY. Run: Windows Python as admin.

Flag semantics (calibrated 2026-10-05 on this MBP14,3):
  BCLM 0xD0 -> writable (verified), CH0B 0xC4 -> writable (verified),
  CHIM 0x80 -> write ignored (verified). Common bit: 0x40 -> *probable* writable.
  Treated as HYPOTHESIS, not verified for every key.
"""
import ctypes, sys, time, struct, json, re

DLL = r"C:\Users\ryz\Desktop\BootCampCharge\inpoutx64.dll"
io = ctypes.WinDLL(DLL)
io.DlPortReadPortUchar.argtypes = [ctypes.c_ushort]
io.DlPortReadPortUchar.restype = ctypes.c_ubyte
io.DlPortWritePortUchar.argtypes = [ctypes.c_ushort, ctypes.c_ubyte]
io.DlPortWritePortUchar.restype = None
io.IsInpOutDriverOpen.restype = ctypes.c_bool

if not io.IsInpOutDriverOpen():
    print("ERROR: InpOut driver not open"); sys.exit(1)

DATA, CMD = 0x300, 0x304
S_READY, S_SETTLING, S_ACCEPTED = 0x01, 0x02, 0x04

def microdelay(us):
    t0 = time.perf_counter_ns()
    while time.perf_counter_ns() - t0 < us * 1000:
        pass

def send_byte(value, port):
    io.DlPortWritePortUchar(port, value)
    for i in range(16):
        microdelay(16)
        st = io.DlPortReadPortUchar(CMD)
        if st & S_SETTLING:
            continue
        if st & S_ACCEPTED:
            return True
        if i == 15:
            break
        microdelay(256)
        io.DlPortWritePortUchar(port, value)
    return False

def wait_read():
    us = 16
    while us < 0x8000:
        microdelay(us)
        if io.DlPortReadPortUchar(CMD) & S_READY:
            return True
        us <<= 1
    return False

def drain():
    for _ in range(16):
        microdelay(16)
        if not (io.DlPortReadPortUchar(CMD) & S_READY):
            break
        io.DlPortReadPortUchar(DATA)

def _transact(cmd, argument, read_len):
    if not send_byte(cmd, CMD):
        return None
    for b in argument:
        if not send_byte(b, DATA):
            return None
    if not send_byte(read_len, DATA):
        return None
    out = []
    for _ in range(read_len):
        if not wait_read():
            return None
        out.append(io.DlPortReadPortUchar(DATA))
    drain()
    return out

def try_transact(cmd, arg, read_len, retries=2):
    for attempt in range(retries):
        r = _transact(cmd, arg, read_len)
        if r is not None:
            return r
        drain()
        time.sleep(0.002 * (attempt + 1))
    return None

def key_info(key):
    r = try_transact(0x13, key.encode('ascii'), 6)
    if r is None:
        return None
    return r[0], bytes(r[1:5]).decode('ascii', 'replace'), r[5]

def read_key(key, length):
    return try_transact(0x10, key.encode('ascii'), length)

def decode(typ, raw):
    """Best-effort decode to a number/flag; raw hex always kept too."""
    try:
        if typ == 'ui8' or typ == 'flag': return raw[0]
        if typ == 'si8': return raw[0] - 256 if raw[0] > 127 else raw[0]
        if typ == 'ui16': return (raw[0] << 8) | raw[1]
        if typ == 'si16':
            v = (raw[0] << 8) | raw[1]
            return v - 0x10000 if v >= 0x8000 else v
        if typ == 'ui32': return (raw[0]<<24)|(raw[1]<<16)|(raw[2]<<8)|raw[3]
        if typ == 'sp78':
            v = (raw[0] << 8) | raw[1]
            if v >= 0x8000: v -= 0x10000
            return round(v / 256.0, 3)
        if typ == 'sp87':
            v = (raw[0] << 8) | raw[1]
            if v >= 0x8000: v -= 0x10000
            return round(v / 64.0, 3)  # sp87: 1 sign + 7 int + 8 frac
        if typ == 'sp5a':
            v = ((raw[0] << 8) | raw[1]) & 0x7FFF
            v = -v if (raw[0] & 0x80) else v
            return round(v / 32.0, 3)
        if typ == 'flt':
            return round(struct.unpack('>f', bytes(raw))[0], 4)
        if typ == 'fp2e':
            return round(struct.unpack('>f', b'\x00' + bytes(raw[:3]))[0], 4) * 2**14
        if typ == 'fpe2':
            return round(struct.unpack('>h', bytes(raw[:2]))[0] / 100.0, 4)
        if typ == 'fp88':
            return round((raw[0] << 8 | raw[1]) / 256.0, 3)
    except Exception:
        pass
    return None

# ---------- enumerate all keys ----------
cnt_raw = read_key("#KEY", 4)
count = (cnt_raw[0]<<24)|(cnt_raw[1]<<16)|(cnt_raw[2]<<8)|cnt_raw[3] if cnt_raw else 0
print(f"#KEY count = {count}", flush=True)

names = []
for idx in range(count):
    arg = [(idx>>24)&0xFF, (idx>>16)&0xFF, (idx>>8)&0xFF, idx&0xFF]
    r = try_transact(0x12, arg, 4)
    if r is None:
        continue
    names.append(bytes(r).decode('ascii', 'replace'))
print(f"enumerated {len(names)} keys", flush=True)

# ---------- read every key ----------
dump = []
fails = []
t0 = time.time()
for i, k in enumerate(names):
    ki = key_info(k)
    if ki is None:
        fails.append({"key": k, "stage": "key_info"})
        continue
    length, typ, flags = ki
    entry = {"key": k, "len": length, "type": typ, "flags": flags,
             "flags_hex": f"0x{flags:02X}", "writable_likely": bool(flags & 0x40)}
    if length == 0:
        entry["value"] = None
        entry["note"] = "len=0"
    elif length > 64:
        entry["value"] = None
        entry["note"] = f"len={length} too long"
    else:
        raw = read_key(k, length)
        if raw is None:
            entry["value"] = None
            entry["note"] = "READ-FAIL"
            fails.append({"key": k, "stage": "read"})
        else:
            entry["raw"] = ' '.join(f'{b:02X}' for b in raw)
            entry["value"] = decode(typ, raw)
            entry["note"] = ""
    dump.append(entry)
    if (i + 1) % 100 == 0:
        print(f"  ...{i+1}/{len(names)} ({time.time()-t0:.0f}s)", flush=True)

print(f"done: {len(dump)} keys, {len(fails)} fails, {time.time()-t0:.0f}s", flush=True)

# ---------- save JSON ----------
out_json = r"C:\Users\ryz\AppData\Local\Hermes\tools\smcscan\smc-full-dump.json"
with open(out_json, 'w', encoding='utf-8') as f:
    json.dump({"generated": time.strftime("%Y-%m-%d %H:%M:%S"),
               "machine": "MBP14,3 (2017 15\", pre-T2)", "total": len(dump),
               "keys": dump, "fails": fails}, f, ensure_ascii=False, indent=1)
print(f"JSON saved: {out_json}")

# ---------- stats ----------
writable = [e for e in dump if e.get("writable_likely")]
print(f"\nkeys with 0x40 flag (LIKELY writable): {len(writable)}")
for e in sorted(writable, key=lambda x: x['key']):
    v = e.get('value')
    print(f"  {e['key']:6s} {e['type']:5s} len={e['len']:<2d} flags={e['flags_hex']} value={v} raw=[{e.get('raw','')}]")
