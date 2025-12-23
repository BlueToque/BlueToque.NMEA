namespace BlueToque.NMEA
{
    public class Constants
    {
        /// <summary>
        /// RMC: Recommended minimum
        /// </summary>
        public const string GPRMC = "$GPRMC";

        /// <summary>
        /// GGA: Essential fix data
        /// </summary>
        public const string GPGGA = "$GPGGA";

        /// <summary>
        /// GNS: Fix data
        /// </summary>
        public const string GPGNS = "$GPGNS";

        /// <summary>
        /// GLL: Lat/Lon
        /// </summary>
        public const string GPGLL = "$GPGLL";

        /// <summary>
        /// GSV: Satellites in view
        /// </summary>
        public const string GLGSV = "$GLGSV";

        /// <summary>
        /// ZDA: Date/Time
        /// </summary>
        public const string GPZDA = "$GPZDA";

        /// <summary>
        /// GSA: GPS DOP and active satellites
        /// </summary>
        public const string GNGSA = "$GNGSA";

        /// <summary>
        /// RMM: Garmin currently active horizontal datum
        /// </summary>
        public const string PGRMM = "$PGRMM";

        /// <summary>
        /// RMZ: Garmin altitude in feet
        /// </summary>
        public const string PGRMZ = "$PGRMZ";

        /// <summary>
        /// RME: Garmin estimated error
        /// </summary>
        public const string PGRME = "$PGRME";

        /// <summary>
        /// HDG: Garmin compass output
        /// </summary>
        public const string HCHDG = "$HCHDG";

        /// <summary>
        /// HDT: Heading from True North
        /// </summary>
        public const string GPHDT = "$GPHDT";

    }
}
