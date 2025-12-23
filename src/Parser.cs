using System;

namespace BlueToque.NMEA
{
    /// <summary>
    /// A light parser for simple sentences
    /// </summary>
    public class Parser
    {
        /// <summary>
        /// Parse the a set of sentences
        /// </summary>
        /// <param name="sentence"></param>
        /// <returns></returns>
        public static PositionItem? ParseNMEA(string sentence)
        {
            try
            {
                // do CRC check on sentence
                if (!Parser.IsValid(sentence))
                {
                    Trace.TraceError("Parser.ParseNMEA: Error - sentence does not pass checksum:\r\n\"{0}\"", sentence);
                    return null;
                }

                string[] words = Parser.GetWords(sentence);
                if (words.Length == 0)
                {
                    Trace.TraceError("Parser.ParseNMEA: Error - sentence does not parse into workds:\r\n\"{0}\"", sentence);
                    return null;
                }

                switch (words[0])
                {
                    case Constants.GPRMC: return ParseRMC(sentence);
                    case Constants.GPGGA: return ParseGGA(sentence);
                    case Constants.GPGNS: return ParseGNS(sentence);
                    case Constants.GPGLL: return ParseGLL(sentence);
                    default: Trace.TraceWarning("Parser.ParseNMEA: sentence {0} not recognized", words[0]); return null;
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError("Parser.ParseNMEA: Error parsing:\r\nSentence: \"{0}\"\r\nError: {1}", sentence, ex);
                return null;
            }
        }

        /// <summary> === RMC - Recommended Minimum Navigation Information ===
        /// 
        /// ------------------------------------------------------------------------------
        ///                                                           12
        ///         1         2 3       4 5        6  7   8   9    10 11|  13
        ///         |         | |       | |        |  |   |   |    |  | |   |
        ///  $--RMC,hhmmss.ss,A,llll.ll,a,yyyyy.yy,a,x.x,x.x,xxxx,x.x,a,m,*hh/r/n
        /// ------------------------------------------------------------------------------
        /// 
        /// Field Number:
        /// 
        /// 1. UTC Time
        /// 2. Status, V=Navigation receiver warning A=Valid
        /// 3. Latitude
        /// 4. N or S
        /// 5. Longitude
        /// 6. E or W
        /// 7. Speed over ground, knots
        /// 8. Track made good, degrees true
        /// 9. Date, ddmmyy
        /// 10. Magnetic Variation, degrees
        /// 11. E or W
        /// 12. FAA mode indicator (NMEA 2.3 and later)
        /// 13. Checksum
        /// 
        /// A status of V means the GPS has a valid fix that is below an internal
        /// quality threshold, e.g. because the dilution of precision is too high 
        /// or an elevation mask test failed.
        /// </summary>
        /// <param name="sentence"></param>
        /// <returns></returns>
        public static PositionItem ParseRMC(string sentence)
        {
            // Divide the sentence into words
            string[] words = GetWords(sentence);
            return new PositionItem
            {
                Position = ParseLatLon(words[3], words[4], words[5], words[6]),
                DateTime = ParseDateTime(words[1], words[9]),
                Speed = ParseSpeed(words[7]),
                Bearing = ParseBearing(words[8]),
                Fix = ParseFix(words[2]),
                Declination = ParseDeclination(words[10], words[11]),
                Source = "RMC"
            };
        }

        /// <summary> === GGA - Global Positioning System Fix Data ===
        /// 
        /// Time, Position and fix related data for a GPS receiver.
        /// 
        /// ------------------------------------------------------------------------------
        ///                                                       11
        ///         1         2       3 4        5 6 7  8   9  10 |  12 13  14   15
        ///         |         |       | |        | | |  |   |   | |   | |   |    |
        ///  $--GGA,hhmmss.ss,llll.ll,a,yyyyy.yy,a,x,xx,x.x,x.x,M,x.x,M,x.x,xxxx*hh/r/n
        /// ------------------------------------------------------------------------------
        /// 
        /// Field Number: 
        /// 
        /// 1. Universal Time Coordinated (UTC)
        /// 2. Latitude
        /// 3. N or S (North or South)
        /// 4. Longitude
        /// 5. E or W (East or West)
        /// 6. GPS Quality Indicator,
        ///      - 0 - fix not available,
        ///      - 1 - GPS fix,
        ///      - 2 - Differential GPS fix
        ///            (values above 2 are 2.3 features)
        ///      - 3 = PPS fix
        ///      - 4 = Real Time Kinematic
        ///      - 5 = Float RTK
        ///      - 6 = estimated (dead reckoning)
        ///      - 7 = Manual input mode
        ///      - 8 = Simulation mode
        /// 7. Number of satellites in view, 00 - 12
        /// 8. Horizontal Dilution of precision (meters)
        /// 9. Antenna Altitude above/below mean-sea-level (geoid) (in meters)
        /// 10. Units of antenna altitude, meters
        /// 11. Geoidal separation, the difference between the WGS-84 earth
        ///      ellipsoid and mean-sea-level (geoid), "-" means mean-sea-level
        ///      below ellipsoid
        /// 12. Units of geoidal separation, meters
        /// 13. Age of differential GPS data, time in seconds since last SC104
        ///      type 1 or 9 update, null field when DGPS is not used
        /// 14. Differential reference station ID, 0000-1023
        /// 15. Checksum        /// </summary>
        /// <param name="sentence"></param>
        /// <returns></returns>
        public static PositionItem ParseGGA(string sentence)
        {
            // Divide the sentence into words
            string[] words = GetWords(sentence);
            PositionItem item = new()
            {
                DateTime = ParseDateTime(words[1], string.Empty),
                Position = ParseLatLon(words[2], words[3], words[4], words[5]),
                LinkQuality = ParseLinkQuality(words[6]),
                NumSatellites = ParseInt(words[7]),
                HDOP = ParseFloat(words[8]),
                Altitude = ParseAltitude(words[9], words[10]),
                Source = "GGA"
            };

            return item;
        }

        /// <summary> === GNS - Fix data ===
        ///
        /// ------------------------------------------------------------------------------
        ///        1         2       3 4        5 6    7  8   9   10  11  12  13
        ///        |         |       | |        | |    |  |   |   |   |   |   |
        /// $--GNS,hhmmss.ss,llll.ll,a,yyyyy.yy,a,c--c,xx,x.x,x.x,x.x,x.x,x.x*hh/r/n
        /// ------------------------------------------------------------------------------
        ///
        /// Field Number:
        /// 0. GNS
        /// 1. UTC
        /// 2. Latitude
        /// 3. N or S (North or South)
        /// 4. Longitude
        /// 5. E or W (East or West)
        /// 6. Mode indicator
        /// 7. Total number of satelites in use,00-99
        /// 8. HDROP
        /// 9. Antenna altitude, meters, re:mean-sea-level(geoid.
        /// 10. Goeidal separation meters
        /// 11. Age of diferential data
        /// 12. Differential reference station ID
        /// 13. CRC
        /// </summary>
        /// <param name="sentence"></param>
        /// <returns></returns>
        public static PositionItem ParseGNS(string sentence)
        {
            // Divide the sentence into words
            string[] words = GetWords(sentence);
            return new PositionItem
            {
                DateTime = ParseDateTime(words[1], string.Empty),
                Position = ParseLatLon(words[2], words[3], words[4], words[5]),
                NumSatellites = ParseInt(words[7]),
                HDOP = ParseFloat(words[8]),
                Altitude = ParseAltitude(words[9], "M"),
                Source = "GNS"
            };
        }

        /// <summary> === GLL - Geographic Position - Latitude/Longitude ===
        /// 
        /// ------------------------------------------------------------------------------
        /// 	1       2 3        4 5         6 7   8
        /// 	|       | |        | |         | |   |
        ///  $--GLL,llll.ll,a,yyyyy.yy,a,hhmmss.ss,a,m,*hh/r/n
        /// ------------------------------------------------------------------------------
        /// 
        /// Field Number: 
        /// 
        /// 1. Latitude
        /// 2. N or S (North or South)
        /// 3. Longitude
        /// 4. E or W (East or West)
        /// 5. Universal Time Coordinated (UTC)
        /// 6. Status A - Data Valid, V - Data Invalid
        /// 7. FAA mode indicator (NMEA 2.3 and later)
        /// 8. Checksum
        /// 
        /// Geographic Position, Latitude / Longitude and time.
        /// 
        /// Geographic Latitude and Longitude is a holdover from Loran data and some old units may not 
        /// send the time and data active information if they are emulating Loran data. If a gps is 
        /// emulating Loran data they may use the LC Loran prefix instead of GP.
        /// 
        /// $GPGLL,4916.45,N,12311.12,W,225444,A,*31
        ///   1  4916.46    Latitude 49 deg. 16.45 min. North
        ///   2  N
        ///   3  12311.12   Longitude 123 deg. 11.12 min. West
        ///   4  W
        ///   5  225444       Fix taken at 22:54:44 UTC
        ///   6  A            Data Active or V (void)
        ///   7  *31          checksum data
        /// </summary>        /// <param name="sentence"></param>
        /// <returns></returns>
        public static PositionItem ParseGLL(string sentence)
        {
            // Divide the sentence into words
            string[] words = GetWords(sentence);
            return new PositionItem
            {
                Position = ParseLatLon(words[1], words[2], words[3], words[4]),
                DateTime = ParseDateTime(words[5], string.Empty),
                Source = "GLL"
            };
        }

        /// <summary>
        /// Divides a sentence into individual words 
        /// words are separated by ','
        /// </summary>
        /// <param name="sentence"></param>
        /// <returns></returns>
        public static string[] GetWords(string sentence)
        {
            string[] buf = sentence.Split('*');
            return buf[0].Split(',');
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="a_lon"></param>
        /// <param name="a_ew"></param>
        /// <returns></returns>
        public static double? ParseLongitude(string a_lon, string a_ew)
        {
            // parse longitude
            int index = a_lon.IndexOf('.') - 2;
            if (index < 0)
                return null;

            string lon = a_lon[..index];

            if (!double.TryParse(lon, out double longitude))
                return null;

            if (!double.TryParse(a_lon[index..], out double val))
                return null;

            longitude += val / 60.0;
            if (a_ew == "W") longitude = -longitude;
            return longitude;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="a_lat"></param>
        /// <param name="a_ns"></param>
        /// <returns></returns>
        public static double? ParseLatitude(string a_lat, string a_ns)
        {
            // parse latitude
            if (a_lat.EndsWith('N') || a_lat.EndsWith('S'))
                a_lat = a_lat.Replace("N", "").Replace("S", "");

            int index = a_lat.IndexOf('.') - 2;
            if (index < 0)
                return null;

            string lat = a_lat[..index];
            if (!double.TryParse(lat, out double latitude))
                return null;

            if (!double.TryParse(a_lat[index..], out double val))
                return null;

            latitude += val / 60.0;
            if (a_ns == "S") latitude = -latitude;
            return latitude;
        }

        /// <summary>
        /// Returns true if a sentence's checksum matches the calculated checksum 
        /// </summary>
        /// <param name="sentence"></param>
        /// <returns></returns>
        public static bool IsValid(string sentence)
        {
            if (!sentence.Contains('*', StringComparison.CurrentCulture))
                return false;

            string checksum1 = GetChecksum(sentence);

            string checksum2 = sentence.Substring(sentence.IndexOf('*') + 1, 2);

            return checksum1 == checksum2;
        }

        /// <summary>
        /// Calculates the checksum for a sentence
        /// </summary>
        /// <param name="sentence"></param>
        /// <returns></returns>
        public static string GetChecksum(string sentence)
        {
            byte checksum = 0;

            // loop through the characters
            foreach (char ch in sentence)
            {
                // skip the '$'
                if (ch == '$') continue;

                // end at the '*'
                if (ch == '*') break;

                // calculate the ckecksum
                checksum = (byte)
                    ((checksum == 0) ?                  // if this is the first character
                        Convert.ToByte(ch) :             // add it to the checksum,
                        checksum ^ Convert.ToByte(ch));  // otherwise, XOR it
            }

            // Return the checksum formatted as a two-character hexadecimal
            return checksum.ToString("X2");
        }

        /// <summary>
		/// utility method to parse latitude and longitude 
		///     4807.038,N   Latitude 48 deg 07.038' N
		///     01131.000,E  Longitude 11 deg 31.000' E
		/// </summary>
		/// <param name="a_lat"></param>
		/// <param name="a_ns"></param>
		/// <param name="a_lon"></param>
		/// <param name="a_ew"></param>
        public static Position? ParseLatLon(string a_lat, string a_ns, string a_lon, string a_ew)
        {
            // Do we have enough values to describe our location?
            if (string.IsNullOrEmpty(a_lat) ||
                string.IsNullOrEmpty(a_ns) ||
                string.IsNullOrEmpty(a_lon) ||
                string.IsNullOrEmpty(a_ew))
                return null;

            double? latitude = ParseLatitude(a_lat, a_ns);

            double? longitude = ParseLongitude(a_lon, a_ew);

            if (latitude == null || longitude == null)
                return null;

            return new Position(latitude.Value, longitude.Value);
        }

        /// <summary>
        /// Parse the bearing
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static float? ParseBearing(string value)
        {
            float? bearing = ParseFloat(value);
            if (!bearing.HasValue)
                return bearing;

            if (bearing > 360 || bearing < 0)
                return null;

            return bearing;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="time"></param>
        /// <param name="day"></param>
        /// <param name="month"></param>
        /// <param name="year"></param>
        /// <returns></returns>
        public static DateTime? ParseDateTime(string time, string day, string month, string year)
        {
            // Do we have enough values to parse satellite-derived time?
            if (string.IsNullOrEmpty(time)) return null;

            if (!int.TryParse(time, out _)) return null;

            // Yes. Extract hours, minutes, seconds and milliseconds
            if (!int.TryParse(time.AsSpan(0, 2), out int UtcHours)) return null;
            if (!int.TryParse(time.AsSpan(2, 2), out int UtcMinutes)) return null;
            if (!int.TryParse(time.AsSpan(4, 2), out int UtcSeconds)) return null;
            int UtcMilliseconds = 0;

            // Extract milliseconds if it is available
            if (time.Length > 7)
                UtcMilliseconds = Convert.ToInt32(time[7..]);

            DateTime dt;// = DateTime.UtcNow;
            _ = int.TryParse(day, out int UtcDays);
            _ = int.TryParse(month, out int UtcMonths);
            _ = int.TryParse(year, out int UtcYears);
            if (UtcDays == 0 || UtcMonths == 0 || UtcYears == 0)
                return null;
            dt = new DateTime(UtcYears, UtcMonths, UtcDays);

            try
            {
                // Now build a DateTime object with all values
                // DateTime Today = System.DateTime.Now.ToUniversalTime();
                return new DateTime(dt.Year, dt.Month, dt.Day, UtcHours, UtcMinutes, UtcSeconds, UtcMilliseconds);
            }
            catch (Exception ex)
            {
                Trace.TraceError("Parser.ParseDateTime: Error parsing datetime:\r\n{0}", ex);
                return null;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="time"></param>
        /// <param name="date"></param>
        /// <returns></returns>
        public static DateTime? ParseDateTime(string time, string date)
        {
            // Do we have enough values to parse satellite-derived time?
            if (string.IsNullOrEmpty(time)) return null;

            if (!float.TryParse(time, out float _)) return null;

            // Yes. Extract hours, minutes, seconds and milliseconds
            if (!int.TryParse(time.AsSpan(0, 2), out int UtcHours)) return null;
            if (!int.TryParse(time.AsSpan(2, 2), out int UtcMinutes)) return null;
            if (!int.TryParse(time.AsSpan(4, 2), out int UtcSeconds)) return null;

            int UtcMilliseconds = 0;

            // Extract milliseconds if it is available
            if (time.Length > 7)
                UtcMilliseconds = Convert.ToInt32(time[7..]);

            DateTime dt = DateTime.UtcNow.Date;
            if (!string.IsNullOrEmpty(date) && date.Length != 6)
            {
                int UtcDays = int.Parse(date[..2]);
                int UtcMonths = int.Parse(date.Substring(2, 2));
                int UtcYears = int.Parse(date.Substring(4, 2)) + 2000;
                if (UtcDays != 0 && UtcMonths != 0 && UtcYears != 0)
                    return null;
                dt = new DateTime(UtcYears, UtcMonths, UtcDays);
            }

            try
            {
                // Now build a DateTime object with all values
                // DateTime Today = System.DateTime.Now.ToUniversalTime();
                return new DateTime(dt.Year, dt.Month, dt.Day, UtcHours, UtcMinutes, UtcSeconds, UtcMilliseconds);
            }
            catch (Exception ex)
            {
                Trace.TraceError("Parser.ParseDateTime: Error parsing datetime:\r\n{0}", ex);
                return null;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static float? ParseSpeed(string value)
        {
            float? speed = ParseFloat(value);
            if (!speed.HasValue)
                return speed;

            // Notify of the new speed. Units are km/h (international standard)
            // if you require another representation, your software is responsible for it.
            return (speed * 1.85200f);  // knots to kph
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fix"></param>
        /// <returns></returns>
        internal static char ParseFix(string fix)
        {
            if (string.IsNullOrEmpty(fix)) return ' ';

            return fix switch
            {
                "A" => 'A',
                "V" => 'V',
                _ => ' ',
            };
        }

        /// <summary>
        /// Parse the link quality
        /// </summary>
        /// <param name="word"></param>
        /// <returns></returns>
        static LinkQuality? ParseLinkQuality(string word)
        {
            try
            {
                return (LinkQuality?)ParseInt(word);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <param name="hemisphere"></param>
        /// <returns></returns>
        internal static float? ParseDeclination(string value, string hemisphere)
        {
            // declination
            float? magvar = ParseFloat(value);
            if (!magvar.HasValue)
                return magvar;

            if (!string.IsNullOrEmpty(hemisphere))
                if (hemisphere == "W")
                    magvar *= -1.0f;
            return magvar;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <param name="units"></param>
        /// <returns></returns>
        internal static float? ParseAltitude(string value, string units)
        {
            float? altitude = ParseFloat(value);
            if (!altitude.HasValue)
                return altitude;

            if (units != "M")
                switch (units.ToLower())
                {
                    case "f": return altitude *= 0.3048f;
                }

            return altitude;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        internal static int? ParseInt(string value)
        {
            if (string.IsNullOrEmpty(value)) 
                return null;

            if (!int.TryParse(value, out int intVal)) 
                return null;

            return intVal;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        internal static float? ParseFloat(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;

            if (!float.TryParse(value, out float floatVal)) return null;

            return floatVal;
        }
    }
}
