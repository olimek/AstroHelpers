using System;
using System.Collections.Generic;

namespace AstroHelpers
{
    /// <summary>
    /// Rzucany, gdy noc astronomiczna nie występuje, a biblioteka
    /// wyznaczyła „fallback” w postaci nocy nautycznej.
    /// </summary>
    public class AstronomicalNightNotAvailableException : Exception
    {
        /// <summary>
        /// Czas zjawiska nautycznego (fallback), który zastępuje noc astronomiczną.
        /// </summary>
        public DateTime FallbackTime { get; }

        public AstronomicalNightNotAvailableException(DateTime fallbackTime, string message)
            : base(message)
        {
            FallbackTime = fallbackTime;
        }
    }

    public static class TwilightCalculator
    {
        /// <summary>
        /// Słownik mapujący nazwę zjawiska (np. "Nautical_Dawn") na parę:
        /// (zenith, isSunrise).
        ///  - zenith: kąt Słońca względem horyzontu,
        ///  - isSunrise: true = zjawisko poranne (dawn), false = zjawisko wieczorne (dusk).
        /// </summary>
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

        /// <summary>
        /// Zwraca moment wystąpienia zadanego zjawiska (np. "Nautical_Dawn") 
        /// dla wskazanej pozycji obserwatora i daty, w podanej strefie czasowej.
        /// Jeśli zjawisko nie występuje, metoda może zwrócić null
        /// lub w przypadku nocy astronomicznej - rzucić wyjątek z fallbackiem (noc nautyczna).
        /// </summary>
        /// <param name="latitude">Szerokość geogr. (N > 0, S < 0)</param>
        /// <param name="longitude">Długość geogr. (E > 0, W < 0)</param>
        /// <param name="phenomenon">Nazwa zjawiska (np. "Astronomical_Dawn")</param>
        /// <param name="date">Data (domyślnie dzisiejsza)</param>
        /// <param name="timeZone">Strefa czasowa (domyślnie lokalna)</param>
        /// <returns>Data i godzina zjawiska lub null, jeśli nie występuje.</returns>
        /// <exception cref="ArgumentException">Gdy zjawisko nie jest rozpoznane.</exception>
        /// <exception cref="AstronomicalNightNotAvailableException">
        /// Gdy zjawisko Astronomical_Dawn/Dusk nie występuje, a obliczono fallback (Nautical_Dawn/Dusk).
        /// </exception>
        public static DateTime? GetPhenomenonTime(
            double latitude,
            double longitude,
            string phenomenon,
            DateTime? date = null,
            TimeZoneInfo timeZone = null)
        {
            // 1. Przygotowanie parametrów
            var actualDate = date?.Date ?? DateTime.Now.Date;
            timeZone ??= TimeZoneInfo.Local;

            // 2. Sprawdzenie, czy zjawisko jest w słowniku
            if (!PhenomenonMap.TryGetValue(phenomenon, out var info))
            {
                throw new ArgumentException($"Nieznane zjawisko: {phenomenon}");
            }

            // 3. Wyznaczenie pory w UTC
            int dayOfYear = actualDate.DayOfYear;
            int year = actualDate.Year;
            double? utcHour = CalculateSunEventUtc(
                dayOfYear,
                latitude,
                longitude,
                info.zenith,
                info.isSunrise);

            // 4. Jeśli obliczenie się powiodło, zwracamy czas w strefie docelowej
            if (utcHour.HasValue)
            {
                return ConvertToLocalTime(year, dayOfYear, utcHour.Value, timeZone);
            }
            else
            {
                // 5. Zjawisko nie występuje. Sprawdzamy, czy chodzi o noc astronomiczną
                if (phenomenon == "Astronomical_Dawn" || phenomenon == "Astronomical_Dusk")
                {
                    // 5a. Obliczamy fallback (noc nautyczna)
                    string fallbackPhenomenon = (phenomenon == "Astronomical_Dawn")
                        ? "Nautical_Dawn"
                        : "Nautical_Dusk";

                    // Pobieramy parametry nautyczne
                    if (PhenomenonMap.TryGetValue(fallbackPhenomenon, out var fallbackInfo))
                    {
                        double? fallbackUtcHour = CalculateSunEventUtc(
                            dayOfYear,
                            latitude,
                            longitude,
                            fallbackInfo.zenith,
                            fallbackInfo.isSunrise);

                        // Jeśli noc nautyczna występuje, rzucamy wyjątek z informacją
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

                // 5b. W innym przypadku zwracamy null
                return null;
            }
        }

        /// <summary>
        /// Oblicza godzinę (0–24) w UTC, o której Słońce osiąga dany zenith.
        /// Używa uproszczonego algorytmu NOAA.
        /// Zwraca null, jeśli zjawisko nie występuje (|cosH| > 1).
        /// </summary>
        private static double? CalculateSunEventUtc(
            int dayOfYear,
            double latitude,
            double longitude,
            double zenith,
            bool isSunrise)
        {
            const double d2r = Math.PI / 180.0; // stopnie -> radiany
            const double r2d = 180.0 / Math.PI; // radiany -> stopnie

            // Przeliczamy długość geogr. na godziny (15° = 1h)
            double lngHour = longitude / 15.0;

            // Wschód: baza 6h, zachód: baza 18h
            double t = isSunrise
                ? dayOfYear + ((6.0 - lngHour) / 24.0)
                : dayOfYear + ((18.0 - lngHour) / 24.0);

            // Średnia anomalia Słońca
            double M = (0.9856 * t) - 3.289;

            // True longitude Słońca
            double L = M
                       + (1.916 * Math.Sin(M * d2r))
                       + (0.020 * Math.Sin(2.0 * M * d2r))
                       + 282.634;
            L = NormalizeDegrees(L);

            // Rektascensja
            double RA = r2d * Math.Atan(0.91764 * Math.Tan(L * d2r));
            RA = NormalizeDegrees(RA);

            // Dopasowanie RA do tego samego "kwadrantu" co L
            double Lquadrant = Math.Floor(L / 90.0) * 90.0;
            double RAquadrant = Math.Floor(RA / 90.0) * 90.0;
            RA += (Lquadrant - RAquadrant);

            // Konwersja RA (stopnie) na godziny
            RA /= 15.0;

            // Deklinacja Słońca
            double sinDec = 0.39782 * Math.Sin(L * d2r);
            double cosDec = Math.Cos(Math.Asin(sinDec));

            // Kąt godzinny
            double cosH = (Math.Cos(zenith * d2r) - sinDec * Math.Sin(latitude * d2r))
                          / (cosDec * Math.Cos(latitude * d2r));

            // Jeśli |cosH| > 1, zjawisko nie występuje (Słońce nie osiąga takiego kąta)
            if (cosH < -1.0 || cosH > 1.0)
            {
                return null;
            }

            // Dla wschodu: 360° - acos(cosH), dla zachodu: acos(cosH)
            double H = isSunrise
                ? 360.0 - r2d * Math.Acos(cosH)
                : r2d * Math.Acos(cosH);

            H /= 15.0; // z stopni na godziny

            // Czas w UTC
            double T = H + RA - (0.06571 * t) - 6.622;

            // Korekta długości geograficznej
            double UT = T - lngHour;

            // Normalizacja do zakresu [0, 24)
            UT = NormalizeHours(UT);

            return UT;
        }

        /// <summary>
        /// Konwertuje (year, dayOfYear, godzinaUTC) na DateTime w wybranej strefie czasowej.
        /// </summary>
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

        /// <summary>
        /// Normalizuje kąt w stopniach do zakresu [0, 360).
        /// </summary>
        private static double NormalizeDegrees(double angle)
        {
            double result = angle % 360.0;
            return (result < 0) ? result + 360.0 : result;
        }

        /// <summary>
        /// Normalizuje czas w godzinach do zakresu [0, 24).
        /// </summary>
        private static double NormalizeHours(double hours)
        {
            double result = hours % 24.0;
            return (result < 0) ? result + 24.0 : result;
        }
    }
}
