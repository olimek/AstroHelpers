using System;

namespace AstroHelpers
{

    public class MoonInfo
    {
        public DateTime DateTimeLocal { get; set; }
        public string PhaseName { get; set; }
        public double AgeInDays { get; set; }
        public double IlluminationFraction { get; set; }
    }

    public static class MoonCalculator
    {
        private const double SynodicMonth = 29.5305882;


        public static MoonInfo GetMoonInfo(DateTime dateTime, TimeZoneInfo timeZone = null)
        {
            timeZone ??= TimeZoneInfo.Local;

            DateTime utcTime = TimeZoneInfo.ConvertTimeToUtc(dateTime, timeZone);
            double jd = ToJulianDate(utcTime);

            double daysSinceEpoch2000 = jd - 2451549.5;

            double moonAge = (daysSinceEpoch2000 % SynodicMonth + SynodicMonth) % SynodicMonth;

            double phaseFraction = moonAge / SynodicMonth;

            double illumination = 0.5 * (1 - Math.Cos(2.0 * Math.PI * phaseFraction));

            string phaseName = GetPhaseName(phaseFraction);

            return new MoonInfo
            {
                DateTimeLocal = dateTime,
                PhaseName = phaseName,
                AgeInDays = moonAge,
                IlluminationFraction = illumination
            };
        }

        private static double ToJulianDate(DateTime utcDateTime)
        {
            if (utcDateTime.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException("ToJulianDate: DateTime musi być w UTC.");
            }

            int year = utcDateTime.Year;
            int month = utcDateTime.Month;
            double day = utcDateTime.Day
                         + utcDateTime.Hour / 24.0
                         + utcDateTime.Minute / 1440.0
                         + utcDateTime.Second / 86400.0;

            if (month <= 2)
            {
                year--;
                month += 12;
            }

            int A = year / 100;
            int B = 2 - A + (A / 4);

            double jd = Math.Floor(365.25 * (year + 4716))
                        + Math.Floor(30.6001 * (month + 1))
                        + day + B - 1524.5;

            return jd;
        }

        private static string GetPhaseName(double phaseFraction)
        {

            if (phaseFraction < 0.03 || phaseFraction > 0.97)
                return "New Moon";
            else if (phaseFraction < 0.22)
                return "Waxing Crescent";
            else if (phaseFraction < 0.28)
                return "First Quarter";
            else if (phaseFraction < 0.47)
                return "Waxing Gibbous";
            else if (phaseFraction < 0.53)
                return "Full Moon";
            else if (phaseFraction < 0.72)
                return "Waning Gibbous";
            else if (phaseFraction < 0.78)
                return "Last Quarter";
            else
                return "Waning Crescent";
        }
    }
}
