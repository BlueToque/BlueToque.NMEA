using BlueToque.Serial;
using BlueToque.Serial.Devices;
using System;
using System.Collections;
using System.IO;

namespace BlueToque.NMEA.Garmin
{
    /// <summary>
    /// Class that implements the Garmin Protocol.
    /// </summary>
    public class Garmin : IDisposable
    {
        #region Constructors

        /// <summary> </summary>
        public Garmin() { }

        #endregion

        #region IDisposable

        /// <summary>
        /// 
        /// </summary>
        ~Garmin()
        {
            Dispose(false);
        }

        /// <summary>
        /// 
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                m_serialPort?.Dispose();
                m_serialPort = null;
            }
        }

        #endregion

        #region Variables
        SerialDevice m_serialPort;
        private bool m_received = false;
        private bool m_logging = false;
        private bool m_done = false;

        private int q = 0;
        private int m_bytesEstimated;

        private readonly ArrayList m_bytesReceived = new(100);
        #endregion

        #region Properties

        /// <summary>
        /// Estimate percentage already transfered
        /// </summary>
        public double Progress => Math.Min(m_bytesReceived.Count * 100 / (double)m_bytesEstimated, 100.0);

        /// <summary>
        /// Get bytes received from GPS to save them or whatever
        /// </summary>
        public ArrayList Bytes => m_bytesReceived;

        #endregion

        #region Public Functions

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public ArrayList GetTracks()
        {
            Trace.TraceInformation("Garmin.GetTracks");

            try
            {
                m_bytesReceived.Clear();
                m_bytesEstimated = -1;
                m_logging = false;

                byte[] sendTracks = [0x10, 0x0A, 0x02, 0x06, 0x00, 0xEE, 0x10, 0x03]; /* Ask for tracks */


                SerialPortSettings settings = new()
                {
                    Name = "com1",
                    Baud = 9600,
                    Parity = Parity.None,
                    StopBits = StopBits.One
                };
                //Serial.BasicPortSettings a = new Serial.BasicPortSettings();
                //a.BaudRate = Serial.BaudRates.CBR_9600;
                m_serialPort = new SerialDevice(settings);

                m_serialPort.DataReceived += new DataReceivedEventHandler(SerialPort_DataReceived);
                m_serialPort.ErrorReceived += new ErrorReceivedEventHandler(SerialPort_ErrorReceived);

                if (!m_serialPort.IsOpen)
                    m_serialPort.Open();

                m_logging = true;
                SendData(sendTracks); /* Ask for tracks */
                //Application.DoEvents();
                System.Threading.Thread.Sleep(200); /* Wait for response */
                if (m_received)
                    m_received = false;
                else
                {
                    m_serialPort.Close();
                    m_serialPort.Dispose();
                    m_serialPort = null;
                    return null;
                }
                q = Environment.TickCount; /* Keep track of elapsed time */
                while (!m_done)
                {
                    if (Environment.TickCount - q > 2000) /* Timeout */
                    {
                        m_serialPort.Close();
                        m_serialPort.Dispose();
                        m_serialPort = null;
                        return null;
                    }

                    //Application.DoEvents();
                    System.Threading.Thread.Sleep(1000);
                }
                m_serialPort.Close();
                m_serialPort.Dispose();
                m_serialPort = null;

                ArrayList retval = new(2);
                Track tempTrack = null;

                ArrayList al = SplitBytes(m_bytesReceived);

                ArrayList command;

                int nWaited = -1;
                int nReceived = 0;
                for (int i = 0; i < al.Count; i++)
                {
                    command = (ArrayList)al[i];

                    if (command.Count >= 7 && (byte)command[0] == 0x10)
                    {
                        if ((byte)command[1] == 0x1B)
                        {
                            // Number of commands to be sent 
                            nWaited = 0;
                            for (int j = 2 + (byte)command[2]; j >= 3; j--)
                            {
                                nWaited <<= 8;
                                nWaited += (byte)command[j];
                            }
                        }
                        else if ((byte)command[1] == 0x06)
                        {
                            // OK - Do nothing 
                        }
                        else if ((byte)command[1] == 0x22)
                        {
                            // New point 
                            ++nReceived; ;
                            // Create new point based on received info. 
                            tempTrack?.AddPoint(command);
                        }
                        else if ((byte)command[1] == 0x63)
                        {
                            // New track
                            ++nReceived;

                            // Last received track is now done. Add it to the values to be returned 
                            if (tempTrack != null)
                                retval.Add(tempTrack);

                            // Create new point based on received info. 
                            tempTrack = new Track(command);
                        }
                        else if ((byte)command[0] == 0x10 &&
                            (byte)command[1] == 0x0C &&
                            (byte)command[2] == 0x02 &&
                            (byte)command[3] == 0x06 &&
                            (byte)command[4] == 0x00 &&
                            (byte)command[5] == 0xEC &&
                            (byte)command[6] == 0x10 &&
                            (byte)command[7] == 0x03)
                        { // EOF 
                          // Last received track is now done. Add it to the values to be returned 
                            if (tempTrack != null)
                                retval.Add(tempTrack);
                            // Check if the information received matches what was expected
                            if (nReceived != nWaited)
                                Trace.TraceError("Garmin.GetTracks: Transfer error...\nNumber of commands expected: " + nWaited.ToString() + "\nNumber of commands received: " + nReceived.ToString());
                        }
                    }
                    else /* command[0] != 0x10 */
                    {
                        /* Invalid! */
                        retval = null;
                    }
                }

                return retval;
            }
            catch (Exception ex)
            {
                Trace.TraceError("Garmin.GetTracks: Error:\r\n{0}", ex);
                return null;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public ArrayList GetTracksFromFile(string filename)
        {
            try
            {
                Trace.TraceInformation("Garmin.GetTracksFromFile");
                m_bytesReceived.Clear();

                byte[] ByteArray = new byte[600000];
                int nBytesRead = 0;
                using (FileStream fs = new FileInfo(filename).OpenRead())
                    nBytesRead = fs.Read(ByteArray, 0, 600000);

                for (int i = 0; i < nBytesRead; i++)
                    m_bytesReceived.Add(ByteArray[i]); /* Pretend we received data from a GPS device */

                ArrayList retval = new(2);
                Track tempTrack = null;

                ArrayList al = SplitBytes(m_bytesReceived);

                ArrayList command;

                int nWaited = -1;
                int nReceived = 0;
                for (int i = 0; i < al.Count; i++)
                {
                    command = (ArrayList)al[i];
                    if (command.Count >= 7 && (byte)command[0] == 0x10)
                    {
                        if ((byte)command[1] == 0x1B)
                        {
                            // Number of commands to be sent 
                            nWaited = 0;
                            for (int j = 2 + (byte)command[2]; j >= 3; j--)
                            {
                                nWaited <<= 8;
                                nWaited += (byte)command[j];
                            }
                        }
                        else if ((byte)command[1] == 0x06)
                        {
                            // OK - Do nothing 
                        }
                        else if ((byte)command[1] == 0x22)
                        {
                            // New point 
                            ++nReceived; ;
                            tempTrack?.AddPoint(command);
                        }
                        else if ((byte)command[1] == 0x63)
                        {
                            // New track 
                            ++nReceived;
                            if (tempTrack != null)
                                retval.Add(tempTrack);

                            tempTrack = new Track(command);
                        }
                        else if ((byte)command[0] == 0x10 &&
                            (byte)command[1] == 0x0C &&
                            (byte)command[2] == 0x02 &&
                            (byte)command[3] == 0x06 &&
                            (byte)command[4] == 0x00 &&
                            (byte)command[5] == 0xEC &&
                            (byte)command[6] == 0x10 &&
                            (byte)command[7] == 0x03)
                        {
                            // EOF 
                            if (tempTrack != null)
                                retval.Add(tempTrack);
                            if (nReceived != nWaited)
                                Trace.TraceError("Garmin.GetTracksFromFile: File corrupt...\nNumber of commands expected: " + nWaited.ToString() + "\nNumber of commands received: " + nReceived.ToString());
                        }
                    }
                    else /* command[0] != 0x10 */
                    {
                        retval = null;
                    }
                }

                return retval;
            }
            catch (Exception ex)
            {
                Trace.TraceError("Garmin.GetTracksFromFile: Error:\r\n{0}", ex);
                return null;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void Exit() => m_serialPort?.Close();

        #endregion

        #region Private Functions

        /// <summary>
        /// Function called each time data comes in which stores the received bytes
        /// </summary>
        /// <param name="bytes">byte[] array whose bytes are to be added to the ArrayList receivedBytes</param>
        /// <returns></returns>
        private bool Append(byte[] bytes)
        {
            for (int i = 0; i < bytes.Length; i++)
                m_bytesReceived.Add(bytes[i]);

            if (m_bytesEstimated == -1) /* Not initialized yet */
            {
                if (m_bytesReceived.Count >= 16)
                {
                    ArrayList Data = SplitBytes(m_bytesReceived);
                    if ((byte)((ArrayList)Data[1])[0] == 0x10 &&
                        (byte)((ArrayList)Data[1])[1] == 0x1B)
                    {
                        // Received number of commands to be sent 

                        m_bytesEstimated = 0;
                        for (int j = 2 + (byte)((ArrayList)Data[1])[2]; j >= 3; j--)
                        {
                            m_bytesEstimated <<= 8;
                            m_bytesEstimated += (byte)((ArrayList)Data[1])[j];
                        }

                        // Estimate each message has 30 bytes average 
                        m_bytesEstimated *= 30;
                    }
                }
            }

            return true;
        }


        /// <summary>
        /// Main function in parsing the received bytes, it splits the received bytes into messages
        /// </summary>
        /// <param name="arrayListBytes">ArrayList filled with elements of type byte which are to be split</param>
        /// <returns></returns>
        private static ArrayList SplitBytes(ArrayList arrayListBytes)
        {
            ArrayList retval = new(10);
            ArrayList tempBytes;
            int count = 0;

            for (int i = 0; i < arrayListBytes.Count; i++)
            {
                if ((byte)arrayListBytes[i] == 0x10)
                {
                    // First byte in a message 
                    tempBytes = new ArrayList(8)
                    {
                        arrayListBytes[i]
                    };
                    for (int x = i + 1; ; x++)
                    {
                        i = x;
                        tempBytes.Add(arrayListBytes[x]);
                        if ((byte)arrayListBytes[x] == 0x10)
                            ++count;
                        else if ((byte)arrayListBytes[x] == 0x03)
                        {
                            // If found an even number of 0x10 followed by 0x03 we have to split the message here 
                            if (count % 2 == 1)
                                break;
                        }
                        else
                            count = 0;
                    }
                    retval.Add(tempBytes);
                }
            }

            return retval;
        }


        /// <summary>
        /// Function used to send data through the serial port
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns></returns>
        private bool SendData(byte[] buffer)
        {
            if (!m_serialPort.IsOpen)
                return false;
            m_serialPort.Write(buffer);
            return true;
        }

        #endregion

        #region Events Actions
        /// <summary>
        /// Response to error event raised by the serial port
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void SerialPort_ErrorReceived(object sender, ErrorReceivedEventArgs e)
        {
            Trace.TraceError("PacketListener.SerialPort_ErrorReceived: {0}", e);
            //MessageBox.Show("An error occured during the transfer:\n" + Description, "Error", MessageBoxButtons.OK, MessageBoxIcon.Hand, MessageBoxDefaultButton.Button1);
        }

        /// <summary>
        /// Response to DataReceived event raised by serialPort called each time new data is received
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SerialPort_DataReceived(object sender, DataReceivedEventArgs e)
        {
            // Keep track of the time in order to check for timeouts 
            q = Environment.TickCount;

            // Received data 
            m_received = true;

            // Keep track of data received 
            if (m_logging)
            {
                string _ = m_serialPort.ReadString();

                byte[] a = new byte[12];

                // Store received bytes
                Append(a);
                if (m_bytesReceived.Count >= 8)
                    if (
                        (byte)m_bytesReceived[^8] == 0x10 &&
                        (byte)m_bytesReceived[^7] == 0x0C &&
                        (byte)m_bytesReceived[^6] == 0x02 &&
                        (byte)m_bytesReceived[^5] == 0x06 &&
                        (byte)m_bytesReceived[^4] == 0x00 &&
                        (byte)m_bytesReceived[^3] == 0xEC &&
                        (byte)m_bytesReceived[^2] == 0x10 &&
                        (byte)m_bytesReceived[^1] == 0x03)
                    {
                        // EOF 

                        // Transfer is complete 
                        m_done = true;

                        // Stop logging 
                        m_logging = false;
                        return;
                    }

                // Ask for next record as soon as data is received 
                SendData([0x10, 0x06, 0x02, 0x22, 0x00, 0xD6, 0x10, 0x03]);
            }
        }
        #endregion
    }
}
