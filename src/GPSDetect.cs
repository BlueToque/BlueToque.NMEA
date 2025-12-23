using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BlueToque.NMEA
{
    public class GPSDetect
    {
        /// <summary>
        /// Scan the ports
        /// </summary>
        /// <param name="stopOnFirst"></param>
        /// <param name="wait"></param>
        public static void BeginScan(bool stopOnFirst = false, int wait = 1700)
        {
            if (Scanning)
            {
                Trace.TraceWarning("GPSDetect.BeginScan: Error - already scanning");
                Error?.Invoke(null, EventArgs.Empty);
                return;
            }

            m_source = new CancellationTokenSource();

            Scanning = true;

            Task.Run(() =>
            {
                try
                {
                    Trace.TraceInformation("GPSDetect.BeginScan: Scan started");

                    Ports.Clear();

                    var valid = ScanPortsInternal(m_source.Token, stopOnFirst, wait);

                    Ports.AddRange(valid);

                    Completed?.Invoke(null, EventArgs.Empty);

                    Trace.TraceInformation("GPSDetect.BeginScan: Scan completed");
                }
                catch (OperationCanceledException)
                {
                    Trace.TraceWarning("GPSDetect.BeginScan: Cancelled");
                    Cancelled?.Invoke(null, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    Trace.TraceError("GPSDetect.BeginScan: Error\r\n{0}", ex);
                    Error?.Invoke(null, EventArgs.Empty);
                }
                finally
                {
                    Scanning = false;
                }
            });
        }

        /// <summary>
        /// These are the baud rates to test for the GPS
        /// </summary>
        static readonly uint[] BaudRates = [9600, 4800];

        /// <summary>
        /// to cancel the thread
        /// </summary>
        static CancellationTokenSource m_source = new();

        /// <summary>
        /// Are we scanning
        /// </summary>
        public static bool Scanning { get; private set; } = false;

        /// <summary>
        /// Cancell scanning
        /// </summary>
        public static void CancelScan() => m_source?.Cancel();

        /// <summary>
        /// The ports we detected a GPS on
        /// </summary>
        public static List<PortType> Ports { get; private set; } = [];

        public static event EventHandler? Completed;
        public static event EventHandler? Cancelled;
        public static event EventHandler? Error;

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        static List<PortType> ScanPortsInternal(CancellationToken cancellationToken, bool stopOnFirst = false, int wait = 1700)
        {
            Trace.TraceInformation("GPSDetect.ScanPorts");

            Interpreter interpreter = new();
            interpreter.RawString += Interpreter_RawString;

            string[] ports = SerialPort.GetPortNames();

            List<PortType> valid = [];
            foreach (uint baud in BaudRates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (string name in ports)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // skip testing if we've already detected it
                    if (valid.FirstOrDefault(x => x.Name == name) != null)
                        continue;

                    try
                    {
                        m_event.Reset();
                        interpreter.Start(name, baud);
                        if (m_event.WaitOne(wait))
                        {
                            //if (!interpreter.HasError)
                            {
                                valid.Add(new PortType(name, baud));
                                if (stopOnFirst)
                                    return valid;
                            }
                        }
                        interpreter.Stop();
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceError("GPSDetect.ScanPorts: Error opening port {0}\r\n{1}", name, ex);
                        try { interpreter.Stop(); }
                        catch { }
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                }
            }

            interpreter.RawString -= Interpreter_RawString;
            return valid;
        }


        /// <summary>
        /// if a valid raw string is recieved then this is a valid port.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="var"></param>
        static void Interpreter_RawString(object sender, string var) => m_event.Set();

        static readonly AutoResetEvent m_event = new(false);

    }
}
