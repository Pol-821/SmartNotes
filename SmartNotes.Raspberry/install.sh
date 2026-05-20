#!/bin/bash
# SmartNotes - Instal·lació al Raspberry Pi
# Executa: chmod +x install.sh && sudo ./install.sh

set -e

echo "========================================"
echo " SmartNotes - Instal·lació Raspberry Pi"
echo "========================================"

PROJECT_DIR="$(cd "$(dirname "$0")" && pwd)"

# 1. Dependències del sistema
echo ""
echo "📦 Instal·lant dependències del sistema..."
apt-get update
apt-get install -y python3-pip python3-pyaudio python3-numpy python3-requests espeak alsa-utils

# 2. Instal·lar llibreria GPIO (RPi.GPIO o lgpio)
echo ""
echo "⚙ Instal·lant llibreria GPIO..."
if apt-get install -y python3-rpi.gpio 2>/dev/null; then
    echo "   ✅ RPi.GPIO instal·lat via apt"
elif apt-get install -y python3-lgpio 2>/dev/null; then
    echo "   ✅ lgpio instal·lat via apt"
else
    echo "   ⚠ Instal·lant RPi.GPIO via pip..."
    pip3 install RPi.GPIO --break-system-packages 2>/dev/null || pip3 install RPi.GPIO
fi

# 3. Dependències Python (venv per compatibilitat amb PEP 668)
echo ""
echo "📦 Instal·lant dependències Python (venv)..."
python3 -m venv --system-site-packages "${PROJECT_DIR}/venv"
"${PROJECT_DIR}/venv/bin/pip" install gpiozero pygame
echo "   ✅ venv creat a ${PROJECT_DIR}/venv"

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

# 5. Crear entrada d'autoinici per escriptori (obre terminal)
echo ""
echo "🖥 Creant autoinici a l'escriptori..."
AUTOSTART_DIR="/home/${SUDO_USER:-pi}/.config/autostart"
mkdir -p "${AUTOSTART_DIR}"
cat > "${AUTOSTART_DIR}/smartnotes.desktop" << EOF
[Desktop Entry]
Type=Application
Name=SmartNotes
Comment=SmartNotes Raspberry Pi Client
Exec=lxterminal -e ${PROJECT_DIR}/venv/bin/python ${PROJECT_DIR}/main.py
Terminal=false
StartupNotify=false
EOF

chown -R "${SUDO_USER:-pi}":"${SUDO_USER:-pi}" "${AUTOSTART_DIR}"

# Aturar i deshabilitar servei systemd (si existeix)
systemctl disable smartnotes.service 2>/dev/null || true
systemctl stop smartnotes.service 2>/dev/null || true

echo ""
echo "✅ Instal·lació completada!"
echo ""
echo "📝 Abans de reiniciar:"
echo "   1. Edita config.json amb la clau API:"
echo "      nano ${PROJECT_DIR}/config.json"
echo ""
echo "🖥 Quan reiniciïs, s'obrirà automàticament una finestra de terminal"
echo "   amb el programa en execució."
echo "📋 Tanca la finestra per aturar el programa."
