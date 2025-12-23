using System;
using System.Collections;

namespace BlueToque.NMEA.Garmin
{
    /// <summary>
    /// Class used to represent a track made of TrackPoints.
    /// </summary>
    public class Track
    {
        #region fields
        private readonly ArrayList m_segments = new(1);

        private string m_name = "";
        private readonly byte[] m_receivedCommand;
        #endregion

        #region Constructors

        /// <summary>
        /// 
        /// </summary>
        /// <param name="Name"></param>
        public Track(string Name)
        {
            m_name = Name;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="command"></param>
        public Track(ArrayList command)
        {
            /* Create a byte[] array from the received ArrayList removing any duplicate 0x10 */
            m_receivedCommand = new byte[command.Count];
            m_receivedCommand[0] = (byte)command[0];
            for (int i = 1; i < command.Count - 2; i++)
            {
                m_receivedCommand[i] = (byte)command[i];
                if ((byte)command[i] == 0x10 && (byte)command[i - 1] == 0x10)
                    command.RemoveAt(i);
            }
            if (command.Count < 4)
                throw new Exception("Information received to initialize track is invalid!");
            if (command.Count >= 2)
                if ((byte)command[1] != 0x63)
                    throw new Exception("Information received to initialize track is invalid!");


            //if (!CheckSum(command, false)) /* Verify data */
            //if (System.Windows.Forms.MessageBox.Show("Data verification failed. The transfer will go on.\nWould you like to see more details on the error?", "Error", System.Windows.Forms.MessageBoxButtons.YesNo, System.Windows.Forms.MessageBoxIcon.Asterisk, System.Windows.Forms.MessageBoxDefaultButton.Button1) == System.Windows.Forms.DialogResult.Yes)
            //{ /* Show details */
            //    CheckSum(command, true);
            //}

            if ((byte)command[2] == 4) /* Not enough information to get the track name */
                m_name = "";
            else /* Set track name */
                for (int i = 5; i <= (byte)command[2] + 1; i++)
                    m_name += Convert.ToChar((byte)command[i]).ToString();
        }

        #endregion

        #region Properties

        /// <summary>
        /// 
        /// </summary>
        public string Name
        {
            get => this.m_name;
            set => this.m_name = value;
        }

        /// <summary>
        /// Returns the bytes used to initialize this track
        /// </summary>
        public byte[] DataCommand => (byte[])(m_receivedCommand.Clone());

        /// <summary>
        /// 
        /// </summary>
        public ArrayList Segments => m_segments;

        #endregion

        #region methods

        /// <summary>
        /// 
        /// </summary>
        /// <param name="command"></param>
        public void AddPoint(ArrayList command)
        {
            TrackPoint newPoint = new(command);
            if (newPoint.IsNewSegment)
            {
                m_segments.Add(new ArrayList(1));
            }

            ((ArrayList)(this.m_segments[m_segments.Count - 1])).Add(new TrackPoint(command));
        }

        #endregion

        #region Private functions

        ///// <summary>
        ///// Function used to graphically display bytes to the user in hexadecimal
        ///// </summary>
        ///// <param name="bytes">Message to display</param>
        ///// <returns>String representing the bytes</returns>
        //private static string ToHEXstring(ArrayList bytes)
        //{
        //    byte high;// = 0;
        //    byte low;// = 0;
        //    string retval = "";
        //    for (int i = 0; i < bytes.Count; i++)
        //    {
        //        high = (byte)(((byte)bytes[i] & 0xF0) >> 4);
        //        low = (byte)((byte)bytes[i] & 0x0F);


        //        if (high >= 0 && high <= 9)
        //            retval += Convert.ToString(high);
        //        else
        //            retval += Convert.ToChar(55 + high).ToString();

        //        if (low >= 0 && low <= 9)
        //            retval += Convert.ToString(low);
        //        else
        //            retval += Convert.ToChar(55 + low).ToString();

        //        retval += " ";
        //    }
        //    return retval;
        //}


        ///// <summary>
        ///// Function that verifies the checksum of a message and compares to the received one
        ///// </summary>
        ///// <param name="command">Message to verify</param>
        ///// <param name="errorDetails">Wether to show details or not if checksum verification fails</param>
        ///// <returns>True if OK</returns>
        //private static bool CheckSum(ArrayList command, bool errorDetails)
        //{
        //    int res = 0;
        //    int orig = (byte)command[command.Count - 3];

        //    for (int i = 1; i < command.Count - 3; i++)
        //    {
        //        res += (byte)command[i];
        //    }
        //    res &= 0xff;
        //    res ^= 0xff;
        //    res += 1;
        //    bool retval = (byte)(res) == (byte)orig;
        //    if (!retval && errorDetails)
        //    {
        //        //System.Windows.Forms.MessageBox.Show(
        //        //    "Received message:\n" +
        //        //    ToHEXstring(command) + "\n\n" +
        //        //    "Received checksum: " + orig.ToString() +
        //        //    "\nCalculated checksum:" + res.ToString(),
        //        //    "Error details",
        //        //    System.Windows.Forms.MessageBoxButtons.OK,
        //        //    System.Windows.Forms.MessageBoxIcon.Asterisk,
        //        //    System.Windows.Forms.MessageBoxDefaultButton.Button1);
        //    }
        //    return (retval);
        //}
        #endregion
    }
}
