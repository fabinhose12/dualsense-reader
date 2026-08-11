using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HidSharp;

namespace DualSenseReader
{
    internal class Program
    {
        // IDs do DualSense
        private const int SONY_VID = 0x054C;
        private const int DUALSENSE_PID = 0x0CE6;
        private const int DUALSENSE_EDGE_PID = 0x0DF2;

        private static async Task Main(string[] args)
        {
            Console.Clear();
            Console.WriteLine("===========================================");
            Console.WriteLine("       DualSense HID Reader v0.1           ");
            Console.WriteLine("===========================================\n");
            Console.WriteLine("Buscando controle DualSense (PS5)...");

            // 1. Localiza o dispositivo nas portas HID locais
            var list = DeviceList.Local;
            var hidDevice = list.GetHidDevices(SONY_VID)
                                .FirstOrDefault(d => d.ProductID == DUALSENSE_PID || d.ProductID == DUALSENSE_EDGE_PID);

            if (hidDevice == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n[ERRO] DualSense não foi encontrado!");
                Console.ResetColor();
                Console.WriteLine("Certifique-se de que o controle está conectado via USB ou Bluetooth.");
                return;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n[SUCESSO] Dispositivo detectado: {hidDevice.GetFriendlyName()}");
            Console.ResetColor();
            Console.WriteLine($"Caminho: {hidDevice.DevicePath}\n");

            // 2. Abre a stream de comunicação HID
            if (!hidDevice.TryOpen(out HidStream stream))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\n[ATENÇÃO] Não foi possível abrir a conexão com o controle.");
                Console.ResetColor();
                Console.WriteLine("Verifique se o Steam, DS4Windows ou outro software de controle está aberto e feche-o.");
                return;
            }

            using (stream)
            {
                stream.ReadTimeout = 1000;
                int maxReportLength = hidDevice.GetMaxInputReportLength();
                byte[] buffer = new byte[maxReportLength];

                Console.WriteLine($"Tamanho do pacote Input Report: {maxReportLength} bytes.");
                Console.WriteLine("Lendo entradas... Pressione [Ctrl + C] para encerrar.\n");
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
                            ParseDualSenseBuffer(buffer, bytesRead);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"\nErro durante a leitura: {ex.Message}");
                        break;
                    }

                    await Task.Delay(10, cts.Token);
                }
            }

            Console.WriteLine("\n\nLeitura finalizada.");
        }

        // 4. Mapeamento bruto dos buffers de dados do DualSense
        private static void ParseDualSenseBuffer(byte[] buffer, int length)
        {
            // Em conexões USB, os analógicos costumam iniciar nos índices 1 a 4:
            // Byte 1: LX (Analógico Esquerdo X)
            // Byte 2: LY (Analógico Esquerdo Y)
            // Byte 3: RX (Analógico Direito X)
            // Byte 4: RY (Analógico Direito Y)
            
            // Caso esteja conectado via Bluetooth, o Report ID e offsets mudam ligeiramente.
            byte lx = buffer[1];
            byte ly = buffer[2];
            byte rx = buffer[3];
            byte ry = buffer[4];

            // Exibe a posição dos analógicos mantendo a mesma linha do console
            Console.SetCursorPosition(0, Console.CursorTop);
            Console.Write($"LX: {lx,3} | LY: {ly,3} | RX: {rx,3} | RY: {ry,3}  [Report ID: 0x{buffer[0]:X2} - Bytes: {length}]   ");
        }
    }
}