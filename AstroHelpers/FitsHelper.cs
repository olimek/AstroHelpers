using System.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AstroHelpers
{
    public static class FitsHelper
    {
        /// <summary>
        /// Odczytuje nagłówek pliku FITS i zwraca słownik z parą klucz-wartość.
        /// </summary>
        /// <param name="filePath">Ścieżka do pliku FITS</param>
        /// <returns>Słownik zawierający odczytane pary nagłówkowe</returns>
        public static Dictionary<string, string> ReadFitsHeader(string filePath)
        {
            var header = new Dictionary<string, string>();
            const int blockSize = 2880;
            const int recordSize = 80;

            using (FileStream fs = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: blockSize,
                useAsync: false))
            {
                byte[] buffer = new byte[blockSize];
                bool endFound = false;

                while (!endFound)
                {
                    int bytesRead = fs.Read(buffer, 0, blockSize);
                    if (bytesRead < blockSize)
                    {
                        // W przypadku niespełnienia rozmiaru bloku przerywamy odczyt
                        break;
                    }

                    // Konwertujemy bajty na tekst zakładając kodowanie ASCII
                    string block = Encoding.ASCII.GetString(buffer);
                    // Przetwarzamy blok w rekordy po 80 znaków
                    for (int i = 0; i < blockSize; i += recordSize)
                    {
                        string record = block.Substring(i, recordSize);
                        // Jeśli natrafimy na rekord zaczynający się od "END", kończymy odczyt nagłówka
                        if (record.StartsWith("END"))
                        {
                            endFound = true;
                            break;
                        }

                        // Pomijamy puste lub składające się wyłącznie z białych znaków rekordy
                        if (string.IsNullOrWhiteSpace(record))
                            continue;

                        // Pierwsze 8 znaków – klucz
                        string key = record.Substring(0, 8).Trim();
                        string value = string.Empty;

                        // Jeśli rekord zawiera znak '=' na pozycji 9, traktujemy to jako rekord z wartością
                        if (record.Length > 10 && record[8] == '=')
                        {
                            // Wartość znajduje się od pozycji 10 (indeks 9) do końca rekordu (może zawierać też komentarz)
                            value = record.Substring(10, 70).Trim();

                            // Opcjonalnie: usuwamy komentarz, który zaczyna się od '/'
                            int slashIndex = value.IndexOf('/');
                            if (slashIndex >= 0)
                            {
                                value = value.Substring(0, slashIndex).Trim();
                            }
                        }

                        if (!string.IsNullOrEmpty(key))
                        {
                            header[key] = value;
                        }
                    }
                }
            }

            return header;
        }
    }
}
