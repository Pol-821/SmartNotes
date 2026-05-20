"""
SmartNotes - Raspberry Pi Client
Gravació d'àudio amb ReSpeaker 2-mics + servo motor + botons
"""

import json
import math
import os
import subprocess
import sys
import time
import wave
from collections import deque
from enum import Enum
from pathlib import Path

import numpy as np
import pyaudio
import requests
from gpiozero import AngularServo, Button

# ──────────────────────── CONFIGURACIÓ ────────────────────────

SCRIPT_DIR = Path(__file__).parent
CONFIG_PATH = SCRIPT_DIR / "config.json"
SOUNDS_DIR = SCRIPT_DIR / "sounds"

with open(CONFIG_PATH) as f:
    cfg = json.load(f)

SERIAL_NUMBER = cfg["serial_number"]
API_BASE_URL = cfg["api_base_url"]
API_KEY = cfg["raspberry_api_key"]

SERVO_PIN = cfg["servo_pin"]
BTN_POWER_PIN = cfg["btn_power_pin"]
BTN_RECORD_PIN = cfg["btn_record_pin"]

SERVO_MIN_PULSE = cfg["servo_min_pulse"]
SERVO_MAX_PULSE = cfg["servo_max_pulse"]
SERVO_STEP = cfg["servo_step_degrees"]
SERVO_INTERVAL = cfg["servo_interval_seconds"]
SERVO_MAX_ANGLE = cfg["servo_max_angle"]

RATE = cfg["audio_sample_rate"]
CHANNELS = cfg["audio_channels"]
CHUNK = cfg["audio_chunk_size"]
FORMAT = pyaudio.paInt16

VOLUME_THRESHOLD = cfg["volume_threshold"]
DIFF_THRESHOLD = cfg["diff_threshold"]
HYSTERESIS = cfg["hysteresis"]
SMOOTHING_WINDOW = cfg["smoothing_window"]

SOUND_FILES = {
    "engegat": "engegat.wav",
    "apagat": "apagat.wav",
    "grabant": "grabant.wav",
    "enviant": "enviant_audio.wav",
}

# ──────────────────────── ESTATS ────────────────────────

class State(Enum):
    DISARMED = 0
    ARMED = 1
    RECORDING = 2

# ──────────────────────── SONS ────────────────────────

def _generate_beep_wav(path, freq=880, duration=0.15, volume=0.5):
    """Genera un to senzill WAV com a fallback."""
    sample_rate = 22050
    n_samples = int(sample_rate * duration)
    t = np.linspace(0, duration, n_samples, endpoint=False)
    wave_data = (volume * 32767 * np.sin(2 * np.pi * freq * t)).astype(np.int16)
    with wave.open(str(path), "w") as wf:
        wf.setnchannels(1)
        wf.setsampwidth(2)
        wf.setframerate(sample_rate)
        wf.writeframes(wave_data.tobytes())

def _generate_speech_wav(path, text):
    """Genera un WAV de parla usant espeak. Fallback a to si no està disponible."""
    try:
        subprocess.run(
            ["espeak", "-w", str(path), text],
            capture_output=True, check=True
        )
    except (FileNotFoundError, subprocess.CalledProcessError):
        print(f"  ⚠ espeak no disponible, generant to per '{text}'")
        dur = max(0.3, len(text) * 0.12)
        _generate_beep_wav(path, freq=660, duration=dur)

def _play_sound(name):
    """Reprodueix un so pel altaveu."""
    wav_path = SOUNDS_DIR / SOUND_FILES[name]
    if not wav_path.exists():
        print(f"  ⚠ So '{name}' no trobat, saltant")
        return
    try:
        subprocess.run(
            ["aplay", str(wav_path)],
            capture_output=True, check=False
        )
    except FileNotFoundError:
        print("  ⚠ aplay no disponible, saltant so")

def ensure_sounds():
    """Genera tots els fitxers de so si no existeixen."""
    SOUNDS_DIR.mkdir(parents=True, exist_ok=True)
    for name, filename in SOUND_FILES.items():
        path = SOUNDS_DIR / filename
        if path.exists():
            continue
        text_map = {
            "engegat": "Engegat",
            "apagat": "Apagat",
            "grabant": "Grabant",
            "enviant": "Enviant audio",
        }
        print(f"  🔊 Generant so: {name}...")
        _generate_speech_wav(path, text_map[name])

