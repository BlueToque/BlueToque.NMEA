using System;
using System.Collections.Concurrent;
using System.IO.Ports;
using System.Text;
using System.Threading.Tasks;

namespace BlueToque.NMEA
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="str"></param>
    /// <returns></returns>
    public delegate bool SentenceParserDelegate(string str);

    //==========================================================================================
    // Microsoft GPS:
    //  GPGSA: GPS DOP and active satellites.
    //  GPGGA: essential fix data which provide 3D location and accuracy data.
    //  GPRMC: Reccomended Minumum
    //  GPGSV: Satellites in View
    // 
    // Note: NMEA 2.3 added mode character to RMC, RMB, VTG, and GLL and optionally some others 
    // including the BWC and XTE (just before checksum)
    // 
    // The value can be A=autonomous, D=differential, E=Estimated, N=not valid, S=Simulator. 
    // Sometimes there can be a null value as well. 
    //==========================================================================================

    /// <summary>
    /// Process informaiton from a GPS Receiver on a com port
    /// NMEA sentences are reported in en-US
    /// private CultureInfo NmeaCultureInfo = new CultureInfo("en-US");
    /// 
    /// based on CodeProject article http://www.codeproject.com/vb/net/WritingGPSApplications1.asp
    /// and significantly re-written
    /// </summary>
    public class Interpreter : IDisposable
    {
        /// <summary>
        /// Load some default sentences
        /// </summary>
        public Interpreter()
        {
            m_sentenceParserList
                .Add(Constants.GPRMC, "Recommended Minumum", ParseGPRMC)
                .Add(Constants.GLGSV, "Satellites in View", ParseGPGSV)
                .Add(Constants.GPGGA, "Essential fix data", ParseGPGGA)
                .Add(Constants.GPGNS, "Fix data", ParseGPGNS)
                .Add(Constants.GNGSA, "GPS DOP and active satellites", ParseGPGSA)
                .Add(Constants.GPGLL, "Lat/Lon", ParseGPGLL)

                .Add(Constants.GPZDA, "Date and Time", ParseGPZDA)

                .Add(Constants.PGRMM, "Garmin currently active horizontal datum", ParsePGRMM)
                .Add(Constants.PGRMZ, "Garmin altitude in feet", ParsePGRMZ)
                .Add(Constants.PGRME, "Garmin estimated error", ParsePGRME)
                .Add(Constants.HCHDG, "Garmin compass output", ParseHCHDG)
                // GNVTG: course and speed relative to the ground

                .Add(Constants.GPHDT, "HEADING FROM TRUE NORTH", ParseGPHDT);
        }

        #region IDisposable

        /// <summary>
        /// 
        /// </summary>
        ~Interpreter()
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
            if (disposing && m_dataItems != null)
                m_dataItems.Dispose();
        }

        #endregion

        #region fields

        /// <summary>
        /// The serial device
        /// </summary>
        private SerialPort? m_serialPort;

        /// <summary>
        /// The sentence parsers
        /// </summary>
        readonly SentenceParserList m_sentenceParserList = [];

        /// <summary>
        /// A buffer to build up sentences before processing
        /// </summary>
        StringBuilder m_buffer = new();

        /// <summary>
        /// A collection to process sentences on another thread
        /// </summary>
        BlockingCollection<string> m_dataItems = [];

        #endregion

        #region properties

        /// <summary>
        /// Is the interpreter started
        /// </summary>
        public bool IsStarted => m_serialPort != null && m_serialPort.IsOpen;

        /// <summary>
        /// Does the interpreter have an error
        /// </summary>
        public bool HasError { get; private set; }

        #endregion

        #region public methods

        /// <summary>
        /// 
        /// </summary>
        /// <param name="device"></param>
        void Start(SerialPort device)
        {
            try
            {
                // initialize the port
                m_serialPort = device;
                //m_serialPort.ReceivedBytesThreshold = 100;

                m_serialPort.DataReceived += HandleDataReceived;
                m_serialPort.ErrorReceived += HandleErrorReceived;

                /// set up recieving thread
                m_dataItems = [];
                //m_buffer = new StringBuilder();
                Task.Factory.StartNew(NMEAParserConsumerTask, TaskCreationOptions.LongRunning);

                // open the port
                m_serialPort.Open();
            }
            catch (Exception ex)
            {
                Trace.TraceError("Interpreter.Start: Error opening Port {0} ({1})\r\n{2}", m_serialPort == null ? "null" : m_serialPort.PortName, ex);
                throw;
            }
        }

        /// <summary>
        /// Start listening to a port for NMEA sentences
        /// </summary>
        /// <param name="port"></param>
        /// <param name="baud"></param>
        public void Start(string port, uint baud)
        {
            try
            {
                Trace.TraceInformation("Interpreter.Start: Port {0} ({1})", port, baud);

                //SerialSettings settings = new SystemPortSettings(port, baud);

                //ISerialDevice serialPort = PortManager.CreateDevice(settings);

                SerialPort serialPort = new(port, (int)baud);

                Start(serialPort);
            }
            catch (Exception ex)
            {
                Trace.TraceError("Interpreter.Start: Error opening Port {0} ({1})\r\n{2}", port, baud, ex);
                throw;
            }
        }

        /// <summary>
        /// Stop listening to a port for NMEA sentences
        /// </summary>
        public void Stop()
        {
            try
            {
                if (m_serialPort == null)
                    return;

                Trace.TraceInformation("Interpreter.Stop: Port {0}", m_serialPort);

                #region wind down the serial port
                m_serialPort.DataReceived -= HandleDataReceived;
                m_serialPort.ErrorReceived -= HandleErrorReceived;

                if (m_serialPort.IsOpen)
                    m_serialPort.Close();
                #endregion

                #region clean up the threads
                m_dataItems.CompleteAdding();

                m_buffer.Clear();

                #endregion

                //PortManager.Release(m_serialPort);
                HasError = false;
            }
            catch (Exception ex)
            {
                Trace.TraceError("Interpreter.Stop: Error closing serial port: {0}\r\n{1}", m_serialPort == null ? "null" : m_serialPort.PortName, ex);
                throw;
            }
        }

        /// <summary>
		/// This core methods parses a NMEA sentence
		/// </summary>
		/// <param name="sentence"></param>
		/// <returns></returns>
        public bool Parse(string sentence)
        {
            try
            {
                // do CRC check on sentence
                if (!Parser.IsValid(sentence))
                {
                    Trace.TraceWarning("Interpreter.Parse: sentence does not pass checksum");
                    return false;
                }

                // send the raw sentence message
                RaiseRawString(sentence);

                // Divide the sentence into words
                string[] words = Parser.GetWords(sentence);
                if (words == null || words.Length == 0)
                {
                    Trace.TraceWarning("Interpreter.Parse: sentence has no type");
                    return false;
                }

                string type = words[0];
                if (!type.StartsWith('$'))
                    type = "$" + type;

                // massage type to get the sentence key
                if (type.Length != 6)
                {
                    Trace.TraceWarning("Interpreter.Parse: parser key {0} is not the correct length", type);
                    return false;
                }

                // take the last three letters
                type = type.Substring(3, 3);

                // find the sentence parser
                if (!m_sentenceParserList.Contains(type))
                {
                    Trace.TraceWarning("Interpreter.Parse: No parser for sentence {0}", type);
                    return false;
                }

                // check if the parser is OK
                SentenceParserDelegate sentenceParser = m_sentenceParserList[type].Parser;
                if (sentenceParser == null)
                {
                    Trace.TraceError("Interpreter.Parse: Sentence parser is null for sentence {0}", type);
                    return false;
                }

                // execute the parser
                try
                {
                    return sentenceParser(sentence);
                }
                catch (Exception ex)
                {
                    Trace.TraceError("Interpreter.Parse: Error parsing sentence {0}: {1}", type, ex);
                }

            }
            catch (Exception ex)
            {
                Trace.TraceError("Interpreter.Parse: Error attempting to parse sentence: {0}\r\n{1}", sentence, ex);
            }
            return false;
        }

        /// <summary>
        /// Add a sentence parser for an NMEA sentence
        /// </summary>
        /// <param name="parser"></param>
        public void Add(SentenceParser parser)
        {
            Trace.TraceInformation("Interpreter.Add: Adding parser for sentence {0}", parser.ID);
            m_sentenceParserList.Add(parser);
        }

        #endregion

        #region events

        /// <summary> Error text </summary>
        public event EventHandler<SerialErrorReceivedEventArgs>? ErrorReceived;

        /// <summary> Bundle all of the position update data into one class </summary>
        public event Action<object, PositionItem>? PositionUpdate;

        /// <summary> The computed position </summary>
        public event Action<object, Position>? PositionReceived;

        /// <summary> The date and time changed Date and time in UTC </summary>
        public event Action<object, DateTime>? DateTimeChanged;

        /// <summary> The current heading </summary>
        public event Action<object, float>? BearingReceived;

        /// <summary> An indication of speed </summary>
        public event Action<object, float>? SpeedReceived;

        /// <summary> the fix (computed location) has been gained </summary>
        public event EventHandler? FixObtained;

        /// <summary> The fix (computed location) has been lost </summary>
        public event EventHandler? FixLost;

        /// <summary> The calculated magnetic variance </summary>
        public event Action<object, float>? MagVar;

        /// <summary> Message indicating that a satellite has been detected </summary>
        public event Action<object, Satellite>? SatelliteReceived;

        /// <summary> the total number of satellites that should be in the sky at this location </summary>
        public event Action<object, int>? SatellitesAvailable;

        /// <summary> The map datum currently set in the gps </summary>
        public event Action<object, string>? MapDatum;

        /// <summary> the computed altitude from the gps </summary>
        public event Action<object, float>? Altitude;

        /// <summary> An indication of link quality for a satellite </summary>
        public event Action<object, LinkQuality>? LinkQuality;

        /// <summary> A raw nmea sentence </summary>
        public event Action<object, string>? RawString;

        /// <summary> Precision </summary>
        public event Action<object, DOP>? Precision;

        /// <summary>
        /// Fix Type
        ///     0 = no fix
        ///     1 = 2D fix
        ///     2 = 3D fix
        /// </summary>
        public event Action<object, int>? FixType;

        /// <summary> The heading </summary>
        public event Action<object, float>? Heading;

        #endregion

        #region raise events

        private void RaisePositionUpdate(PositionItem item) => PositionUpdate?.Invoke(this, item);

        /// <summary>
        /// Trigger for when the position has been determined
        /// </summary>
        /// <param name="item"></param>
        protected void RaisePositionReceived(Position item) => PositionReceived?.Invoke(this, item);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="latitude"></param>
        /// <param name="longitude"></param>
		protected void RaisePositionReceived(double latitude, double longitude) => PositionReceived?.Invoke(this, new Position() { Lat = latitude, Lon = longitude });

        /// <summary>
        /// Trigger for when the date/time has been determined
        /// </summary>
        /// <param name="dateTime"></param>
        protected void RaiseDateTimeChanged(DateTime dateTime) => DateTimeChanged?.Invoke(this, dateTime);

        /// <summary>
        /// trigger for when the bearing has been determined
        /// </summary>
        /// <param name="bearing"></param>
        protected void RaiseBearingReceived(float bearing) => BearingReceived?.Invoke(this, bearing);

        /// <summary>
        /// trigger for when the speed has been determined
        /// </summary>
        /// <param name="speed"></param>
        protected void RaiseSpeedReceived(float speed) => SpeedReceived?.Invoke(this, speed);

        /// <summary>
        /// trigger for when the fix has been obtained
        /// </summary>
        protected void RaiseFixObtained() => FixObtained?.Invoke(this, EventArgs.Empty);

        /// <summary>
        /// trigger for when the fix has been lost
        /// </summary>
        protected void RaiseFixLost() => FixLost?.Invoke(this, EventArgs.Empty);

        /// <summary>
        /// Trigger for when the magnetic variance has been determined
        /// </summary>
        /// <param name="magVar"></param>
        protected void RaiseMagVar(float magVar) => MagVar?.Invoke(this, magVar);

        /// <summary>
        /// trigger for when a satellite data set has been received
        /// </summary>
        /// <param name="pseudoRandomCode"></param>
        /// <param name="azimuth"></param>
        /// <param name="elevation"></param>
        /// <param name="signalToNoiseRatio"></param>
        protected void RaiseSatelliteReceived(int pseudoRandomCode, int azimuth, int elevation, int signalToNoiseRatio) =>
            SatelliteReceived?.Invoke(this, new Satellite()
            {
                azimuth = azimuth,
                elevation = elevation,
                signalToNoiseRatio = signalToNoiseRatio,
                pseudoRandomCode = pseudoRandomCode
            });

        /// <summary>
        /// trigger idicating the number of satellites available
        /// </summary>
        /// <param name="num"></param>
        protected void RaiseSatellitesAvailable(int num) => SatellitesAvailable?.Invoke(this, num);

        /// <summary>
        /// Trigger idicating the datum at which the GPS has been set
        /// </summary>
        /// <param name="datum"></param>
        protected void RaiseMapDatum(string datum) => MapDatum?.Invoke(this, datum);

        /// <summary>
        /// trigger idicating altitude
        /// </summary>
        /// <param name="alt"></param>
        protected void RaiseAltitude(float alt) => Altitude?.Invoke(this, alt);

        /// <summary>
        /// trigger indicating link quality
        /// </summary>
        /// <param name="num"></param>
        protected void RaiseLinkQuality(LinkQuality num) => LinkQuality?.Invoke(this, num);

        /// <summary>
        /// The raw GPS data
        /// </summary>
        /// <param name="str"></param>
        protected void RaiseRawString(string str) => RawString?.Invoke(this, str);

        /// <summary>
        /// Sends an indication of dilution of precision
        /// DOP is a unitless number, smaller is better
        /// 1.0 or lower is perfect
        /// </summary>
        /// <param name="PDOP"></param>
        /// <param name="HDOP"></param>
        /// <param name="VDOP"></param>
        private void RaiseDOPObtained(float PDOP, float HDOP, float VDOP) => Precision?.Invoke(this, new DOP() { PDOP = PDOP, VDOP = VDOP, HDOP = HDOP });

        /// <summary>
        /// Fix Type
        ///     0 = no fix
        ///     1 = 2D fix
        ///     2 = 3D fix
        /// </summary>
        /// <param name="fixType"></param>
        private void RaiseFixType(int fixType) => FixType?.Invoke(this, fixType);

        /// <summary>
        /// The heading from GPHDT
        /// </summary>
        /// <param name="heading"></param>
        private void RaiseHeadingReceived(float heading) => Heading?.Invoke(this, heading);

        #endregion

        #region sentence parsers

        /// <summary>
        /// GPRMC: Reccomended Minumum 
        /// 
        /// RMC - NMEA has its own version of essential gps pvt (position, velocity, time) data. 
        /// It is called RMC, The Recommended Minimum, which will look similar to:
        ///
        /// $GPRMC,123519,A,4807.038,N,01131.000,E,022.4,084.4,230394,003.1,W*6A
        ///
        /// Where:
        ///     0      RMC          Recommended Minimum sentence C
        ///     1      123519       Fix taken at 12:35:19 UTC
        ///     2      A            Status A=active or V=Void.
        ///     3,4    4807.038,N   Latitude 48 deg 07.038' N
        ///     5,6    01131.000,E  Longitude 11 deg 31.000' E
        ///     7      022.4        Speed over the ground in knots
        ///     8      084.4        Track angle in degrees True
        ///     9      230394       Date - 23rd of March 1994
        ///     10,11  003.1,W      Magnetic Variation
        ///     12                  Mode indicator, added for NMEA 2.3, 
        ///                             A=Autonomous, 
        ///                             D=Differential, 
        ///                             E=Estimated, 
        ///                             N=Data not valid
        ///                         for NMEA before 2.3, this will not be here.
        ///     13                  *6A           The checksum data, always begins with *
        ///
        /// Note that, as of the 2.3 release of NMEA, there is a new field in the RMC sentence 
        /// at the end just prior to the checksum. This is the Mode Indicator field, and may
        /// not be present in older implementations of NMEA
        /// 
        /// Recommended minimum specific GPS/Transit data
        ///     word[1]  = UTC time of fix
        ///     word[2]  = Data status (A=Valid position, V=navigation receiver warning)
        ///     word[3]  = Latitude of fix
        ///     word[4]  = N or S of longitude
        ///     word[5]  = Longitude of fix
        ///     word[6]  = E or W of longitude
        ///     word[7]  = Speed over ground in knots
        ///     word[8]  = Track made good in degrees True
        ///     word[9]  = UTC date of fix
        ///     word[10] = Magnetic variation degrees (Easterly var. subtracts from true course)
        ///     word[11] = E or W of magnetic variation
        ///     word[12] = Mode indicator, (A=Autonomous, D=Differential, E=Estimated, N=Data not valid)
        ///     word[13] = Checksum
        /// 
        /// </summary>
        /// <param name="sentence"></param>
        /// <returns></returns>
        private bool ParseGPRMC(string sentence)
        {
            PositionItem item = Parser.ParseRMC(sentence);

            if (item.Position.HasValue)
                RaisePositionReceived(item.Position.Value);
            if (item.DateTime.HasValue)
                RaiseDateTimeChanged(item.DateTime.Value);
            if (item.Speed.HasValue)
                RaiseSpeedReceived(item.Speed.Value);
            if (item.Bearing.HasValue)
                RaiseBearingReceived(item.Bearing.Value);
            if (item.Declination.HasValue)
                RaiseMagVar(item.Declination.Value);

            switch (item.Fix)
            {
                case 'A': RaiseFixObtained(); break;
                case 'V': RaiseFixLost(); break;
                case ' ': break;
            }

            RaisePositionUpdate(item);

            //Indicate that the sentence was recognized
            return true;
        }

        /// <summary> 
        /// GGA - essential fix data which provide 3D location and accuracy data.
        ///
        /// $GPGGA,123519,4807.038,N,01131.000,E,1,08,0.9,545.4,M,46.9,M,,*47
        ///
        /// Where:
        ///     GGA          Global Positioning System Fix Data
        ///     123519       Fix taken at 12:35:19 UTC
        ///     4807.038,N   Latitude 48 deg 07.038' N
        ///     01131.000,E  Longitude 11 deg 31.000' E
        ///     1            Fix quality: 0 = invalid
        ///                               1 = GPS fix (SPS)
        ///                               2 = DGPS fix
        ///                               3 = PPS fix
        ///                               4 = Real Time Kinematic
        ///                               5 = Float RTK
        ///                               6 = estimated (dead reckoning) (2.3 feature)
        ///                               7 = Manual input mode
        ///                               8 = Simulation mode
        ///     08           Number of satellites being tracked
        ///     0.9          Horizontal dilution of position
        ///     545.4,M      Altitude, Meters, above mean sea level
        ///     46.9,M       Height of geoid (mean sea level) above WGS84
        ///                      ellipsoid
        ///     (empty field) time in seconds since last DGPS update
        ///     (empty field) DGPS station ID number
        ///     *47          the checksum data, always begins with *
        ///
        /// If the height of geoid is missing then the altitude should be suspect. Some non-standard 
        /// implementations report altitude with respect to the ellipsoid rather than geoid altitude. 
        /// Some units do not report negative altitudes at all. This is the only sentence that 
        /// reports altitude. 
        /// 
        /// Lat/Lon, Altitude and Link Quality
        /// </summary>
        /// 	word[1]  = hhmmss.ss = UTC of position
        ///	    word[2]  = ddmm.mmm = latitude of position
        /// 	word[3]  = a = N or S, latitutde hemisphere
        ///	    word[4]  = dddmm.mmm = longitude of position
        /// 	word[5]  = b = E or W, longitude hemisphere
        ///	    word[6]  = q = GPS Quality indicator (
        ///                 0=No fix, 
        ///                 1 = SPS (Non-differential GPS) fix 
        ///                 2 = DGPS (Differential GPS) fix
        ///                 3 = PPS fix
        ///                 4 = real time kinematic
        ///                 5 = float RTK
        ///                 6 = Estimated fix
        ///                 7 = manual input mode
        ///                 8 = simulation mode
        ///	    word[7]  = xx = number of satellites in use
        /// 	word[8]  = p.p = horizontal dilution of precision
        /// 	word[9]  = a.b = Antenna altitude above mean-sea-level
        /// 	word[10] = M = units of antenna altitude, meters
        /// 	word[11] = c.d = Geoidal height
        ///  	word[12] = M = units of geoidal height, meters
        ///	    word[13] = x.x = Age of Differential GPS data (seconds since last valid RTCM transmission)
        ///	    word[14] = nnnn = Differential reference station ID, 0000 to 1023 
        /// 
        ///
        /// 
        /// <param name="sentence"></param>
        /// <returns></returns>
        private bool ParseGPGGA(string sentence)
        {
            PositionItem item = Parser.ParseGGA(sentence);

            if (item.Position.HasValue)
                RaisePositionReceived(item.Position.Value);

            if (item.Altitude.HasValue)
                RaiseAltitude(item.Altitude.Value);

            if (item.LinkQuality.HasValue)
                RaiseLinkQuality(item.LinkQuality.Value);

            RaisePositionUpdate(item);

            return true;
        }

        /// <summary> === GNS - Fix data ===
        ///
        /// ------------------------------------------------------------------------------
        ///        1         2       3 4        5 6    7  8   9   10  11  12  13
        ///        |         |       | |        | |    |  |   |   |   |   |   |
        /// $--GNS,hhmmss.ss,llll.ll,a,yyyyy.yy,a,c--c,xx,x.x,x.x,x.x,x.x,x.x*hh
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
        bool ParseGPGNS(string sentence)
        {
            PositionItem item = Parser.ParseGNS(sentence);

            if (item.DateTime.HasValue)
                RaiseDateTimeChanged(item.DateTime.Value);

            if (item.Position.HasValue)
                RaisePositionReceived(item.Position.Value);

            if (item.Altitude.HasValue)
                RaiseAltitude(item.Altitude.Value);

            RaisePositionUpdate(item);

            return true;
        }

        /// <summary>
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
        /// </summary>
        /// <param name="sentence"></param>
        /// <returns></returns>
        private bool ParseGPGLL(string sentence)
        {
            PositionItem item = Parser.ParseGLL(sentence);

            if (item.Position.HasValue)
                RaisePositionReceived(item.Position.Value);

            RaisePositionUpdate(item);
            return true;
        }

        /// <summary> GSV Satellites in view
        /// GSV - Satellites in View shows data about the satellites that the unit might be able to find 
        /// based on its viewing mask and almanac data. It also shows current ability to track this data. 
        /// Note that one GSV sentence only can provide data for up to 4 satellites and thus there may 
        /// need to be 3 sentences for the full information. It is reasonable for the GSV sentence to 
        /// contain more satellites than GGA might indicate since GSV may include satellites that are 
        /// not used as part of the solution. It is not a requirment that the GSV sentences all appear 
        /// in sequence. To avoid overloading the data bandwidth some receivers may place the various 
        /// sentences in totally different samples since each sentence identifies which one it is.
        ///
        /// The field called SNR (Signal to Noise Ratio) in the NMEA standard is often referred to as 
        /// signal strength. SNR is an indirect but more useful value that raw signal strength. It can 
        /// range from 0 to 99 and has units of dB according to the NMEA standard, but the various 
        /// manufacturers send different ranges of numbers with different starting numbers so the values 
        /// themselves cannot necessarily be used to evaluate different units. The range of working values 
        /// in a given gps will usually show a difference of about 25 to 35 between the lowest and highest 
        /// values, however 0 is a special case and may be shown on satellites that are in view but not 
        /// being tracked.
        ///
        ///  $GPGSV,2,1,08,01,40,083,46,02,17,308,41,12,07,344,39,14,22,228,45*75
        ///
        /// Where:
        ///      GSV          Satellites in view
        ///      2            Number of sentences for full data
        ///      1            sentence 1 of 2
        ///      08           Number of satellites in view
        ///
        ///      01           Satellite PRN number
        ///      40           Elevation, degrees
        ///      083          Azimuth, degrees
        ///      46           SNR - higher is better
        ///           for up to 4 satellites per sentence
        ///      *75          the checksum data, always begins with *
        ///
        ///
        /// words[1]    = Total number of messages of this type in this cycle
        /// words[2]    = Message number
        /// words[3]    = Total number of SVs in view
        /// words[4]    = SV PRN number
        /// words[5]    = Elevation in degrees, 90 maximum
        /// words[6]    = Azimuth, degrees from true north, 000 to 359
        /// words[7]    = SNR, 00-99 dB (null when not tracking)
        /// words[8-11] = Information about second SV, same as field 4-7
        /// words[12-15]= Information about third SV, same as field 4-7
        /// words[16-19]= Information about fourth SV, same as field 4-7
        /// </summary>
        /// <param name="sentence"></param>
        /// <returns></returns>
        private bool ParseGPGSV(string sentence)
        {
            int pseudoRandomCode;
            int azimuth;
            int elevation;
            int signalToNoiseRatio;

            // Divide the sentence into words
            string[] words = Parser.GetWords(sentence);

            // Each sentence contains four blocks of satellite information.
            if (!string.IsNullOrEmpty(words[3]))
            {
                RaiseSatellitesAvailable(Convert.ToInt32(words[3]));
            }

            // Read each block and report each satellite's information
            for (int i = 1; i <= 4; i++)
            {
                // Does the sentence have enough words to analyze?
                int index = i * 4;
                if (index + 3 < words.Length)
                {
                    // Yes.  Proceed with analyzing the block.  Does it contain any
                    // information?

                    if (!string.IsNullOrEmpty(words[index]) &&
                        !string.IsNullOrEmpty(words[index + 1]) &&
                        !string.IsNullOrEmpty(words[index + 2]) &&
                        !string.IsNullOrEmpty(words[index + 3]))
                    {
                        // Yes. Extract satellite information and report it

                        pseudoRandomCode = Convert.ToInt32(words[index]);
                        elevation = Convert.ToInt32(words[index + 1]);
                        azimuth = Convert.ToInt32(words[index + 2]);
                        signalToNoiseRatio = Convert.ToInt32(words[index + 3]);

                        // Notify of this satellite's information
                        RaiseSatelliteReceived(pseudoRandomCode, azimuth, elevation, signalToNoiseRatio);
                    }
                }
            }

            // Indicate that the sentence was recognized
            return true;
        }

        /// <summary> GPS DOP and Active Satellites
        /// GPS DOP and active satellites. This sentence provides details on the nature of the fix. It 
        /// includes the numbers of the satellites being used in the current solution and the DOP. DOP 
        /// (dilution of precision) is an indication of the effect of satellite geometry on the accuracy 
        /// of the fix. It is a unitless number where smaller is better. For 3D fixes using 4 satellites 
        /// a 1.0 would be considered to be a perfect number, however for overdetermined solutions it is 
        /// possible to see numbers below 1.0.
        ///
        /// There are differences in the way the PRN's are presented which can effect the ability of some 
        /// programs to display this data. For example, in the example shown below there are 5 satellites 
        /// in the solution and the null fields are scattered indicating that the almanac would show 
        /// satellites in the null positions that are not being used as part of this solution. Other 
        /// receivers might output all of the satellites used at the beginning of the sentence with the 
        /// null field all stacked up at the end. This difference accounts for some satellite display 
        /// programs not always being able to display the satellites being tracked. Some units may show 
        /// all satellites that have ephemeris data without regard to their use as part of the solution 
        /// but this is non-standard. 
        /// 
        ///  $GPGSA,A,3,04,05,,09,12,,,24,,,,,2.5,1.3,2.1*39
        ///  Where:
        ///  0      GSA      Satellite status
        ///  1      A        Auto selection of 2D or 3D fix (M = manual) 
        ///  2      3        3D fix - values include: 1 = no fix
        ///                                       2 = 2D fix
        ///                                       3 = 3D fix
        ///  3-14   04,05... PRNs of satellites used for fix (space for 12) 
        ///  15     2.5      PDOP (dilution of precision) 
        ///  16     1.3      Horizontal dilution of precision (HDOP) 
        ///  17     2.1      Vertical dilution of precision (VDOP)
        ///  18     *39      the checksum data, always begins with *
        /// </summary>
        private bool ParseGPGSA(string sentence)
        {
            // Divide the sentence into words
            string[] words = Parser.GetWords(sentence);

            if (!string.IsNullOrEmpty(words[2]))
            {
                int fixType = Convert.ToInt32(words[2]);
                RaiseFixType(fixType);
            }

            if (!string.IsNullOrEmpty(words[15]) &&
                !string.IsNullOrEmpty(words[16]) &&
                !string.IsNullOrEmpty(words[17]))
            {
                float PDOP = Convert.ToSingle(words[15]);
                float HDOP = Convert.ToSingle(words[16]);
                float VDOP = Convert.ToSingle(words[17]);
                RaiseDOPObtained(PDOP, HDOP, VDOP);
            }

            return true;
        }

        #region garmin sentences

        /// <summary> PGRMZ Altitude in feet
        /// $PGRMZ,93,f,3*21
        /// where:
        ///      93,f         Altitude in feet
        ///      3            Position fix dimensions 2 = user altitude
        ///                                           3 = GPS altitude
        ///   This sentence shows in feet, regardless of units shown on the display.
        ///   Note that for units with an altimeter this will be altitude computed
        ///   by the internal altimeter.
        /// 
        /// Proprietary Garmin Altitude
        /// Alitude Information        
        /// words[1]  201   Altitude
        ///	words[2]  F     Units - f-Feet
        ///	words[3]  checksum
        /// </summary>
        /// <param name="sentence"></param>
        /// <returns></returns>
        private bool ParsePGRMZ(string sentence) =>
            //			if (OnAltitude!=null )
            //			{
            //				// Divide the sentence into words
            //				string[] words = GetWords(sentence);
            //
            //				// Each sentence contains four blocks of satellite information.
            //				if (words[1] != string.Empty)
            //					OnAltitude(words[1]);
            //			}
            true;

        /// <summary> Position error
        /// $PGRME,15.0,M,45.0,M,25.0,M*1C
        ///
        /// where:
        ///     15.0,M       Estimated horizontal position error in meters (HPE)
        ///     45.0,M       Estimated vertical error (VPE) in meters
        ///     25.0,M       Overall spherical equivalent position error
        /// </summary>
        /// <param name="sentence"></param>
        /// <returns></returns>
        private bool ParsePGRME(string sentence) => false;

        /// <summary> active datum
        /// $PGRMM,NAD27 Canada*2F
        /// Currently active horizontal datum
        /// </summary>
        /// <param name="sentence"></param>
        /// <returns></returns>
        private bool ParsePGRMM(string sentence)
        {
            // Divide the sentence into words
            string[] words = Parser.GetWords(sentence);

            // Each sentence contains four blocks of satellite information.
            if (!string.IsNullOrEmpty(words[1]))
                RaiseMapDatum(words[1]);

            return true;
        }

        /// <summary> compass output
        /// HCHDG - Compass output is used on Garmin etrex summit, vista , 
        /// and 76S receivers to output the value of the internal flux-gate compass. 
        /// Only the magnetic heading and magnetic variation is shown in the message.
        ///  $HCHDG,101.1,,,7.1,W*3C
        ///
        /// where:
        ///     HCHDG    Magnetic heading, deviation, variation
        ///     101.1    heading
        ///     ,,       deviation (no data)
        ///     7.1,W    variation
        ///
        /// </summary>
        /// <param name="sentence"></param>
        /// <returns></returns>
        private bool ParseHCHDG(string sentence) => false;

        #endregion

        /// <summary> GPZDA - Date and Time
        ///
        ///  $GPZDA,hhmmss.ss,dd,mm,yyyy,xx,yy*CC
        ///  $GPZDA,201530.00,04,07,2002,00,00*6E
        ///
        /// where:
        ///    1 hhmmss.ss  HrMinSec(UTC)
        ///    2 dd,        day
        ///    3 mm,        month
        ///    4 yyyy       year
        ///    5 xx         local zone hours -13..13
        ///    6 yy         local zone minutes 0..59
        ///    7 *CC        checksum
        /// </summary>
        /// <param name="sentence"></param>
        /// <returns></returns>
        private bool ParseGPZDA(string sentence)
        {

            // Divide the sentence into words
            string[] words = Parser.GetWords(sentence);

            if (string.IsNullOrEmpty(words[1]) ||
                string.IsNullOrEmpty(words[2]) ||
                string.IsNullOrEmpty(words[3]) ||
                string.IsNullOrEmpty(words[4]) ||
                string.IsNullOrEmpty(words[5]) ||
                string.IsNullOrEmpty(words[6]))
            {
                Trace.TraceInformation("Interpreter.ParseGPZDA: some fields are null, cannot parse date/time");
                return false;
            }

            //Do we have enough values to parse satellite-derived time?
            {
                // Yes. Extract hours, minutes, seconds and milliseconds
                int UtcHours = int.Parse(words[1][..2]);
                int UtcMinutes = int.Parse(words[1].Substring(2, 2));
                int UtcSeconds = int.Parse(words[1].Substring(4, 2));
                int UtcMilliseconds = 0;

                // Extract milliseconds if it is available
                if (words[1].Length > 7)
                    UtcMilliseconds = Convert.ToInt32(words[1][7..]);

                int UtcDays = int.Parse(words[2][..2]);
                int UtcMonths = int.Parse(words[3][..2]);
                int UtcYears = int.Parse(words[4][..4]);

                try
                {
                    // Now build a DateTime object with all values
                    // DateTime Today = System.DateTime.Now.ToUniversalTime();
                    DateTime SatelliteTime = new(UtcYears, UtcMonths, UtcDays, UtcHours, UtcMinutes, UtcSeconds, UtcMilliseconds);

                    //Notify of the new time, adjusted to the local time zone
                    RaiseDateTimeChanged(SatelliteTime);
                }
                catch (Exception ex)
                {
                    Trace.TraceError("Interpreter.ParseGPZDA: Error parsing datetime in GPRMC:\r\n{0}", ex);
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// HEADING FROM TRUE NORTH
        /// An example of the HDT string is:
        /// $GPHDT,123.456,T*00/r/n
        /// Heading from true north message fields
        /// Field Meaning
        ///     0	Message ID $GPHDT, also $xxHDT could be used, where xx is some other characters.
        ///     1	Heading in degrees
        ///     2	T: Indicates heading relative to True North
        ///     3	The checksum data, always begins with*
        /// </summary>
        /// <param name="sentence"></param>
        /// <returns></returns>
        private bool ParseGPHDT(string sentence)
        {
            string[] words = Parser.GetWords(sentence);

            int heading = Convert.ToInt32(words[2]);

            RaiseHeadingReceived(heading);

            return true;
        }

        #endregion

        #region private methods

        /// <summary>
        /// This thread waits for items to be added to the blocking queue
        /// </summary>
        private void NMEAParserConsumerTask()
        {
            while (!m_dataItems.IsCompleted)
            {
                string? data = null;

                // Blocks if number.Count == 0
                // IOE means that Take() was called on a completed collection.
                // Some other thread can call CompleteAdding after we pass the
                // IsCompleted check but before we call Take. 
                // In this example, we can simply catch the exception since the 
                // loop will break on the next iteration.
                try
                {
                    data = m_dataItems.Take();
                }
                catch (InvalidOperationException) { }

                if (!string.IsNullOrEmpty(data))
                    ReceiveString(data);
            }
        }

        /// <summary>
        /// Called to process a string received from the serial port
        /// This builds up sentences in a buffer and processes them when they are "complete"
        /// It deletes those with no start so we don't have incomplete.
        /// </summary>
        /// <param name="buffer"></param>
        private void ReceiveString(string buffer)
        {
            // if we're not started, then exit
            if (!IsStarted) return;

            // if the incomming buffer is null then just exit
            if (m_buffer == null) return;

            // split the sentences on the \r, trim the \n
            string[] sentences = buffer.Split(['\n'], StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < sentences.Length; i++)
            {
                if (i == 0)
                {
                    if (m_buffer.Length != 0 && !sentences[0].StartsWith('$'))
                        sentences[0] = m_buffer.ToString() + sentences[0];
                    m_buffer = new StringBuilder();
                }

                if (i == sentences.Length - 1 && !sentences[i].EndsWith('\r'))
                {
                    if (m_buffer == null)
                        return;
                    m_buffer.Append(sentences[i]);
                    continue;
                }

                if (!this.IsStarted)
                    return;

                Parse(sentences[i]);
            }
        }

        #endregion

        #region event handlers

        /// <summary>
        /// An error while reading the serial port
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void HandleErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            if (e.EventType == SerialError.RXOver)
                return;

            if (m_serialPort == null || !m_serialPort.IsOpen)
                return;

            HasError = true;

            ErrorReceived?.Invoke(this, e);
        }

        /// <summary>
		/// Callback for when serial port receives data
        /// Just add the ascii data to the blocking collection where another thread handles the processing
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		void HandleDataReceived(object? sender, SerialDataReceivedEventArgs e)
        {
            if (m_serialPort == null || !m_serialPort.IsOpen) 
                return;

            try
            {
                m_dataItems.Add(m_serialPort.ReadExisting());
            }
            catch (Exception ex)
            {
                Trace.TraceError("Interpreter.DataReceived: Error receiving data from the COM port {0}:\r\n{1}", m_serialPort, ex);
            }
        }

        #endregion
    }
}
