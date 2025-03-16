using System;
using System.Collections.Generic;

namespace AstroHelpers
{

    public class AstronomicalNightNotAvailableException : Exception
    {

        public DateTime FallbackTime { get; }

        public AstronomicalNightNotAvailableException(DateTime fallbackTime, string message)
            : base(message)
        {
            FallbackTime = fallbackTime;
        }
    }

    public static class TwilightCalculator
    {

        private static readonly Dictionary<string, (double zenith, bool isSunrise)> PhenomenonMap
            = new Dictionary<string, (double, bool)>
        {
            { "Astronomical_Dawn", (108.0, true) },   // świt astronomiczny
            { "Astronomical_Dusk", (108.0, false) },  // zmierzch astronomiczny
            { "Nautical_Dawn", (102.0, true) },        // świt nautyczny
            { "Nautical_Dusk", (102.0, false) },       // zmierzch nautyczny
            { "Civil_Dawn", (96.0, true) },            // świt cywilny
            { "Civil_Dusk", (96.0, false) },           // zmierzch cywilny
            { "Sunrise", (90.833, true) },             // wschód słońca
            { "Sunset", (90.833, false) }              // zachód słońca
        };

        
        public static DateTime? GetPhenomenonTime(
            double latitude,
            double longitude,
            string phenomenon,
            DateTime? date = null,
            TimeZoneInfo timeZone = null)
        {
            var actualDate = date?.Date ?? DateTime.Now.Date;
            timeZone ??= TimeZoneInfo.Local;

            if (!PhenomenonMap.TryGetValue(phenomenon, out var info))
            {
                throw new ArgumentException($"Nieznane zjawisko: {phenomenon}");
            }

            int dayOfYear = actualDate.DayOfYear;
            int year = actualDate.Year;
            double? utcHour = CalculateSunEventUtc(
                dayOfYear,
                latitude,
                longitude,
                info.zenith,
                info.isSunrise);

            if (utcHour.HasValue)
            {
                return ConvertToLocalTime(year, dayOfYear, utcHour.Value, timeZone);
            }
            else
            {
                if (phenomenon == "Astronomical_Dawn" || phenomenon == "Astronomical_Dusk")
                {
                    string fallbackPhenomenon = (phenomenon == "Astronomical_Dawn")
                        ? "Nautical_Dawn"
                        : "Nautical_Dusk";

                    if (PhenomenonMap.TryGetValue(fallbackPhenomenon, out var fallbackInfo))
                    {
                        double? fallbackUtcHour = CalculateSunEventUtc(
                            dayOfYear,
                            latitude,
                            longitude,
                            fallbackInfo.zenith,
                            fallbackInfo.isSunrise);

                        if (fallbackUtcHour.HasValue)
                        {
                            DateTime fallbackTime = ConvertToLocalTime(year, dayOfYear, fallbackUtcHour.Value, timeZone);

                            throw new AstronomicalNightNotAvailableException(
                                fallbackTime,
                                $"Zjawisko {phenomenon} nie występuje dla tej daty i lokalizacji. " +
                                $"Zamiast tego wystąpi {fallbackPhenomenon} o {fallbackTime}."
                            );
                        }
                    }
                }

                return null;
            }
        }

        private static double? CalculateSunEventUtc(
            int dayOfYear,
            double latitude,
            double longitude,
            double zenith,
            bool isSunrise)
        {
            const double d2r = Math.PI / 180.0; 
            const double r2d = 180.0 / Math.PI; 

            double lngHour = longitude / 15.0;

            double t = isSunrise
                ? dayOfYear + ((6.0 - lngHour) / 24.0)
                : dayOfYear + ((18.0 - lngHour) / 24.0);

            double M = (0.9856 * t) - 3.289;

            double L = M
                       + (1.916 * Math.Sin(M * d2r))
                       + (0.020 * Math.Sin(2.0 * M * d2r))
                       + 282.634;
            L = NormalizeDegrees(L);

            double RA = r2d * Math.Atan(0.91764 * Math.Tan(L * d2r));
            RA = NormalizeDegrees(RA);

            double Lquadrant = Math.Floor(L / 90.0) * 90.0;
            double RAquadrant = Math.Floor(RA / 90.0) * 90.0;
            RA += (Lquadrant - RAquadrant);

            RA /= 15.0;

            double sinDec = 0.39782 * Math.Sin(L * d2r);
            double cosDec = Math.Cos(Math.Asin(sinDec));

            double cosH = (Math.Cos(zenith * d2r) - sinDec * Math.Sin(latitude * d2r))
                          / (cosDec * Math.Cos(latitude * d2r));

            if (cosH < -1.0 || cosH > 1.0)
            {
                return null;
            }

            double H = isSunrise
                ? 360.0 - r2d * Math.Acos(cosH)
                : r2d * Math.Acos(cosH);

            H /= 15.0; 

            double T = H + RA - (0.06571 * t) - 6.622;

            double UT = T - lngHour;

            UT = NormalizeHours(UT);

            return UT;
        }

        private static DateTime ConvertToLocalTime(int year, int dayOfYear, double utcHour, TimeZoneInfo timeZone)
        {
            int hour = (int)utcHour;
            int minute = (int)((utcHour - hour) * 60.0);
            int second = 0;

            var dateUtc = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                .AddDays(dayOfYear - 1)
                .AddHours(hour)
                .AddMinutes(minute)
                .AddSeconds(second);

            return TimeZoneInfo.ConvertTimeFromUtc(dateUtc, timeZone);
        }

        private static double NormalizeDegrees(double angle)
        {
            double result = angle % 360.0;
            return (result < 0) ? result + 360.0 : result;
        }

        private static double NormalizeHours(double hours)
        {
            double result = hours % 24.0;
            return (result < 0) ? result + 24.0 : result;
        }
    }
}