# ──────────────────────── SERVER (direcció d'àudio) ────────────────────────

class AudioDirectionDetector:
    """Detecta si el so ve de l'esquerra o dreta usant mitjana mòbil amb histèresi."""

    def __init__(self, threshold, diff_threshold, hysteresis, window_size):
        self.threshold = threshold
        self.diff_threshold = diff_threshold
        self.hysteresis = hysteresis
        self.history = deque(maxlen=window_size)
        self.last_direction = 0

    def analyze(self, data):
        audio = np.frombuffer(data, dtype=np.int16).astype(np.float64)
        left = audio[0::2]
        right = audio[1::2]

        rms_left = np.sqrt(np.mean(left ** 2))
        rms_right = np.sqrt(np.mean(right ** 2))

        # Si els dos canals estan en silenci, no fer res
        if rms_left < self.threshold and rms_right < self.threshold:
            return 0

        diff = rms_left - rms_right
        self.history.append(diff)

        if len(self.history) < self.history.maxlen:
            return 0

        smoothed = sum(self.history) / len(self.history)

        # Histèresi: cal superar el llindar + la zona morta
        if smoothed > (self.diff_threshold + self.hysteresis):
            self.last_direction = 1  # esquerra
        elif smoothed < -(self.diff_threshold + self.hysteresis):
            self.last_direction = -1  # dreta
        else:
            return 0

        return self.last_direction

# ──────────────────────── SERVO ────────────────────────

class SmoothServo:
    """Control del servo amb moviment suau i desacoblament per evitar tremolors."""

    def __init__(self, pin, min_pulse, max_pulse, max_angle, step_deg, min_interval):
        self.servo = AngularServo(
            pin,
            min_angle=-max_angle,
            max_angle=max_angle,
            min_pulse_width=min_pulse,
            max_pulse_width=max_pulse,
        )
        self.max_angle = max_angle
        self.step = step_deg
        self.min_interval = min_interval

        self.current_angle = 0.0
        self.target_angle = 0.0
        self.is_moving = False
        self.move_start_time = 0
        self.move_duration = 0.8
        self.start_angle = 0.0
        self.last_move_time = 0

    def calibrate(self):
        print("  ⚙ Calibrant servo...")
        self.servo.angle = -self.max_angle
        time.sleep(1.5)
        self.servo.angle = 0
        time.sleep(1.5)
        self.servo.detach()
        self.current_angle = 0.0
        self.target_angle = 0.0
        print("  ✅ Servo calibrat")

    def request_move(self, direction):
        """Sol·licita un moviment si ha passat l'interval mínim."""
        now = time.time()
        if now - self.last_move_time < self.min_interval:
            return False

        new_target = self.target_angle - (self.step * direction)
        new_target = max(-self.max_angle, min(self.max_angle, new_target))

        if new_target == self.target_angle:
            return False

        self.start_angle = self.current_angle
        self.target_angle = new_target
        self.is_moving = True
        self.move_start_time = now
        self.last_move_time = now
        return True

    def update(self):
        """Actualitza la posició del servo (cridar cada iteració del bucle)."""
        now = time.time()
        if not self.is_moving:
            return

        elapsed = now - self.move_start_time
        if elapsed >= self.move_duration:
            self.servo.angle = self.target_angle
            self.current_angle = self.target_angle
            self.servo.detach()
            self.is_moving = False
            return

        t = elapsed / self.move_duration
        ease = (1 - math.cos(math.pi * t)) / 2
        angle = self.start_angle + (self.target_angle - self.start_angle) * ease
        self.servo.angle = angle
        self.current_angle = angle

    def center_and_detach(self):
        self.servo.angle = 0
        time.sleep(1)
        self.servo.detach()
        self.current_angle = 0.0
        self.target_angle = 0.0
        self.is_moving = False

    def cleanup(self):
        self.servo.detach()

# ──────────────────────── AUDIO ────────────────────────

