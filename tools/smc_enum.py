#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Enumerate ALL SMC keys (read-only) and dump battery/adapter-related values.
Resolves: what does ACIN really mean on THIS machine, is there a current key."""
import ctypes, sys, time

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
    target = us * 1000
    while time.perf_counter_ns() - t0 < target:
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

def transact(cmd, argument, read_len):
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
        r = transact(cmd, arg, read_len)
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

def read_num(key):
    ki = key_info(key)
    if ki is None or ki[0] == 0:
        return None, None
    raw = read_key(key, ki[0])
    if raw is None:
        return ki, None
    return ki, raw

# --- 1) total key count ---
cnt_raw = read_key("#KEY", 4)
count = (cnt_raw[0] << 24) | (cnt_raw[1] << 16) | (cnt_raw[2] << 8) | cnt_raw[3] if cnt_raw else 0
print(f"#KEY count = {count}")

# --- 2) enumerate all keys by index ---
names = []
for idx in range(min(count, 1000)):
    arg = [(idx >> 24) & 0xFF, (idx >> 16) & 0xFF, (idx >> 8) & 0xFF, idx & 0xFF]
    r = try_transact(0x12, arg, 4, retries=2)
    if r is None:
        continue
    names.append(bytes(r).decode('ascii', 'replace'))
print(f"enumerated {len(names)} keys")

# --- 3) filter interesting ones ---
import re
pat = re.compile(r'^(AC|CH|IB|B0|BATP|BF|BR|BS|A[MN]p|TB0|TB1|TB2)', re.I)
interesting = [n for n in names if pat.match(n)]
print(f"battery/adapter-related: {len(interesting)}")
print("KEYS:", ' '.join(sorted(interesting)))

# --- 4) read short values of all interesting keys ---
print()
print("=== values ===")
for k in sorted(interesting):
    ki, raw = read_num(k)
    if ki is None:
        print(f"{k:6s} <no key info>")
        continue
    length, typ, flags = ki
    if length == 0 or length > 32:
        print(f"{k:6s} type={typ} len={length} (skip)")
        continue
    if raw is None:
        print(f"{k:6s} type={typ} len={length} READ-FAIL")
        continue
    hexs = ' '.join(f'{b:02X}' for b in raw)
    extra = ""
    if typ == 'ui16' and len(raw) == 2:
        extra = f" = {(raw[0] << 8) | raw[1]}"
    elif typ == 'ui8' or typ == 'flag':
        extra = f" = {raw[0]}"
    elif typ == 'sp78' and len(raw) == 2:
        v = raw[0] if raw[0] < 128 else raw[0] - 256
        extra = f" = {v}"
    elif typ == 'sp87' and len(raw) == 2:
        v = (raw[0] << 8 | raw[1])
        if v >= 0x8000: v -= 0x10000
        extra = f" = {v / 256.0:.2f}"
    elif typ == 'sp5a' and len(raw) == 2:
        v = (raw[0] << 8 | raw[1]) & 0x7FFF
        s = -v if (raw[0] & 0x80) else v
        extra = f" = {s / 32.0:.3f}"
    print(f"{k:6s} type={typ} len={length} [{hexs}]{extra}")
