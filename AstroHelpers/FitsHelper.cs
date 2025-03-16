using System.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AstroHelpers
{
    public static class FitsHelper
    {

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
                        break;
                    }
                    string block = Encoding.ASCII.GetString(buffer);
                    
                    for (int i = 0; i < blockSize; i += recordSize)
                    {
                        string record = block.Substring(i, recordSize);
                      
                        if (record.StartsWith("END"))
                        {
                            endFound = true;
                            break;
                        }

                        if (string.IsNullOrWhiteSpace(record))
                            continue;

                        string key = record.Substring(0, 8).Trim();
                        string value = string.Empty;

                        if (record.Length > 10 && record[8] == '=')
                        {
                            value = record.Substring(10, 70).Trim();

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