class AudioRecorder:
    """Gravació d'àudio des del ReSpeaker."""

    def __init__(self, rate, channels, chunk, fmt):
        self.rate = rate
        self.channels = channels
        self.chunk = chunk
        self.fmt = fmt
        self.p = pyaudio.PyAudio()
        self.stream = None
        self.frames = []
        self.is_recording = False
        self.input_device = self._find_device()

    def _find_device(self):
        for i in range(self.p.get_device_count()):
            info = self.p.get_device_info_by_index(i)
            name = info["name"].lower()
            if "seeed" in name or "wm8960" in name or "reSpeaker" in name:
                print(f"  🎤 Dispositiu trobat: {info['name']} (índex {i})")
                return i
        print("  ⚠ ReSpeaker no trobat, usant dispositiu per defecte")
        return None

    def start(self):
        self.frames = []
        self.is_recording = True
        self.stream = self.p.open(
            format=self.fmt,
            channels=self.channels,
            rate=self.rate,
            input=True,
            frames_per_buffer=self.chunk,
            input_device_index=self.input_device,
        )

    def read_chunk(self):
        if not self.is_recording or self.stream is None:
            return None
        try:
            data = self.stream.read(self.chunk, exception_on_overflow=False)
        except OSError as e:
            print(f"  ⚠ Error de lectura d'àudio: {e}. Reobrint stream...")
            self.stop()
            self.start()
            return None
        self.frames.append(data)
        return data

    def stop(self):
        self.is_recording = False
        if self.stream:
            self.stream.stop_stream()
            self.stream.close()
            self.stream = None

    def save_wav(self, path):
        if not self.frames:
            return False
        with wave.open(str(path), "wb") as wf:
            wf.setnchannels(self.channels)
            wf.setsampwidth(self.p.get_sample_size(self.fmt))
            wf.setframerate(self.rate)
            wf.writeframes(b"".join(self.frames))
        return True

    def cleanup(self):
        self.stop()
        self.p.terminate()

# ──────────────────────── API ────────────────────────

class SmartNotesAPI:
    """Puja l'àudio al backend."""

    def __init__(self, base_url, serial, api_key):
        self.upload_url = f"{base_url}/transcription/raspberry"
        self.headers = {
            "X-Serial-Number": serial,
            "X-Raspberry-Key": api_key,
        }

    def upload(self, wav_path):
        if not wav_path.exists():
            print("  ✗ Fitxer d'àudio no trobat")
            return False
        try:
            with open(wav_path, "rb") as f:
                files = {"audioFile": ("audio.wav", f, "audio/wav")}
                resp = requests.post(self.upload_url, headers=self.headers, files=files, timeout=120)
            if resp.ok:
                print(f"  ✅ Àudio pujat correctament (jobId: {resp.json().get('jobId', '?')})")
                return True
            else:
                print(f"  ✗ Error al pujar: {resp.status_code} - {resp.text}")
                return False
        except requests.exceptions.RequestException as e:
            print(f"  ✗ Error de connexió: {e}")
            return False

# ──────────────────────── BOTONS ────────────────────────

class ButtonHandler:
    """Gestiona els botons amb debounce."""

    def __init__(self, power_pin, record_pin, on_power, on_record):
        self.btn_power = Button(power_pin, pull_up=True, bounce_time=0.05)
        self.btn_record = Button(record_pin, pull_up=True, bounce_time=0.05)
        self.btn_power.when_pressed = on_power
        self.btn_record.when_pressed = on_record

    def cleanup(self):
        self.btn_power.close()
        self.btn_record.close()

# ──────────────────────── PROGRAMA PRINCIPAL ────────────────────────

