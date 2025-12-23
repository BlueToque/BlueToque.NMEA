using System;

namespace BlueToque.NMEA
{
    /// <summary>
    /// A class containing all position update information
    /// </summary>
    public class PositionItem
    {
        /// <summary>
        /// Position in latitude / longitude
        /// </summary>
        public Position? Position { get; internal set; }

        /// <summary>
        /// Altitude in metres
        /// </summary>
        public float? Altitude { get; internal set; }

        /// <summary>
        /// DateTime of the fix
        /// </summary>
        public DateTime? DateTime { get; internal set; }

        /// <summary>
        /// Speed in km/h
        /// </summary>
        public float? Speed { get; internal set; }

        /// <summary>
        /// bearing in degrees
        /// </summary>
        public float? Bearing { get; internal set; }

        /// <summary>
        /// declination in degrees
        /// </summary>
        public float? Declination { get; internal set; }

        /// <summary>
        /// GPS Quality indicator 
        ///                 0 = No fix, 
        ///                 1 = SPS (Non-differential GPS) fix 
        ///                 2 = DGPS (Differential GPS) fix
        ///                 3 = PPS fix
        ///                 4 = real time kinematic
        ///                 5 = float RTK
        ///                 6 = Estimated fix
        ///                 7 = manual input mode
        ///                 8 = simulation mode
        /// </summary>
        public LinkQuality? LinkQuality { get; internal set; }

        /// <summary>
        /// Number of satellites
        /// </summary>
        public int? NumSatellites { get; internal set; }

        /// <summary>
        /// Horizontal dilution of precision, metres
        /// </summary>
        public float? HDOP { get; internal set; }

        /// <summary>
        /// 
        /// </summary>
        public float? PDOP { get; internal set; }

        /// <summary>
        /// 
        /// </summary>
        public float? VDOP { get; internal set; }

        /// <summary>
        /// 'A' - obtained
        /// 'V' - lost
        /// </summary>
        public char Fix { get; internal set; }

        /// <summary>
        /// 
        /// </summary>
        public static readonly char FIX_OBTAINED = 'A';

        /// <summary>
        /// 
        /// </summary>
        public static readonly char FIX_LOST = 'V';

        /// <summary>
        /// using Filtered UERE, rms
        /// Estimated Position Error (EPE) 
        /// 
        /// EPE (1-sigma) = HDOP * UERE (1-sigma) (1)
        /// EPE (2drms)  = HDOP * UERE * 2 
        /// EPE (2drms) = 2 * HDOP * SQRT [URE^2 + UEE^2] (2)
        /// choosing 1.5 as UERE from table below
        /// </summary>
        public float? HError => !HDOP.HasValue ? null : (float?)(HDOP.Value * 1.5f);

        /// <summary>
        /// See HError, choosing 1.5 as UERE from table below
        /// </summary>
        public float? VError => !VDOP.HasValue ? null : (float?)(VDOP.Value * 1.5f);

        /// <summary>
        /// A record to store where the data came from
        /// </summary>
        public string Source { get; internal set; } = "";
    }

    /* http://edu-observatory.org/gps/gps_accuracy.html

        Standard error model - L1 C/A (no SA)

        Error source      		    Bias    Random 	Total   DGPS
        ------------------------------------------------------------
        Ephemeris data 			    2.1 	0.0 	2.1	    0.0
        Satellite clock 		    2.0 	0.7 	2.1     0.0
        Ionosphere 			        4.0 	0.5 	4.0     0.4
        Troposphere 			    0.5 	0.5 	0.7     0.2
        Multipath 			        1.0 	1.0 	1.4     1.4
        Receiver measurement 	    0.5 	0.2  	0.5     0.5
        ------------------------------------------------------------
        User equivalent range 
          error (UERE), rms* 	    5.1 	1.4 	5.3     1.6
        Filtered UERE, rms 		    5.1 	0.4  	5.1     1.5
        ------------------------------------------------------------

        Vertical one-sigma errors--VDOP= 2.5           12.8     3.9
        Horizontal one-sigma errors--HDOP= 2.0         10.2     3.1

     */

    /// <summary>
    /// 
    /// </summary>
    public enum LinkQuality : int
    {
        /// 0 = No fix, 
        NoFix = 0,
        /// 1 = SPS (Non-differential GPS) fix 
        SPS = 1,
        /// 2 = DGPS (Differential GPS) fix
        DGPS = 2,
        /// 3 = PPS fix
        PPS = 3,
        /// 4 = real time kinematic
        RTK = 4,
        /// 5 = float RTK
        FloatRTK = 5,
        /// 6 = Estimated fix
        Estimate = 6,
        /// 7 = manual input mode
        Manual = 7,
        /// 8 = simulation mode
        Simulated = 8,
    }
}
