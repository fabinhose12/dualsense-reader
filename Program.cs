using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Fleck;
using HidSharp;

namespace DualSenseReader
{
    internal class Program
    {
        private const int SONY_VID = 0x054C;
        private const int DUALSENSE_PID = 0x0CE6;
        private const int DUALSENSE_EDGE_PID = 0x0DF2;

        private static readonly List<IWebSocketConnection> Sockets = new();

        private static byte _offsetLX = 128;
        private static byte _offsetLY = 128;
        private static byte _offsetRX = 128;
        private static byte _offsetRY = 128;

        private static double _deadzoneLeftPercent = 0.10;  // 10% de deadzone padrão
        private static double _deadzoneRightPercent = 0.10; // 10% de deadzone padrão

        private static async Task Main(string[] args)
        {
            Console.Clear();
            Console.WriteLine("===========================================");
            Console.WriteLine(" DualSense WebServer & Reader (Calibrado)  ");
            Console.WriteLine("===========================================\n");

            var server = new WebSocketServer("ws://127.0.0.1:8181");
            server.Start(socket =>
            {
                socket.OnOpen = () =>
                {
                    Console.WriteLine($"\n[Web] Cliente conectado: {socket.ConnectionInfo.Id}");
                    Sockets.Add(socket);
                };
                socket.OnClose = () =>
                {
                    Console.WriteLine($"\n[Web] Cliente desconectado: {socket.ConnectionInfo.Id}");
                    Sockets.Remove(socket);
                };
                socket.OnMessage = message => HandleWebMessage(message);
            });

            Console.WriteLine("Servidor WebSocket rodando em ws://127.0.0.1:8181");
            Console.WriteLine("Buscando controle DualSense (PS5)...");

            var list = DeviceList.Local;
            var hidDevice = list.GetHidDevices(SONY_VID)
                                .FirstOrDefault(d => d.ProductID == DUALSENSE_PID || d.ProductID == DUALSENSE_EDGE_PID);

            if (hidDevice == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n[ERRO] DualSense não foi encontrado!");
                Console.ResetColor();
                return;
            }

            if (!hidDevice.TryOpen(out HidStream stream))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\n[ATENÇÃO] Não foi possível abrir a conexão com o controle.");
                Console.ResetColor();
                return;
            }

            using (stream)
            {
                stream.ReadTimeout = 1000;
                byte[] buffer = new byte[hidDevice.GetMaxInputReportLength()];

                Console.WriteLine("\nConectado! Pressione [Ctrl + C] para encerrar.");
                Console.WriteLine("------------------------------------------------------------------");

                using var cts = new CancellationTokenSource();
                Console.CancelKeyPress += (sender, eventArgs) =>
                {
                    eventArgs.Cancel = true;
                    cts.Cancel();
                };

                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cts.Token);

