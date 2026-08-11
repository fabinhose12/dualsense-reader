// Conecta ao servidor WebSocket iniciado pelo C#
const socket = new WebSocket('ws://127.0.0.1:8181');

const statusBadge = document.getElementById('statusBadge');
const statusText = document.getElementById('statusText');

socket.onopen = () => {
  if (statusBadge) statusBadge.className = 'status-badge connected';
  if (statusText) statusText.innerText = 'DISPOSITIVO CONECTADO';
};

socket.onclose = () => {
  if (statusBadge) statusBadge.className = 'status-badge';
  if (statusText) statusText.innerText = 'DESCONECTADO DO SERVIDOR';
};

socket.onmessage = (event) => {
  const data = JSON.parse(event.data);

  // 1. Analógico Esquerdo
  if (data.LX !== undefined) {
    document.getElementById('valLX').innerText = data.LX;
    document.getElementById('valLY').innerText = data.LY;
    
    // Mapeia (0-255) para deslocamento visual em pixels (-35px a +35px)
    const moveX = ((data.LX - 128) / 128) * 35;
    const moveY = ((data.LY - 128) / 128) * 35;
    const stickLeft = document.getElementById('stickLeftDot');
    if (stickLeft) stickLeft.style.transform = `translate(${moveX}px, ${moveY}px)`;
  }

  // 2. Analógico Direito
  if (data.RX !== undefined) {
    document.getElementById('valRX').innerText = data.RX;
    document.getElementById('valRY').innerText = data.RY;
    
    const moveX = ((data.RX - 128) / 128) * 35;
    const moveY = ((data.RY - 128) / 128) * 35;
    const stickRight = document.getElementById('stickRightDot');
    if (stickRight) stickRight.style.transform = `translate(${moveX}px, ${moveY}px)`;
  }

  // 3. Gatilhos L2 / R2
  if (data.L2 !== undefined) {
    document.getElementById('valL2').innerText = `${data.L2}%`;
    document.getElementById('barL2').style.height = `${data.L2}%`;
    document.getElementById('clickL2').classList.toggle('active', data.L2Click);

    document.getElementById('valR2').innerText = `${data.R2}%`;
    document.getElementById('barR2').style.height = `${data.R2}%`;
    document.getElementById('clickR2').classList.toggle('active', data.R2Click);
  }

  // 4. Botões de Ação
  toggleActive('btnSquare', data.Square);
  toggleActive('btnCross', data.Cross);
  toggleActive('btnCircle', data.Circle);
  toggleActive('btnTriangle', data.Triangle);

  // 5. D-Pad
  const dpad = data.DPad;
  toggleActive('dpadUp', dpad === 0 || dpad === 1 || dpad === 7);
  toggleActive('dpadRight', dpad === 1 || dpad === 2 || dpad === 3);
  toggleActive('dpadDown', dpad === 3 || dpad === 4 || dpad === 5);
  toggleActive('dpadLeft', dpad === 5 || dpad === 6 || dpad === 7);
};

function toggleActive(id, condition) {
  const el = document.getElementById(id);
  if (el) el.classList.toggle('active', condition);
}