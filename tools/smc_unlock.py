#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Unlock charge-inhibit via SMC key writes (InpOut driver, no shutdown).
Escalation: CH0B toggle 2->0, then CHIM=0, then BCLM=100 (factory default).
All writes reversible; previously exercised by SmcDiag on this machine."""
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

def try_transact(cmd, arg, read_len, retries=3):
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

def read_num(key):
    ki = key_info(key)
    if ki is None or ki[0] == 0:
        return None
    return read_key(key, ki[0])

def write_key(key, data_bytes):
    arg = key.encode('ascii') + bytes(data_bytes)
    # transact write: cmd 0x11, arg=4 key bytes, len byte, data bytes
    if not send_byte(0x11, CMD):
        return False
    for b in key.encode('ascii'):
        if not send_byte(b, DATA):
            return False
    if not send_byte(len(data_bytes), DATA):
        return False
    for b in data_bytes:
        if not send_byte(b, DATA):
            return False
    drain()
    return True

def write_verified(key, value):
    ok = write_key(key, [value])
    if not ok:
        print(f"  WRITE {key}={value}: send FAILED")
        return False
    time.sleep(0.5)
    rb = read_num(key)
    if rb is None:
        print(f"  WRITE {key}={value}: sent, readback FAIL")
        return False
    if rb[0] == value:
        print(f"  WRITE {key}={value}: OK (readback={rb[0]})")
        return True
    print(f"  WRITE {key}={value}: sent, readback={rb[0]} (IGNORED by firmware)")
    return False

def snap(label):
    ibac = read_num("IBAC")
    chim = read_num("CHIM")
    brsc = read_num("BRSC")
    b0tf = read_num("B0TF")
    if ibac is None or chim is None or brsc is None:
        print(f"  [{label}] read fail")
        return None
    frac = (ibac[1] / 256.0) if len(ibac) >= 2 else 0.0
    amps = ibac[0] + frac
    tf = (b0tf[0] << 8) | b0tf[1] if b0tf else -1
    print(f"  [{label}] IBAC={amps:+.2f}A CHIM={chim[0]} BRSC={brsc[0]}% B0TF={'N/A' if tf==0xFFFF else tf}")
    return chim[0], amps, brsc[0]

# ---------- main ----------
print("=== 0. key write-permission flags (reference: BCLM is known-writable) ===")
for k in ["BCLM", "CH0B", "CHIM", "CH0C", "CHGI", "CHII"]:
    ki = key_info(k)
    if ki is None:
        print(f"{k}: <no key>")
    else:
        print(f"{k}: len={ki[0]} type={ki[1]} flags=0x{ki[2]:02X}")

print()
print("=== 1. baseline ===")
snap("t0")

print()
print("=== 2. CH0B full inhibit cycle (2 -> hold 5s -> 0) ===")
write_verified("CH0B", 2)
time.sleep(5)
snap("CH0B=2 held")
write_verified("CH0B", 0)
print("  polling 24s ...")
for i in range(8):
    time.sleep(3)
    r = snap(f"poll{i+1}")
    if r and r[0] == 0 and r[1] > 0.05:
        print(">>> CHARGE CURRENT DETECTED — unlocked by CH0B cycle")
        break

print()
print("=== 3. CHIM=0 attempt ===")
write_verified("CHIM", 0)
print("  polling 18s ...")
for i in range(6):
    time.sleep(3)
    r = snap(f"poll{i+1}")
    if r and r[0] == 0 and r[1] > 0.05:
        print(">>> CHARGE CURRENT DETECTED — unlocked by CHIM write")
        break

print()
print("=== 4. restore BCLM factory default (100) ===")
write_verified("BCLM", 100)

print()
print("=== 5. final state ===")
snap("final")
for k in ["BCLM", "CH0B", "CHIM", "ACIN", "BATP", "ACPW"]:
    v = read_num(k)
    print(f"  {k} = {v[0] if v else 'FAIL'}")
