#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Read SMC battery keys via inpoutx64.dll (port 0x300/0x304), pre-T2 protocol.
Read-only: no SMC writes. Mirrors PortTransport.cs logic from BootCampCharge."""
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

def try_transact(cmd, arg, read_len):
    for attempt in range(3):
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
    r = try_transact(0x10, key.encode('ascii'), length)
    return r

# ---- main ----
print("InpOut driver: OPEN")
keys = ["BCLM", "CH0B", "CH0C", "BRSC", "ACIN", "BATP", "B0AV", "B0RM", "B0FC", "B0CT"]
results = {}
for k in keys:
    ki = key_info(k)
    if ki is None:
        print(f"{k}: <no such key / read fail>")
        continue
    length, typ, flags = ki
    raw = read_key(k, length)
    if raw is None:
        print(f"{k}: len={length} type={typ} -> READ FAIL")
        continue
    results[k] = (typ, raw)
    hexs = ' '.join(f'{b:02X}' for b in raw)
    print(f"{k}: len={length} type={typ} raw=[{hexs}]")

print()
bclm = results.get("BCLM")
if bclm:
    print(f"*** BCLM (charge limit) = {bclm[1][0]}  (100 = default/unset)")
ch0b = results.get("CH0B")
if ch0b:
    v = ch0b[1][0]
    print(f"*** CH0B (charge inhibit) = {v}  (0=allow charge, 2=inhibit)")
ch0c = results.get("CH0C")
if ch0c:
    print(f"*** CH0C = {ch0c[1][0]}")
brsc = results.get("BRSC")
if brsc and len(brsc[1]) == 4:
    b = brsc[1]
    pct = (b[0] << 8) | b[1]
    mwh = (b[2] << 8) | b[3]
    print(f"*** BRSC: percent={pct} raw16={mwh}")
ac = results.get("ACIN")
if ac:
    print(f"*** ACIN (charger connected) = {ac[1][0]}")
