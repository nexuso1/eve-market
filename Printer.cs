using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
namespace eve_market
{
    public class Printer
    {
        public TextWriter output;
        public MainEsiInterface mainEsiInterface;
        public Printer( MainEsiInterface esiInterface,TextWriter writer)
        {
            output = writer;
            mainEsiInterface = esiInterface;
        }

        public void PrintLine(int length)
        {
            for (int i = 0; i < length; i++)
            {
                output.Write('-');
            }
            output.WriteLine();
        }

        public void PrintLine(List<string> fields, int width)
        {
            for (int i = 0; i < (width * (fields.Count + 1)); i++)
            {
                output.Write('-');
            }

            output.WriteLine();
        }

        public void PrintTableHeader(List<string> fields, int width)
        {
            for (int i = 0; i < fields.Count; i++)
            {
                int length = fields[i].Length;
                output.Write(fields[i]);
                for (int j = 0; j < fields[i].Length; j++)
                {
                    output.Write(' ');
                }

                output.Write('|');
            }

            output.WriteLine();
        }

        public void PrintJsonList<T>(List<T> jsonList, int width, int rows, List<string> fields)
        {
            int counter = 0;
            var buffer = new StringBuilder();

            for (int i = 0; i < jsonList.Count; i++)
            {
                if (counter >= rows)
                {
                    break;
                }
                var json = JObject.FromObject(jsonList[i]);
                var fieldString = "";
                for (int j = 0; j < fields.Count; j++)
                {
                    var key = fields[j];
                    if (mainEsiInterface.IsIdField(key))
                    {
                        fieldString = mainEsiInterface.universeInterface.IdToName(json[key].ToObject<long>());
                    }
                    else
                    {
                        fieldString = json[key].ToString();
                    }

                    if (fieldString.Length > width)
                    {
                        for (int k = 0; k < width - 3; k++)
                        {
                            buffer.Append(fieldString[k]);
                        }

                        buffer.Append("...");
                    }

                    else
                    {
                        buffer.Append(fieldString);
                        for (int k = fieldString.Length; k < width; k++)
                        {
                            buffer.Append(' ');
                        }
                    }

                    buffer.Append('|');
                }
            }

            output.WriteLine(buffer.ToString());
            PrintLine(fields, width);
        }
    }
}
