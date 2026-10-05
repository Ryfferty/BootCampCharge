#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Quick charging-state check: BCLM CH0B CHIM IBAC BRSC B0TF B0AV ACPW ACIC."""
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

def try_transact(cmd, arg, read_len, retries=3):
    for attempt in range(retries):
        r = transact(cmd, arg, read_len)
        if r is not None:
            return r
        drain()
        time.sleep(0.002 * (attempt + 1))
    return None

def key_len(key):
    r = try_transact(0x13, key.encode('ascii'), 6)
    return None if r is None else r[0]

def read(key):
    n = key_len(key)
    if not n:
        return None
    return try_transact(0x10, key.encode('ascii'), n)

def u16(b): return (b[0] << 8) | b[1]
def sp78(b):
    v = (b[0] << 8) | b[1]
    if v >= 0x8000: v -= 0x10000
    return v / 256.0

bclm = read("BCLM"); ch0b = read("CH0B"); chim = read("CHIM")
ibac = read("IBAC"); brsc = read("BRSC"); b0tf = read("B0TF")
b0av = read("B0AV"); b0rm = read("B0RM"); acpw = read("ACPW")
acic = read("ACIC"); acin = read("ACIN"); batp = read("BATP")
chbv = read("CHBV"); chgc = read("CHGC")

print(f"BCLM  = {bclm[0] if bclm else 'FAIL'}   (charge limit %)")
print(f"CH0B  = {ch0b[0] if ch0b else 'FAIL'}   (0=allow)")
print(f"CHIM  = {chim[0] if chim else 'FAIL'}   (charge-inhibit status)")
print(f"IBAC  = {sp78(ibac):+.2f} A  (battery current, + = charging)" if ibac else "IBAC  = FAIL")
print(f"BRSC  = {u16(brsc)} %   (remaining)" if brsc else "BRSC  = FAIL")
print(f"B0TF  = {u16(b0tf)} min (time-to-full; 65535 = not charging)" if b0tf else "B0TF  = FAIL")
print(f"B0AV  = {u16(b0av)} mV  (pack voltage)" if b0av else "B0AV  = FAIL")
print(f"B0RM  = {u16(b0rm)} mAh (remaining)" if b0rm else "B0RM  = FAIL")
print(f"ACPW  = {(acpw[0]<<24|acpw[1]<<16|acpw[2]<<8|acpw[3])/1000:.1f} W  (adapter)" if acpw else "ACPW  = FAIL")
print(f"ACIC  = {u16(acic)} mA  (adapter current)" if acic else "ACIC  = FAIL")
print(f"ACIN  = {acin[0] if acin else 'FAIL'}   BATP = {batp[0] if batp else 'FAIL'}  (flags, unreliable on this Mac)")
print(f"CHBV  = {u16(chbv)} mV (charge voltage)" if chbv else "CHBV  = FAIL")
print(f"CHGC  = {u16(chgc)} (charge status raw)" if chgc else "CHGC  = FAIL")

verdict = (ibac and sp78(ibac) > 0.05) or (b0tf and u16(b0tf) != 0xFFFF)
print()
print(">>> CHARGING NOW" if verdict else ">>> NOT charging")