def main():
    print("=" * 50)
    print(" SmartNotes - Client Raspberry Pi")
    print("=" * 50)

    # Comprovar configuració
    if not API_KEY:
        print("❌ ERROR: raspberry_api_key buit a config.json")
        print("   Posa la clau API global del servidor (Raspberry:ApiKey)")
        sys.exit(1)

    print(f"\n📡 Serial Number: {SERIAL_NUMBER}")
    print(f"🌐 API: {API_BASE_URL}")

    # Preparar sons
    print("\n🔊 Preparant sons...")
    ensure_sounds()

    # Inicialitzar components
    print("\n🔧 Inicialitzant components...")
    detector = AudioDirectionDetector(VOLUME_THRESHOLD, DIFF_THRESHOLD, HYSTERESIS, SMOOTHING_WINDOW)
    servo = SmoothServo(SERVO_PIN, SERVO_MIN_PULSE, SERVO_MAX_PULSE, SERVO_MAX_ANGLE, SERVO_STEP, SERVO_INTERVAL)
    recorder = AudioRecorder(RATE, CHANNELS, CHUNK, FORMAT)
    api = SmartNotesAPI(API_BASE_URL, SERIAL_NUMBER, API_KEY)

    servo.calibrate()

    # Estat
    state = State.DISARMED
    current_recording_path = None
    btn_lock = False
    lock_time = 0

    def on_power_press():
        nonlocal state, current_recording_path, btn_lock, lock_time
        now = time.time()

        # Evitar rebots ràpids
        if btn_lock and (now - lock_time) < 0.3:
            return
        btn_lock = True
        lock_time = now

        if state == State.DISARMED:
            state = State.ARMED
            print("\n🟢 SISTEMA ENCÉS")
            _play_sound("engegat")

        elif state == State.ARMED:
            state = State.DISARMED
            print("\n🔴 SISTEMA APAGAT")
            _play_sound("apagat")

        elif state == State.RECORDING:
            state = State.DISARMED
            print("\n⏹ Aturant gravació (sense desar)...")
            recorder.stop()
            current_recording_path = None
            _play_sound("apagat")

        time.sleep(0.05)
        btn_lock = False

    def on_record_press():
        nonlocal state, current_recording_path, btn_lock, lock_time
        now = time.time()

        if btn_lock and (now - lock_time) < 0.3:
            return
        btn_lock = True
        lock_time = now

        if state == State.ARMED:
            # Iniciar gravació
            state = State.RECORDING
            print("\n🔴 GRAVANT...")
            recorder.start()
            _play_sound("grabant")

        elif state == State.RECORDING:
            # Aturar gravació i pujar
            state = State.ARMED
            print("\n⏹ Aturant gravació...")
            recorder.stop()
            _play_sound("enviant")

            # Guardar
            timestamp = time.strftime("%Y%m%d_%H%M%S")
            wav_path = SCRIPT_DIR / f"audio_{timestamp}.wav"
            print(f"  💾 Guardant àudio...")
            if recorder.save_wav(wav_path):
                print(f"  ✅ Guardat: {wav_path.name}")
                print(f"  📤 Pujant a l'API...")
                api.upload(wav_path)
                os.remove(wav_path)
                print(f"  🗑 Fitxer local esborrat")
            else:
                print("  ✗ Error al guardar l'àudio")

        time.sleep(0.05)
        btn_lock = False

    buttons = ButtonHandler(BTN_POWER_PIN, BTN_RECORD_PIN, on_power_press, on_record_press)
    print("\n✅ Sistema llest. Prem el botó d'encesa per començar.\n")

    state_map_str = {
        State.DISARMED: "APAGAT",
        State.ARMED: "ENCÉS",
        State.RECORDING: "GRAVANT",
    }
    last_print = ""

    try:
        while True:
            now = time.time()

            if state == State.RECORDING:
                data = recorder.read_chunk()
                if data is not None:
                    direction = detector.analyze(data)
                    if direction != 0:
                        moved = servo.request_move(direction)
                        if moved:
                            side = "ESQUERRA" if direction == 1 else "DRETA"
                            print(f"    Servo -> {side} ({servo.target_angle:.0f}º)")
                servo.update()

            elif state == State.ARMED or state == State.DISARMED:
                servo.update()

            # Mostrar estat cada 5 segons
            status = state_map_str[state]
            if status != last_print:
                print(f"  📌 Estat: {status}")
                last_print = status

            time.sleep(0.01)

    except KeyboardInterrupt:
        print("\n\n⏹ Aturant...")
    finally:
        if state == State.RECORDING:
            recorder.stop()
        buttons.cleanup()
        servo.center_and_detach()
        recorder.cleanup()
        print("👋 Fet!")

if __name__ == "__main__":
    main()
