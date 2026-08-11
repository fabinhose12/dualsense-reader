using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HidSharp;

namespace DualSenseReader
{
    internal class Program
    {
        // IDs do DualSense (Sony Interactive Entertainment)
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
                Console.WriteLine("----------------------------------------------------------------------------------");

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

        // 4. Mapeamento dos pacotes HID (Conexão USB Padrão - Report ID 0x01)
        private static void ParseDualSenseBuffer(byte[] buffer, int length)
        {
            // Valida se o buffer recebido possui o tamanho mínimo para leitura segura
            if (length < 10) return;

            // --- LEITURA ANALÓGICA DOS GATILHOS L2 E R2 (0 a 255) ---
            byte l2Pressure = buffer[5];
            byte r2Pressure = buffer[6];

            // Converte a pressão analógica em porcentagem (0% a 100%)
            int l2Percent = (int)Math.Round((l2Pressure / 255.0) * 100);
            int r2Percent = (int)Math.Round((r2Pressure / 255.0) * 100);

            // --- BOTÕES DE OMBRO E CLIQUE NO FIM DO CURSO DOS GATILHOS ---
            byte buttons2 = buffer[9];
            bool l2Clicked = (buttons2 & 0x04) != 0; // Acionamento digital no fim do curso do L2
            bool r2Clicked = (buttons2 & 0x08) != 0; // Acionamento digital no fim do curso do R2

            // Desenha barras de progresso no terminal para feedback visual
            string l2Bar = GetProgressBar(l2Percent);
            string r2Bar = GetProgressBar(r2Percent);

            // Exibe as leituras na mesma linha do console
            Console.SetCursorPosition(0, Console.CursorTop);
            Console.Write($"L2: {l2Pressure,3} ({l2Percent,3}%) [{l2Bar}] {(l2Clicked ? "[CLICK]" : "       ")} | ");
            Console.Write($"R2: {r2Pressure,3} ({r2Percent,3}%) [{r2Bar}] {(r2Clicked ? "[CLICK]" : "       ")}  ");
        }

        // Método auxiliar para construir a barra visual no console
        private static string GetProgressBar(int percent, int totalBlocks = 10)
        {
            int filledBlocks = (int)Math.Round((percent / 100.0) * totalBlocks);
            return new string('█', filledBlocks) + new string('-', totalBlocks - filledBlocks);
        }
    }
}