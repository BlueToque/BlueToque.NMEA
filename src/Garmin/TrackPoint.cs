//#define SHOWCOMMANDS

using System;
using System.Collections;

namespace BlueToque.NMEA.Garmin
{
    /// <summary>
    /// Class used for storing points in a track.
    /// </summary>
    public struct TrackPoint
    {
        #region Variables
        #endregion

        #region Constructors
        /// <summary>
        /// 
        /// </summary>
        /// <param name="command"></param>
        public TrackPoint(ArrayList command)
        {
            Latitude = 0.0;
            Longitude = 0.0;
            Height = 0.0F;
            Time = 0;
            IsNewSegment = false;

            DataCommand = new byte[command.Count];
            DataCommand[0] = (byte)command[0];
            for (int i = 1; i < command.Count - 2; i++)
            {
                DataCommand[i] = (byte)command[i];
                if ((byte)command[i] == 0x10 && (byte)command[i - 1] == 0x10)
                    command.RemoveAt(i);
            }

            if (command.Count != 30)
                throw new Exception("Information received to initialize TrackPoint is invalid!");
            if ((byte)command[1] != 0x22)
                throw new Exception("Information received to initialize TrackPoint is invalid!");


            //if (!CheckSum(command, false)) /* Verify data */
            //    if (System.Windows.Forms.MessageBox.Show("Data verification failed. The transfer will go on.\nWould you like to see more details on the error?", "Error", System.Windows.Forms.MessageBoxButtons.YesNo, System.Windows.Forms.MessageBoxIcon.Asterisk, System.Windows.Forms.MessageBoxDefaultButton.Button1) == System.Windows.Forms.DialogResult.Yes)
            //    { /* Show details */
            //        CheckSum(command, true);
            //    }

            Latitude =
                (((byte)command[6] << 24 |
                  (byte)command[5] << 16 |
                  (byte)command[4] << 8 |
                  (byte)command[3]) * (180.0 / 2147483648.0));
            Longitude =
                (((byte)command[10] << 24 |
                  (byte)command[9] << 16 |
                  (byte)command[8] << 8 |
                  (byte)command[7]) * (180.0 / 2147483648.0));

            Time = (uint)((byte)command[14] << 24 | (byte)command[13] << 16 | (byte)command[12] << 8 | (byte)command[11]);
            if ((byte)command[23] == 1)
                IsNewSegment = true;
            else
                IsNewSegment = false;

            /* Formulae used to get the float represented by 4 bytes */
            int h = (byte)command[18] << 24 | (byte)command[17] << 16 | (byte)command[16] << 8 | (byte)command[15];
            int exp = (h & 0x7f800000) / (2 << 22);
            int frac = h & 0x7fffff;
            Height = 1 + (float)frac / ((float)(2 << 22));
            Height *= 2 << (exp - 128);
        }

        #endregion

        #region Properties

        /// <summary>
        /// 
        /// </summary>
        public double Latitude { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public double Longitude { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public float Height { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public uint Time { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public bool IsNewSegment { get; }

        /// <summary>
        /// 
        /// </summary>
        public byte[] DataCommand { get; }

        #endregion

        #region Public Functions

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override readonly string ToString() => $"Lat: {Latitude} Lon: {Longitude} Elev: {Height} Time: {Time}";
        //#if (SHOWCOMMANDS)//				"Com: " + ToHEXstring(receivedCommand) + " " +//#endif

        #endregion

        #region Private Functions

        //private bool CheckSum(ArrayList command, bool errorDetails)
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

        //private string ToHEXstring(byte[] bytes)
        //{
        //    byte high = 0;
        //    byte low = 0;
        //    string retval = "";
        //    for (int i = 0; i < bytes.Length; i++)
        //    {
        //        high = (byte)((bytes[i] & 0xF0) >> 4);
        //        low = (byte)(bytes[i] & 0x0F);


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

        //private string ToHEXstring(System.Collections.ArrayList bytes)
        //{
        //    byte high = 0;
        //    byte low = 0;
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

        #endregion
    }
}