                        if (bytesRead > 0)
                        {
                            var state = ProcessBuffer(buffer, bytesRead);

                            if (state != null)
                            {
                                if (Sockets.Any())
                                {
                                    string json = JsonSerializer.Serialize(state);
                                    foreach (var socket in Sockets.ToList())
                                    {
                                        socket.Send(json);
                                    }
                                }

                                PrintTerminalOutput(state);
                            }
                        }
                    }
                    catch (OperationCanceledException) { break; }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"\nErro durante a leitura: {ex.Message}");
                        break;
                    }

                    await Task.Delay(10, cts.Token);
                }
            }
        }

        private static void HandleWebMessage(string message)
        {
            try
            {
                using var doc = JsonDocument.Parse(message);
                var root = doc.RootElement;

                if (root.TryGetProperty("action", out var actionProp))
                {
                    string action = actionProp.GetString() ?? "";

                    if (action == "calibrateCenter")
                    {
                        _offsetLX = _lastRawLX;
                        _offsetLY = _lastRawLY;
                        _offsetRX = _lastRawRX;
                        _offsetRY = _lastRawRY;
                        Console.WriteLine($"\n[CALIBRAÇÃO] Novo centro gravado: LS({_offsetLX},{_offsetLY}) RS({_offsetRX},{_offsetRY})");
                    }
                    else if (action == "resetCalibration")
                    {
                        _offsetLX = 128;
                        _offsetLY = 128;
                        _offsetRX = 128;
                        _offsetRY = 128;
                        Console.WriteLine("\n[CALIBRAÇÃO] Ponto zero restaurado para o valor de fábrica (128).");
                    }
                    else if (action == "setDeadzone" && root.TryGetProperty("value", out var valProp))
                    {
                        double val = valProp.GetDouble() / 100.0; // Converte 0-50% para 0.0-0.5
                        _deadzoneLeftPercent = val;
                        _deadzoneRightPercent = val;
                        Console.WriteLine($"\n[CALIBRAÇÃO] Deadzone ajustada para {val * 100:F0}%");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nErro ao processar mensagem Web: {ex.Message}");
            }
        }

        private static byte _lastRawLX = 128, _lastRawLY = 128, _lastRawRX = 128, _lastRawRY = 128;

        private static DualSenseState? ProcessBuffer(byte[] buffer, int length)
        {
            if (length < 11) return null;

            _lastRawLX = buffer[1];
            _lastRawLY = buffer[2];
            _lastRawRX = buffer[3];
            _lastRawRY = buffer[4];

            byte b8 = buffer[8];
            byte b9 = buffer[9];
            byte b10 = buffer[10];

            var (calLX, calLY) = ApplyAnalogCalibration(_lastRawLX, _lastRawLY, _offsetLX, _offsetLY, _deadzoneLeftPercent);
            var (calRX, calRY) = ApplyAnalogCalibration(_lastRawRX, _lastRawRY, _offsetRX, _offsetRY, _deadzoneRightPercent);

            return new DualSenseState
            {
                RawLX = _lastRawLX,
                RawLY = _lastRawLY,
                RawRX = _lastRawRX,
                RawRY = _lastRawRY,

                CalibratedLX = calLX,
                CalibratedLY = calLY,
                CalibratedRX = calRX,
                CalibratedRY = calRY,

                L2 = (int)Math.Round((buffer[5] / 255.0) * 100),
                R2 = (int)Math.Round((buffer[6] / 255.0) * 100),
                DPad = b8 & 0x0F,
                Square = (b8 & 0x10) != 0,
                Cross = (b8 & 0x20) != 0,
                Circle = (b8 & 0x40) != 0,
                Triangle = (b8 & 0x80) != 0,
                L1 = (b9 & 0x01) != 0,
                R1 = (b9 & 0x02) != 0,
                L2Click = (b9 & 0x04) != 0,
                R2Click = (b9 & 0x08) != 0,
                Create = (b9 & 0x10) != 0,
                Options = (b9 & 0x20) != 0,
                L3 = (b9 & 0x40) != 0,
                R3 = (b9 & 0x80) != 0,
                PS = (b10 & 0x01) != 0,
                Touchpad = (b10 & 0x02) != 0,
                Mute = (b10 & 0x04) != 0
            };
        }

        private static (double normX, double normY) ApplyAnalogCalibration(byte rawX, byte rawY, byte offsetX, byte offsetY, double deadzonePercent)
        {
            double x = (rawX - offsetX) / 128.0;
            double y = (rawY - offsetY) / 128.0;

            double magnitude = Math.Sqrt(x * x + y * y);

            if (magnitude < deadzonePercent)
            {
                return (0.0, 0.0);
            }

            double normalizedMagnitude = Math.Min(1.0, (magnitude - deadzonePercent) / (1.0 - deadzonePercent));
            double scale = normalizedMagnitude / magnitude;

            return (x * scale, y * scale);
        }

        private static void PrintTerminalOutput(DualSenseState state)
        {
            Console.SetCursorPosition(0, Console.CursorTop);
            Console.Write($"LS:({state.CalibratedLX,5:F2},{state.CalibratedLY,5:F2}) | RS:({state.CalibratedRX,5:F2},{state.CalibratedRY,5:F2}) | L2:{state.L2,3}% R2:{state.R2,3}%   ");
        }
    }

    public class DualSenseState
    {
        public byte RawLX { get; set; }
        public byte RawLY { get; set; }
        public byte RawRX { get; set; }
        public byte RawRY { get; set; }

        public double CalibratedLX { get; set; }
        public double CalibratedLY { get; set; }
        public double CalibratedRX { get; set; }
        public double CalibratedRY { get; set; }

        public int L2 { get; set; }
        public int R2 { get; set; }
        public bool L2Click { get; set; }
        public bool R2Click { get; set; }
        public int DPad { get; set; }
        public bool Square { get; set; }
        public bool Cross { get; set; }
        public bool Circle { get; set; }
        public bool Triangle { get; set; }
        public bool L1 { get; set; }
        public bool R1 { get; set; }
        public bool L3 { get; set; }
        public bool R3 { get; set; }
        public bool Create { get; set; }
        public bool Options { get; set; }
        public bool PS { get; set; }
        public bool Touchpad { get; set; }
        public bool Mute { get; set; }
    }
}