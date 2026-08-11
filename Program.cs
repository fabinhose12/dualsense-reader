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

        private static async Task Main(string[] args)
        {
            Console.Clear();
            Console.WriteLine("===========================================");
            Console.WriteLine("    DualSense WebServer & Reader v0.2      ");
            Console.WriteLine("===========================================\n");

            // 1. Servidor WebSocket
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
            });

            Console.WriteLine("Servidor WebSocket rodando em ws://127.0.0.1:8181");
            Console.WriteLine("Buscando controle DualSense (PS5)...");

            // 2. Conexão HID
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

                Console.WriteLine($"\nConectado! Pressione [Ctrl + C] para encerrar.");
                Console.WriteLine("------------------------------------------------------------------");

                using var cts = new CancellationTokenSource();
                Console.CancelKeyPress += (sender, eventArgs) =>
                {
                    eventArgs.Cancel = true;
                    cts.Cancel();
                };

                // 3. Loop de leitura de bytes
                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cts.Token);

                        if (bytesRead > 0)
                        {
                            // A) Processa os dados do buffer
                            var state = ProcessBuffer(buffer, bytesRead);

                            if (state != null)
                            {
                                // B) Se houver navegadores conectados via WebSocket, envia o JSON
                                if (Sockets.Any())
                                {
                                    string json = JsonSerializer.Serialize(state);
                                    foreach (var socket in Sockets.ToList())
                                    {
                                        socket.Send(json);
                                    }
                                }

                                // C) Imprime o estado detalhado no terminal
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

        // 4. Mapeamento completo dos dados
        private static DualSenseState? ProcessBuffer(byte[] buffer, int length)
        {
            if (length < 11) return null;

            byte b8 = buffer[8];
            byte b9 = buffer[9];
            byte b10 = buffer[10];

            return new DualSenseState
            {
                LX = buffer[1],
                LY = buffer[2],
                RX = buffer[3],
                RY = buffer[4],
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

        // 5. Exibição no Console
        private static void PrintTerminalOutput(DualSenseState state)
        {
            string dpadState = state.DPad switch
            {
                0 => "Cima",
                1 => "Cima-Dir",
                2 => "Direita",
                3 => "Baixo-Dir",
                4 => "Baixo",
                5 => "Baixo-Esq",
                6 => "Esquerda",
                7 => "Cima-Esq",
                _ => "Solto"
            };

            string acoes = "";
            if (state.Square)   acoes += "[□] ";
            if (state.Cross)    acoes += "[X] ";
            if (state.Circle)   acoes += "[O] ";
            if (state.Triangle) acoes += "[Δ] ";
            if (state.L1)       acoes += "[L1] ";
            if (state.R1)       acoes += "[R1] ";
            if (state.L3)       acoes += "[L3] ";
            if (state.R3)       acoes += "[R3] ";
            if (string.IsNullOrEmpty(acoes)) acoes = "Nenhum";

            Console.SetCursorPosition(0, Console.CursorTop);
            Console.Write($"LX:{state.LX,3} LY:{state.LY,3} | RX:{state.RX,3} RY:{state.RY,3} | L2:{state.L2,3}% R2:{state.R2,3}% | DPad:{dpadState,-9} | BTN:{acoes,-18}   ");
        }
    }

    // Modelo de dados fortemente tipado
    public class DualSenseState
    {
        public byte LX { get; set; }
        public byte LY { get; set; }
        public byte RX { get; set; }
        public byte RY { get; set; }
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