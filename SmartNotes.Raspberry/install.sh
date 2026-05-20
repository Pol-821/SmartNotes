#!/bin/bash
# SmartNotes - Instal·lació al Raspberry Pi
# Executa: chmod +x install.sh && sudo ./install.sh

set -e

echo "========================================"
echo " SmartNotes - Instal·lació Raspberry Pi"
echo "========================================"

# 1. Dependències del sistema
echo ""
echo "📦 Instal·lant dependències del sistema..."
apt-get update
apt-get install -y python3-pip python3-pyaudio espeak alsa-utils pigpio

# 2. Activar i iniciar el dimoni pigpio (necessari pel servo)
echo ""
echo "⚙ Activant dimoni pigpio..."
systemctl enable pigpiod
systemctl start pigpiod

# 3. Dependències Python
echo ""
echo "📦 Instal·lant dependències Python..."
pip3 install numpy gpiozero requests pygame

# 4. Configurar àudio (forçar sortida per jack 3.5mm)
echo ""
echo "🔊 Configurant àudio..."
cat > /etc/asound.conf << 'EOF'
pcm.!default {
    type hw
    card 0
}
ctl.!default {
    type hw
    card 0
}
EOF

# 5. Crear servei systemd per iniciar el programa en arrencar
echo ""
echo "⚙ Creant servei systemd..."
PROJECT_DIR="$(cd "$(dirname "$0")" && pwd)"

cat > /etc/systemd/system/smartnotes.service << EOF
[Unit]
Description=SmartNotes Raspberry Pi Client
After=network.target pigpiod.service
Wants=pigpiod.service

[Service]
ExecStartPre=/usr/bin/python3 -c "import pyaudio; import numpy; import gpiozero"
ExecStart=/usr/bin/python3 ${PROJECT_DIR}/main.py
WorkingDirectory=${PROJECT_DIR}
Restart=always
RestartSec=5
User=pi
Group=pi
StandardOutput=journal
StandardError=journal

[Install]
WantedBy=multi-user.target
EOF

systemctl enable smartnotes.service

echo ""
echo "✅ Instal·lació completada!"
echo ""
echo "📝 Abans de reiniciar:"
echo "   1. Edita config.json amb la clau API del servidor:"
echo "      nano ${PROJECT_DIR}/config.json"
echo "   2. Reinicia la Raspberry: sudo reboot"
echo ""
echo "📋 Per veure logs: sudo journalctl -u smartnotes -f"
