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

  // 1. Analógico Esquerdo Calibrado (-1.0 a +1.0)
  if (data.CalibratedLX !== undefined) {
    document.getElementById('valLX').innerText = data.RawLX;
    document.getElementById('valLY').innerText = data.RawLY;

    // Converte os valores calibrados para deslocamento em pixels
    const moveX = data.CalibratedLX * 35;
    const moveY = data.CalibratedLY * 35;
    const stickLeft = document.getElementById('stickLeftDot');
    if (stickLeft) stickLeft.style.transform = `translate(${moveX}px, ${moveY}px)`;
  }

  // 2. Analógico Direito Calibrado (-1.0 a +1.0)
  if (data.CalibratedRX !== undefined) {
    document.getElementById('valRX').innerText = data.RawRX;
    document.getElementById('valRY').innerText = data.RawRY;

    const moveX = data.CalibratedRX * 35;
    const moveY = data.CalibratedRY * 35;
    const stickRight = document.getElementById('stickRightDot');
    if (stickRight) stickRight.style.transform = `translate(${moveX}px, ${moveY}px)`;
  }

  // 3. Gatilhos
  if (data.L2 !== undefined) {
    document.getElementById('valL2').innerText = `${data.L2}%`;
    document.getElementById('barL2').style.height = `${data.L2}%`;
    document.getElementById('clickL2').classList.toggle('active', data.L2Click);

    document.getElementById('valR2').innerText = `${data.R2}%`;
    document.getElementById('barR2').style.height = `${data.R2}%`;
    document.getElementById('clickR2').classList.toggle('active', data.R2Click);
  }

  // 4. Botões de Ação e D-Pad
  toggleActive('btnSquare', data.Square);
  toggleActive('btnCross', data.Cross);
  toggleActive('btnCircle', data.Circle);
  toggleActive('btnTriangle', data.Triangle);

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

// --- Comandos do Painel de Calibração para o C# ---
document.getElementById('btnCalibrate')?.addEventListener('click', () => {
  socket.send(JSON.stringify({ action: 'calibrateCenter' }));
});

document.getElementById('btnReset')?.addEventListener('click', () => {
  socket.send(JSON.stringify({ action: 'resetCalibration' }));
  document.getElementById('sliderDeadzone').value = 10;
  document.getElementById('lblDeadzone').innerText = '10%';
});

document.getElementById('sliderDeadzone')?.addEventListener('input', (e) => {
  const val = e.target.value;
  document.getElementById('lblDeadzone').innerText = `${val}%`;
  socket.send(JSON.stringify({ action: 'setDeadzone', value: parseFloat(val) }));
});